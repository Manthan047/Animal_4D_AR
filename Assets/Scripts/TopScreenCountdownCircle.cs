using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightSide; // UniText support

/// <summary>
/// Circular countdown HUD displayed at the top of the screen ("above the screen and in circle")
/// during AR camera video recording.
/// </summary>
[DisallowMultipleComponent]
public class TopScreenCountdownCircle : MonoBehaviour
{
    public static TopScreenCountdownCircle Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform circleRect;
    [SerializeField] private Image circleBackground;
    [SerializeField] private Image outerGlowRing;
    [SerializeField] private Image recordingDot;
    [SerializeField] private TMP_Text countdownTmpText;
    [SerializeField] private UniText countdownUniText;
    [SerializeField] private Text countdownLegacyText;

    [Header("Styling")]
    [SerializeField] private Color normalRingColor = new Color(1f, 0.25f, 0.25f, 0.85f);
    [SerializeField] private Color savedRingColor = new Color(0.2f, 0.9f, 0.4f, 0.95f);
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.12f, 0.16f, 0.92f);
    [SerializeField] private bool showSecondsSuffix = false; // e.g. "60s" vs "60"

    private Coroutine animCoroutine;
    private Coroutine pulseCoroutine;
    private bool isVisible = false;

    public bool IsVisible => isVisible;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (circleRect == null)
            circleRect = GetComponent<RectTransform>();

        // Start hidden by default
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Activates the top circular countdown HUD and begins ticking.
    /// </summary>
    public void ShowCountdown(float totalDuration)
    {
        isVisible = true;
        gameObject.SetActive(true);

        if (outerGlowRing != null)
            outerGlowRing.color = normalRingColor;

        if (recordingDot != null)
            recordingDot.gameObject.SetActive(true);

        int initialSeconds = Mathf.CeilToInt(totalDuration);
        SetText(FormatSeconds(initialSeconds));

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateVisibilityRoutine(true));

        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    /// <summary>
    /// Updates the remaining countdown seconds displayed inside the circle.
    /// </summary>
    public void UpdateSeconds(int secondsRemaining)
    {
        SetText(FormatSeconds(secondsRemaining));

    }

    /// <summary>
    /// Displays saved confirmation feedback briefly, then smoothly fades out.
    /// </summary>
    public void ShowSaved(string message = "Saved!")
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (outerGlowRing != null)
            outerGlowRing.color = savedRingColor;

        if (recordingDot != null)
            recordingDot.gameObject.SetActive(false);

        SetText(message);

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(ShowSavedAndHideRoutine());
    }

    /// <summary>
    /// Immediately hides the countdown HUD.
    /// </summary>
    public void HideImmediately()
    {
        isVisible = false;
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    private string FormatSeconds(int seconds)
    {
        return showSecondsSuffix ? $"{seconds}s" : seconds.ToString();
    }

    private void SetText(string text)
    {
        if (countdownTmpText != null) countdownTmpText.text = text;
        if (countdownUniText != null) countdownUniText.Text = text;
        if (countdownLegacyText != null) countdownLegacyText.text = text;
    }

    private IEnumerator AnimateVisibilityRoutine(bool show)
    {
        if (canvasGroup == null)
        {
            gameObject.SetActive(show);
            yield break;
        }

        float startAlpha = canvasGroup.alpha;
        float targetAlpha = show ? 1f : 0f;
        Vector3 startScale = show ? new Vector3(0.7f, 0.7f, 1f) : circleRect.localScale;
        Vector3 targetScale = show ? Vector3.one : new Vector3(0.7f, 0.7f, 1f);

        circleRect.localScale = startScale;
        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Smooth ease out overshoot on appear
            float scaleT = show ? EaseOutBack(t) : EaseOutQuad(t);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            circleRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, scaleT);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        circleRect.localScale = targetScale;

        if (!show)
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator PulseRoutine()
    {
        // Gentle breathing pulse for the recording dot and glow ring
        while (isVisible)
        {
            float wave = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f; // 0 to 1

            if (recordingDot != null)
            {
                Color c = recordingDot.color;
                c.a = Mathf.Lerp(0.35f, 1f, wave);
                recordingDot.color = c;
            }

            if (outerGlowRing != null)
            {
                Color gc = normalRingColor;
                gc.a = Mathf.Lerp(0.45f, 0.95f, wave);
                outerGlowRing.color = gc;
            }

            yield return null;
        }
    }

    private IEnumerator ShowSavedAndHideRoutine()
    {
        // Punch scale on saved feedback
        Vector3 origScale = Vector3.one;
        circleRect.localScale = origScale * 1.1f;

        yield return new WaitForSecondsRealtime(0.12f);
        circleRect.localScale = origScale;

        yield return new WaitForSecondsRealtime(1.35f);

        isVisible = false;
        yield return AnimateVisibilityRoutine(false);
    }

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>
    /// Auto-locates or creates the TopScreenCountdownCircle under the scene's primary Canvas.
    /// </summary>
    public static TopScreenCountdownCircle EnsureInstance(Canvas parentCanvas = null)
    {
        if (Instance != null) return Instance;

        // Fast search active and inactive objects in current scene
        var existing = Object.FindAnyObjectByType<TopScreenCountdownCircle>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var foundObj = GameObject.Find("TopRecordingCountdownCircle");
        if (foundObj != null)
        {
            var comp = foundObj.GetComponent<TopScreenCountdownCircle>();
            if (comp == null) comp = foundObj.AddComponent<TopScreenCountdownCircle>();
            Instance = comp;
            return Instance;
        }

        // Dynamically build if not found
        if (parentCanvas == null)
        {
            parentCanvas = Object.FindAnyObjectByType<Canvas>();
        }

        if (parentCanvas == null)
        {
            Debug.LogWarning("[TopScreenCountdownCircle] No Canvas found in scene to parent countdown HUD.");
            return null;
        }

        Sprite circleSprite = FindCircleSprite();

        // 1. Root GameObject
        GameObject root = new GameObject("TopRecordingCountdownCircle", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
        root.transform.SetParent(parentCanvas.transform, false);

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, -45f);
        rootRt.sizeDelta = new Vector2(90f, 90f);

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // 2. Outer Glow Ring
        GameObject ringObj = new GameObject("OuterGlowRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ringObj.transform.SetParent(root.transform, false);
        RectTransform ringRt = ringObj.GetComponent<RectTransform>();
        ringRt.anchorMin = Vector2.zero;
        ringRt.anchorMax = Vector2.one;
        ringRt.sizeDelta = new Vector2(10f, 10f); // slight border outside
        ringRt.anchoredPosition = Vector2.zero;
        Image ringImg = ringObj.GetComponent<Image>();
        if (circleSprite != null) ringImg.sprite = circleSprite;
        ringImg.color = new Color(1f, 0.25f, 0.25f, 0.85f);
        ringImg.raycastTarget = false;

        // 3. Inner Circle Background
        GameObject bgObj = new GameObject("CircleBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgObj.transform.SetParent(root.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        bgRt.anchoredPosition = Vector2.zero;
        Image bgImg = bgObj.GetComponent<Image>();
        if (circleSprite != null) bgImg.sprite = circleSprite;
        bgImg.color = new Color(0.12f, 0.12f, 0.16f, 0.92f);
        bgImg.raycastTarget = false;

        // 4. Pulsing REC dot at top of circle
        GameObject dotObj = new GameObject("RecDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dotObj.transform.SetParent(root.transform, false);
        RectTransform dotRt = dotObj.GetComponent<RectTransform>();
        dotRt.anchorMin = new Vector2(0.5f, 1f);
        dotRt.anchorMax = new Vector2(0.5f, 1f);
        dotRt.pivot = new Vector2(0.5f, 1f);
        dotRt.anchoredPosition = new Vector2(0f, -12f);
        dotRt.sizeDelta = new Vector2(10f, 10f);
        Image dotImg = dotObj.GetComponent<Image>();
        if (circleSprite != null) dotImg.sprite = circleSprite;
        dotImg.color = new Color(1f, 0.2f, 0.2f, 1f);
        dotImg.raycastTarget = false;

        // 5. Countdown Text in center of circle
        GameObject textObj = new GameObject("CountdownText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(root.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = new Vector2(-10f, -14f);
        textRt.anchoredPosition = new Vector2(0f, -5f);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "60";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 30;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 16;
        tmp.fontSizeMax = 32;
        tmp.raycastTarget = false;

        // Wire up TopScreenCountdownCircle component
        var hud = root.AddComponent<TopScreenCountdownCircle>();
        hud.canvasGroup = cg;
        hud.circleRect = rootRt;
        hud.circleBackground = bgImg;
        hud.outerGlowRing = ringImg;
        hud.recordingDot = dotImg;
        hud.countdownTmpText = tmp;

        Instance = hud;
        root.SetActive(false);

        Debug.Log("[TopScreenCountdownCircle] Auto-created circular countdown HUD at top of Canvas.");
        return hud;
    }

    private static Sprite FindCircleSprite()
    {
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in buttons)
        {
            if (b.image != null && b.image.sprite != null)
                return b.image.sprite;
        }
        return null;
    }
}
