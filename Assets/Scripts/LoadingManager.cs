using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;

    // Call this method (e.g., from a button or at scene start)
    public void StartSingleSceneLoading()
    {
        StartCoroutine(SimulateLoadingProcess());
    }

    private IEnumerator SimulateLoadingProcess()
    {
        // 1. Show loading panel
        loadingPanel.SetActive(true);
        progressBar.value = 0f;

        // 2. Perform your setup tasks here (e.g., instantiating prefabs, loading data)
        // Example: Simulating a 5-step loading sequence
        int totalSteps = 5;
        for (int i = 1; i <= totalSteps; i++)
        {
            // Do actual work here (e.g., Instantiate(myPrefab))
            
            float progress = (float)i / totalSteps;
            progressBar.value = progress;
            progressText.text = Mathf.RoundToInt(progress * 100f) + "%";

            // Pause for a frame/time so the UI has time to visually update
            yield return new WaitForSeconds(0.5f); 
        }

        // 3. Hide loading panel when done
        loadingPanel.SetActive(false);
    }
}