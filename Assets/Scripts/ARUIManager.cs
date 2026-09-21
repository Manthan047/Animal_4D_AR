using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ARUIManager : MonoBehaviour
{
    [Header("Panel Root")]
    [Tooltip("The whole AR scan panel. Hidden whenever the catalog is open.")]
    [SerializeField] private GameObject arScanPanel;

    [Header("Cross-Reference (for mutual exclusivity)")]
    [SerializeField] private AnimalCatalogManager animalCatalogManager;

    [Header("Bottom HUD - Action Buttons")]
    [SerializeField] private GameObject actionButtonsPanel;
    [SerializeField] private Button actionButton;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button idleButton;
    [SerializeField] private Button infoToggleBtn;

    [Header("Side Info Panel")]
    [SerializeField] private GameObject infoCardPanel;
    [SerializeField] private bool autoShowInfoCardOnScan = false;
    [SerializeField] private TextMeshProUGUI infoTitleText;
    [SerializeField] private TextMeshProUGUI infoDescriptionText;
    [SerializeField] private TextMeshProUGUI infoFunFactText;
    [SerializeField] private Button closeInfoBtn;

    [Tooltip("The Scroll Rect that wraps the description text. It is reset to the top whenever the description changes. Auto-found from the description text if left empty.")]
    [SerializeField] private ScrollRect infoScrollRect;

    [Header("Description Narration Audio Toggle")]
    [SerializeField] private Toggle audioToggle;
    [Tooltip("AudioSource used to play the description narration clip. Auto-added if left empty.")]
    [SerializeField] private AudioSource descriptionAudioSource;

    private bool isPlayingDescriptionAudio = false;

    [Header("Side Panel Footer Icons")]
    [SerializeField] private Button mode4DButton;
    [SerializeField] private TextMeshProUGUI mode4DLabelText;
    [SerializeField] private Image flatImageDisplay;
    [SerializeField] private Button wikipediaButton;
    [SerializeField] private Button shareButton;

    private bool isShowing3DModel = true;
    private AnimalData currentAnimalData;

    [Header("2D / 3D View Toggle")]
    [SerializeField] private AnimatedToggle view2D3DToggle;

    [Header("Mini 3D Viewer")]
    [SerializeField] private ModelPreviewController modelPreviewController;

    [Header("Scanning Guidance Overlay")]
    [SerializeField] private GameObject scanningOverlayPanel;
    [SerializeField] private TextMeshProUGUI scanningText;

    private readonly Dictionary<Transform, Vector3> _buttonBaseScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Coroutine> _buttonPressRoutines = new Dictionary<Transform, Coroutine>();

    private Coroutine resetScrollRoutine;

    // Gate set by PanelController once the start button -> loading -> instructions
    // sequence has finished. Until then, actionButtonsPanel/infoCardPanel stay
    // hidden even if an animal was already detected in the background during intro.
    private bool introFinished = false;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (animalCatalogManager == null)
        {
            animalCatalogManager = FindFirstObjectByType<AnimalCatalogManager>(FindObjectsInactive.Include);
        }

        if (modelPreviewController == null)
        {
            modelPreviewController = FindFirstObjectByType<ModelPreviewController>(FindObjectsInactive.Include);
        }

        if (infoScrollRect == null && infoDescriptionText != null)
        {
            infoScrollRect = infoDescriptionText.GetComponentInParent<ScrollRect>(true);
        }

        if (descriptionAudioSource == null)
        {
            descriptionAudioSource = gameObject.AddComponent<AudioSource>();
            descriptionAudioSource.playOnAwake = false;
            descriptionAudioSource.loop = false;
        }
    }

    private void OnEnable()
    {
        if (ARAnimalManager.Instance != null)
        {
            ARAnimalManager.Instance.OnSelectedAnimalChanged += HandleSelectedAnimalChanged;
            if (ARAnimalManager.Instance.SelectedAnimal != null)
            {
                HandleSelectedAnimalChanged(ARAnimalManager.Instance.SelectedAnimal);
            }
        }
    }

    private void OnDisable()
    {
        if (ARAnimalManager.Instance != null)
        {
            ARAnimalManager.Instance.OnSelectedAnimalChanged -= HandleSelectedAnimalChanged;
        }

        resetScrollRoutine = null;
        StopDescriptionAudio();
    }

    private void Start()
    {
        if (actionButton != null) actionButton.onClick.AddListener(OnActionButtonClicked);
        if (moveButton != null) moveButton.onClick.AddListener(OnMoveButtonClicked);
        if (idleButton != null) idleButton.onClick.AddListener(OnIdleButtonClicked);
        if (infoToggleBtn != null) infoToggleBtn.onClick.AddListener(ToggleInfoCard);
        if (closeInfoBtn != null) closeInfoBtn.onClick.AddListener(HideInfoCard);
        if (mode4DButton != null) mode4DButton.onClick.AddListener(OnMode4DButtonClicked);
        if (wikipediaButton != null) wikipediaButton.onClick.AddListener(OnWikipediaButtonClicked);
        if (shareButton != null) shareButton.onClick.AddListener(OnShareButtonClicked);
        if (view2D3DToggle != null) view2D3DToggle.onValueChanged.AddListener(OnView2D3DToggleChanged);

        if (audioToggle != null) audioToggle.onValueChanged.AddListener(OnAudioToggleChanged);

        if (flatImageDisplay != null) flatImageDisplay.gameObject.SetActive(false);

        if (modelPreviewController != null)
        {
            modelPreviewController.gameObject.SetActive(false);
            modelPreviewController.ClearModel();
        }

        if (infoCardPanel != null) infoCardPanel.SetActive(false);

        if (view2D3DToggle != null) view2D3DToggle.SetIsOnWithoutNotify(isShowing3DModel);

        UpdateAudioToggleInteractable();

        IAnimalAction initialAnimal = ARAnimalManager.Instance != null ? ARAnimalManager.Instance.SelectedAnimal : null;
        UpdateUIState(initialAnimal);
    }

    private void OnDestroy()
    {
        if (actionButton != null) actionButton.onClick.RemoveListener(OnActionButtonClicked);
        if (moveButton != null) moveButton.onClick.RemoveListener(OnMoveButtonClicked);
        if (idleButton != null) idleButton.onClick.RemoveListener(OnIdleButtonClicked);
        if (infoToggleBtn != null) infoToggleBtn.onClick.RemoveListener(ToggleInfoCard);
        if (closeInfoBtn != null) closeInfoBtn.onClick.RemoveListener(HideInfoCard);
        if (mode4DButton != null) mode4DButton.onClick.RemoveListener(OnMode4DButtonClicked);
        if (wikipediaButton != null) wikipediaButton.onClick.RemoveListener(OnWikipediaButtonClicked);
        if (shareButton != null) shareButton.onClick.RemoveListener(OnShareButtonClicked);
        if (view2D3DToggle != null) view2D3DToggle.onValueChanged.RemoveListener(OnView2D3DToggleChanged);

        if (audioToggle != null) audioToggle.onValueChanged.RemoveListener(OnAudioToggleChanged);
    }

    // =========================================================
    // INTRO HAND-OFF
    // =========================================================

    /// <summary>
    /// Called by PanelController once the start button -> loading -> instructions
    /// sequence has finished. Before this is called, action buttons / info card
    /// stay hidden even if an animal was already detected in the background.
    /// </summary>
    public void OnIntroFinished()
    {
        introFinished = true;

        IAnimalAction current = ARAnimalManager.Instance != null ? ARAnimalManager.Instance.SelectedAnimal : null;
        UpdateUIState(current);
    }

    // =========================================================
    // ANIMAL SELECTION
    // =========================================================

    private void HandleSelectedAnimalChanged(IAnimalAction animal)
    {
        Debug.Log($"[ARUIManager] Selected animal changed: {(animal != null ? animal.AnimalName : "NONE")}");

        StopDescriptionAudio();

        if (animal != null)
        {
            AnimalData data = animal.GetAnimalData();
            if (data != null)
            {
                currentAnimalData = data;
            }

            ShowARScanPanel();
        }

        UpdateUIState(animal);
    }

    public bool IsShowing3DModel => isShowing3DModel;

    public void SetCurrentAnimal(AnimalData data, bool openInfoPanel = false)
    {
        if (data == null) return;

        StopDescriptionAudio();

        currentAnimalData = data;
        UpdateAnimalInfoTexts(currentAnimalData);
        UpdateAudioToggleInteractable();

        if (openInfoPanel)
        {
            ShowInfoCard();
        }
        else
        {
            SetPreviewDisplayMode(isShowing3DModel);
        }
    }

    // =========================================================
    // PANEL VISIBILITY
    // =========================================================

    public void ShowARScanPanel()
    {
        if (arScanPanel != null) arScanPanel.SetActive(true);
        if (animalCatalogManager != null) animalCatalogManager.CloseCatalogPanel();
    }

    public void HideARScanPanel()
    {
        if (arScanPanel != null) arScanPanel.SetActive(false);
    }

    // =========================================================
    // UPDATE UI STATE
    // =========================================================

    private void UpdateUIState(IAnimalAction animal)
    {
        // Gated: won't show action buttons until the intro sequence is done,
        // even if an animal was already detected while it was still playing.
        bool hasActiveAnimal = introFinished && animal != null;

        if (actionButtonsPanel != null) actionButtonsPanel.SetActive(hasActiveAnimal);

        if (scanningOverlayPanel != null)
        {
            scanningOverlayPanel.SetActive(introFinished && !hasActiveAnimal);
            if (scanningText != null) scanningText.text = "Point camera at an Animal card to begin 4D AR experience";
        }

        if (animal != null)
        {
            AnimalData data = animal.GetAnimalData();
            if (data != null)
            {
                currentAnimalData = data;
                UpdateAnimalInfoTexts(currentAnimalData);
                UpdateAudioToggleInteractable();

                if (introFinished && autoShowInfoCardOnScan && infoCardPanel != null) ShowInfoCard();
            }
        }

        if (infoCardPanel != null && infoCardPanel.activeSelf)
        {
            if (currentAnimalData != null) UpdateAnimalInfoTexts(currentAnimalData);
            SetPreviewDisplayMode(isShowing3DModel);
        }
    }

    private void UpdateAnimalInfoTexts(AnimalData data)
    {
        if (data == null) return;

        if (infoTitleText != null) infoTitleText.text = data.animalName;
        if (infoDescriptionText != null) infoDescriptionText.text = data.description;

        if (infoFunFactText != null)
        {
            if (!string.IsNullOrEmpty(data.funFact))
            {
                infoFunFactText.text = "Fun Fact: " + data.funFact;
                infoFunFactText.gameObject.SetActive(true);
            }
            else
            {
                infoFunFactText.text = "";
                infoFunFactText.gameObject.SetActive(false);
            }
        }

        // New text is in place, so send the description back to the top.
        ResetInfoScroll();
    }

    // =========================================================
    // RESET DESCRIPTION SCROLL
    // =========================================================

    private void ResetInfoScroll()
    {
        if (infoScrollRect == null || !isActiveAndEnabled) return;

        if (resetScrollRoutine != null) StopCoroutine(resetScrollRoutine);
        resetScrollRoutine = StartCoroutine(ResetInfoScrollRoutine());
    }

    private IEnumerator ResetInfoScrollRoutine()
    {
        // Wait a frame so the new text has been laid out. Setting the position
        // in the same frame as the text change gets overwritten by the rebuild.
        yield return null;

        if (infoScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();

            if (infoScrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(infoScrollRect.content);
            }

            infoScrollRect.StopMovement();                  // cancel leftover inertia
            infoScrollRect.verticalNormalizedPosition = 1f; // 1 = top
        }

        resetScrollRoutine = null;
    }

    // =========================================================
    // SHOW & TOGGLE INFO CARD
    // =========================================================

    public void ShowInfoCard()
    {
        if (infoCardPanel != null) infoCardPanel.SetActive(true);

        if (currentAnimalData == null)
        {
            if (ARAnimalManager.Instance != null && ARAnimalManager.Instance.SelectedAnimal != null)
            {
                currentAnimalData = ARAnimalManager.Instance.SelectedAnimal.GetAnimalData();
            }

            if (currentAnimalData == null)
            {
                AnimalData[] all = Resources.LoadAll<AnimalData>("");
                if (all != null && all.Length > 0) currentAnimalData = all[0];
            }
        }

        if (currentAnimalData != null)
        {
            UpdateAnimalInfoTexts(currentAnimalData);
            UpdateAudioToggleInteractable();
        }

        SetPreviewDisplayMode(isShowing3DModel);
    }

    public void ToggleInfoCard()
    {
        if (infoToggleBtn != null) AnimateButtonPress(infoToggleBtn.transform);

        if (infoCardPanel != null)
        {
            bool nextState = !infoCardPanel.activeSelf;
            if (nextState) ShowInfoCard();
            else HideInfoCard();
        }
    }

    public void HidePreviews()
    {
        if (modelPreviewController != null)
        {
            modelPreviewController.ClearModel();
            modelPreviewController.gameObject.SetActive(false);
        }

        if (flatImageDisplay != null) flatImageDisplay.gameObject.SetActive(false);
    }

    public void HideInfoCard()
    {
        if (infoCardPanel != null) infoCardPanel.SetActive(false);

        StopDescriptionAudio();
        HidePreviews();
    }

    // =========================================================
    // ACTION BUTTONS
    // =========================================================

    private void OnActionButtonClicked()
    {
        if (actionButton != null) AnimateButtonPress(actionButton.transform);
        if (ARAnimalManager.Instance != null) ARAnimalManager.Instance.TriggerAction();
    }

    private void OnMoveButtonClicked()
    {
        if (moveButton != null) AnimateButtonPress(moveButton.transform);
        if (ARAnimalManager.Instance != null) ARAnimalManager.Instance.TriggerMove();
    }

    private void OnIdleButtonClicked()
    {
        if (idleButton != null) AnimateButtonPress(idleButton.transform);
        if (ARAnimalManager.Instance != null) ARAnimalManager.Instance.TriggerIdle();
    }

    private void OnMode4DButtonClicked()
    {
        if (mode4DButton != null) AnimateButtonPress(mode4DButton.transform);
        SetPreviewDisplayMode(!isShowing3DModel);
    }

    // =========================================================
    // DESCRIPTION NARRATION AUDIO TOGGLE LOGIC
    // =========================================================

    private void OnAudioToggleChanged(bool isOn)
    {
        if (isOn)
        {
            PlayDescriptionAudio();
        }
        else
        {
            StopDescriptionAudio();
        }
    }

    private void PlayDescriptionAudio()
    {
        if (currentAnimalData == null || currentAnimalData.descriptionAudioClip == null)
        {
            Debug.LogWarning("[ARUIManager] No description audio clip assigned for the current animal.");
            if (audioToggle != null) audioToggle.SetIsOnWithoutNotify(false);
            return;
        }

        if (descriptionAudioSource == null) return;

        descriptionAudioSource.clip = currentAnimalData.descriptionAudioClip;
        descriptionAudioSource.Play();
        isPlayingDescriptionAudio = true;
    }

    private void StopDescriptionAudio()
    {
        if (descriptionAudioSource != null && descriptionAudioSource.isPlaying)
        {
            descriptionAudioSource.Stop();
        }

        isPlayingDescriptionAudio = false;

        if (audioToggle != null)
        {
            audioToggle.SetIsOnWithoutNotify(false);
        }
    }

    private void UpdateAudioToggleInteractable()
    {
        if (audioToggle == null) return;

        bool hasClip = currentAnimalData != null && currentAnimalData.descriptionAudioClip != null;
        audioToggle.interactable = hasClip;
    }

    private void Update()
    {
        if (isPlayingDescriptionAudio && descriptionAudioSource != null && !descriptionAudioSource.isPlaying)
        {
            isPlayingDescriptionAudio = false;
            if (audioToggle != null)
            {
                audioToggle.SetIsOnWithoutNotify(false);
            }
        }
    }

    // =========================================================
    // SET 2D / 3D DISPLAY
    // =========================================================

    public void SetPreviewDisplayMode(bool show3D)
    {
        isShowing3DModel = show3D;

        if (currentAnimalData == null)
        {
            if (ARAnimalManager.Instance != null && ARAnimalManager.Instance.SelectedAnimal != null)
            {
                currentAnimalData = ARAnimalManager.Instance.SelectedAnimal.GetAnimalData();
            }

            if (currentAnimalData == null)
            {
                AnimalData[] all = Resources.LoadAll<AnimalData>("");
                if (all != null && all.Length > 0) currentAnimalData = all[0];
            }

            UpdateAudioToggleInteractable();
        }

        if (currentAnimalData == null)
        {
            HidePreviews();
            return;
        }

        if (show3D)
        {
            if (flatImageDisplay != null) flatImageDisplay.gameObject.SetActive(false);

            if (modelPreviewController != null)
            {
                modelPreviewController.gameObject.SetActive(true);
                modelPreviewController.ShowModel(currentAnimalData);
            }
        }
        else
        {
            if (modelPreviewController != null)
            {
                modelPreviewController.ClearModel();
                modelPreviewController.gameObject.SetActive(false);
            }

            if (flatImageDisplay != null)
            {
                Sprite spriteToShow = currentAnimalData.image2D != null ? currentAnimalData.image2D : currentAnimalData.thumbnail;

                if (spriteToShow != null)
                {
                    flatImageDisplay.sprite = spriteToShow;
                    flatImageDisplay.preserveAspect = true;
                    flatImageDisplay.color = Color.white;
                }

                flatImageDisplay.gameObject.SetActive(true);
            }
        }

        if (mode4DLabelText != null) mode4DLabelText.text = show3D ? "3D Mode" : "2D Mode";

        if (view2D3DToggle != null) view2D3DToggle.SetIsOnWithoutNotify(show3D);
    }

    // =========================================================
    // WIKIPEDIA & SHARE
    // =========================================================

    private void OnWikipediaButtonClicked()
    {
        if (wikipediaButton != null) AnimateButtonPress(wikipediaButton.transform);

        string animalName = currentAnimalData != null && !string.IsNullOrEmpty(currentAnimalData.animalName)
            ? currentAnimalData.animalName
            : infoTitleText != null ? infoTitleText.text : "Animal";

        string encodedName = System.Uri.EscapeDataString(animalName);
        Application.OpenURL($"https://en.wikipedia.org/wiki/{encodedName}");
    }

    private void OnShareButtonClicked()
    {
        if (shareButton != null) AnimateButtonPress(shareButton.transform);

        string animalName = currentAnimalData != null && !string.IsNullOrEmpty(currentAnimalData.animalName)
            ? currentAnimalData.animalName
            : infoTitleText != null ? infoTitleText.text : "Animal";

        Debug.Log($"[ARUIManager] Share requested: Check out {animalName} in Animal 4D+ AR!");
    }

    private void OnView2D3DToggleChanged(bool is3D)
    {
        SetPreviewDisplayMode(is3D);
    }

    private void AnimateButtonPress(Transform btnTransform)
    {
        if (btnTransform == null) return;

        if (!_buttonBaseScales.TryGetValue(btnTransform, out Vector3 baseScale))
        {
            baseScale = btnTransform.localScale;
            _buttonBaseScales[btnTransform] = baseScale;
        }

        if (_buttonPressRoutines.TryGetValue(btnTransform, out Coroutine existing) && existing != null)
        {
            StopCoroutine(existing);
        }

        _buttonPressRoutines[btnTransform] = StartCoroutine(AnimateButtonPressRoutine(btnTransform, baseScale));
    }

    private IEnumerator AnimateButtonPressRoutine(Transform btnTransform, Vector3 baseScale)
    {
        btnTransform.localScale = baseScale * 0.9f;
        yield return new WaitForSeconds(0.08f);
        btnTransform.localScale = baseScale;
    }
}