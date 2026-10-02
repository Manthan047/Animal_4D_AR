using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CardViewSlider : MonoBehaviour
{
    [Header("Assign these")]
    [SerializeField] private RectTransform card;
    [SerializeField] private Button button;

    [Header("Button Images")]
    [Tooltip("The Image component that shows the icon. If empty, the Button's own image is used.")]
    [SerializeField] private Image buttonImage;
    [Tooltip("Sprite shown when the card is UP (visible)")]
    [SerializeField] private Sprite cardUpSprite;
    [Tooltip("Sprite shown when the card is DOWN (hidden)")]
    [SerializeField] private Sprite cardDownSprite;

    [Header("Settings")]
    [SerializeField] private float moveDistance = 0f;
    [SerializeField] private float extraOffset = 20f;
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector2 shownPos;
    private Vector2 hiddenPos;
    private bool isShown = true;
    private Coroutine routine;

    private void Start()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);

        shownPos = card.anchoredPosition;

        float distance = moveDistance > 0f ? moveDistance : card.rect.height;
        hiddenPos = shownPos + Vector2.down * (distance + extraOffset);

        if (buttonImage == null && button != null)
            buttonImage = button.image;

        UpdateButtonImage();   // set the correct image at start
        button.onClick.AddListener(Toggle);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        isShown = !isShown;
        UpdateButtonImage();   // change image immediately on click

        Vector2 target = isShown ? shownPos : hiddenPos;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Slide(target));
    }

    private void UpdateButtonImage()
    {
        if (buttonImage == null) return;

        Sprite s = isShown ? cardUpSprite : cardDownSprite;
        if (s != null) buttonImage.sprite = s;
    }

    private IEnumerator Slide(Vector2 target)
    {
        Vector2 start = card.anchoredPosition;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
            card.anchoredPosition = Vector2.LerpUnclamped(start, target, easing.Evaluate(Mathf.Clamp01(t)));
            yield return null;
        }

        card.anchoredPosition = target;
    }
}