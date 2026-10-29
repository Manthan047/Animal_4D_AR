using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightSide; // For UniText used in Animal_4D_AR

public class ScreenRecorderToggle : MonoBehaviour
{
    public enum CountdownFormat
    {
        SecondsOnly,       // "60s", "59s" ... "0s"
        RawNumber,         // "60", "59" ... "0"
        MinutesAndSeconds, // "01:00", "00:59" ... "00:00"
        LabelWithSeconds   // "REC 60s", "REC 59s" ...
    }

    [Header("UI References")]
    [Tooltip("The Video Button. Auto-detected by name 'Video_button' if not assigned.")]
    [SerializeField] private Button recordButton;

    [Tooltip("UniText component (used in this project). Auto-detected if not assigned.")]
    [SerializeField] private UniText uniButtonText;

    [Tooltip("TextMeshPro text component. Auto-detected if present.")]
    [SerializeField] private TMP_Text buttonText;

    [Tooltip("Legacy UI Text component. Auto-detected if present.")]
    [SerializeField] private Text legacyButtonText;

    [Header("Camera Capture Reference")]
    [Tooltip("CameraTextureCapture component on ARCamera. Auto-detected if empty.")]
    [SerializeField] private CameraTextureCapture cameraCapture;

    [Header("Timer Settings")]
    [Tooltip("Countdown duration in seconds.")]
    [SerializeField] private float recordingDuration = 60f;

    [Tooltip("Display format for the countdown text.")]
    [SerializeField] private CountdownFormat countdownFormat = CountdownFormat.SecondsOnly;

    [Header("Save & Gallery Settings")]
    [Tooltip("Album name in device gallery where saved videos will appear.")]
    [SerializeField] private string galleryAlbumName = "Animal4D";

    [Tooltip("Open native Share sheet after recording finishes.")]
    [SerializeField] private bool shareAfterRecording = false;

    [Header("Visual Feedback (Optional)")]
    [Tooltip("Graphic to tint while recording (e.g. record button image).")]
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color recordingColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Top Screen Circular Countdown Indicator")]
    [Tooltip("Circular countdown HUD visible above the screen during video recording. Auto-detected or created if empty.")]
    [SerializeField] private TopScreenCountdownCircle topCountdownCircle;

    public static event Action<bool> OnRecordingStateChanged;
    public static event Action<int> OnRecordingCountdownTick;

    // Runtime state
    private bool isRecording = false;
    private string idleText = "Video";
    private Color originalGraphicColor = Color.white;
    private Coroutine countdownCoroutine;
    private Coroutine feedbackCoroutine;
    private string currentOutputPath = string.Empty;
    private string currentFileName = string.Empty;

    public bool IsRecording => isRecording;
    public string LastSavedVideoPath => currentOutputPath;

    private float lastToggleTime = 0f;
    private float recordingStartTime = 0f;

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();

        if (recordButton != null)
        {
            recordButton.onClick.RemoveListener(ToggleRecording);

            // If CameraModeSwipeController is in the scene, it manages the button click
            // to avoid duplicate simultaneous calls.
            if (FindAnyObjectByType<CameraModeSwipeController>() == null)
            {
                recordButton.onClick.AddListener(ToggleRecording);
            }
        }
    }

    private void OnDestroy()
    {
        if (recordButton != null)
        {
            recordButton.onClick.RemoveListener(ToggleRecording);
        }

        if (isRecording)
        {
            StopRecording();
        }
    }

    private void ResolveReferences()
    {
        // 1. Resolve Button
        if (recordButton == null)
        {
            recordButton = GetComponent<Button>();

            if (recordButton == null)
            {
                var foundObj = GameObject.Find("Video_button");
                if (foundObj != null)
                {
                    recordButton = foundObj.GetComponent<Button>();
                }
            }
        }

        // 2. Resolve Text Components
        if (recordButton != null)
        {
            if (uniButtonText == null)
            {
                uniButtonText = recordButton.GetComponentInChildren<UniText>(true);
            }

            if (buttonText == null)
            {
                buttonText = recordButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (legacyButtonText == null)
            {
                legacyButtonText = recordButton.GetComponentInChildren<Text>(true);
            }

            if (targetGraphic == null)
            {
                targetGraphic = recordButton.targetGraphic;
            }
        }

        // 3. Resolve CameraTextureCapture
        if (cameraCapture == null)
        {
            var arCameraObj = GameObject.Find("ARCamera");
            Camera cam = null;

            if (arCameraObj != null)
            {
                cam = arCameraObj.GetComponent<Camera>();
                cameraCapture = arCameraObj.GetComponent<CameraTextureCapture>();
            }

            if (cam == null)
            {
                cam = Camera.main ?? FindAnyObjectByType<Camera>();
            }

            if (cam != null && cameraCapture == null)
            {
                cameraCapture = cam.GetComponent<CameraTextureCapture>();
                if (cameraCapture == null)
                {
                    cameraCapture = cam.gameObject.AddComponent<CameraTextureCapture>();
                    Debug.Log($"[ScreenRecorderToggle] Added CameraTextureCapture to: {cam.gameObject.name}");
                }
            }
        }

        // 4. Resolve TopScreenCountdownCircle
        if (topCountdownCircle == null)
        {
            topCountdownCircle = TopScreenCountdownCircle.Instance ?? TopScreenCountdownCircle.EnsureInstance();
        }
    }

    private void CacheInitialState()
    {
        string current = GetCurrentText();
        if (!string.IsNullOrEmpty(current))
        {
            idleText = current.Trim();
        }

        if (targetGraphic != null)
        {
            originalGraphicColor = targetGraphic.color;
        }
    }

    public void ToggleRecording()
    {
        // Debounce protection: ignore calls less than 0.5s apart to prevent accidental double-trigger
        if (Time.unscaledTime - lastToggleTime < 0.5f)
        {
            Debug.Log("[ScreenRecorderToggle] Ignored rapid ToggleRecording call (debounce active).");
            return;
        }
        lastToggleTime = Time.unscaledTime;

        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopRecording();
        }
    }

    public void StartRecording()
    {
        if (isRecording) return;

        isRecording = true;
        recordingStartTime = Time.unscaledTime;
        Debug.Log($"[ScreenRecorderToggle] Starting in-app AR camera recording for {recordingDuration} seconds...");

        if (targetGraphic != null)
        {
            targetGraphic.color = recordingColor;
        }

        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        // Start hardware encoder & frame capture
        StartInAppRecording();

        // Show circular countdown at the top of the screen
        if (topCountdownCircle == null)
        {
            topCountdownCircle = TopScreenCountdownCircle.Instance ?? TopScreenCountdownCircle.EnsureInstance();
        }
        if (topCountdownCircle != null)
        {
            topCountdownCircle.ShowCountdown(recordingDuration);
        }

        OnRecordingStateChanged?.Invoke(true);

        // Start 60 -> 0 Countdown
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }
        countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    public void StopRecording()
    {
        if (!isRecording && countdownCoroutine == null) return;

        // Ensure recording runs for at least 0.8s so media encoder writes actual frames
        if (isRecording && (Time.unscaledTime - recordingStartTime < 0.8f))
        {
            Debug.LogWarning("[ScreenRecorderToggle] Recording must run for at least 0.8 seconds before stopping.");
            return;
        }

        isRecording = false;

        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        // Show saving status in circular countdown and on button
        if (topCountdownCircle != null)
        {
            topCountdownCircle.ShowSaved("Saved!");
        }

        OnRecordingStateChanged?.Invoke(false);

        SetDisplayText("Saving...");
        if (targetGraphic != null)
        {
            targetGraphic.color = originalGraphicColor;
        }

        Debug.Log("[ScreenRecorderToggle] Stopping in-app AR camera recording...");
        StopInAppRecording();
    }

    private void StartInAppRecording()
    {
        currentFileName = "Animal4D_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";
        currentOutputPath = Path.Combine(Application.persistentDataPath, currentFileName);

        if (cameraCapture == null)
        {
            ResolveReferences();
        }

        int width = 720;
        int height = 1280;
        int fps = 30;

        if (cameraCapture != null)
        {
            cameraCapture.CalculateDimensions();
            width = cameraCapture.CaptureWidth;
            height = cameraCapture.CaptureHeight;
            fps = cameraCapture.TargetFps;
        }

#if !UNITY_EDITOR && UNITY_ANDROID
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var recorder = new AndroidJavaClass("com.animal4d.recorder.CameraVideoRecorder"))
            {
                bool success = recorder.CallStatic<bool>("startRecording", activity, currentOutputPath, width, height, fps, 4_000_000);
                Debug.Log($"[ScreenRecorderToggle] Native MediaCodec startRecording result: {success} ({width}x{height} @ {fps}fps)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScreenRecorderToggle] Failed to start native Android recorder: {ex.Message}");
        }
#else
        Debug.Log($"[ScreenRecorderToggle] [Editor Simulation] Started camera recording. Simulated Output: {currentOutputPath} ({width}x{height})");
#endif

        if (cameraCapture != null)
        {
            cameraCapture.StartCapture(width, height, fps);
        }
    }

    private void StopInAppRecording()
    {
        if (cameraCapture != null)
        {
            cameraCapture.StopCapture();
        }

#if !UNITY_EDITOR && UNITY_ANDROID
        try
        {
            using (var recorder = new AndroidJavaClass("com.animal4d.recorder.CameraVideoRecorder"))
            {
                bool stopped = recorder.CallStatic<bool>("stopRecording");
                int frameCount = recorder.CallStatic<int>("getFrameCount");
                Debug.Log($"[ScreenRecorderToggle] Native MediaCodec stopRecording result: {stopped}, encoded frames: {frameCount}");
            }

            // Verify file was written and has contents
            if (File.Exists(currentOutputPath))
            {
                FileInfo fi = new FileInfo(currentOutputPath);
                Debug.Log($"[ScreenRecorderToggle] Output MP4 file size: {fi.Length} bytes");

                if (fi.Length > 0)
                {
                    NativeGallery.SaveVideoToGallery(currentOutputPath, galleryAlbumName, currentFileName, (success, path) =>
                    {
                        if (success)
                        {
                            Debug.Log($"<color=green>[ScreenRecorderToggle] Video saved to Gallery album '{galleryAlbumName}'!</color> Path: {path}");
                        }
                        else
                        {
                            Debug.LogWarning("[ScreenRecorderToggle] NativeGallery save callback returned false.");
                        }

                        if (shareAfterRecording && !string.IsNullOrEmpty(path))
                        {
                            new NativeShare()
                                .AddFile(path)
                                .SetSubject("Animal 4D AR Video")
                                .SetTitle("Share Animal 4D Video")
                                .Share();
                        }
                    });
                }
                else
                {
                    Debug.LogWarning("[ScreenRecorderToggle] Output MP4 file was empty (0 bytes).");
                }
            }
            else
            {
                Debug.LogWarning($"[ScreenRecorderToggle] Output file does not exist: {currentOutputPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScreenRecorderToggle] Error stopping native Android recorder: {ex.Message}");
        }
#else
        Debug.Log($"[ScreenRecorderToggle] [Editor Simulation] Video recording finished: {currentOutputPath}");
#endif

        feedbackCoroutine = StartCoroutine(ShowSavedFeedbackRoutine("Saved!"));
    }

    private IEnumerator CountdownRoutine()
    {
        float timeLeft = recordingDuration;

        while (timeLeft > 0f)
        {
            int displaySeconds = Mathf.CeilToInt(timeLeft);
            SetDisplayText(FormatCountdownText(displaySeconds));
            if (topCountdownCircle != null)
            {
                topCountdownCircle.UpdateSeconds(displaySeconds);
            }
            OnRecordingCountdownTick?.Invoke(displaySeconds);

            yield return null;
            timeLeft -= Time.deltaTime;
        }

        SetDisplayText(FormatCountdownText(0));
        if (topCountdownCircle != null)
        {
            topCountdownCircle.UpdateSeconds(0);
        }
        OnRecordingCountdownTick?.Invoke(0);
        yield return new WaitForSeconds(0.25f);

        // Auto-stop when timer reaches 0
        StopRecording();
    }

    private string FormatCountdownText(int seconds)
    {
        switch (countdownFormat)
        {
            case CountdownFormat.RawNumber:
                return seconds.ToString();

            case CountdownFormat.MinutesAndSeconds:
                int mins = seconds / 60;
                int secs = seconds % 60;
                return $"{mins:00}:{secs:00}";

            case CountdownFormat.LabelWithSeconds:
                return $"REC {seconds}s";

            case CountdownFormat.SecondsOnly:
            default:
                return $"{seconds}s";
        }
    }

    private void SetDisplayText(string text)
    {
        if (uniButtonText != null) uniButtonText.Text = text;
        if (buttonText != null) buttonText.text = text;
        if (legacyButtonText != null) legacyButtonText.text = text;
    }

    private string GetCurrentText()
    {
        if (uniButtonText != null && !string.IsNullOrEmpty(uniButtonText.Text))
            return uniButtonText.Text;

        if (buttonText != null && !string.IsNullOrEmpty(buttonText.text))
            return buttonText.text;

        if (legacyButtonText != null && !string.IsNullOrEmpty(legacyButtonText.text))
            return legacyButtonText.text;

        return "";
    }
    
    private IEnumerator ShowSavedFeedbackRoutine(string message = "Saved!")
    {
        SetDisplayText(message);
        yield return new WaitForSeconds(2.0f);
        SetDisplayText(idleText);
    }
}