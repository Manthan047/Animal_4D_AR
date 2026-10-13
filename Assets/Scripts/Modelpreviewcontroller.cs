using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

// Renders a standalone, rotatable 3D model into a mini viewer window in the UI,
// completely independent of AR image tracking.
//
// Hierarchy created under ModelAnchor (all LOCAL, nothing is re-positioned in world space):
//
//   ModelAnchor
//   └── __Stage            (fixed local position: floor height below the anchor)
//       ├── __Platform     (grey 3D disc, fixed size, never rotates)
//       └── __ModelHolder  (local position = 0 ALWAYS; only its rotation (yaw) and scale (zoom) change)
//           └── Model instance (offset once so its centre + feet sit on the holder origin)
//
// Because __ModelHolder never moves, every model turns and zooms around the same
// platform centre, and the platform + model are identical for every animal.
[RequireComponent(typeof(RectTransform))]
public class ModelPreviewController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [Header("Preview Stage")]
    [Tooltip("Empty transform in front of the preview camera. Spawned models are parented here.")]
    [SerializeField] private Transform modelAnchor;
    [Tooltip("Layer the preview camera's Culling Mask is set to. Spawned models are moved onto this layer automatically.")]
    [SerializeField] private string previewLayerName = "Preview";

    [Header("Rotation (left / right only)")]
    [SerializeField] private float dragRotateSpeed = 0.3f;
    [SerializeField] private bool autoRotateWhenIdle = true;
    [SerializeField] private float autoRotateSpeed = 15f; // degrees/sec

    [Header("Auto-Framing")]
    [Tooltip("Every spawned model is uniformly scaled so its bounding box diagonal equals this many world units.")]
    [SerializeField] private float desiredPreviewSize = 2f;

    [Header("Zoom Speed")]
    [Tooltip("How much each mouse-wheel scroll tick zooms in/out.")]
    [SerializeField] private float scrollZoomSensitivity = 0.1f;
    [Tooltip("How much a pinch gesture zooms in/out.")]
    [SerializeField] private float pinchZoomSensitivity = 0.01f;

    [Header("Zoom Limits")]
    [Tooltip("ZOOM OUT LIMIT. Smallest size the model can shrink to. 1 = the default framed size, 0.5 = half size, 0.2 = very small. Lower value = can zoom out more.")]
    [FormerlySerializedAs("minZoomFactor")]
    [SerializeField, Range(0.05f, 1f)] private float zoomOutLimit = 0.5f;

    [Tooltip("ZOOM IN LIMIT. Largest size the model can grow to. 1 = the default framed size, 3 = three times bigger.")]
    [FormerlySerializedAs("maxZoomFactor")]
    [SerializeField, Range(1f, 10f)] private float zoomInLimit = 3f;

    [Header("Floor (same for ALL models)")]
    [Tooltip("Height of the platform top relative to ModelAnchor, in world units. Negative = below the anchor. " +
             "Change this once to move the platform + every model up or down together.")]
    [SerializeField] private float floorOffsetY = -0.4f;

    [Header("Grey 3D Platform (same for ALL models)")]
    [SerializeField] private bool showPlatform = true;
    [Tooltip("Colour of the lower (side) part of the disc.")]
    [SerializeField] private Color platformBaseColor = new Color(0.32f, 0.32f, 0.34f, 1f);
    [Tooltip("Colour of the top surface the model stands on.")]
    [SerializeField] private Color platformTopColor = new Color(0.55f, 0.55f, 0.58f, 1f);
    [Tooltip("Fixed platform diameter in world units. Not affected by which model is shown.")]
    [SerializeField, Range(0.5f, 6f)] private float platformDiameter = 1.8f;
    [Tooltip("Total height of the disc in world units.")]
    [SerializeField, Range(0.02f, 0.5f)] private float platformThickness = 0.12f;
    [Tooltip("ON = platform grows/shrinks together with the model when zooming. OFF = platform stays the same size.")]
    [SerializeField] private bool platformFollowsZoom = true;

    private GameObject currentInstance;
    private bool isDragging;

    // Stage objects (all created once, all local to ModelAnchor)
    private Transform stageRoot;
    private Transform modelHolder;   // local position stays (0,0,0). Only yaw + zoom change.
    private Transform platformRoot;
    private Transform platformBase;
    private Transform platformTop;
    private Material platformBaseMat;
    private Material platformTopMat;
    private float lastFloorLocalY = float.NaN;

    // Rotation state - yaw (left/right) only.
    private float currentYaw;

    // Zoom state - applied to the holder scale.
    private float currentZoomFactor = 1f;

    // While true, rotation and zoom are frozen so the model can be measured cleanly.
    private bool isFitting;
    private Coroutine refitRoutine;

    // Keeps the limits valid when edited in the Inspector.
    private void OnValidate()
    {
        zoomOutLimit = Mathf.Clamp(zoomOutLimit, 0.05f, 1f);
        zoomInLimit = Mathf.Max(zoomInLimit, 1f);
    }

    private void Update()
    {
        if (!isDragging && autoRotateWhenIdle && currentInstance != null && !isFitting)
        {
            currentYaw += autoRotateSpeed * Time.deltaTime;
            ApplyRotation();
        }

        HandlePinchZoom();
    }

    private void LateUpdate()
    {
        UpdateStage();
    }

    private void OnDisable()
    {
        // Coroutines stop when the object is disabled - make sure we never stay frozen.
        refitRoutine = null;
        isFitting = false;
    }

    private void OnDestroy()
    {
        if (platformBaseMat != null) Destroy(platformBaseMat);
        if (platformTopMat != null) Destroy(platformTopMat);
    }

    private void HandlePinchZoom()
    {
        if (currentInstance == null || isFitting) return;
        if (Input.touchCount != 2) return;

        Touch touch0 = Input.GetTouch(0);
        Touch touch1 = Input.GetTouch(1);

        Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
        Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

        float prevDistance = (touch0PrevPos - touch1PrevPos).magnitude;
        float currentDistance = (touch0.position - touch1.position).magnitude;

        float deltaDistance = currentDistance - prevDistance;
        ApplyZoom(deltaDistance * pinchZoomSensitivity);
    }

    // =========================================================
    // SHOW / CLEAR MODEL
    // =========================================================

    /// <summary>
    /// Spawns (or swaps to) the preview model for the given animal data.
    /// Safe to call repeatedly - always clears out the previous instance first.
    /// </summary>
    public void ShowModel(AnimalData data)
    {
        ClearModel();

        if (data == null)
        {
            Debug.LogWarning("[ModelPreviewController] ShowModel called with null AnimalData.");
            return;
        }

        if (data.previewModelPrefab == null)
        {
            Debug.LogWarning($"[ModelPreviewController] AnimalData '{data.animalName}' has NO 'previewModelPrefab' assigned!");
            return;
        }

        if (!EnsureStage())
            return;

        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[ModelPreviewController] Layer '{previewLayerName}' does not exist in Project Settings -> Tags and Layers.");
        }

        // Reset the holder. Its position is ALWAYS zero (platform centre at floor level).
        currentYaw = 0f;
        currentZoomFactor = 1f;
        modelHolder.localPosition = Vector3.zero;
        modelHolder.localRotation = Quaternion.identity;
        modelHolder.localScale = Vector3.one;

        SyncStagePosition();

        currentInstance = Instantiate(data.previewModelPrefab, modelHolder);
        currentInstance.transform.localPosition = Vector3.zero;
        currentInstance.transform.localRotation = Quaternion.identity;

        // Prevent physics/gravity from dropping or moving the preview model
        foreach (var rb in currentInstance.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (var col in currentInstance.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        // Prevent audio sources on preview model from playing
        foreach (var audio in currentInstance.GetComponentsInChildren<AudioSource>(true))
        {
            audio.playOnAwake = false;
            audio.Stop();
            audio.enabled = false;
        }

        // Skinned meshes: keep bounds updating even when the camera considers them off-screen,
        // otherwise the measured bounds can be stale and the model ends up offset.
        foreach (var smr in currentInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
        }

        // Ensure layer is set for preview camera (model, stage and platform)
        if (layer >= 0)
        {
            SetLayerRecursively(currentInstance, layer);
            SetLayerRecursively(stageRoot.gameObject, layer);
        }

        // Animators: play the idle pose, but NEVER let root motion move the model away from the holder.
        foreach (Animator anim in currentInstance.GetComponentsInChildren<Animator>(true))
        {
            anim.enabled = true;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.speed = 1f;
            anim.Update(0f); // evaluate the pose NOW so we measure the real idle pose, not the bind/T-pose
        }

        // Scale to the preview size and put the feet on the platform, centred on it.
        isFitting = true;
        FrameModelOnFloor(currentInstance, true);

        ApplyRotation();
        UpdateStage();

        // Measure once more after the animator has really run a frame, and correct any leftover offset.
        if (isActiveAndEnabled)
        {
            refitRoutine = StartCoroutine(RefitNextFrame(currentInstance));
        }
        else
        {
            isFitting = false;
        }

        Debug.Log($"[ModelPreviewController] Successfully spawned preview model for: {data.animalName}");
    }

    private IEnumerator RefitNextFrame(GameObject instance)
    {
        yield return null; // let the Animator + skinned meshes update once

        if (instance != null && instance == currentInstance)
        {
            // The holder is frozen at identity rotation / unit scale during fitting,
            // so this measurement is not distorted by yaw or zoom.
            FrameModelOnFloor(instance, false);
        }

        isFitting = false;
        refitRoutine = null;
    }

    /// <summary>
    /// Moves the model so that:
    ///  - its horizontal centre sits exactly at the holder origin (platform centre),
    ///  - its lowest point (feet) touches the holder origin height (platform top).
    /// If rescale is true the model is first scaled to desiredPreviewSize.
    /// The correction is applied INSIDE the holder; the holder itself never moves.
    /// </summary>
    private void FrameModelOnFloor(GameObject instance, bool rescale)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = GetBounds(renderers);

        if (rescale)
        {
            float currentSize = bounds.size.magnitude;
            if (currentSize <= 0.0001f) return;

            float scaleFactor = desiredPreviewSize / currentSize;
            instance.transform.localScale *= scaleFactor;

            // Recompute bounds after scaling so the offset is accurate
            bounds = GetBounds(renderers);
        }

        Vector3 target = modelHolder.position; // platform centre at floor height
        Vector3 offset = new Vector3(
            target.x - bounds.center.x,
            target.y - bounds.min.y,
            target.z - bounds.center.z);

        instance.transform.position += offset;
    }

    private static Bounds GetBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    /// <summary>
    /// Destroys the current preview instance, if any.
    /// </summary>
    public void ClearModel()
    {
        if (refitRoutine != null)
        {
            StopCoroutine(refitRoutine);
            refitRoutine = null;
        }
        isFitting = false;

        if (currentInstance != null)
        {
            Destroy(currentInstance);
            currentInstance = null;
        }

        if (platformRoot != null) platformRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns the model to its default framed size.
    /// </summary>
    public void ResetZoom()
    {
        if (currentInstance == null) return;

        currentZoomFactor = 1f;
        if (modelHolder != null) modelHolder.localScale = Vector3.one;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return;

        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // =========================================================
    // STAGE (stage root + holder + platform) - identical for every model
    // =========================================================

    private bool EnsureStage()
    {
        if (modelAnchor == null)
        {
            var anchorObj = GameObject.Find("ModelAnchor");
            if (anchorObj != null)
            {
                modelAnchor = anchorObj.transform;
            }
            else
            {
                Debug.LogWarning("[ModelPreviewController] 'Model Anchor' Transform is not assigned and no GameObject named 'ModelAnchor' was found in the scene!");
                return false;
            }
        }

        if (stageRoot == null)
        {
            var stageGo = new GameObject("__Stage");
            stageRoot = stageGo.transform;
            stageRoot.SetParent(modelAnchor, false);
            stageRoot.localRotation = Quaternion.identity;
            stageRoot.localScale = Vector3.one;
            lastFloorLocalY = float.NaN;
        }

        if (modelHolder == null)
        {
            var holderGo = new GameObject("__ModelHolder");
            modelHolder = holderGo.transform;
            modelHolder.SetParent(stageRoot, false);
            modelHolder.localPosition = Vector3.zero;
            modelHolder.localRotation = Quaternion.identity;
            modelHolder.localScale = Vector3.one;
        }

        if (platformRoot == null)
        {
            var rootGo = new GameObject("__Platform");
            platformRoot = rootGo.transform;
            platformRoot.SetParent(stageRoot, false);
            platformRoot.localPosition = Vector3.zero;
            platformRoot.localRotation = Quaternion.identity;
            platformRoot.localScale = Vector3.one;

            platformBase = CreateDisc("Base", platformRoot, out platformBaseMat);
            platformTop = CreateDisc("Top", platformRoot, out platformTopMat);
        }

        return true;
    }

    // Floor height is set ONCE in the anchor's local space (and only re-applied if
    // Floor Offset Y or the anchor scale changes), so the stage never jitters or drifts.
    private void SyncStagePosition()
    {
        if (stageRoot == null || modelAnchor == null) return;

        float anchorScale = Mathf.Max(0.0001f, modelAnchor.lossyScale.x);
        float localY = floorOffsetY / anchorScale;

        if (float.IsNaN(lastFloorLocalY) || !Mathf.Approximately(lastFloorLocalY, localY))
        {
            stageRoot.localPosition = new Vector3(0f, localY, 0f);
            lastFloorLocalY = localY;
        }
    }

    // Builds a flat cylinder from Unity's built-in primitive (works in Built-in, URP and HDRP).
    private static Transform CreateDisc(string name, Transform parent, out Material mat)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = name;

        // Remove the collider so it never blocks touches/raycasts.
        Collider col = disc.GetComponent<Collider>();
        if (col != null) Destroy(col);

        disc.transform.SetParent(parent, false);

        MeshRenderer mr = disc.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mat = mr.material; // instance, so colour changes don't affect other objects

        return disc.transform;
    }

    private void UpdateStage()
    {
        if (modelAnchor == null || stageRoot == null || platformRoot == null) return;

        SyncStagePosition();

        bool platformVisible = currentInstance != null && showPlatform;
        if (platformRoot.gameObject.activeSelf != platformVisible)
            platformRoot.gameObject.SetActive(platformVisible);

        if (!platformVisible) return;

        platformBaseMat.color = platformBaseColor;
        platformTopMat.color = platformTopColor;

        float f = platformFollowsZoom ? currentZoomFactor : 1f;
        float s = 1f / Mathf.Max(0.0001f, modelAnchor.lossyScale.x); // world units -> anchor-local units

        float totalH = platformThickness * f;
        float topH = totalH * 0.3f;          // thin lighter top plate
        float baseH = totalH - topH;         // darker lower body
        float baseD = platformDiameter * f;
        float topD = baseD * 0.92f;          // slightly smaller -> visible rim/step

        // The platform top face is at the stage origin (= feet level). Everything is LOCAL.
        // Unity's cylinder primitive is 1 wide and 2 tall, so scaleY = height / 2.
        platformTop.localPosition = new Vector3(0f, -topH * 0.5f * s, 0f);
        platformTop.localScale = new Vector3(topD * s, topH * 0.5f * s, topD * s);

        platformBase.localPosition = new Vector3(0f, -(topH + baseH * 0.5f) * s, 0f);
        platformBase.localScale = new Vector3(baseD * s, baseH * 0.5f * s, baseD * s);
    }

    // =========================================================
    // DRAG TO ROTATE (LEFT / RIGHT ONLY)
    // =========================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Ignore drag while a two-finger pinch is in progress, so rotate and zoom
        // gestures don't fight each other on mobile.
        if (currentInstance == null || isFitting || Input.touchCount > 1) return;

        // Horizontal movement only -> yaw. Vertical movement is intentionally ignored.
        currentYaw -= eventData.delta.x * dragRotateSpeed;
        ApplyRotation();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    private void ApplyRotation()
    {
        if (currentInstance == null || modelHolder == null) return;
        modelHolder.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
    }

    // =========================================================
    // ZOOM (MOUSE SCROLL + PINCH)
    // =========================================================

    public void OnScroll(PointerEventData eventData)
    {
        if (currentInstance == null || isFitting) return;
        ApplyZoom(eventData.scrollDelta.y * scrollZoomSensitivity);
    }

    private void ApplyZoom(float zoomIncrement)
    {
        if (currentInstance == null || modelHolder == null) return;

        // Zoom out limit (smallest) and zoom in limit (largest) come from the Inspector.
        currentZoomFactor = Mathf.Clamp(currentZoomFactor + zoomIncrement, zoomOutLimit, zoomInLimit);
        modelHolder.localScale = Vector3.one * currentZoomFactor;
    }
}