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
//       ├── __Platform     (grey 3D disc, never rotates)
//       └── __ModelHolder  (local position = 0 ALWAYS; only its rotation (yaw) and scale (zoom) change)
//           └── Model instance (offset once so its centre + feet sit on the holder origin)
//
// DEVICE-AWARE ZOOM
// -----------------
// Zoom limits are calculated at runtime from:
//   1. the size of the area the preview camera can see at the model's distance,
//   2. the model + platform size (measured once, at zoom = 1, with no rotation),
//   3. the viewer's real size on screen (pixels + DPI -> centimetres).
// They are recalculated automatically when the viewer size or camera aspect changes
// (phone rotation, split-screen, different resolution, canvas scaler changes).
// Turn "Auto Zoom Limits For Device" OFF to use the fixed Zoom Out / Zoom In Limit values instead.
[RequireComponent(typeof(RectTransform))]
public class ModelPreviewController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [Header("Preview Stage")]
    [Tooltip("Empty transform in front of the preview camera. Spawned models are parented here.")]
    [SerializeField] private Transform modelAnchor;
    [Tooltip("Layer the preview camera's Culling Mask is set to. Spawned models are moved onto this layer automatically.")]
    [SerializeField] private string previewLayerName = "Preview";
    [Tooltip("The camera that renders the preview. Assign it if you can. If empty it is searched by the preview layer, then Camera.main.")]
    [SerializeField] private Camera previewCamera;

    [Header("Rotation (left / right only)")]
    [SerializeField] private float dragRotateSpeed = 0.3f;
    [SerializeField] private bool autoRotateWhenIdle = true;
    [SerializeField] private float autoRotateSpeed = 15f; // degrees/sec

    [Header("Auto-Framing")]
    [Tooltip("Every spawned model is uniformly scaled so its bounding box diagonal equals this many world units.")]
    [SerializeField] private float desiredPreviewSize = 2f;

    [Header("Zoom Speed")]
    [Tooltip("Mouse wheel: each scroll tick multiplies the zoom by e^(tick * this). 0.1 = about 10% per tick.")]
    [SerializeField] private float scrollZoomSensitivity = 0.1f;
    [Tooltip("Pinch: 1 = the model follows the fingers exactly. Lower = slower, higher = faster.")]
    [SerializeField, Range(0.1f, 3f)] private float pinchZoomMultiplier = 1f;

    [Header("Auto Zoom Limits (device based)")]
    [Tooltip("ON = zoom limits are calculated from the viewer size, camera and screen DPI.\nOFF = the manual Zoom Out / Zoom In Limit below are used.")]
    [SerializeField] private bool autoZoomLimitsForDevice = true;
    [Tooltip("How much of the viewer the model fills when it is first shown. 0.8 = 80%.")]
    [SerializeField, Range(0.3f, 1.2f)] private float defaultFillFraction = 0.8f;
    [Tooltip("ZOOM OUT LIMIT: the model never gets smaller than this fraction of the viewer.")]
    [SerializeField, Range(0.1f, 0.7f)] private float zoomOutFillFraction = 0.35f;
    [Tooltip("ZOOM IN LIMIT: the model can grow up to this fraction of the viewer (1.6 = 160%).")]
    [SerializeField, Range(1f, 4f)] private float zoomInFillFraction = 1.6f;
    [Tooltip("ZOOM OUT LIMIT: the model never appears smaller than this many centimetres on the real phone screen.")]
    [SerializeField] private float minModelSizeCm = 2f;
    [Tooltip("ON = fit the model so it stays inside the viewer at ANY rotation (best with auto-rotate). OFF = fit using the front view only (long animals get bigger, but may clip while turning).")]
    [SerializeField] private bool fitForAnyRotation = true;

    [Header("Manual Zoom Limits (used when Auto is OFF)")]
    [Tooltip("ZOOM OUT LIMIT. 1 = the default framed size, 0.5 = half size. Lower value = can zoom out more.")]
    [FormerlySerializedAs("minZoomFactor")]
    [SerializeField, Range(0.05f, 1f)] private float zoomOutLimit = 0.5f;

    [Tooltip("ZOOM IN LIMIT. 1 = the default framed size, 3 = three times bigger.")]
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
    [Tooltip("Platform diameter in world units. Not affected by which model is shown.")]
    [SerializeField, Range(0.5f, 6f)] private float platformDiameter = 1.8f;
    [Tooltip("Total height of the disc in world units.")]
    [SerializeField, Range(0.02f, 0.5f)] private float platformThickness = 0.12f;
    [Tooltip("ON = platform grows/shrinks together with the model when zooming. OFF = platform stays the same size.")]
    [SerializeField] private bool platformFollowsZoom = true;

    private const float FallbackDpi = 160f;

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
    private float effZoomOut = 0.5f;   // limits actually used
    private float effZoomIn = 3f;
    private float defaultZoom = 1f;    // zoom used when a model is first shown / reset

    // Model + platform size at zoom = 1 (world units), measured while the holder is un-rotated and un-scaled.
    private Vector2 contentSize;
    private bool hasContentSize;
    private Vector3 lastViewSignature;

    // UI helpers
    private RectTransform rectTransform;
    private Canvas viewerCanvas;
    private readonly Vector3[] worldCorners = new Vector3[4];
    private bool pinchActive;

    // While true, rotation and zoom are frozen so the model can be measured cleanly.
    private bool isFitting;
    private Coroutine refitRoutine;

    // Keeps the values valid when edited in the Inspector.
    private void OnValidate()
    {
        zoomOutLimit = Mathf.Clamp(zoomOutLimit, 0.05f, 1f);
        zoomInLimit = Mathf.Max(zoomInLimit, 1f);

        minModelSizeCm = Mathf.Max(0f, minModelSizeCm);
        zoomOutFillFraction = Mathf.Min(zoomOutFillFraction, defaultFillFraction * 0.9f);
        zoomInFillFraction = Mathf.Max(zoomInFillFraction, defaultFillFraction * 1.1f);
    }

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        viewerCanvas = GetComponentInParent<Canvas>();

        effZoomOut = zoomOutLimit;
        effZoomIn = zoomInLimit;
        defaultZoom = 1f;
    }

    private void OnEnable()
    {
        // Force a limit recalculation: the layout may have changed while the viewer was hidden.
        lastViewSignature = Vector3.zero;
        pinchActive = false;
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
        RefreshLimitsIfViewChanged();
    }

    private void OnDisable()
    {
        // Coroutines stop when the object is disabled - make sure we never stay frozen.
        refitRoutine = null;
        isFitting = false;
        pinchActive = false;
    }

    private void OnDestroy()
    {
        if (platformBaseMat != null) Destroy(platformBaseMat);
        if (platformTopMat != null) Destroy(platformTopMat);
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

        // Holder is un-rotated and un-scaled right now, so this measurement is clean.
        MeasureContent(currentInstance);
        RecalculateZoomLimits(true);

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
            // Rotation and zoom are frozen while fitting, so this measurement is not distorted.
            FrameModelOnFloor(instance, false);
            MeasureContent(instance);
            RecalculateZoomLimits(true);
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
        hasContentSize = false;

        if (currentInstance != null)
        {
            Destroy(currentInstance);
            currentInstance = null;
        }

        if (platformRoot != null) platformRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns the model to its default framed size for this device.
    /// </summary>
    public void ResetZoom()
    {
        if (currentInstance == null) return;
        SetZoom(defaultZoom);
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
    // DEVICE-BASED ZOOM LIMITS
    // =========================================================

    private Camera GetPreviewCamera()
    {
        if (previewCamera != null) return previewCamera;

        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer >= 0)
        {
            int mask = 1 << layer;

            // 1) a camera that renders ONLY the preview layer
            foreach (Camera c in Camera.allCameras)
            {
                if (c.cullingMask == mask)
                {
                    previewCamera = c;
                    return c;
                }
            }

            // 2) any other camera that renders the preview layer
            foreach (Camera c in Camera.allCameras)
            {
                if (c != Camera.main && (c.cullingMask & mask) != 0)
                {
                    previewCamera = c;
                    return c;
                }
            }
        }

        previewCamera = Camera.main;
        return previewCamera;
    }

    private Camera GetUiCamera()
    {
        if (viewerCanvas == null) return null;
        Canvas root = viewerCanvas.rootCanvas;
        return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
    }

    // Size of the viewer on the real screen, in pixels.
    private bool TryGetViewerPixelSize(out Vector2 pixels)
    {
        pixels = Vector2.zero;
        if (rectTransform == null) return false;

        rectTransform.GetWorldCorners(worldCorners);
        Camera uiCam = GetUiCamera();
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCam, worldCorners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCam, worldCorners[2]);

        pixels = new Vector2(Mathf.Abs(topRight.x - bottomLeft.x), Mathf.Abs(topRight.y - bottomLeft.y));
        return pixels.x > 1f && pixels.y > 1f;
    }

    // Measures model + platform size at zoom = 1. Only call while the holder is un-rotated and un-scaled
    // (that is guaranteed while isFitting is true).
    private void MeasureContent(GameObject instance)
    {
        if (instance == null || modelHolder == null) return;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = GetBounds(renderers);
        float z = Mathf.Max(0.0001f, modelHolder.localScale.x);

        // Diagonal of the footprint = the widest the model gets at ANY yaw while it rotates.
        float width = fitForAnyRotation
            ? Mathf.Sqrt(b.size.x * b.size.x + b.size.z * b.size.z) / z
            : b.size.x / z;
        float height = b.size.y / z;

        // The platform only limits the fit when it scales together with the model.
        if (showPlatform && platformFollowsZoom)
        {
            width = Mathf.Max(width, platformDiameter);
            height += platformThickness;
        }

        contentSize = new Vector2(Mathf.Max(0.01f, width), Mathf.Max(0.01f, height));
        hasContentSize = true;
    }

    // World-space size the preview camera can see at the model's distance.
    private bool TryGetVisibleSize(out float visW, out float visH)
    {
        visW = visH = 0f;
        Camera cam = GetPreviewCamera();
        if (cam == null || modelHolder == null) return false;

        if (cam.orthographic)
        {
            visH = cam.orthographicSize * 2f;
        }
        else
        {
            float depth = Vector3.Dot(modelHolder.position - cam.transform.position, cam.transform.forward);
            if (depth <= 0.01f) return false;
            visH = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        visW = visH * cam.aspect;
        return visW > 0.0001f && visH > 0.0001f;
    }

    // Calculates default / zoom-out / zoom-in for the current device.
    // resetToDefault = true  -> snap to the default size (new model).
    // resetToDefault = false -> keep the user's relative zoom (rotation / resize).
    private void RecalculateZoomLimits(bool resetToDefault)
    {
        float oldDefault = defaultZoom;
        float visW = 0f, visH = 0f;

        bool auto = autoZoomLimitsForDevice
                    && hasContentSize
                    && TryGetVisibleSize(out visW, out visH);

        if (auto)
        {
            // Zoom at which the model exactly fills the viewer (tighter side wins).
            float fitZoom = Mathf.Min(visW / contentSize.x, visH / contentSize.y);

            // Smallest allowed size so the model stays readable on the physical screen.
            float minFillFromCm = 0f;
            if (TryGetViewerPixelSize(out Vector2 px))
            {
                float dpi = Screen.dpi > 0f ? Screen.dpi : FallbackDpi; // Unity returns 0 if unknown
                float viewerMinSideCm = Mathf.Min(px.x, px.y) / dpi * 2.54f;
                if (viewerMinSideCm > 0.01f)
                    minFillFromCm = minModelSizeCm / viewerMinSideCm;
            }

            float outFill = Mathf.Max(zoomOutFillFraction, minFillFromCm);
            outFill = Mathf.Min(outFill, defaultFillFraction * 0.9f);   // zoom-out must stay below the default
            float inFill = Mathf.Max(zoomInFillFraction, defaultFillFraction * 1.1f);

            defaultZoom = Mathf.Clamp(fitZoom * defaultFillFraction, 0.01f, 50f);
            effZoomOut = Mathf.Clamp(fitZoom * outFill, 0.01f, defaultZoom);
            effZoomIn = Mathf.Clamp(fitZoom * inFill, defaultZoom, 50f);
        }
        else
        {
            // Manual mode / fallback: 1 = the framed size.
            defaultZoom = 1f;
            effZoomOut = zoomOutLimit;
            effZoomIn = zoomInLimit;
        }

        if (resetToDefault || oldDefault <= 0.0001f)
            currentZoomFactor = defaultZoom;
        else
            currentZoomFactor *= defaultZoom / oldDefault; // keep the same relative zoom

        SetZoom(currentZoomFactor);
    }

    // Re-calculates when the viewer size or the camera aspect changes (rotation, split-screen, etc.).
    private void RefreshLimitsIfViewChanged()
    {
        if (!autoZoomLimitsForDevice || !hasContentSize || currentInstance == null || isFitting) return;
        if (!TryGetViewerPixelSize(out Vector2 px)) return;

        Camera cam = GetPreviewCamera();
        Vector3 signature = new Vector3(px.x, px.y, cam != null ? cam.aspect : 0f);

        if ((signature - lastViewSignature).sqrMagnitude > 0.01f)
        {
            lastViewSignature = signature;
            RecalculateZoomLimits(false);
        }
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
    // ZOOM (MOUSE SCROLL + PINCH) - multiplicative, so it feels the same at any zoom level
    // =========================================================

    public void OnScroll(PointerEventData eventData)
    {
        if (currentInstance == null || isFitting) return;
        ApplyZoomMultiplier(Mathf.Exp(eventData.scrollDelta.y * scrollZoomSensitivity));
    }

    private void HandlePinchZoom()
    {
        if (currentInstance == null || isFitting || Input.touchCount != 2)
        {
            pinchActive = false;
            return;
        }

        Touch touch0 = Input.GetTouch(0);
        Touch touch1 = Input.GetTouch(1);

        // A pinch only counts if it STARTS on the viewer, so pinching elsewhere in the UI doesn't zoom the model.
        if (!pinchActive)
        {
            Vector2 mid = (touch0.position + touch1.position) * 0.5f;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, mid, GetUiCamera()))
                return;
            pinchActive = true;
        }

        Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
        Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

        float prevDistance = (touch0PrevPos - touch1PrevPos).magnitude;
        float currentDistance = (touch0.position - touch1.position).magnitude;

        if (prevDistance < 1f || currentDistance < 1f) return;

        float ratio = currentDistance / prevDistance;
        ApplyZoomMultiplier(Mathf.Pow(ratio, pinchZoomMultiplier));
    }

    private void ApplyZoomMultiplier(float multiplier)
    {
        if (currentInstance == null || modelHolder == null) return;
        SetZoom(currentZoomFactor * multiplier);
    }

    private void SetZoom(float zoom)
    {
        currentZoomFactor = Mathf.Clamp(zoom, effZoomOut, effZoomIn);
        if (modelHolder != null)
            modelHolder.localScale = Vector3.one * currentZoomFactor;
    }
}