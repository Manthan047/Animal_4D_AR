using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aurora LoadBar - a procedural, animated loading bar for Unity UI.
///
/// Features:
///  * Smooth sub-pixel fill edge
///  * Flowing aurora gradient that drifts over time
///  * White shimmer band sweeping across the filled area
///  * Pulsing glow at the leading edge
///  * Eased progress (the bar glides toward the target value)
///
/// No sprites or materials needed: it draws into a Texture2D shown by a RawImage.
///
/// Setup:
///  1. Create a UI > Raw Image in a Canvas (stretch it to the size you want the bar).
///  2. Add this component to it. (Optionally assign a UI Text for the percentage.)
///  3. Call SetProgress(0..1), or use FollowOperation() with scene loading.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class AuroraLoadBar : MonoBehaviour
{
    [Header("Optional")]
    [SerializeField] private Text percentText;

    [Header("Texture")]
    [SerializeField] private int textureWidth = 256;
    [SerializeField] private int textureHeight = 24;

    [Header("Gradient")]
    [Range(0f, 1f)] [SerializeField] private float hueStart = 0.50f;
    [Range(0f, 1f)] [SerializeField] private float hueRange = 0.38f;
    [SerializeField] private float hueDriftSpeed = 0.08f;
    [Range(0f, 1f)] [SerializeField] private float saturation = 0.75f;

    [Header("Shimmer & Glow")]
    [SerializeField] private float shimmerSpeed = 0.35f;     // bar-widths per second
    [Range(0.01f, 0.3f)] [SerializeField] private float shimmerWidth = 0.06f;
    [Range(0f, 1f)] [SerializeField] private float shimmerStrength = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float headGlowStrength = 0.8f;

    [Header("Behaviour")]
    [SerializeField] private float smoothSpeed = 6f;
    [SerializeField] private Color emptyColor = new Color(0.27f, 0.27f, 0.33f, 1f);

    [Header("Demo")]
    [Tooltip("Fills the bar automatically with random speed. Handy for testing.")]
    [SerializeField] private bool runDemo = false;

    private RawImage rawImage;
    private Texture2D tex;
    private Color32[] pixels;

    private float target;
    private float displayed;

    /// <summary>Current target progress (0..1).</summary>
    public float Progress => target;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();

        tex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        pixels = new Color32[textureWidth * textureHeight];
        rawImage.texture = tex;
        rawImage.color = Color.white;
    }

    private void OnEnable()
    {
        if (runDemo) StartCoroutine(DemoRoutine());
    }

    private void OnDestroy()
    {
        if (tex != null) Destroy(tex);
    }

    private void Update()
    {
        // Frame-rate independent easing toward the target.
        float k = 1f - Mathf.Exp(-smoothSpeed * Time.unscaledDeltaTime);
        displayed = Mathf.Lerp(displayed, target, k);
        if (Mathf.Abs(displayed - target) < 0.0005f) displayed = target;

        Draw(Time.unscaledTime);

        if (percentText != null)
        {
            percentText.text = displayed >= 0.999f && target >= 1f
                ? "Done"
                : $"{displayed * 100f:0}%";
        }
    }

    /// <summary>Set progress, 0..1.</summary>
    public void SetProgress(float value01)
    {
        target = Mathf.Clamp01(value01);
    }

    /// <summary>Jump instantly with no easing.</summary>
    public void SetProgressImmediate(float value01)
    {
        target = displayed = Mathf.Clamp01(value01);
    }

    /// <summary>
    /// Drive the bar from an AsyncOperation (e.g. SceneManager.LoadSceneAsync).
    /// Unity reports 0..0.9 while loading, so this remaps it to 0..1.
    /// </summary>
    public IEnumerator FollowOperation(AsyncOperation op)
    {
        while (!op.isDone)
        {
            SetProgress(op.progress / 0.9f);
            yield return null;
        }
        SetProgress(1f);
    }

    // ------------------------------------------------------------------

    private void Draw(float t)
    {
        int w = textureWidth;
        int h = textureHeight;

        float fillX = displayed * w;                       // fill edge in pixels
        float shimmerPos = Mathf.Repeat(t * shimmerSpeed, 1.4f) - 0.2f; // 0..1 range, with margin
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f);
        float glowRadius = Mathf.Max(3f, w * 0.025f);

        for (int x = 0; x < w; x++)
        {
            float u = (float)x / (w - 1);

            // Aurora gradient, drifting over time.
            float hue = Mathf.Repeat(hueStart + hueRange * u + t * hueDriftSpeed, 1f);
            Color fill = Color.HSVToRGB(hue, saturation, 1f);

            // Shimmer band.
            float s = Mathf.Clamp01(1f - Mathf.Abs(u - shimmerPos) / shimmerWidth) * shimmerStrength;
            fill = Color.Lerp(fill, Color.white, s);

            // Glow at the leading edge.
            if (displayed > 0f && displayed < 1f)
            {
                float d = (x - fillX) / glowRadius;
                float glow = Mathf.Exp(-d * d) * headGlowStrength * (0.6f + 0.4f * pulse);
                fill = Color.Lerp(fill, Color.white, glow);
            }

            // Sub-pixel coverage gives a smooth fill edge.
            float coverage = Mathf.Clamp01(fillX - x);
            Color col = Color.Lerp(emptyColor, fill, coverage);

            for (int y = 0; y < h; y++)
            {
                // Soft vertical shading: bright top highlight, slightly darker edges.
                float v = (float)y / (h - 1);                    // 0 bottom .. 1 top
                float shade = 1f - 0.30f * Mathf.Abs(v - 0.55f) * 2f;
                shade += v > 0.75f ? 0.12f * coverage : 0f;      // glassy highlight on filled part

                Color c = col * Mathf.Clamp(shade, 0.5f, 1.2f);
                c.a = 1f;
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    private IEnumerator DemoRoutine()
    {
        while (true)
        {
            float p = 0f;
            SetProgressImmediate(0f);
            while (p < 1f)
            {
                p += Random.Range(0.01f, 0.08f);
                SetProgress(p);
                yield return new WaitForSecondsRealtime(Random.Range(0.1f, 0.4f));
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }
    }
}