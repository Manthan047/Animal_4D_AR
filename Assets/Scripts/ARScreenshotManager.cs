using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ARScreenshotManager : MonoBehaviour
{
    public enum LogoPosition
    {
        TopMiddle,
        BottomMiddle,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Custom
    }

    [Header("UI References")]
    [Tooltip("CanvasGroup to hide while taking the photo.")]
    [SerializeField] private CanvasGroup uiCanvasGroup;

    [Header("Logo (saved photo only)")]
    [Tooltip("Assign the UI Image containing your logo.")]
    [SerializeField] private Image logoImage;

    [SerializeField] private LogoPosition logoPosition =
        LogoPosition.TopMiddle;

    [Range(0.05f, 0.5f)]
    [SerializeField] private float logoWidthPercent = 0.2f;

    [Range(0f, 0.1f)]
    [SerializeField] private float logoMarginPercent = 0.03f;

    [Header("Logo Custom Position")]
    [Tooltip("Used only when Logo Position = Custom.\n" +
             "X: 0 = left edge, 1 = right edge.\n" +
             "Y: 0 = bottom edge, 1 = top edge.\n" +
             "The value places the CENTER of the logo.")]
    [SerializeField] private Vector2 customPosition =
        new Vector2(0.5f, 0.9f);

    [Header("Logo Extra Offset")]
    [Tooltip("Extra pixel offset added on top of ANY position " +
             "(preset or custom). +X = right, +Y = up.")]
    [SerializeField] private Vector2 pixelOffset = Vector2.zero;

    private bool isCapturing;

    public void CaptureModelPhoto()
    {
        if (isCapturing) return;

        StartCoroutine(CaptureScreenshotRoutine());
    }

    /// <summary>
    /// Set the logo position at runtime (normalized 0-1, logo center).
    /// X: 0 = left, 1 = right. Y: 0 = bottom, 1 = top.
    /// </summary>
    public void SetCustomLogoPosition(float x, float y)
    {
        logoPosition = LogoPosition.Custom;
        customPosition = new Vector2(
            Mathf.Clamp01(x), Mathf.Clamp01(y));
    }

    private IEnumerator CaptureScreenshotRoutine()
    {
        isCapturing = true;

        Texture2D screenshot = null;

        try
        {
            // Hide the main UI before capturing.
            SetUIVisible(false);

            yield return new WaitForEndOfFrame();

            screenshot = ScreenCapture.CaptureScreenshotAsTexture();

            // Restore UI immediately after capture.
            SetUIVisible(true);

            if (screenshot == null)
            {
                Debug.LogError(
                    "[ARScreenshotManager] Screenshot capture failed.");
                yield break;
            }

            // Add logo to the saved image only.
            ApplyLogo(screenshot);

            byte[] bytes = screenshot.EncodeToPNG();

            string fileName =
                $"AR_Photo_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";

#if UNITY_EDITOR
            string path = Path.Combine(
                Application.dataPath, fileName);

            File.WriteAllBytes(path, bytes);

            Debug.Log(
                $"[ARScreenshotManager] Saved: {path}");

#elif UNITY_ANDROID || UNITY_IOS
            NativeGallery.SaveImageToGallery(
                bytes, "Animal4D", fileName);
#else
            string path = Path.Combine(
                Application.persistentDataPath, fileName);

            File.WriteAllBytes(path, bytes);

            Debug.Log(
                $"[ARScreenshotManager] Saved: {path}");
#endif
        }
        finally
        {
            // Ensure UI is restored and the capture state is reset.
            SetUIVisible(true);

            if (screenshot != null)
                Destroy(screenshot);

            isCapturing = false;
        }
    }

    private void SetUIVisible(bool visible)
    {
        if (uiCanvasGroup == null) return;

        uiCanvasGroup.alpha = visible ? 1f : 0f;
        uiCanvasGroup.interactable = visible;
        uiCanvasGroup.blocksRaycasts = visible;
    }

    private void ApplyLogo(Texture2D target)
    {
        if (logoImage == null || logoImage.sprite == null)
        {
            Debug.LogError(
                "[ARScreenshotManager] Assign Logo Image and its Source Image.");
            return;
        }

        Sprite sprite = logoImage.sprite;
        Texture2D source = sprite.texture;

        if (!source.isReadable)
        {
            Debug.LogError(
                "[ARScreenshotManager] Enable Read/Write on the logo texture.");
            return;
        }

        // Extract the actual sprite area, including sliced/atlas sprites.
        Rect rect = sprite.textureRect;

        int sourceX = Mathf.RoundToInt(rect.x);
        int sourceY = Mathf.RoundToInt(rect.y);
        int sourceW = Mathf.RoundToInt(rect.width);
        int sourceH = Mathf.RoundToInt(rect.height);

        if (sourceW <= 0 || sourceH <= 0)
        {
            Debug.LogError("Logo sprite has invalid dimensions.");
            return;
        }

        int logoW = Mathf.Max(
            1, Mathf.RoundToInt(target.width * logoWidthPercent));

        int logoH = Mathf.Max(
            1, Mathf.RoundToInt(
                logoW * (float)sourceH / sourceW));

        // Keep logo height within the screenshot.
        if (logoH > target.height)
        {
            float scale = (float)target.height / logoH;
            logoH = target.height;
            logoW = Mathf.Max(1, Mathf.RoundToInt(logoW * scale));
        }

        int margin = Mathf.RoundToInt(
            target.width * logoMarginPercent);

        int startX;
        switch (logoPosition)
        {
            case LogoPosition.Custom:
                // Logo center placed at normalized X.
                startX = Mathf.RoundToInt(
                    customPosition.x * target.width - logoW * 0.5f);
                break;

            case LogoPosition.TopMiddle:
            case LogoPosition.BottomMiddle:
                startX = (target.width - logoW) / 2;
                break;

            case LogoPosition.TopLeft:
            case LogoPosition.BottomLeft:
                startX = margin;
                break;

            default:
                startX = target.width - logoW - margin;
                break;
        }

        int startY;
        switch (logoPosition)
        {
            case LogoPosition.Custom:
                // Logo center placed at normalized Y (0 = bottom).
                startY = Mathf.RoundToInt(
                    customPosition.y * target.height - logoH * 0.5f);
                break;

            case LogoPosition.BottomMiddle:
            case LogoPosition.BottomLeft:
            case LogoPosition.BottomRight:
                startY = margin;
                break;

            default:
                startY = target.height - logoH - margin;
                break;
        }

        // Apply extra pixel offset (works with any position mode).
        startX += Mathf.RoundToInt(pixelOffset.x);
        startY += Mathf.RoundToInt(pixelOffset.y);

        startX = Mathf.Clamp(startX, 0, target.width - logoW);
        startY = Mathf.Clamp(startY, 0, target.height - logoH);

        Color[] background = target.GetPixels(
            startX, startY, logoW, logoH);

        // Sample the sprite's actual region and blend its transparency.
        for (int y = 0; y < logoH; y++)
        {
            for (int x = 0; x < logoW; x++)
            {
                float u = (x + 0.5f) / logoW;
                float v = (y + 0.5f) / logoH;

                Color logoPixel = source.GetPixelBilinear(
                    (sourceX + u * sourceW) / source.width,
                    (sourceY + v * sourceH) / source.height);

                // Respect the UI Image's tint and transparency.
                logoPixel *= logoImage.color;

                int index = y * logoW + x;
                Color bg = background[index];
                float alpha = logoPixel.a;

                background[index] = new Color(
                    logoPixel.r * alpha + bg.r * (1f - alpha),
                    logoPixel.g * alpha + bg.g * (1f - alpha),
                    logoPixel.b * alpha + bg.b * (1f - alpha),
                    bg.a
                );
            }
        }

        target.SetPixels(startX, startY, logoW, logoH, background);
        target.Apply();

        Debug.Log(
            $"[ARScreenshotManager] Logo applied: {logoW}x{logoH} at ({startX}, {startY})");
    }
}