using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vuforia;
using LightSide; // UniText namespace

// Vuforia also has a class named "Image", so tell the compiler we mean the UI one.
using Image = UnityEngine.UI.Image;

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
    [Tooltip("The Settings panel (the image box that holds the Language / Sound buttons).")]
    [SerializeField] private GameObject settingPanel;

    [Header("Interactive Buttons & Header")]
    [SerializeField] private Button okButton;
    [SerializeField] private Button closeInstructionButton;
    [Tooltip("The main SETTING button (the one the user taps to open the Setting Panel). Needs a Button component. Must NOT be a child of the Setting Panel.")]
    [SerializeField] private GameObject LanguagesettingButton;
    [Tooltip("The Back button inside the Setting Panel. Auto-found (a Button named like 'back') if left empty.")]
    [SerializeField] private Button settingBackButton;
    [SerializeField] private GameObject logoObject;
    [Tooltip("The Info button. Must NOT be a child of the logo, setting button or actions panel.")]
    [SerializeField] private GameObject Info_button;

    [Tooltip("The Back button of the info screen. Auto-found (a Button named like 'Info...Back') if left empty.")]
    [SerializeField] private Button infoBackButton;

    [Header("Settings Menu (auto-found by name if left empty)")]
    [Tooltip("Button inside the Setting Panel that opens the language list (e.g. 'Language_option').")]
    [SerializeField] private Button languageOptionButton;
    [Tooltip("The language list / sub panel (e.g. 'Language_sub_panel'). Shown INSTEAD of the Setting Panel.")]
    [SerializeField] private GameObject languageSubPanel;
    [Tooltip("Back button inside the language sub panel. Returns to the Setting Panel. Auto-found (a Button named like 'back') if left empty.")]
    [SerializeField] private Button languageSubBackButton;

    [Tooltip("Button inside the Setting Panel that opens the sound options (e.g. 'Sound_option').")]
    [SerializeField] private Button soundOptionButton;
    [Tooltip("The sound options sub panel (e.g. 'Sound_sub_panel'). Shown INSTEAD of the Setting Panel.")]
    [SerializeField] private GameObject soundSubPanel;
    [Tooltip("Back button inside the sound sub panel. Returns to the Setting Panel. Auto-found (a Button named like 'back') if left empty.")]
    [SerializeField] private Button soundSubBackButton;

    [Header("Loading UI Elements")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private UniText progressText;
    [SerializeField] private GameObject loadingImage;

    [Header("Background Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject backgroundObject;

    [Header("Loading Settings")]
    [Tooltip("Total loading animation time in seconds. Lower = faster. NOTE: the Inspector value overrides this default.")]
    [SerializeField] private float loadingDuration = 1f;
    [Tooltip("Pause after reaching 100% before showing the next panel.")]
    [SerializeField] private float finishDelay = 0.1f;

    [Header("Card Detection (Vuforia)")]
    [Tooltip("Seconds to wait after the card is lost before hiding the action buttons (prevents flicker).")]
    [SerializeField] private float hideDelay = 0.5f;

    [Header("Cross-Reference")]
    [SerializeField] private ARUIManager arUIManager;

    private Coroutine loadingRoutine;
    private CanvasGroup progressTextGroup;
    private Transform cachedBgChild;
    private Image cachedBgChildImage;
    private Image cachedPanelImage;

    private string cachedLoadPrefix = "Loading";
    private int lastShownPercent = -1;

    // Card detection state
    private bool introFinished;
    private bool actionsVisible;
    private float lostTimer;
    private bool infoModeActive;
    private readonly List<ObserverBehaviour> cardTargets = new List<ObserverBehaviour>();

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (FindAnyObjectByType<LanguageManager>(FindObjectsInactive.Include) == null)
            gameObject.AddComponent<LanguageManager>();

        AutoResolveReferences();
        DetachSubPanelsFromSettingPanel();
        CacheBackgroundRefs();
        SetupButtonListeners();

        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.interactable = false;
        }

        if (progressText != null)
        {
            progressTextGroup = progressText.GetComponent<CanvasGroup>();
            if (progressTextGroup == null)
                progressTextGroup = progressText.gameObject.AddComponent<CanvasGroup>();

            progressTextGroup.blocksRaycasts = false;
            progressTextGroup.interactable = false;
        }
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    private void Start()
    {
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (cautionObject != null) cautionObject.SetActive(true);
        if (okButton != null) okButton.gameObject.SetActive(true);

        SetBackgroundVisible(true);
        SetLoadingBarVisible(false);

        if (instructionPanel != null) instructionPanel.SetActive(false);
        if (logoObject != null) logoObject.SetActive(false);
        if (LanguagesettingButton != null) LanguagesettingButton.SetActive(false);
        CloseSettingsCompletely();
        if (actionsButtonsPanel != null) actionsButtonsPanel.SetActive(false);
        if (infoCardPanel != null) infoCardPanel.SetActive(false);
        if (infoBackButton != null) infoBackButton.gameObject.SetActive(false);

        introFinished = false;
        actionsVisible = false;
        lostTimer = 0f;

        FindCardTargets();

        // Pay the expensive UniText first-use cost NOW, while the user reads the caution box,
        // instead of at the moment OK is clicked.
        StartCoroutine(PrewarmProgressText());
    }

    private void Update()
    {
        // Action buttons only exist after the intro is done AND a card is being tracked.
        if (!introFinished || infoModeActive || actionsButtonsPanel == null)
            return;

        bool cardTracked = IsAnyCardTracked();

        if (cardTracked)
        {
            lostTimer = 0f;
            if (!actionsVisible) SetActionsVisible(true);
        }
        else if (actionsVisible)
        {
            lostTimer += Time.deltaTime;
            if (lostTimer >= hideDelay) SetActionsVisible(false);
        }
    }

    private void SetActionsVisible(bool visible)
    {
        actionsVisible = visible;
        if (actionsButtonsPanel != null)
            actionsButtonsPanel.SetActive(visible);
    }

    // =========================================================
    // VUFORIA CARD DETECTION
    // =========================================================

    private void FindCardTargets()
    {
        // Finds every Vuforia target in the scene (Image Targets, etc.), including inactive ones.
        cardTargets.Clear();
        cardTargets.AddRange(FindObjectsByType<ObserverBehaviour>(FindObjectsInactive.Include));
    }

    // Polled every frame, so the result is always the CURRENT state
    // (no dependency on a "lost" event firing).
    private bool IsAnyCardTracked()
    {
        for (int i = 0; i < cardTargets.Count; i++)
        {
            ObserverBehaviour target = cardTargets[i];
            if (target == null || !target.isActiveAndEnabled) continue;

            // Only a live TRACKED status counts. EXTENDED_TRACKED / LIMITED / NO_POSE = card not visible.
            if (target.TargetStatus.Status == Status.TRACKED)
                return true;
        }
        return false;
    }

    // =========================================================
    // PREWARM (removes the freeze on first click)
    // =========================================================

    private IEnumerator PrewarmProgressText()
    {
        if (progressText == null || progressTextGroup == null) yield break;

        progressTextGroup.alpha = 0f; // invisible while warming up
        progressText.gameObject.SetActive(true);

        cachedLoadPrefix = BuildLoadPrefix();
        progressText.Text = $"{cachedLoadPrefix}... 100%"; // widest string, builds needed glyphs

        yield return null;
        yield return null;

        progressTextGroup.alpha = 1f;

        if (loadingRoutine == null)
            progressText.gameObject.SetActive(false);

        lastShownPercent = -1;
    }

    // =========================================================
    // AUTO RESOLVE
    // =========================================================

    private void AutoResolveReferences()
    {
        if (arUIManager == null)
            arUIManager = FindAnyObjectByType<ARUIManager>(FindObjectsInactive.Include);

        // Only do the expensive full-scene scan if something is actually missing
        bool needScan =
            cautionObject == null || loadingPanel == null || instructionPanel == null ||
            LanguagesettingButton == null || settingPanel == null ||
            logoObject == null || backgroundObject == null || actionsButtonsPanel == null ||
            infoCardPanel == null || progressBar == null || progressText == null || okButton == null;

        if (needScan)
        {
            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (Transform t in allTransforms)
            {
                string tName = t.name.Trim();

                if (cautionObject == null && (tName == "caution" || tName == "Caution"))
                    cautionObject = t.gameObject;

                if (loadingPanel == null && (tName == "Loading_Panel" || tName == "LoadingPanel"))
                    loadingPanel = t.gameObject;

                if (instructionPanel == null && (tName == "Instruction Panel" || tName == "Instruction_Panel"))
                    instructionPanel = t.gameObject;

                if (LanguagesettingButton == null && (tName == "setting_button" || tName == "Setting_button" || tName == "Setting_Button"))
                    LanguagesettingButton = t.gameObject;

                if (settingPanel == null && (tName == "Setting_panel" || tName == "Setting_Panel" || tName == "Setting_Pannel"))
                    settingPanel = t.gameObject;

                if (logoObject == null && (tName == "Logo" || tName == "logo" || tName == "Aug"))
                    logoObject = t.gameObject;

                if (backgroundObject == null && (tName == "BackGround_Image" || tName == "backgroundImage" || tName == "Background_Image"))
                    backgroundObject = t.gameObject;

                if (actionsButtonsPanel == null && (tName == "Actions Buttons Panel" || tName == "ActionButtonsPanel"))
                    actionsButtonsPanel = t.gameObject;

                if (infoCardPanel == null && (tName == "info card panel" || tName == "InfoCardPanel"))
                    infoCardPanel = t.gameObject;

                if (progressBar == null && (tName == "LoadBar" || tName == "Loading_Bar" || tName == "ProgressBar" || tName == "Slider"))
                {
                    Slider s = t.GetComponent<Slider>();
                    if (s != null) progressBar = s;
                }

                if (progressText == null && (tName == "ProgressText" || tName == "LoadingText" || tName == "Progress_Text"))
                    progressText = t.GetComponent<UniText>();

                if (okButton == null && (tName == "ok_button" || tName == "OK_Button" || tName == "okButton"))
                    okButton = t.GetComponent<Button>();
            }
        }

        if (loadingPanel != null)
        {
            if (progressBar == null)
                progressBar = loadingPanel.GetComponentInChildren<Slider>(true);

            if (progressText == null)
            {
                foreach (UniText u in loadingPanel.GetComponentsInChildren<UniText>(true))
                {
                    if (cautionObject != null && u.transform.IsChildOf(cautionObject.transform))
                        continue;

                    progressText = u;
                    break;
                }
            }
        }

        if (okButton == null && cautionObject != null)
            okButton = cautionObject.GetComponentInChildren<Button>(true);

        if (closeInstructionButton == null && instructionPanel != null)
        {
            Button[] buttons = instructionPanel.GetComponentsInChildren<Button>(true);
            foreach (Button b in buttons)
            {
                if (b.name.ToLowerInvariant().Contains("close"))
                {
                    closeInstructionButton = b;
                    break;
                }
            }

            if (closeInstructionButton == null && buttons.Length > 0)
                closeInstructionButton = buttons[0];
        }

        if (backgroundImage == null)
        {
            if (backgroundObject != null)
                backgroundImage = backgroundObject.GetComponent<Image>();
            else if (loadingPanel != null)
                backgroundImage = loadingPanel.GetComponent<Image>();
        }

        if (infoBackButton == null)
        {
            foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include))
            {
                string n = b.name.ToLowerInvariant();
                if (n.Contains("info") && n.Contains("back"))
                {
                    infoBackButton = b;
                    break;
                }
            }
        }

        AutoResolveSettingsMenu();
    }

    // Finds the settings items by name, so the Inspector fields can stay empty:
    //   Language_option, Sound_option, Language_sub_panel, Sound_sub_panel, back buttons
    private void AutoResolveSettingsMenu()
    {
        if (settingPanel == null) return;

        // Pass 1: sub panels (inside Setting panel first, then anywhere in the scene)
        Transform[] children = settingPanel.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t == settingPanel.transform) continue;
            MatchSubPanel(t);
        }

        if (languageSubPanel == null || soundSubPanel == null)
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
                MatchSubPanel(t);
        }

        // Pass 2: buttons directly in the Setting panel (ignoring anything inside a sub panel)
        foreach (Transform t in children)
        {
            if (t == settingPanel.transform) continue;
            if (languageSubPanel != null && t.IsChildOf(languageSubPanel.transform)) continue;
            if (soundSubPanel != null && t.IsChildOf(soundSubPanel.transform)) continue;

            string n = t.name.Trim().ToLowerInvariant().Replace(" ", "_");
            Button b = t.GetComponent<Button>();
            if (b == null) continue;

            if (languageOptionButton == null && n.Contains("language") && !n.Contains("sub"))
                languageOptionButton = b;

            if (soundOptionButton == null && n.Contains("sound") && !n.Contains("sub"))
                soundOptionButton = b;

            if (settingBackButton == null && n.Contains("back"))
                settingBackButton = b;
        }

        // Pass 3: back buttons inside the sub panels
        if (languageSubBackButton == null) languageSubBackButton = FindBackButtonIn(languageSubPanel);
        if (soundSubBackButton == null)    soundSubBackButton    = FindBackButtonIn(soundSubPanel);
    }

    private void MatchSubPanel(Transform t)
    {
        string n = t.name.Trim().ToLowerInvariant().Replace(" ", "_");

        if (languageSubPanel == null && n.Contains("language") && n.Contains("sub"))
            languageSubPanel = t.gameObject;

        if (soundSubPanel == null && n.Contains("sound") && n.Contains("sub"))
            soundSubPanel = t.gameObject;
    }

    private Button FindBackButtonIn(GameObject panel)
    {
        if (panel == null) return null;

        foreach (Button b in panel.GetComponentsInChildren<Button>(true))
        {
            if (b.name.ToLowerInvariant().Contains("back"))
                return b;
        }
        return null;
    }

    // If a sub panel is a CHILD of the Setting panel, hiding the Setting panel would hide it too.
    // So at startup it is moved next to the Setting panel (same parent), which lets the
    // Setting panel be invisible while the sub panel is showing.
    private void DetachSubPanelsFromSettingPanel()
    {
        if (settingPanel == null || settingPanel.transform.parent == null) return;

        DetachFromSettingPanel(languageSubPanel);
        DetachFromSettingPanel(soundSubPanel);
    }

    private void DetachFromSettingPanel(GameObject panel)
    {
        if (panel == null) return;
        if (panel.transform == settingPanel.transform) return;

        if (panel.transform.IsChildOf(settingPanel.transform))
            panel.transform.SetParent(settingPanel.transform.parent, false);
    }

    private void CacheBackgroundRefs()
    {
        if (loadingPanel == null) return;

        cachedBgChild = loadingPanel.transform.Find("BackGround_Image");
        if (cachedBgChild != null)
            cachedBgChildImage = cachedBgChild.GetComponent<Image>();

        cachedPanelImage = loadingPanel.GetComponent<Image>();
    }

    // =========================================================
    // BUTTON LISTENERS
    // =========================================================

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

        // Main setting button
        if (LanguagesettingButton != null)
        {
            Button btn = LanguagesettingButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnSettingButtonClicked);
                btn.onClick.AddListener(OnSettingButtonClicked);
            }
            else
            {
                Debug.LogWarning("PanelController: the Setting button object has no Button component. " +
                                 "Assign the object that actually has the Button.");
            }
        }
        else
        {
            Debug.LogWarning("PanelController: 'Languagesetting Button' is not assigned and no object named 'setting_button' was found.");
        }

        // Buttons inside the setting panel
        if (settingBackButton != null)
        {
            settingBackButton.onClick.RemoveListener(OnSettingBackClicked);
            settingBackButton.onClick.AddListener(OnSettingBackClicked);
        }

        if (languageOptionButton != null)
        {
            languageOptionButton.onClick.RemoveListener(OnLanguageOptionClicked);
            languageOptionButton.onClick.AddListener(OnLanguageOptionClicked);
        }

        if (soundOptionButton != null)
        {
            soundOptionButton.onClick.RemoveListener(OnSoundOptionClicked);
            soundOptionButton.onClick.AddListener(OnSoundOptionClicked);
        }

        // Back buttons inside the sub panels
        if (languageSubBackButton != null)
        {
            languageSubBackButton.onClick.RemoveListener(OnSubPanelBackClicked);
            languageSubBackButton.onClick.AddListener(OnSubPanelBackClicked);
        }

        if (soundSubBackButton != null)
        {
            soundSubBackButton.onClick.RemoveListener(OnSubPanelBackClicked);
            soundSubBackButton.onClick.AddListener(OnSubPanelBackClicked);
        }

        if (Info_button != null)
        {
            Button infoBtn = Info_button.GetComponent<Button>();
            if (infoBtn != null)
            {
                infoBtn.onClick.RemoveListener(OnInfoButtonClicked);
                infoBtn.onClick.AddListener(OnInfoButtonClicked);
            }
        }

        if (infoBackButton != null)
        {
            infoBackButton.onClick.RemoveListener(OnInfoBackButtonClicked);
            infoBackButton.onClick.AddListener(OnInfoBackButtonClicked);
        }
    }

    private void RemoveButtonListeners()
    {
        if (okButton != null)
            okButton.onClick.RemoveListener(OnOkButtonClicked);

        if (closeInstructionButton != null)
            closeInstructionButton.onClick.RemoveListener(OnInstructionPanelClose);

        if (LanguagesettingButton != null)
        {
            Button btn = LanguagesettingButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.RemoveListener(OnSettingButtonClicked);
        }

        if (settingBackButton != null)
            settingBackButton.onClick.RemoveListener(OnSettingBackClicked);

        if (languageOptionButton != null)
            languageOptionButton.onClick.RemoveListener(OnLanguageOptionClicked);

        if (soundOptionButton != null)
            soundOptionButton.onClick.RemoveListener(OnSoundOptionClicked);

        if (languageSubBackButton != null)
            languageSubBackButton.onClick.RemoveListener(OnSubPanelBackClicked);

        if (soundSubBackButton != null)
            soundSubBackButton.onClick.RemoveListener(OnSubPanelBackClicked);

        if (Info_button != null)
        {
            Button infoBtn = Info_button.GetComponent<Button>();
            if (infoBtn != null)
                infoBtn.onClick.RemoveListener(OnInfoButtonClicked);
        }

        if (infoBackButton != null)
            infoBackButton.onClick.RemoveListener(OnInfoBackButtonClicked);
    }

    // =========================================================
    // SETTINGS MENU
    //
    //   setting_button      -> opens Setting_panel (closes everything if already open)
    //   Language_option     -> Setting_panel becomes INVISIBLE, Language_sub_panel shows
    //   Sound_option        -> Setting_panel becomes INVISIBLE, Sound_sub_panel shows
    //   sub panel Back      -> sub panel closes, Setting_panel shows again
    //   Setting_panel Back  -> closes everything
    // =========================================================

    private bool IsSettingsOpen()
    {
        return (settingPanel != null && settingPanel.activeSelf)
            || (languageSubPanel != null && languageSubPanel.activeSelf)
            || (soundSubPanel != null && soundSubPanel.activeSelf);
    }

    /// <summary>
    /// Setting button pressed: opens the Setting panel, or closes the whole settings menu if it is open.
    /// </summary>
    public void OnSettingButtonClicked()
    {
        Debug.Log("PanelController: Setting button clicked");

        if (infoModeActive) { Debug.Log("Blocked: infoModeActive"); return; }

        if (settingPanel == null)
        {
            Debug.LogWarning("PanelController: Setting Panel is not assigned!");
            return;
        }

        if (IsSettingsOpen())
        {
            CloseSettingsCompletely();
        }
        else
        {
            ShowSettingPanel();
        }
    }

    /// <summary>
    /// Back button inside the Setting panel: closes the whole settings menu.
    /// </summary>
    public void OnSettingBackClicked()
    {
        CloseSettingsCompletely();
    }

    /// <summary>
    /// Back button inside a sub panel: goes back to the Setting panel.
    /// </summary>
    public void OnSubPanelBackClicked()
    {
        ShowSettingPanel();
    }

    public void OnLanguageOptionClicked() { OpenSubPanel(languageSubPanel); }
    public void OnSoundOptionClicked()    { OpenSubPanel(soundSubPanel); }

    // Shows the main Setting panel (and hides the sub panels).
    private void ShowSettingPanel()
    {
        CloseSubPanels();

        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
            settingPanel.transform.SetAsLastSibling(); // draw on top
        }
    }

    // Hides the Setting panel and shows only the chosen sub panel.
    private void OpenSubPanel(GameObject panel)
    {
        if (panel == null)
        {
            Debug.LogWarning("PanelController: that sub panel is not assigned and was not found by name " +
                             "(expected names like 'Language_sub_panel' / 'Sound_sub_panel').");
            return;
        }

        CloseSubPanels();

        if (settingPanel != null)
            settingPanel.SetActive(false);   // <- Setting panel becomes invisible

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();  // draw on top

        Debug.Log($"PanelController: opened '{panel.name}', parent='{(panel.transform.parent != null ? panel.transform.parent.name : "none")}', " +
                  $"activeInHierarchy={panel.activeInHierarchy}", panel);
    }

    private void CloseSubPanels()
    {
        if (languageSubPanel != null) languageSubPanel.SetActive(false);
        if (soundSubPanel != null)    soundSubPanel.SetActive(false);
    }

    private void CloseSettingsCompletely()
    {
        CloseSubPanels();
        if (settingPanel != null)
            settingPanel.SetActive(false);
    }

    // Kept so any old Inspector OnClick that still points to the old name keeps working.
    public void OnLangaugeSettingButtonClicked()
    {
        OnSettingButtonClicked();
    }

    // =========================================================
    // INFO BUTTON / INFO BACK BUTTON
    // =========================================================

    /// <summary>
    /// Info button pressed: logo + setting button hidden. Only the Info button stays visible.
    /// </summary>
    public void OnInfoButtonClicked()
    {
        SetInfoMode(true);
    }

    /// <summary>
    /// Info Back button pressed: leaves the info screen and brings back the
    /// logo, setting button and Info button.
    /// </summary>
    public void OnInfoBackButtonClicked()
    {
        SetInfoMode(false);
    }

    private void SetInfoMode(bool enable)
    {
        if (!introFinished || infoModeActive == enable) return;

        infoModeActive = enable;
        bool normalMode = !enable;

        // Logo + setting button
        if (logoObject != null) logoObject.SetActive(normalMode);
        if (LanguagesettingButton != null) LanguagesettingButton.SetActive(normalMode);

        // Info button is visible in both modes
        if (Info_button != null) Info_button.SetActive(true);

        // Back button only makes sense while the info screen is open
        if (infoBackButton != null) infoBackButton.gameObject.SetActive(enable);

        lostTimer = 0f;

        if (enable)
        {
            // Close the whole settings menu and the action buttons while in info mode
            CloseSettingsCompletely();
            SetActionsVisible(false);
        }
        else
        {
            // Close the info screen. The action buttons come back by themselves
            // once a card is tracked again (see Update).
            if (infoCardPanel != null) infoCardPanel.SetActive(false);
        }
    }

    // =========================================================
    // OK BUTTON / LOADING
    // =========================================================

    public void OnOkButtonClicked()
    {
        if (loadingRoutine != null)
            return;

        if (cautionObject != null) cautionObject.SetActive(false);
        if (okButton != null) okButton.gameObject.SetActive(false);

        SetBackgroundVisible(true);
        if (loadingPanel != null) loadingPanel.SetActive(true);

        // Refresh the localized prefix ONCE (not every frame)
        cachedLoadPrefix = BuildLoadPrefix();
        lastShownPercent = -1;

        SetLoadingBarVisible(true);
        SetProgress(0f);

        loadingRoutine = StartCoroutine(SimulateLoadingProcess());
    }

    private IEnumerator SimulateLoadingProcess()
    {
        // Let the first frame (with any one-time hitch) pass before the timer starts
        yield return null;

        float elapsed = 0f;
        while (elapsed < loadingDuration)
        {
            // Clamp so a single slow frame can't jump or stall the bar
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            SetProgress(Mathf.Clamp01(elapsed / loadingDuration));
            yield return null;
        }

        SetProgress(1f);
        if (finishDelay > 0f)
            yield return new WaitForSecondsRealtime(finishDelay);

        SetLoadingBarVisible(false);
        SetBackgroundVisible(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);

        if (instructionPanel != null) instructionPanel.SetActive(true);
        if (logoObject != null) logoObject.SetActive(true);
        if (LanguagesettingButton != null) LanguagesettingButton.SetActive(true);

        loadingRoutine = null;
    }

    private void SetLoadingBarVisible(bool visible)
    {
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(visible);
            if (visible) progressBar.value = 0f;
        }

        if (progressText != null)
            progressText.gameObject.SetActive(visible);

        if (loadingImage != null)
            loadingImage.gameObject.SetActive(visible);
    }

    private string BuildLoadPrefix()
    {
        string prefix = LanguageManager.Instance != null
            ? LanguageManager.Instance.GetText("lbl_loading", "Loading...")
            : "Loading...";

        if (prefix.EndsWith("..."))
            prefix = prefix.Substring(0, prefix.Length - 3).Trim();

        return prefix;
    }

    private void SetProgress(float progress)
    {
        if (progressBar != null)
            progressBar.value = progress;

        // Only touch the (expensive) UniText when the visible number changes
        int percent = Mathf.RoundToInt(progress * 100f);
        if (percent == lastShownPercent)
            return;

        lastShownPercent = percent;

        if (progressText != null)
            progressText.Text = $"{cachedLoadPrefix}... {percent}%";
    }

    // =========================================================
    // INSTRUCTION PANEL
    // =========================================================

    public void OnInstructionPanelClose()
    {
        // From now on, the action buttons may appear when a card is detected.
        introFinished = true;
        lostTimer = 0f;

        SetBackgroundVisible(false);

        if (instructionPanel != null) instructionPanel.SetActive(false);
        if (logoObject != null) logoObject.SetActive(true);
        if (LanguagesettingButton != null) LanguagesettingButton.SetActive(true);
        if (Info_button != null) Info_button.SetActive(true);

        if (arUIManager != null)
            arUIManager.OnIntroFinished();
    }

    // =========================================================
    // BACKGROUND
    // =========================================================

    private void SetBackgroundVisible(bool visible)
    {
        if (backgroundObject != null)
            backgroundObject.SetActive(visible);

        if (backgroundImage != null)
        {
            backgroundImage.gameObject.SetActive(visible);
            backgroundImage.enabled = visible;
        }

        if (cachedBgChild != null)
        {
            cachedBgChild.gameObject.SetActive(visible);
            if (cachedBgChildImage != null) cachedBgChildImage.enabled = visible;
        }

        if (cachedPanelImage != null)
            cachedPanelImage.enabled = visible;
    }
}