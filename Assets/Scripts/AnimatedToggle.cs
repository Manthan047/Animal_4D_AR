using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class AnimatedToggle : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform knob;
    [SerializeField] private Image background;

    [Header("Direct Buttons (Optional)")]
    [SerializeField] private Button twoDButton;
    [SerializeField] private Button threeDButton;

    [Header("Optional Arrows / Labels")]
    [SerializeField] private TMP_Text onArrows;   // 3D side arrows/label
    [SerializeField] private TMP_Text offArrows;  // 2D side arrows/label

    [Header("Colors")]
    [SerializeField] private Color onColor = new Color(0.2f, 0.6f, 1f);   // 3D Color
    [SerializeField] private Color offColor = new Color(0.8f, 0.8f, 0.8f); // 2D Color

    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.15f;
    [Tooltip("If the knob's anchored X is 0 (centered), specify a manual travel distance here. 0 = auto-detect from anchors/width.")]
    [SerializeField] private float manualTravelDistance = 55f;

    // Event used by ARUIManager (true = 3D, false = 2D)
    [System.Serializable]
    public class BoolEvent : UnityEvent<bool> { }

    public BoolEvent onValueChanged = new BoolEvent();

    public bool IsOn { get; private set; }

    public bool isOn
    {
        get => IsOn;
        set => SetIsOn(value);
    }

    private Vector2 onPosition;
    private Vector2 offPosition;
    private Coroutine animCoroutine;
    private bool isInitialized = false;

    private void Awake()
    {
        InitializePositions();

        if (twoDButton != null)
        {
            twoDButton.onClick.AddListener(Set2D);
        }

        if (threeDButton != null)
        {
            threeDButton.onClick.AddListener(Set3D);
        }
    }

    private void InitializePositions()
    {
        if (isInitialized) return;

        if (knob == null)
        {
            Debug.LogWarning("[AnimatedToggle] Knob is not assigned.", this);
            return;
        }

        float travelX = Mathf.Abs(knob.anchoredPosition.x);

        if (Mathf.Approximately(travelX, 0f))
        {
            if (manualTravelDistance > 0f)
            {
                travelX = manualTravelDistance;
            }
            else if (knob.parent is RectTransform parentRect)
            {
                travelX = Mathf.Max(20f, (parentRect.rect.width - knob.rect.width) / 2f);
            }
            else
            {
                travelX = 55f;
            }
        }

        float offX = -Mathf.Abs(travelX);
        float onX = Mathf.Abs(travelX);

        offPosition = new Vector2(offX, knob.anchoredPosition.y);
        onPosition = new Vector2(onX, knob.anchoredPosition.y);
        isInitialized = true;
    }

    // --------------------------------------------------
    // Public API for Buttons / OnClick
    // --------------------------------------------------

    public void Toggle()
    {
        SetIsOn(!IsOn);
    }

    public void Set3D()
    {
        SetIsOn(true);
    }

    public void Set2D()
    {
        SetIsOn(false);
    }

    // --------------------------------------------------
    // Changes state and invokes event
    // --------------------------------------------------

    public void SetIsOn(bool value)
    {
        InitializePositions();

        if (IsOn == value)
        {
            UpdateVisuals(true);
            return;
        }

        IsOn = value;
        UpdateVisuals(true);
        onValueChanged.Invoke(IsOn);
    }

    // --------------------------------------------------
    // Changes state WITHOUT invoking event
    // --------------------------------------------------

    public void SetIsOnWithoutNotify(bool value)
    {
        InitializePositions();
        IsOn = value;
        UpdateVisuals(false);
    }

    // --------------------------------------------------
    // Update UI
    // --------------------------------------------------

    private void UpdateVisuals(bool animate)
    {
        if (background != null)
        {
            background.color = IsOn ? onColor : offColor;
        }

        if (onArrows != null)
        {
            onArrows.gameObject.SetActive(IsOn);
        }

        if (offArrows != null)
        {
            offArrows.gameObject.SetActive(!IsOn);
        }

        if (knob == null)
            return;

        Vector2 target = IsOn ? onPosition : offPosition;

        if (!animate || !Application.isPlaying || animationDuration <= 0f)
        {
            knob.anchoredPosition = target;
            return;
        }

        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
        }

        animCoroutine = StartCoroutine(AnimateKnob(target));
    }

    private IEnumerator AnimateKnob(Vector2 target)
    {
        Vector2 start = knob.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Ease out for smooth motion
            t = 1f - (1f - t) * (1f - t);

            knob.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        knob.anchoredPosition = target;
        animCoroutine = null;
    }
}