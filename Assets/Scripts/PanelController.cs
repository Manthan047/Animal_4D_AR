using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PanelController : MonoBehaviour
{
    [Header("Main Panels")]
    [Tooltip("The caution overlay / box containing the OK button.")]
    [SerializeField] private GameObject cautionObject;
    [Tooltip("The loading root panel.")]
    [SerializeField] private GameObject loadingPanel;
    [Tooltip("The instructions panel.")]
    [SerializeField] private GameObject instructionPanel;
    [Tooltip("The AR HUD action buttons panel.")]
    [SerializeField] private GameObject actionsButtonsPanel;
    [Tooltip("The animal info card panel.")]
    [SerializeField] private GameObject infoCardPanel;

    [Header("Interactive Buttons & Header")]
    [Tooltip("OK button on the caution panel.")]
    [SerializeField] private Button okButton;
    [Tooltip("Close button on the instructions panel.")]
    [SerializeField] private Button closeInstructionButton;
    [Tooltip("Settings button on top header.")]
    [SerializeField] private GameObject settingButton;
    [Tooltip("Logo object.")]
    [SerializeField] private GameObject logoObject;

    [Header("Loading UI Elements")]
    [Tooltip("Loading progress slider bar.")]
    [SerializeField] private Slider progressBar;
    [Tooltip("Loading percentage text.")]
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Background Elements")]
    [Tooltip("Background Image to disable when loading starts.")]
    [SerializeField] private Image backgroundImage;
    [Tooltip("Background GameObject to disable when loading starts.")]
    [SerializeField] private GameObject backgroundObject;
    [Tooltip("Whether the background should be disabled during and after loading.")]
    [SerializeField] private bool hideBackgroundWhileLoading = true;

    [Header("Loading Settings")]
    [Tooltip("Duration in seconds for the loading animation.")]
    [SerializeField] private float loadingDuration = 2f;

    [Header("Cross-Reference")]
    [Tooltip("AR UI Manager to notify once the intro instructions sequence finishes.")]
    [SerializeField] private ARUIManager arUIManager;

    private Coroutine loadingRoutine;

    private void Awake()
    {
        AutoResolveReferences();
        SetupButtonListeners();

        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.interactable = false;
        }
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    private void Start()
    {
        // 1. Loading panel must be active at start so caution box is visible
        bool cautionInsideLoading = cautionObject != null && loadingPanel != null
                                    && cautionObject.transform.IsChildOf(loadingPanel.transform);

        if (loadingPanel != null)
            loadingPanel.SetActive(cautionInsideLoading);

        // 2. Caution popup + OK button are active
        if (cautionObject != null)
            cautionObject.SetActive(true);

        if (okButton != null)
            okButton.gameObject.SetActive(true);

        // 3. Background is active initially for caution popup
        SetBackgroundVisible(true);

        // 4. Loading bar and progress text are hidden until OK is clicked
        if (progressBar != null)
            progressBar.gameObject.SetActive(false);

        if (progressText != null)
            progressText.gameObject.SetActive(false);

        // 5. Instruction panel, logo, and settings button start hidden until loading finishes
        if (instructionPanel != null)
            instructionPanel.SetActive(false);

        if (logoObject != null)
            logoObject.SetActive(false);

        if (settingButton != null)
            settingButton.SetActive(false);

        // 6. Action buttons & info panel start hidden
        if (actionsButtonsPanel != null)
            actionsButtonsPanel.SetActive(false);

        if (infoCardPanel != null)
            infoCardPanel.SetActive(false);
    }

    private void AutoResolveReferences()
    {
        if (arUIManager == null)
        {
            arUIManager = FindFirstObjectByType<ARUIManager>(FindObjectsInactive.Include);
        }

        // Search through all scene Transforms (including inactive objects)
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in allTransforms)
        {
            string tName = t.name.Trim();

            if (cautionObject == null && (tName == "caution" || tName == "Caution"))
                cautionObject = t.gameObject;

            if (loadingPanel == null && (tName == "Loading_Panel" || tName == "LoadingPanel"))
                loadingPanel = t.gameObject;

            if (instructionPanel == null && (tName == "Instruction Panel" || tName == "Instruction Panel " || tName == "Instruction_Panel"))
                instructionPanel = t.gameObject;

            if (settingButton == null && (tName == "setting_button" || tName == "setting_button "))
                settingButton = t.gameObject;

            if (logoObject == null && (tName == "Logo" || tName == "logo" || tName == "Aug"))
                logoObject = t.gameObject;

            if (backgroundObject == null && (tName == "BackGround_Image" || tName == "backgroundImage" || tName == "Background_Image"))
                backgroundObject = t.gameObject;

            if (actionsButtonsPanel == null && (tName == "Actions Buttons Panel" || tName == "Actions Buttons Panel " || tName == "ActionButtonsPanel"))
                actionsButtonsPanel = t.gameObject;

            if (infoCardPanel == null && (tName == "info card panel" || tName == "info card panel " || tName == "InfoCardPanel"))
                infoCardPanel = t.gameObject;
        }

        // Auto-find OK button if missing
        if (okButton == null)
        {
            if (cautionObject != null)
                okButton = cautionObject.GetComponentInChildren<Button>(true);

            if (okButton == null)
            {
                foreach (Transform t in allTransforms)
                {
                    string tName = t.name.Trim();
                    if (tName == "ok_button" || tName == "ok_button " || tName == "OK_Button" || tName == "okButton")
                    {
                        okButton = t.GetComponent<Button>();
                        if (okButton != null) break;
                    }
                }
            }
        }

        // Auto-find Close instruction button if missing
        if (closeInstructionButton == null)
        {
            if (instructionPanel != null)
            {
                Button[] buttons = instructionPanel.GetComponentsInChildren<Button>(true);
                foreach (Button b in buttons)
                {
                    string bName = b.name.Trim();
                    if (bName == "Close Button" || bName == "Close Button " || bName == "CloseButton" || bName.ToLower().Contains("close"))
                    {
                        closeInstructionButton = b;
                        break;
                    }
                }
                if (closeInstructionButton == null && buttons.Length > 0)
                    closeInstructionButton = buttons[0];
            }
        }

        // Auto-find Background Image if missing
        if (backgroundImage == null)
        {
            if (backgroundObject != null)
            {
                backgroundImage = backgroundObject.GetComponent<Image>();
            }
            else if (loadingPanel != null)
            {
                backgroundImage = loadingPanel.GetComponent<Image>();
            }
        }
    }

    private void SetupButtonListeners()
    {
        if (okButton != null)
        {
            okButton.onClick.RemoveListener(OnOkButtonClicked);
            okButton.onClick.AddListener(OnOkButtonClicked);
        }

        if (closeInstructionButton != null)
        {
            closeInstructionButton.onClick.RemoveListener(OnInstructionPanelClose);
            closeInstructionButton.onClick.AddListener(OnInstructionPanelClose);
        }
    }

    private void RemoveButtonListeners()
    {
        if (okButton != null)
            okButton.onClick.RemoveListener(OnOkButtonClicked);

        if (closeInstructionButton != null)
            closeInstructionButton.onClick.RemoveListener(OnInstructionPanelClose);
    }

    /// <summary>
    /// Step 1: User clicks the OK button on the caution panel.
    /// Caution box disappears, load bar and text appear while background stays visible.
    /// </summary>
    public void OnOkButtonClicked()
    {
        if (loadingRoutine != null)
            return;

        // 1. Hide caution popup and OK button
        if (cautionObject != null)
            cautionObject.SetActive(false);

        if (okButton != null)
            okButton.gameObject.SetActive(false);

        // 2. Keep background visible while loading bar & text are shown
        SetBackgroundVisible(true);

        // 3. Keep Loading_Panel root active to display load bar & progress text
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        // 4. Show loading progress bar and percentage text
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(true);
            progressBar.value = 0f;
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = "Loading... 0%";
        }

        // 5. Start progress simulation
        loadingRoutine = StartCoroutine(SimulateLoadingProcess());
    }

    private IEnumerator SimulateLoadingProcess()
    {
        SetProgress(0f);

        float elapsed = 0f;
        while (elapsed < loadingDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetProgress(Mathf.Clamp01(elapsed / loadingDuration));
            yield return null;
        }

        SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.2f);

        // 1. Hide loading bar, text and loading panel
        if (progressBar != null)
            progressBar.gameObject.SetActive(false);

        if (progressText != null)
            progressText.gameObject.SetActive(false);

        // 2. Disappear background now that loading has finished
        SetBackgroundVisible(false);

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        // 3. Show instruction panel, logo, and settings button
        if (instructionPanel != null)
            instructionPanel.SetActive(true);

        if (logoObject != null)
            logoObject.SetActive(true);

        if (settingButton != null)
            settingButton.SetActive(true);

        loadingRoutine = null;
    }

    private void SetProgress(float progress)
    {
        if (progressBar != null)
            progressBar.value = progress;

        if (progressText != null)
            progressText.text = "Loading... " + Mathf.RoundToInt(progress * 100f) + "%";
    }

    /// <summary>
    /// Step 3: User clicks close on the instruction panel.
    /// Instruction panel hides, setting button & logo stay available, and ARUIManager begins AR scanning.
    /// </summary>
    public void OnInstructionPanelClose()
    {
        SetBackgroundVisible(false);

        if (instructionPanel != null)
            instructionPanel.SetActive(false);

        if (logoObject != null)
            logoObject.SetActive(true);

        if (settingButton != null)
            settingButton.SetActive(true);

        // Notify AR UI manager that intro flow is complete
        if (arUIManager != null)
        {
            arUIManager.OnIntroFinished();
        }
    }

    private void SetBackgroundVisible(bool visible)
    {
        if (backgroundObject != null)
            backgroundObject.SetActive(visible);

        if (backgroundImage != null)
        {
            backgroundImage.gameObject.SetActive(visible);
            backgroundImage.enabled = visible;
        }

        if (loadingPanel != null)
        {
            Transform bgChild = loadingPanel.transform.Find("BackGround_Image");
            if (bgChild != null)
            {
                bgChild.gameObject.SetActive(visible);
                Image img = bgChild.GetComponent<Image>();
                if (img != null) img.enabled = visible;
            }

            Image panelImg = loadingPanel.GetComponent<Image>();
            if (panelImg != null && !visible)
            {
                panelImg.enabled = false;
            }
        }
    }
}