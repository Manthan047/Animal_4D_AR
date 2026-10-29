using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LightSide; // UniText namespace

/// <summary>
/// Controls swapping between Photo mode and Video mode using single alternating arrow buttons:
/// - Photo Mode: ONLY the left arrow is visible. Clicking it switches to Video.
/// - Video Mode: ONLY the right arrow is visible. Clicking it switches to Photo.
/// - Both arrows are permanent GameObjects under 'Actions Buttons Panel'. Never visible together.
/// - While recording, no "RECORDING" text is shown. The label stays "VIDEO" (or is hidden).
/// - While recording, the ring around the video button fills (radial 360) from 0 to 100%
///   over 'recordingDuration' seconds (default 60). Optionally auto-stops when full.
///   Alternatively the ring can rotate continuously (fillRingInsteadOfRotate = false).
/// All text uses UniText (no TextMeshPro dependency).
/// </summary>
[DisallowMultipleComponent]
public class CameraModeSwipeController : MonoBehaviour
{
    public enum CameraMode
    {
        Photo = 0,
        Video = 1
    }

    public static CameraModeSwipeController Instance { get; private set; }

    [Header("Current Mode")]
    [SerializeField] private CameraMode currentMode = CameraMode.Photo;

    [Header("Capture Buttons")]
    [Tooltip("The Photo capture button ('Take_photo'). Auto-detected if null.")]
    [SerializeField] private Button photoButton;

    [Tooltip("The Video capture button ('Video_button'). Auto-detected if null.")]
    [SerializeField] private Button videoButton;

    [Header("Alternating Arrow Buttons")]
    [Tooltip("Visible in Photo mode. Clicking changes to Video mode and hides this button.")]
    [SerializeField] private Button leftArrowBtn;

    [Tooltip("Visible in Video mode. Clicking changes to Photo mode and hides this button.")]
    [SerializeField] private Button rightArrowBtn;

    [Header("Mode Indicator Label (Optional)")]
    [Tooltip("Subtle mode indicator label ('PHOTO' / 'VIDEO') above button.")]
    [SerializeField] private UniText modeIndicatorUniText;

    [Tooltip("If true, the mode label is hidden completely while recording. If false, it keeps showing 'VIDEO'.")]
    [SerializeField] private bool hideModeLabelWhileRecording = false;

    [Header("Recording Ring")]
    [Tooltip("The ring image around the Video button (NOT the button itself). Auto-detected by name if null.")]
    [SerializeField] private RectTransform recordingRing;

    [Tooltip("Fill the ring like a progress bar instead of rotating it.")]
    [SerializeField] private bool fillRingInsteadOfRotate = true;

    [Tooltip("Seconds for the ring to fill completely.")]
    [SerializeField] private float recordingDuration = 60f;

    [Tooltip("Automatically stop recording when the ring is full.")]
    [SerializeField] private bool autoStopAtDuration = true;

    [Tooltip("Rotation speed in degrees per second (only used when 'Fill Ring Instead Of Rotate' is off).")]
    [SerializeField] private float ringRotationSpeed = 180f;

    [Tooltip("Fill/rotate clockwise (true) or counter-clockwise (false).")]
    [SerializeField] private bool ringClockwise = true;

    [Tooltip("Reset the ring (rotation and fill) when recording stops.")]
    [SerializeField] private bool resetRingOnStop = true;

    [Tooltip("If true, the ring is only visible while recording.")]
    [SerializeField] private bool showRingOnlyWhileRecording = false;

    [Header("Managers")]
    [SerializeField] private ARScreenshotManager screenshotManager;
    [SerializeField] private ScreenRecorderToggle screenRecorder;

    [Header("Layout & Animation Settings")]
    [SerializeField] private float arrowSpacing = 85f; // Distance from center to the arrow buttons
    [SerializeField] private float swipeThreshold = 35f;
    [SerializeField] private float transitionDuration = 0.2f;
    [SerializeField] private float offscreenOffset = 220f;
    [SerializeField] private Color photoModeColor = Color.white;
    [SerializeField] private Color videoModeColor = new Color(1f, 0.32f, 0.32f, 1f);

    public CameraMode CurrentMode => currentMode;
    public event Action<CameraMode> OnModeChanged;

    // Internal state
    private Vector2 captureCenterPos = new Vector2(0f, 18.4f);
    private CanvasGroup photoCanvasGroup;
    private CanvasGroup videoCanvasGroup;
    private RectTransform photoRect;
    private RectTransform videoRect;
    private Coroutine transitionCoroutine;
    private bool isTransitioning = false;
    private bool isInitialized = false;
    private bool wasRecording = false;

    // Recording ring state
    private Image ringImage;
    private float recordStartTime;
    private bool autoStopRequested;

    // Mobile touch tracking
    private Vector2 touchStartPos;
    private bool isTrackingTouch = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        ResolveAllReferences();
        InitializeButtons();
        EnsureArrowSwapUI();
        ApplyModeImmediate(currentMode);
    }

    private void OnEnable()
    {
        if (!isInitialized)
        {
            ResolveAllReferences();
            InitializeButtons();
            EnsureArrowSwapUI();
        }

        // Every time the panel appears (e.g. card detected again), start in Photo mode.
        ResetToPhoto();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        if (recordingDuration < 1f) recordingDuration = 1f;

        if (Application.isPlaying && isInitialized)
        {
            ApplyModeImmediate(currentMode);
        }
    }

    private void Update()
    {
        HandleKeyboardTesting();
        HandleMobileTouches();
        UpdateRecordingState();
    }

    // ----------------------------------------------------
    // UNITEXT HELPERS
    // ----------------------------------------------------
    private static void SetLabel(UniText label, string text, Color color)
    {
        if (label == null) return;
        label.Text = text;
        label.color = color; // UniText is a UI Graphic -> lowercase 'color'
    }

    /// <summary>
    /// Creates a UniText child. Used for the arrow glyphs and the mode label.
    /// </summary>
    private static UniText CreateUniText(string name, Transform parent, string text, float fontSize, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UniText));
        obj.transform.SetParent(parent, false);

        UniText label = obj.GetComponent<UniText>();
        label.Text = text;
        label.color = color;
        label.FontSize = fontSize;

        // Don't let the label block clicks on the button underneath
        Graphic g = obj.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = false;

        return label;
    }

    // ----------------------------------------------------
    // INITIALIZATION & REFERENCES
    // ----------------------------------------------------
    private static readonly string[] RingNames =
    {
        "Ring", "RecordingRing", "Recording_Ring", "Video_ring", "VideoRing", "Video_Ring"
    };

    private static bool IsRingName(string n)
    {
        n = n.Trim();
        foreach (var r in RingNames)
        {
            if (string.Equals(n, r, StringComparison.OrdinalIgnoreCase)) return true;
        }

        // Fallback: any name containing "ring"
        return n.IndexOf("ring", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public void ResolveAllReferences()
    {
        Transform parentTransform = transform.parent != null ? transform.parent : transform;

        // 1. Resolve Photo Button
        if (photoButton == null)
        {
            Transform found = parentTransform.Find("Take_photo") ?? parentTransform.Find("TakePhoto") ?? parentTransform.Find("Photo_button");
            if (found != null) photoButton = found.GetComponent<Button>();
            if (photoButton == null)
            {
                var go = GameObject.Find("Take_photo");
                if (go != null) photoButton = go.GetComponent<Button>();
            }
        }

        // 2. Resolve Video Button
        if (videoButton == null)
        {
            Transform found = parentTransform.Find("Video_button") ?? parentTransform.Find("VideoButton") ?? parentTransform.Find("Record_button");
            if (found != null) videoButton = found.GetComponent<Button>();
            if (videoButton == null)
            {
                var go = GameObject.Find("Video_button");
                if (go != null) videoButton = go.GetComponent<Button>();
            }
        }

        // 3. Resolve arrows and label from hierarchy
        if (photoButton != null && photoButton.transform.parent != null)
            parentTransform = photoButton.transform.parent;

        if (leftArrowBtn == null)
        {
            Transform l = parentTransform.Find("LeftArrowBtn");
            if (l != null) leftArrowBtn = l.GetComponent<Button>();
        }

        if (rightArrowBtn == null)
        {
            Transform r = parentTransform.Find("RightArrowBtn");
            if (r != null) rightArrowBtn = r.GetComponent<Button>();
        }

        if (modeIndicatorUniText == null)
        {
            Transform m = parentTransform.Find("ModeIndicatorLabel");
            if (m != null) modeIndicatorUniText = m.GetComponent<UniText>();
        }

        // 4. Resolve Recording Ring (prefer children of the video button, then siblings)
        if (recordingRing == null && videoButton != null)
        {
            foreach (var rt in videoButton.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt != videoButton.transform && IsRingName(rt.name))
                {
                    recordingRing = rt;
                    break;
                }
            }

            if (recordingRing == null && videoButton.transform.parent != null)
            {
                foreach (Transform sibling in videoButton.transform.parent)
                {
                    if (sibling != videoButton.transform && IsRingName(sibling.name))
                    {
                        recordingRing = sibling as RectTransform;
                        if (recordingRing != null) break;
                    }
                }
            }
        }

        // 5. Resolve ARScreenshotManager
        if (screenshotManager == null)
        {
            screenshotManager = FindAnyObjectByType<ARScreenshotManager>();
        }

        // 6. Resolve ScreenRecorderToggle
        if (screenRecorder == null)
        {
            screenRecorder = FindAnyObjectByType<ScreenRecorderToggle>();
        }
    }

    private void InitializeButtons()
    {
        if (photoButton != null)
        {
            photoRect = photoButton.GetComponent<RectTransform>();
            photoCanvasGroup = photoButton.GetComponent<CanvasGroup>();
            if (photoCanvasGroup == null)
                photoCanvasGroup = photoButton.gameObject.AddComponent<CanvasGroup>();

            captureCenterPos = photoRect.anchoredPosition;

            AttachSwipeReceiver(photoButton.gameObject);

            photoButton.onClick.RemoveListener(OnPhotoButtonClicked);
            photoButton.onClick.AddListener(OnPhotoButtonClicked);
        }

        if (videoButton != null)
        {
            videoRect = videoButton.GetComponent<RectTransform>();
            videoCanvasGroup = videoButton.GetComponent<CanvasGroup>();
            if (videoCanvasGroup == null)
                videoCanvasGroup = videoButton.gameObject.AddComponent<CanvasGroup>();

            // Align Video Button to match Photo Button's anchors and pivot
            if (photoRect != null)
            {
                videoRect.anchorMin = photoRect.anchorMin;
                videoRect.anchorMax = photoRect.anchorMax;
                videoRect.pivot = photoRect.pivot;
            }

            AttachSwipeReceiver(videoButton.gameObject);

            videoButton.onClick.RemoveListener(OnVideoButtonClicked);
            videoButton.onClick.AddListener(OnVideoButtonClicked);
        }

        // Hook up arrow click listeners
        if (leftArrowBtn != null)
        {
            leftArrowBtn.onClick.RemoveListener(OnLeftArrowClicked);
            leftArrowBtn.onClick.AddListener(OnLeftArrowClicked);
        }

        if (rightArrowBtn != null)
        {
            rightArrowBtn.onClick.RemoveListener(OnRightArrowClicked);
            rightArrowBtn.onClick.AddListener(OnRightArrowClicked);
        }

        // Configure the recording ring
        if (recordingRing != null)
        {
            // The ring must never block clicks on the video button
            Graphic ringGraphic = recordingRing.GetComponent<Graphic>();
            if (ringGraphic != null) ringGraphic.raycastTarget = false;

            // Radial fill setup
            ringImage = recordingRing.GetComponent<Image>();
            if (ringImage != null && fillRingInsteadOfRotate)
            {
                ringImage.type = Image.Type.Filled;
                ringImage.fillMethod = Image.FillMethod.Radial360;
                ringImage.fillOrigin = (int)Image.Origin360.Top;
                ringImage.fillClockwise = ringClockwise;
                ringImage.fillAmount = 0f;
            }
            else if (ringImage == null && fillRingInsteadOfRotate)
            {
                Debug.LogWarning("[CameraModeSwipeController] Recording ring has no Image component; fill mode can't work.", recordingRing);
            }
        }
        else
        {
            Debug.LogWarning("[CameraModeSwipeController] Recording ring NOT found. Assign it in the Inspector.", this);
        }

        if (screenRecorder == null)
        {
            Debug.LogWarning("[CameraModeSwipeController] ScreenRecorderToggle NOT found.", this);
        }

        isInitialized = true;
    }

    private void AttachSwipeReceiver(GameObject targetObj)
    {
        if (targetObj == null) return;
        var existing = targetObj.GetComponent<SwipeDragForwarder>();
        if (existing == null)
        {
            var fwd = targetObj.AddComponent<SwipeDragForwarder>();
            fwd.Init(this);
        }
    }

    // ----------------------------------------------------
    // ARROW ACTIONS (ONLY ONE VISIBLE AT A TIME)
    // ----------------------------------------------------
    /// <summary>User clicks the left arrow while in Photo mode.</summary>
    public void OnLeftArrowClicked()
    {
        if (IsRecordingActive())
        {
            TriggerLockedFeedback();
            return;
        }

        SwitchToMode(CameraMode.Video, true);
        PunchButtonScale(leftArrowBtn);
    }

    /// <summary>User clicks the right arrow while in Video mode.</summary>
    public void OnRightArrowClicked()
    {
        if (IsRecordingActive())
        {
            TriggerLockedFeedback();
            return;
        }

        SwitchToMode(CameraMode.Photo, true);
        PunchButtonScale(rightArrowBtn);
    }

    public void SwitchToMode(CameraMode newMode, bool animated = true)
    {
        if (IsRecordingActive())
        {
            Debug.LogWarning("[CameraModeSwipeController] Cannot switch camera mode while video recording is active.");
            TriggerLockedFeedback();
            return;
        }

        if (currentMode == newMode && !isTransitioning) return;

        currentMode = newMode;
        OnModeChanged?.Invoke(currentMode);

        if (animated && gameObject.activeInHierarchy)
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(AnimateModeTransition(newMode));
        }
        else
        {
            ApplyModeImmediate(newMode);
        }
    }

    /// <summary>
    /// Forces Photo mode with no animation. Called whenever the panel re-appears
    /// (card detected again). Does nothing while a recording is in progress.
    /// </summary>
    public void ResetToPhoto()
    {
        if (IsRecordingActive()) return;

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }
        isTransitioning = false;

        bool changed = currentMode != CameraMode.Photo;
        currentMode = CameraMode.Photo;
        if (changed) OnModeChanged?.Invoke(currentMode);

        ApplyModeImmediate(CameraMode.Photo);
    }

    public void SwipeLeft()
    {
        // Swipe left -> Photo Mode
        SwitchToMode(CameraMode.Photo, true);
    }

    public void SwipeRight()
    {
        // Swipe right -> Video Mode
        SwitchToMode(CameraMode.Video, true);
    }

    public void OnDragDelta(float deltaX)
    {
        if (IsRecordingActive()) return;

        if (deltaX < -swipeThreshold)
        {
            SwipeLeft();
        }
        else if (deltaX > swipeThreshold)
        {
            SwipeRight();
        }
    }

    private bool IsRecordingActive()
    {
        return screenRecorder != null && screenRecorder.IsRecording;
    }

    // ----------------------------------------------------
    // RECORDING STATE (arrow lock, label, recording ring)
    // ----------------------------------------------------
    /// <summary>
    /// Locks the arrow buttons while recording, optionally hides the mode label,
    /// and updates the recording ring.
    /// </summary>
    private void UpdateRecordingState()
    {
        bool recording = IsRecordingActive();

        if (leftArrowBtn != null && leftArrowBtn.gameObject.activeSelf)
        {
            leftArrowBtn.interactable = !recording;
        }

        if (rightArrowBtn != null && rightArrowBtn.gameObject.activeSelf)
        {
            rightArrowBtn.interactable = !recording;
        }

        if (modeIndicatorUniText != null)
        {
            bool showLabel = !(hideModeLabelWhileRecording && recording);
            if (modeIndicatorUniText.gameObject.activeSelf != showLabel)
            {
                modeIndicatorUniText.gameObject.SetActive(showLabel);
            }
        }

        if (Application.isPlaying)
        {
            UpdateRecordingRing(recording);
        }
    }

    /// <summary>
    /// Fill mode: the ring fills from 0 to 100% over 'recordingDuration' seconds,
    /// then optionally stops the recording.
    /// Rotate mode: the ring spins continuously while recording.
    /// Uses unscaled time so it is unaffected by Time.timeScale.
    /// </summary>
    private void UpdateRecordingRing(bool recording)
    {
        if (recordingRing == null)
        {
            wasRecording = recording;
            return;
        }

        if (showRingOnlyWhileRecording && recordingRing.gameObject.activeSelf != recording)
        {
            recordingRing.gameObject.SetActive(recording);
        }

        // Recording just started
        if (recording && !wasRecording)
        {
            recordStartTime = Time.unscaledTime;
            autoStopRequested = false;

            if (ringImage != null && fillRingInsteadOfRotate)
                ringImage.fillAmount = 0f;
        }

        if (recording)
        {
            if (fillRingInsteadOfRotate && ringImage != null)
            {
                float elapsed = Time.unscaledTime - recordStartTime;
                ringImage.fillAmount = Mathf.Clamp01(elapsed / recordingDuration);

                // Ring full -> stop recording
                if (autoStopAtDuration && elapsed >= recordingDuration && !autoStopRequested)
                {
                    autoStopRequested = true;
                    if (screenRecorder != null) screenRecorder.ToggleRecording();
                }
            }
            else
            {
                float direction = ringClockwise ? -1f : 1f;
                recordingRing.Rotate(0f, 0f, direction * ringRotationSpeed * Time.unscaledDeltaTime);
            }
        }
        else if (wasRecording && resetRingOnStop)
        {
            recordingRing.localRotation = Quaternion.identity;

            if (ringImage != null && fillRingInsteadOfRotate)
                ringImage.fillAmount = 0f;
        }

        wasRecording = recording;
    }

    /// <summary>
    /// Sets immediate visual state for the selected mode:
    /// Photo Mode: Photo Button visible, left arrow visible, right arrow hidden.
    /// Video Mode: Video Button visible, right arrow visible, left arrow hidden.
    /// </summary>
    public void ApplyModeImmediate(CameraMode mode)
    {
        bool isPhoto = (mode == CameraMode.Photo);

        // 1. Photo Button
        if (photoRect != null && photoCanvasGroup != null)
        {
            photoRect.anchoredPosition = isPhoto ? captureCenterPos : (captureCenterPos + Vector2.left * offscreenOffset);
            photoRect.localScale = isPhoto ? Vector3.one : new Vector3(0.8f, 0.8f, 1f);
            photoCanvasGroup.alpha = isPhoto ? 1f : 0f;
            photoCanvasGroup.interactable = isPhoto;
            photoCanvasGroup.blocksRaycasts = isPhoto;
        }

        // 2. Video Button
        if (videoRect != null && videoCanvasGroup != null)
        {
            videoRect.anchoredPosition = !isPhoto ? captureCenterPos : (captureCenterPos + Vector2.right * offscreenOffset);
            videoRect.localScale = !isPhoto ? Vector3.one : new Vector3(0.8f, 0.8f, 1f);
            videoCanvasGroup.alpha = !isPhoto ? 1f : 0f;
            videoCanvasGroup.interactable = !isPhoto;
            videoCanvasGroup.blocksRaycasts = !isPhoto;
        }

        // 3. Mutually exclusive arrow visibility
        if (leftArrowBtn != null)
        {
            leftArrowBtn.gameObject.SetActive(isPhoto);
        }

        if (rightArrowBtn != null)
        {
            rightArrowBtn.gameObject.SetActive(!isPhoto);
        }

        // 4. Mode Indicator Text
        SetLabel(modeIndicatorUniText,
                 isPhoto ? "PHOTO" : "VIDEO",
                 isPhoto ? photoModeColor : videoModeColor);
    }

    private IEnumerator AnimateModeTransition(CameraMode targetMode)
    {
        isTransitioning = true;
        bool isPhoto = (targetMode == CameraMode.Photo);

        // Hide inactive arrow immediately so they are never visible simultaneously
        if (isPhoto && rightArrowBtn != null) rightArrowBtn.gameObject.SetActive(false);
        if (!isPhoto && leftArrowBtn != null) leftArrowBtn.gameObject.SetActive(false);

        // Starting values for buttons
        Vector2 photoStartPos = photoRect != null ? photoRect.anchoredPosition : Vector2.zero;
        Vector2 photoTargetPos = isPhoto ? captureCenterPos : (captureCenterPos + Vector2.left * offscreenOffset);
        float photoStartAlpha = photoCanvasGroup != null ? photoCanvasGroup.alpha : 0f;
        float photoTargetAlpha = isPhoto ? 1f : 0f;
        Vector3 photoStartScale = photoRect != null ? photoRect.localScale : Vector3.one;
        Vector3 photoTargetScale = isPhoto ? Vector3.one : new Vector3(0.8f, 0.8f, 1f);

        Vector2 videoStartPos = videoRect != null ? videoRect.anchoredPosition : Vector2.zero;
        Vector2 videoTargetPos = !isPhoto ? captureCenterPos : (captureCenterPos + Vector2.right * offscreenOffset);
        float videoStartAlpha = videoCanvasGroup != null ? videoCanvasGroup.alpha : 0f;
        float videoTargetAlpha = !isPhoto ? 1f : 0f;
        Vector3 videoStartScale = videoRect != null ? videoRect.localScale : Vector3.one;
        Vector3 videoTargetScale = !isPhoto ? Vector3.one : new Vector3(0.8f, 0.8f, 1f);

        if (photoCanvasGroup != null)
        {
            photoCanvasGroup.interactable = isPhoto;
            photoCanvasGroup.blocksRaycasts = isPhoto;
        }
        if (videoCanvasGroup != null)
        {
            videoCanvasGroup.interactable = !isPhoto;
            videoCanvasGroup.blocksRaycasts = !isPhoto;
        }

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float ease = EaseOutQuad(t);

            if (photoRect != null)
            {
                photoRect.anchoredPosition = Vector2.Lerp(photoStartPos, photoTargetPos, ease);
                photoRect.localScale = Vector3.Lerp(photoStartScale, photoTargetScale, ease);
            }
            if (photoCanvasGroup != null)
            {
                photoCanvasGroup.alpha = Mathf.Lerp(photoStartAlpha, photoTargetAlpha, ease);
            }

            if (videoRect != null)
            {
                videoRect.anchoredPosition = Vector2.Lerp(videoStartPos, videoTargetPos, ease);
                videoRect.localScale = Vector3.Lerp(videoStartScale, videoTargetScale, ease);
            }
            if (videoCanvasGroup != null)
            {
                videoCanvasGroup.alpha = Mathf.Lerp(videoStartAlpha, videoTargetAlpha, ease);
            }

            yield return null;
        }

        ApplyModeImmediate(targetMode);
        isTransitioning = false;
    }

    private void PunchButtonScale(Button btn)
    {
        if (btn != null && btn.gameObject.activeInHierarchy)
        {
            StartCoroutine(PunchScaleRoutine(btn.GetComponent<RectTransform>()));
        }
    }

    private IEnumerator PunchScaleRoutine(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector3 orig = Vector3.one;
        rt.localScale = orig * 0.82f;
        yield return new WaitForSecondsRealtime(0.08f);
        rt.localScale = orig * 1.15f;
        yield return new WaitForSecondsRealtime(0.08f);
        rt.localScale = orig;
    }

    private void TriggerLockedFeedback()
    {
        if (leftArrowBtn != null && leftArrowBtn.gameObject.activeSelf)
            StartCoroutine(ShakeRoutine(leftArrowBtn.GetComponent<RectTransform>()));

        if (rightArrowBtn != null && rightArrowBtn.gameObject.activeSelf)
            StartCoroutine(ShakeRoutine(rightArrowBtn.GetComponent<RectTransform>()));
    }

    private IEnumerator ShakeRoutine(RectTransform target)
    {
        if (target == null) yield break;
        Vector2 orig = target.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            float offset = Mathf.Sin(elapsed * 50f) * 6f;
            target.anchoredPosition = new Vector2(orig.x + offset, orig.y);
            yield return null;
        }
        target.anchoredPosition = orig;
    }

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    // ----------------------------------------------------
    // BUTTON ACTIONS
    // ----------------------------------------------------
    private void OnPhotoButtonClicked()
    {
        if (currentMode != CameraMode.Photo)
        {
            SwitchToMode(CameraMode.Photo, true);
            return;
        }

        if (screenshotManager != null)
        {
            screenshotManager.CaptureModelPhoto();
        }
        else
        {
            Debug.LogWarning("[CameraModeSwipeController] ARScreenshotManager not assigned.");
        }
    }

    private void OnVideoButtonClicked()
    {
        if (currentMode != CameraMode.Video)
        {
            SwitchToMode(CameraMode.Video, true);
            return;
        }

        if (screenRecorder != null)
        {
            screenRecorder.ToggleRecording();
        }
        else
        {
            Debug.LogWarning("[CameraModeSwipeController] ScreenRecorderToggle not assigned.");
        }
    }

    // ----------------------------------------------------
    // INPUT HANDLING: MOBILE TOUCH & KEYBOARD
    // ----------------------------------------------------
    private void HandleMobileTouches()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.position;
                isTrackingTouch = true;
            }
            else if (touch.phase == TouchPhase.Ended && isTrackingTouch)
            {
                isTrackingTouch = false;
                Vector2 delta = touch.position - touchStartPos;

                if (touchStartPos.y < Screen.height * 0.45f)
                {
                    if (Mathf.Abs(delta.x) > swipeThreshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    {
                        if (delta.x < 0) SwipeLeft();
                        else SwipeRight();
                    }
                }
            }
        }
    }

    private void HandleKeyboardTesting()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            OnLeftArrowClicked();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            OnRightArrowClicked();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            if (currentMode == CameraMode.Photo) OnPhotoButtonClicked();
            else OnVideoButtonClicked();
        }
#endif
    }

    // ----------------------------------------------------
    // AUTO-SETUP OF ARROW UI (If not already created)
    // ----------------------------------------------------
    [ContextMenu("Rebuild Arrow Buttons in Hierarchy")]
    public void EnsureArrowSwapUI()
    {
        Transform parentTransform = (photoButton != null) ? photoButton.transform.parent : transform;
        if (parentTransform == null) return;

        // Clean up any old text bar if present
        Transform oldBar = parentTransform.Find("CameraModeBar");
        if (oldBar != null)
        {
            oldBar.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(oldBar.gameObject);
            else DestroyImmediate(oldBar.gameObject);
        }

        // 1. Left Arrow Button
        Transform existingLeft = parentTransform.Find("LeftArrowBtn");
        if (existingLeft != null)
        {
            leftArrowBtn = existingLeft.GetComponent<Button>();
        }

        // 2. Right Arrow Button
        Transform existingRight = parentTransform.Find("RightArrowBtn");
        if (existingRight != null)
        {
            rightArrowBtn = existingRight.GetComponent<Button>();
        }

        // 3. Mode Indicator Label
        Transform existingModeLbl = parentTransform.Find("ModeIndicatorLabel");
        if (existingModeLbl != null)
        {
            modeIndicatorUniText = existingModeLbl.GetComponent<UniText>();
        }

        // If elements are already found in hierarchy, just apply visibility and return
        if (leftArrowBtn != null && rightArrowBtn != null)
        {
            bool isPhotoNow = (currentMode == CameraMode.Photo);
            leftArrowBtn.gameObject.SetActive(isPhotoNow);
            rightArrowBtn.gameObject.SetActive(!isPhotoNow);
            return;
        }

        float centerY = captureCenterPos.y + 50f;
        Sprite circleSprite = FindCircleSprite();

        if (leftArrowBtn == null)
        {
            leftArrowBtn = CreateArrowButton("LeftArrowBtn", "<", parentTransform,
                new Vector2(-arrowSpacing, centerY), new Vector2(-1f, 1f), circleSprite, OnLeftArrowClicked);
        }

        if (rightArrowBtn == null)
        {
            rightArrowBtn = CreateArrowButton("RightArrowBtn", ">", parentTransform,
                new Vector2(arrowSpacing, centerY), new Vector2(1f, 1f), circleSprite, OnRightArrowClicked);
        }

        if (modeIndicatorUniText == null)
        {
            UniText lbl = CreateUniText("ModeIndicatorLabel", parentTransform, "PHOTO", 15f, photoModeColor);

            RectTransform lblRt = lbl.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0.5f, 0f);
            lblRt.anchorMax = new Vector2(0.5f, 0f);
            lblRt.pivot = new Vector2(0.5f, 0.5f);
            lblRt.anchoredPosition = new Vector2(0f, captureCenterPos.y + 125f);
            lblRt.sizeDelta = new Vector2(160f, 26f);

            modeIndicatorUniText = lbl;
        }

        // Apply mutually exclusive visibility immediately
        bool isPhoto = (currentMode == CameraMode.Photo);
        if (leftArrowBtn != null) leftArrowBtn.gameObject.SetActive(isPhoto);
        if (rightArrowBtn != null) rightArrowBtn.gameObject.SetActive(!isPhoto);
    }

    private Button CreateArrowButton(string objName, string glyph, Transform parent,
                                     Vector2 position, Vector2 textOffset, Sprite circleSprite,
                                     UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(48f, 48f);

        Image img = obj.GetComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(0.12f, 0.15f, 0.20f, 0.85f);

        // Arrow glyph (UniText)
        UniText arrowText = CreateUniText("ArrowText", obj.transform, glyph, 26f, Color.white);
        RectTransform tRt = arrowText.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.sizeDelta = Vector2.zero;
        tRt.anchoredPosition = textOffset;

        Button btn = obj.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.7f, 0.85f, 1f, 1f);
        cb.pressedColor = new Color(0.5f, 0.7f, 1f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        return btn;
    }

    private Sprite FindCircleSprite()
    {
        if (photoButton != null && photoButton.image != null && photoButton.image.sprite != null)
            return photoButton.image.sprite;
        if (videoButton != null && videoButton.image != null && videoButton.image.sprite != null)
            return videoButton.image.sprite;
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoEnsureController()
    {
        if (Instance != null) return;

        GameObject actionsPanel = GameObject.Find("Actions Buttons Panel") ?? GameObject.Find("ActionButtonsPanel");
        if (actionsPanel != null)
        {
            var comp = actionsPanel.GetComponent<CameraModeSwipeController>();
            if (comp == null) comp = actionsPanel.AddComponent<CameraModeSwipeController>();
            Instance = comp;
        }
    }
}

/// <summary>
/// Added to the Photo/Video buttons so horizontal drags on them switch camera mode.
/// </summary>
public class SwipeDragForwarder : MonoBehaviour, IDragHandler, IEndDragHandler
{
    private CameraModeSwipeController controller;

    public void Init(CameraModeSwipeController owner)
    {
        controller = owner;
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (controller == null) return;

        Vector2 delta = eventData.position - eventData.pressPosition;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            controller.OnDragDelta(delta.x);
        }
    }
}