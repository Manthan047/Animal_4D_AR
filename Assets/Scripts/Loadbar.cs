using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using LightSide; // UniText namespace

/// <summary>
/// Reusable load bar.
/// - Works with a Slider and/or a filled Image (Image Type = Filled).
/// - Optional UniText percentage label.
/// - Either runs by itself over 'duration' seconds (Play) or is driven manually (SetProgress).
/// - Fires OnCompleted when it reaches 100%.
/// Uses unscaled time, so it is unaffected by Time.timeScale.
/// </summary>
[DisallowMultipleComponent]
public class LoadBar : MonoBehaviour
{
    [Header("Visuals (assign one or both)")]
    [Tooltip("Slider used as the bar. Its value is set from 0 to 1.")]
    [SerializeField] private Slider slider;

    [Tooltip("Filled Image used as the bar (Image Type is set to Filled automatically).")]
    [SerializeField] private Image fillImage;

    [Tooltip("Optional percentage label, e.g. 'Loading 45%'.")]
    [SerializeField] private UniText percentText;

    [Tooltip("Text format. {0} is replaced by the percentage number.")]
    [SerializeField] private string textFormat = "Loading {0}%";

    [Header("Timed Loading")]
    [Tooltip("Seconds to go from 0% to 100% when using Play().")]
    [SerializeField] private float duration = 1f;

    [Tooltip("Pause at 100% before OnCompleted fires.")]
    [SerializeField] private float finishDelay = 0.1f;

    [Tooltip("Start playing automatically when this object is enabled.")]
    [SerializeField] private bool playOnEnable = false;

    [Tooltip("Hide this object when loading completes.")]
    [SerializeField] private bool hideOnComplete = false;

    [Header("Smoothing (manual SetProgress only)")]
    [Tooltip("How quickly the bar catches up to the target value. 0 = instant.")]
    [SerializeField] private float smoothSpeed = 6f;

    [Header("Events")]
    public UnityEvent OnCompleted;

    /// <summary>Current displayed progress, 0..1.</summary>
    public float Progress { get; private set; }

    /// <summary>True while a timed Play() is running.</summary>
    public bool IsPlaying => playRoutine != null;

    /// <summary>C# event version of OnCompleted.</summary>
    public event Action Completed;

    private Coroutine playRoutine;
    private float targetProgress;
    private int lastPercent = -1;
    private bool completedFired;

    private void Awake()
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = false;   // user must not drag it
        }

        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        Apply(0f);
    }

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    private void OnDisable()
    {
        playRoutine = null; // coroutines stop automatically when disabled
    }

    private void Update()
    {
        // Manual mode: ease the displayed value toward the target.
        if (playRoutine != null) return;
        if (Mathf.Approximately(Progress, targetProgress)) return;

        float next = smoothSpeed <= 0f
            ? targetProgress
            : Mathf.MoveTowards(Progress, targetProgress, smoothSpeed * Time.unscaledDeltaTime);

        Apply(next);

        if (Progress >= 1f) FireCompleted();
    }

    // ----------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------

    /// <summary>Runs the bar from 0% to 100% over 'duration' seconds.</summary>
    public void Play()
    {
        Play(duration);

    }

    public void Play(float seconds)
    {
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

        if (playRoutine != null) StopCoroutine(playRoutine);
        completedFired = false;
        targetProgress = 0f;
        Apply(0f);
        playRoutine = StartCoroutine(PlayRoutine(Mathf.Max(0.01f, seconds)));
    }

    /// <summary>Sets the target progress (0..1). Use this for real loading (e.g. async operations).</summary>
    public void SetProgress(float value)
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        targetProgress = Mathf.Clamp01(value);
        if (targetProgress < 1f) completedFired = false;
    }

    /// <summary>Jumps to a value immediately with no smoothing.</summary>
    public void SetProgressInstant(float value)
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        targetProgress = Mathf.Clamp01(value);
        completedFired = false;
        Apply(targetProgress);

        if (Progress >= 1f) FireCompleted();
    }

    /// <summary>Stops and resets the bar to 0%.</summary>
    public void ResetBar()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        targetProgress = 0f;
        completedFired = false;
        lastPercent = -1;
        Apply(0f);
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }

    // ----------------------------------------------------
    // INTERNAL
    // ----------------------------------------------------

    private IEnumerator PlayRoutine(float seconds)
    {
        // Let the first frame (with any one-time hitch) pass before the timer starts.
        yield return null;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            // Clamp so a single slow frame can't make the bar jump.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Apply(Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        Apply(1f);
        targetProgress = 1f;

        if (finishDelay > 0f)
            yield return new WaitForSecondsRealtime(finishDelay);

        playRoutine = null;
        FireCompleted();
    }

    private void Apply(float value)
    {
        Progress = Mathf.Clamp01(value);

        if (slider != null) slider.value = Progress;
        if (fillImage != null) fillImage.fillAmount = Progress;

        UpdateText();
    }

    private void UpdateText()
    {
        if (percentText == null) return;

        int percent = Mathf.RoundToInt(Progress * 100f);
        if (percent == lastPercent) return;   // only re-layout the text when the number changes
        lastPercent = percent;

        percentText.Text = string.Format(textFormat, percent);
    }

    private void FireCompleted()
    {
        if (completedFired) return;
        completedFired = true;

        OnCompleted?.Invoke();
        Completed?.Invoke();

        if (hideOnComplete) gameObject.SetActive(false);
    }

    private void OnValidate()
    {
        if (duration < 0.01f) duration = 0.01f;
        if (finishDelay < 0f) finishDelay = 0f;
    }
}