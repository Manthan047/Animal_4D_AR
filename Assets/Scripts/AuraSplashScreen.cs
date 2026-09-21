using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AuraSplashScreen : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("Attach the CanvasGroup component located on your SplashPanel.")]
    [SerializeField] private CanvasGroup splashPanelGroup;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float displayDuration = 3.0f;
    [SerializeField] private string nextSceneName = "MainARVRScene";

    private void Start()
    {
        // Enforce Landscape orientation
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        // Hide panel initially for smooth fade-in
        if (splashPanelGroup != null) splashPanelGroup.alpha = 0f;

        StartCoroutine(RunSplashScreen());
    }

    private IEnumerator RunSplashScreen()
    {
        // 1. Smooth Fade-In
        float fadeTimer = 0f;
        while (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.deltaTime;
            if (splashPanelGroup != null) 
                splashPanelGroup.alpha = fadeTimer / fadeDuration;
            yield return null;
        }

        if (splashPanelGroup != null) splashPanelGroup.alpha = 1f;

        // 2. Display splash screen for exactly 3 seconds
        yield return new WaitForSeconds(displayDuration);

        // 3. Transition directly to the main app scene
        SceneManager.LoadScene("Animal_4D");
    }
}