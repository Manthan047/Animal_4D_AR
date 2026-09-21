using UnityEngine;
using UnityEngine.EventSystems;

// Renders a standalone, rotatable 3D model into a mini viewer window in the UI,
// completely independent of AR image tracking.
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
    [Tooltip("Every spawned model is uniformly scaled so its bounding box diagonal equals this many world units, then re-centered on ModelAnchor.")]
    [SerializeField] private float desiredPreviewSize = 2f;

    [Header("Zoom")]
    [Tooltip("How much each mouse-wheel scroll tick zooms in/out.")]
    [SerializeField] private float scrollZoomSensitivity = 0.1f;
    [Tooltip("How much a pinch gesture zooms in/out.")]
    [SerializeField] private float pinchZoomSensitivity = 0.01f;
    [Tooltip("Zoom multiplier applied on top of the auto-framed base scale.")]
    [SerializeField] private float minZoomFactor = 0.5f;
    [SerializeField] private float maxZoomFactor = 3f;

    private GameObject currentInstance;
    private bool isDragging;

    // Rotation state - yaw (left/right) only, tracked explicitly so drag and
    // auto-rotate can't drift or fight each other via repeated Transform.Rotate calls.
    private float currentYaw;

    // Zoom state - stored relative to the scale FrameModel() settles on for this
    // model, so zooming never fights the auto-framing logic.
    private Vector3 baseScale = Vector3.one;
    private float currentZoomFactor = 1f;

    private void Update()
    {
        if (!isDragging && autoRotateWhenIdle && currentInstance != null)
        {
            currentYaw += autoRotateSpeed * Time.deltaTime;
            ApplyRotation();
        }

        HandlePinchZoom();
    }

    private void HandlePinchZoom()
    {
        if (currentInstance == null) return;
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
                return;
            }
        }

        currentInstance = Instantiate(data.previewModelPrefab, modelAnchor);
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

        // Ensure layer is set for preview camera
        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer >= 0)
        {
            SetLayerRecursively(currentInstance, layer);
        }
        else
        {
            Debug.LogWarning($"[ModelPreviewController] Layer '{previewLayerName}' does not exist in Project Settings -> Tags and Layers.");
        }

        // Trigger Idle animation if animator present
        Animator anim = currentInstance.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.enabled = true;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.speed = 1f;
        }

        FrameModel(currentInstance);

        // Reset rotation/zoom state for the new model, relative to its freshly-framed scale.
        currentYaw = 0f;
        currentZoomFactor = 1f;
        baseScale = currentInstance.transform.localScale;
        ApplyRotation();

        Debug.Log($"[ModelPreviewController] Successfully spawned preview model for: {data.animalName}");
    }

    private void FrameModel(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float currentSize = bounds.size.magnitude;
        if (currentSize <= 0.0001f) return;

        float scaleFactor = desiredPreviewSize / currentSize;
        instance.transform.localScale *= scaleFactor;

        // Recompute bounds after scaling so the re-centering offset is accurate
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 worldCenterOffset = bounds.center - instance.transform.position;
        instance.transform.position -= worldCenterOffset;
    }

    /// <summary>
    /// Destroys the current preview instance, if any.
    /// </summary>
    public void ClearModel()
    {
        if (currentInstance != null)
        {
            Destroy(currentInstance);
            currentInstance = null;
        }
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
        if (currentInstance == null || Input.touchCount > 1) return;

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
        if (currentInstance == null) return;
        currentInstance.transform.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
    }

    // =========================================================
    // ZOOM (MOUSE SCROLL + PINCH)
    // =========================================================

    public void OnScroll(PointerEventData eventData)
    {
        if (currentInstance == null) return;
        ApplyZoom(eventData.scrollDelta.y * scrollZoomSensitivity);
    }

    private void ApplyZoom(float zoomIncrement)
    {
        if (currentInstance == null) return;

        currentZoomFactor = Mathf.Clamp(currentZoomFactor + zoomIncrement, minZoomFactor, maxZoomFactor);
        currentInstance.transform.localScale = baseScale * currentZoomFactor;
    }
}