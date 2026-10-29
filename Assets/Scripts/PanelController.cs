using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    [Tooltip("The Back button inside the Setting Panel. Closes the whole settings menu. Auto-found (a Button named like 'back') if left empty.")]
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
    [Tooltip("Back button inside the language sub panel. Closes the whole settings menu. Auto-found (a Button named like 'back') if left empty.")]
    [SerializeField] private Button languageSubBackButton;

    [Tooltip("Button inside the Setting Panel that opens the sound options (e.g. 'Sound_option').")]
    [SerializeField] private Button soundOptionButton;
    [Tooltip("The sound options sub panel (e.g. 'Sound_sub_panel'). Shown INSTEAD of the Setting Panel.")]
    [SerializeField] private GameObject soundSubPanel;
    [Tooltip("Back button inside the sound sub panel. Closes the whole settings menu. Auto-found (a Button named like 'back') if left empty.")]
    [SerializeField] private Button soundSubBackButton;

    [Header("Loading UI Elements")]
    [SerializeField] private Slider progressBar;
    [Tooltip("Percentage text shown while loading.")]
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

    [Header("Default Card")]
    [Tooltip("Card shown when no card is tracked. If empty, the first card in the scene (sorted by name) is used.")]
    [SerializeField] private ObserverBehaviour defaultCard;

    [Header("Card Selection (tap / next / previous)")]
    [Tooltip("Camera used to detect taps on cards. Uses Camera.main if empty.")]
    [SerializeField] private Camera arCamera;
    [Tooltip("Optional UI button: show the next card's details.")]
    [SerializeField] private Button nextCardButton;
    [Tooltip("Optional UI button: show the previous card's details.")]
    [SerializeField] private Button previousCardButton;
    [SerializeField] private float tapRayDistance = 100f;
    [Tooltip("Swipe left / right at least this many pixels to go to the next / previous card.")]
    [SerializeField] private float swipeMinDistance = 80f;

    [Header("Cross-Reference")]
    [SerializeField] private ARUIManager arUIManager;
    [SerializeField] private AnimalCatalogManager animalCatalogManager;

    private Coroutine loadingRoutine;
    private Transform cachedBgChild;
    private Image cachedBgChildImage;
    private Image cachedPanelImage;
    private int lastShownPercent = -1;

    // Card detection state
    private bool introFinished;
    private bool actionsVisible;
    private float lostTimer;
    private bool infoModeActive;
    private readonly List<ObserverBehaviour> cardTargets = new List<ObserverBehaviour>();
    private ObserverBehaviour shownCard;   // card whose description is currently displayed
    private ObserverBehaviour manualCard;  // card the user picked by tapping / next / previous
    private AnimalData manualAnimalData;   // animal data the user picked from UI catalog or code
    private ObserverBehaviour lastTrackedCard;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    // Pointer state (tap / swipe)
    private Vector2 pressPos;
    private bool pressing;
    private int lastInfoClickFrame = -1;

    public GameObject InfoButtonObject => Info_button;

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
        shownCard = null;

        FindCardTargets();
    }

    private void Update()
    {
        // While the info screen is open, keep the description in sync:
        // tracked card if there is one, otherwise the default (first) card.
        if (infoModeActive)
        {
            HandlePointer();
            RefreshShownCard();   // tracked card, a tapped / swiped card, or the first card
            return;
        }

        if (!introFinished || actionsButtonsPanel == null)
            return;

        // Action buttons only exist after the intro is done AND a card is being tracked.
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

        // Stable order: by name (e.g. Card_01, Card_02...). Change to sibling index if you prefer.
        cardTargets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (defaultCard == null && cardTargets.Count > 0)
            defaultCard = cardTargets[0];
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

    private ObserverBehaviour GetTrackedCard()
    {
        if (ARAnimalManager.Instance != null && ARAnimalManager.Instance.HasActiveAnimal && ARAnimalManager.Instance.SelectedAnimal != null)
        {
            Component c = ARAnimalManager.Instance.SelectedAnimal as Component;
            if (c != null)
            {
                ObserverBehaviour obs = c.GetComponentInParent<ObserverBehaviour>();
                if (obs != null) return obs;
            }
        }

        for (int i = 0; i < cardTargets.Count; i++)
        {
            ObserverBehaviour t = cardTargets[i];
            if (t != null && t.isActiveAndEnabled && t.TargetStatus.Status == Status.TRACKED)
                return t;
        }
        return null;
    }

    // Priority: card the user picked > tracked card > default (first) card.
    // A newly tracked card replaces the user's pick.
    private ObserverBehaviour GetSelectedCard()
    {
        ObserverBehaviour tracked = GetTrackedCard();
        if (tracked != lastTrackedCard)
        {
            lastTrackedCard = tracked;
            if (tracked != null)
            {
                manualCard = null;
                manualAnimalData = null;
            }
        }

        if (manualCard != null) return manualCard;
        return tracked != null ? tracked : defaultCard;
    }

    // Updates the displayed description only when the selected card changes
    // (or always, when force is true).
    private void RefreshShownCard(bool force = false)
    {
        ObserverBehaviour tracked = GetTrackedCard();
        if (tracked != lastTrackedCard)
        {
            lastTrackedCard = tracked;
            if (tracked != null)
            {
                manualCard = null;
                manualAnimalData = null;
            }
        }

        // If user manually picked an animal card in the info panel UI, respect that selection!
        if (manualAnimalData != null)
        {
            if (force)
            {
                if (arUIManager != null) arUIManager.SetCurrentAnimal(manualAnimalData, false);
                else if (animalCatalogManager != null) animalCatalogManager.UpdateDetailUI(manualAnimalData);
            }
            return;
        }

        ObserverBehaviour card = GetSelectedCard();
        if (!force && card != null && card == shownCard) return;

        shownCard = card;
        ApplyCardToUI(card);
    }

    // Shows this card's animal in the info card (description, 3D / 2D viewer, audio)
    // using ARUIManager's existing public SetCurrentAnimal(...).
    private void ApplyCardToUI(ObserverBehaviour card)
    {
        if (arUIManager == null)
        {
            if (infoCardPanel != null) infoCardPanel.SetActive(true);
            return;
        }

        AnimalData data = null;

        if (card != null)
        {
            IAnimalAction animal = card.GetComponentInChildren<IAnimalAction>(true);
            if (animal == null) animal = card.GetComponentInParent<IAnimalAction>(true);
            if (animal != null) data = animal.GetAnimalData();
        }

        if (data == null && ARAnimalManager.Instance != null && ARAnimalManager.Instance.HasActiveAnimal && ARAnimalManager.Instance.SelectedAnimal != null)
        {
            data = ARAnimalManager.Instance.SelectedAnimal.GetAnimalData();
        }

        // If no animal is tracked, fall back to the first animal card
        if (data == null)
        {
            data = GetDefaultFirstAnimalData();
        }

        if (data != null)
        {
            arUIManager.SetCurrentAnimal(data, true);   // true = open / refresh the info card
        }
        else
        {
            if (infoCardPanel != null) infoCardPanel.SetActive(true);
            arUIManager.ShowInfoCard();
        }
    }

    private AnimalData GetDefaultFirstAnimalData()
    {
        if (animalCatalogManager != null && animalCatalogManager.animals != null && animalCatalogManager.animals.Count > 0)
        {
            for (int i = 0; i < animalCatalogManager.animals.Count; i++)
            {
                if (animalCatalogManager.animals[i] != null)
                    return animalCatalogManager.animals[i];
            }
        }

        if (defaultCard != null)
        {
            IAnimalAction animal = defaultCard.GetComponentInChildren<IAnimalAction>(true);
            if (animal == null) animal = defaultCard.GetComponentInParent<IAnimalAction>(true);
            if (animal != null && animal.GetAnimalData() != null) return animal.GetAnimalData();
        }

        AnimalData[] all = Resources.LoadAll<AnimalData>("Data");
        if (all == null || all.Length == 0) all = Resources.LoadAll<AnimalData>("");
        if (all != null && all.Length > 0) return all[0];

        return null;
    }

    /// <summary>
    /// Called when an animal card is selected in the UI.
    /// Locks manual selection so RefreshShownCard won't snap back to default/tracked card.
    /// </summary>
    public void SelectAnimalData(AnimalData data)
    {
        if (data == null) return;
        manualAnimalData = data;

        manualCard = null;
        for (int i = 0; i < cardTargets.Count; i++)
        {
            ObserverBehaviour t = cardTargets[i];
            if (t == null) continue;
            IAnimalAction action = t.GetComponentInChildren<IAnimalAction>(true);
            if (action != null && action.GetAnimalData() == data)
            {
                manualCard = t;
                break;
            }
            if (t.name.ToLowerInvariant().Contains(data.animalName.english.ToLowerInvariant()))
            {
                manualCard = t;
                break;
            }
        }

        shownCard = manualCard;
    }

    /// <summary>Show the details of a specific card (call from a UI button too).</summary>
    public void SelectCard(ObserverBehaviour card)
    {
        if (card == null) return;
        manualAnimalData = null;
        manualCard = card;
        if (infoModeActive)
            RefreshShownCard(true);
    }

    public void SelectNextCard()     { StepCard(+1); }
    public void SelectPreviousCard() { StepCard(-1); }

    private void StepCard(int dir)
    {
        if (cardTargets.Count == 0) return;
        int current = shownCard != null ? cardTargets.IndexOf(shownCard) : -1;
        int next = (current + dir + cardTargets.Count) % cardTargets.Count;
        SelectCard(cardTargets[next]);
    }

    // =========================================================
    // TAP / SWIPE (info screen)
    //   tap on a card (needs a Collider)  -> that card
    //   swipe left / right                -> next / previous card
    // =========================================================

    private void HandlePointer()
    {
        if (!GetPointer(out Vector2 pos, out bool down, out bool up)) return;

        if (down)
        {
            pressing = !IsPointerOverButton(pos);
            pressPos = pos;
            return;
        }

        if (!up || !pressing) return;
        pressing = false;

        Vector2 delta = pos - pressPos;

        if (Mathf.Abs(delta.x) >= swipeMinDistance && Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.5f)
        {
            if (delta.x < 0f) SelectNextCard();
            else SelectPreviousCard();
            return;
        }

        if (delta.magnitude < 30f)
            TapAt(pos);
    }

    private bool IsPointerOverButton(Vector2 pos)
    {
        if (EventSystem.current == null) return false;

        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, uiHits);
        return uiHits.Count > 0 && uiHits[0].gameObject.GetComponentInParent<Selectable>() != null;
    }

    private void TapAt(Vector2 pos)
    {
        Camera cam = arCamera != null ? arCamera : Camera.main;
        if (cam == null) return;

        if (Physics.Raycast(cam.ScreenPointToRay(pos), out RaycastHit hit, tapRayDistance))
        {
            ObserverBehaviour card = hit.collider.GetComponentInParent<ObserverBehaviour>();
            if (card != null) SelectCard(card);
        }
    }

    private bool GetPointer(out Vector2 pos, out bool down, out bool up)
    {
        pos = default; down = false; up = false;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts != null)
        {
            var t = ts.primaryTouch;
            if (t.press.wasPressedThisFrame)  { pos = t.position.ReadValue(); down = true; return true; }
            if (t.press.wasReleasedThisFrame) { pos = t.position.ReadValue(); up = true;   return true; }
        }
        var ms = UnityEngine.InputSystem.Mouse.current;
        if (ms != null)
        {
            if (ms.leftButton.wasPressedThisFrame)  { pos = ms.position.ReadValue(); down = true; return true; }
            if (ms.leftButton.wasReleasedThisFrame) { pos = ms.position.ReadValue(); up = true;   return true; }
        }
#else
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) { pos = t.position; down = true; return true; }
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { pos = t.position; up = true; return true; }
        }
        if (Input.GetMouseButtonDown(0)) { pos = Input.mousePosition; down = true; return true; }
        if (Input.GetMouseButtonUp(0))   { pos = Input.mousePosition; up = true;   return true; }
#endif
        return false;
    }

    // =========================================================
    // AUTO RESOLVE
    // =========================================================

    private void AutoResolveReferences()
    {
        if (arUIManager == null)
            arUIManager = FindAnyObjectByType<ARUIManager>(FindObjectsInactive.Include);

        if (animalCatalogManager == null)
            animalCatalogManager = FindAnyObjectByType<AnimalCatalogManager>(FindObjectsInactive.Include);

        // Only do the expensive full-scene scan if something is actually missing
        bool needScan =
            cautionObject == null || loadingPanel == null || instructionPanel == null ||
            LanguagesettingButton == null || settingPanel == null ||
            logoObject == null || backgroundObject == null || actionsButtonsPanel == null ||
            infoCardPanel == null || progressBar == null || okButton == null;

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

                if (okButton == null && (tName == "ok_button" || tName == "OK_Button" || tName == "okButton"))
                    okButton = t.GetComponent<Button>();
            }
        }

        if (loadingPanel != null)
        {
            if (progressBar == null)
                progressBar = loadingPanel.GetComponentInChildren<Slider>(true);
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

        // Pass 4 (fallback): find the Setting panel's own Back button even if the
        // Button component is on a child object or the "back" name is on the parent.
        if (settingBackButton == null)
        {
            foreach (Button b in settingPanel.GetComponentsInChildren<Button>(true))
            {
                if (languageSubPanel != null && b.transform.IsChildOf(languageSubPanel.transform)) continue;
                if (soundSubPanel != null && b.transform.IsChildOf(soundSubPanel.transform)) continue;

                string own = b.name.ToLowerInvariant();
                string parent = b.transform.parent != null
                    ? b.transform.parent.name.ToLowerInvariant() : "";

                if (own.Contains("back") || parent.Contains("back"))
                {
                    settingBackButton = b;
                    break;
                }
            }
        }

        if (settingBackButton == null)
        {
            Debug.LogWarning("PanelController: Setting Back button not found. " +
                             "Assign it manually in the Inspector (Setting Back Button).");
        }
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
                bool hasPersistent = false;
                for (int i = 0; i < infoBtn.onClick.GetPersistentEventCount(); i++)
                {
                    if (infoBtn.onClick.GetPersistentMethodName(i) == nameof(OnInfoButtonClicked))
                    {
                        hasPersistent = true;
                        break;
                    }
                }

                if (!hasPersistent)
                {
                    infoBtn.onClick.RemoveListener(OnInfoButtonClicked);
                    infoBtn.onClick.AddListener(OnInfoButtonClicked);
                }
            }
        }

        if (infoBackButton != null)
        {
            infoBackButton.onClick.RemoveListener(OnInfoBackButtonClicked);
            infoBackButton.onClick.AddListener(OnInfoBackButtonClicked);
        }

        if (nextCardButton != null)
        {
            nextCardButton.onClick.RemoveListener(SelectNextCard);
            nextCardButton.onClick.AddListener(SelectNextCard);
        }

        if (previousCardButton != null)
        {
            previousCardButton.onClick.RemoveListener(SelectPreviousCard);
            previousCardButton.onClick.AddListener(SelectPreviousCard);
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

        if (nextCardButton != null)
            nextCardButton.onClick.RemoveListener(SelectNextCard);

        if (previousCardButton != null)
            previousCardButton.onClick.RemoveListener(SelectPreviousCard);
    }

    // =========================================================
    // SETTINGS MENU
    //
    //   setting_button      -> opens Setting_panel (closes everything if already open)
    //   Language_option     -> Setting_panel becomes INVISIBLE, Language_sub_panel shows
    //   Sound_option        -> Setting_panel becomes INVISIBLE, Sound_sub_panel shows
    //   Any Back button     -> closes EVERYTHING (sub panels + Setting_panel)
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
            CloseSettingsCompletely();
        else
            ShowSettingPanel();
    }

    /// <summary>
    /// Back button inside the Setting panel: closes the whole settings menu.
    /// </summary>
    public void OnSettingBackClicked()
    {
        CloseSettingsCompletely();
    }

    /// <summary>
    /// Back button inside a sub panel (Language / Sound): closes the whole settings menu
    /// (sub panel + Setting panel).
    /// </summary>
    public void OnSubPanelBackClicked()
    {
        CloseSettingsCompletely();
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
            settingPanel.SetActive(false);   // Setting panel becomes invisible

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
        if (Time.frameCount == lastInfoClickFrame)
            return; // Ignore duplicate calls in the exact same frame
        lastInfoClickFrame = Time.frameCount;

        if (infoModeActive)
        {
            ObserverBehaviour currentCard = GetSelectedCard();
            if (currentCard == shownCard && shownCard != null)
            {
                SetInfoMode(false);
                return;
            }
        }

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
        introFinished = true;
        if (arUIManager != null)
            arUIManager.OnIntroFinished();

        if (infoModeActive == enable)
        {
            if (enable)
            {
                // Re-clicking info button when already open: refresh immediately to detected model
                manualCard = null;
                manualAnimalData = null;
                shownCard = null;
                RefreshShownCard(true);
            }
            return;
        }

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

            if (infoCardPanel != null)
                infoCardPanel.SetActive(true);

            shownCard = null;
            manualCard = null;
            manualAnimalData = null;

            RefreshShownCard(true);
        }
        else
        {
            // Close the info screen. The action buttons come back by themselves
            // once a card is tracked again (see Update).
            shownCard = null;
            manualCard = null;
            manualAnimalData = null;
            if (arUIManager != null) arUIManager.HideInfoCard();
            else if (infoCardPanel != null) infoCardPanel.SetActive(false);
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
        if (Info_button != null) Info_button.SetActive(true);

        loadingRoutine = null;
    }

    private void SetLoadingBarVisible(bool visible)
    {
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(visible);
            if (visible) progressBar.value = 0f;
        }

        // Percentage text follows the loading bar
        if (progressText != null)
        {
            progressText.gameObject.SetActive(visible);
            if (visible)
            {
                lastShownPercent = -1;   // force a refresh
                SetProgressText(0f);
            }
        }

        if (loadingImage != null)
            loadingImage.SetActive(visible);
    }

    private void SetProgress(float progress)
    {
        if (progressBar != null)
            progressBar.value = progress;

        SetProgressText(progress);
    }

    private void SetProgressText(float progress)
    {
        if (progressText == null) return;

        int percent = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f);
        if (percent == lastShownPercent) return;   // only re-layout the text when the number changes
        lastShownPercent = percent;

        progressText.Text = $"Loading {percent}%";
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