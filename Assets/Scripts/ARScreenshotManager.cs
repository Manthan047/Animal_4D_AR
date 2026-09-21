using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ARScreenshotManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag your main UI Canvas or Panel here to hide buttons while taking the shot.")]
    [SerializeField] private CanvasGroup uiCanvasGroup;

    [Tooltip("Optional flash panel to give visual feedback when taking a photo.")]
    [SerializeField] private Image flashOverlay;
    
    [Header("Settings")]
    [SerializeField] private float flashDuration = 0.15f;
    

    public void CaptureModelPhoto()
    {
        StartCoroutine(CaptureScreenshotRoutine());
    }

    private IEnumerator CaptureScreenshotRoutine()
    {
        // 1. Hide the UI so buttons don't appear in the captured photo
        if (uiCanvasGroup != null)
        {
            uiCanvasGroup.alpha = 0f;
            uiCanvasGroup.interactable = false;
            uiCanvasGroup.blocksRaycasts = false;
        }

        // Wait for the end of the frame to ensure rendering is complete
        yield return new WaitForEndOfFrame();

        // 2. Capture the screen texture
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();

        // 3. Re-enable UI
        if (uiCanvasGroup != null)
        {
            uiCanvasGroup.alpha = 1f;
            uiCanvasGroup.interactable = true;
            uiCanvasGroup.blocksRaycasts = true;
        }

        // 4. Trigger visual flash effect
        if (flashOverlay != null)
        {
            StartCoroutine(TriggerFlashEffect());
        }

        // 5. Encode texture to PNG
        byte[] bytes = screenshot.EncodeToPNG();
        Destroy(screenshot); // Clean up memory

        // 6. Save to device storage
        string fileName = $"AR_Photo_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";

#if UNITY_EDITOR
        string path = Path.Combine(Application.dataPath, fileName);
        File.WriteAllBytes(path, bytes);
        Debug.Log($"[ARScreenshotManager] Screenshot saved in Editor: {path}");
#elif UNITY_ANDROID || UNITY_IOS
        // Native Gallery plugin call (if installed):
        NativeGallery.SaveImageToGallery(bytes, "Animal4D", fileName);
        
        // Default fallback path for persistent data
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllBytes(path, bytes);
        Debug.Log($"[ARScreenshotManager] Screenshot saved to persistent path: {path}");
#endif
    }

    private IEnumerator TriggerFlashEffect()
    {
        if (flashOverlay == null) yield break;

        flashOverlay.gameObject.SetActive(true);
        Color flashColor = flashOverlay.color;
        flashColor.a = 1f;
        flashOverlay.color = flashColor;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            flashColor.a = Mathf.Lerp(1f, 0f, elapsed / flashDuration);
            flashOverlay.color = flashColor;
            yield return null;
        }

        flashOverlay.gameObject.SetActive(false);
    }
}