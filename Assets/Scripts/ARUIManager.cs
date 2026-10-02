using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ARUIManager : MonoBehaviour
{
    [Header("Cross-Reference (for mutual exclusivity)")]
    [SerializeField] private AnimalCatalogManager animalCatalogManager;

    [Header("Bottom HUD - Action Buttons")]
    [SerializeField] private GameObject actionButtonsPanel;
    [SerializeField] private Button actionButton;
    [SerializeField] private Button infoToggleBtn;

    [Header("Back Button")]
    [Tooltip("Closes the info card if it is open. Otherwise fires On Back Pressed (hook up catalog / previous screen here).")]
    [SerializeField] private Button backButton;
    [SerializeField] private UnityEvent onBackPressed;

    [Header("Side Info Panel")]
    [SerializeField] private GameObject infoCardPanel;
    [SerializeField] private bool autoShowInfoCardOnScan = false;

    [Header("Description Narration Audio Toggle")]
    [SerializeField] private Toggle audioToggle;
    [Tooltip("AudioSource used to play the description narration clip. Auto-added if left empty.")]
    [SerializeField] private AudioSource descriptionAudioSource;

    private bool isPlayingDescriptionAudio = false;

    [Header("Side Panel Footer Icons")]
    [SerializeField] private Image flatImageDisplay;
    [SerializeField] private Button shareButton;

    [Header("Share Settings")]
    [SerializeField] private string shareSubject = "Animal 4D+ AR";
    [Tooltip("Optional. Added to the end of the share message (e.g. your Play Store link).")]
    [SerializeField] private string shareLink = "";
    [Tooltip("Also attach a screenshot. Needs the NativeShare plugin and the scripting define symbol USE_NATIVE_SHARE.")]
    [SerializeField] private bool shareScreenshot = false;

    private bool isSharing = false;

    private bool isShowing3DModel = true;
    private AnimalData currentAnimalData;

    [Header("2D / 3D View Toggle")]
    [SerializeField] private AnimatedToggle view2D3DToggle;

    [Header("Mini 3D Viewer")]
    [SerializeField] private ModelPreviewController modelPreviewController;

    private readonly Dictionary<Transform, Vector3> _buttonBaseScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Coroutine> _buttonPressRoutines = new Dictionary<Transform, Coroutine>();

    // Gate set by PanelController once the start button -> loading -> instructions
    // sequence has finished. Until then, actionButtonsPanel/infoCardPanel stay
    // hidden even if an animal was already detected in the background during intro.
    private bool introFinished = false;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        AutoResolveReferences();

        if (animalCatalogManager == null)
        {
            animalCatalogManager = FindAnyObjectByType<AnimalCatalogManager>(FindObjectsInactive.Include);
        }

        if (modelPreviewController == null)
        {
            modelPreviewController = FindAnyObjectByType<ModelPreviewController>(FindObjectsInactive.Include);
        }

        if (descriptionAudioSource == null)
        {
            descriptionAudioSource = GetComponent<AudioSource>();
            if (descriptionAudioSource == null)
            {
                descriptionAudioSource = gameObject.AddComponent<AudioSource>();
            }
            descriptionAudioSource.playOnAwake = false;
            descriptionAudioSource.loop = false;
        }
    }

    private void AutoResolveReferences()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);

        foreach (Transform t in allTransforms)
        {
            string tName = t.name.Trim();

            if (actionButtonsPanel == null && (tName == "Actions Buttons Panel" || tName == "ActionButtonsPanel"))
                actionButtonsPanel = t.gameObject;

            if (infoCardPanel == null && (tName == "info card panel" || tName == "InfoCardPanel"))
                infoCardPanel = t.gameObject;

            if (infoToggleBtn == null && (tName == "Info" || tName == "info" || tName == "InfoButton" || tName == "info_background"))
            {
                Button b = t.GetComponent<Button>();
                if (b != null) infoToggleBtn = b;
                else
                {
                    Button cb = t.GetComponentInChildren<Button>(true);
                    if (cb != null) infoToggleBtn = cb;
                }
            }

            if (actionButton == null && (tName == "Action Button" || tName == "ActionButton"))
                actionButton = t.GetComponent<Button>();

            if (backButton == null && (tName == "Back_button" || tName == "BackButton" || tName == "Back Button"))
                backButton = t.GetComponent<Button>();

            if (view2D3DToggle == null && (tName == "Toggle" || tName == "Animated_toggle"))
            {
                if (infoCardPanel != null && t.IsChildOf(infoCardPanel.transform))
                {
                    view2D3DToggle = t.GetComponent<AnimatedToggle>();
                    if (view2D3DToggle == null) view2D3DToggle = t.GetComponentInChildren<AnimatedToggle>(true);
                }
            }

            if (flatImageDisplay == null && (tName == "2DImage" || tName == "2D_Image"))
            {
                if (infoCardPanel != null && t.IsChildOf(infoCardPanel.transform))
                {
                    flatImageDisplay = t.GetComponent<Image>();
                }
            }

            if (modelPreviewController == null && (tName == "RawImage" || tName == "ModelPreviewController"))
            {
                if (infoCardPanel != null && t.IsChildOf(infoCardPanel.transform))
                {
                    modelPreviewController = t.GetComponent<ModelPreviewController>();
                }
            }

            if (audioToggle == null && (tName == "Volume" || tName == "AudioToggle"))
            {
                if (infoCardPanel != null && t.IsChildOf(infoCardPanel.transform))
                {
                    audioToggle = t.GetComponent<Toggle>();
                }
            }

            // Share button: matched by name anywhere in the scene (it no longer has to be
            // inside the info card panel). If the Button component is on a child, find it.
            if (shareButton == null && (tName == "Share_button" || tName == "ShareButton" || tName == "Share" || tName == "share"))
            {
                Button sb = t.GetComponent<Button>();
                if (sb == null) sb = t.GetComponentInChildren<Button>(true);
                if (sb != null) shareButton = sb;
            }
        }
    }

    public bool IsShowing3DModel => isShowing3DModel;
    public AnimalData CurrentAnimalData => currentAnimalData;

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

        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            LanguageManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }
    }

    private void OnDisable()
    {
        if (ARAnimalManager.Instance != null)
        {
            ARAnimalManager.Instance.OnSelectedAnimalChanged -= HandleSelectedAnimalChanged;
        }

        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        }

        StopDescriptionAudio();
    }

    private void Start()
    {
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            LanguageManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }

        if (actionButton != null) actionButton.onClick.AddListener(OnActionButtonClicked);
        if (infoToggleBtn != null) infoToggleBtn.onClick.AddListener(ToggleInfoCard);
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClicked);
        if (view2D3DToggle != null) view2D3DToggle.onValueChanged.AddListener(OnView2D3DToggleChanged);

        if (shareButton != null)
        {
            shareButton.onClick.AddListener(OnShareButtonClicked);
        }
        else
        {
            Debug.LogWarning("[ARUIManager] Share button not found. Drag it into the 'Share Button' field in the Inspector.", this);
        }

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
        if (infoToggleBtn != null) infoToggleBtn.onClick.RemoveListener(ToggleInfoCard);
        if (backButton != null) backButton.onClick.RemoveListener(OnBackButtonClicked);
        if (shareButton != null) shareButton.onClick.RemoveListener(OnShareButtonClicked);
        if (view2D3DToggle != null) view2D3DToggle.onValueChanged.RemoveListener(OnView2D3DToggleChanged);

        if (audioToggle != null) audioToggle.onValueChanged.RemoveListener(OnAudioToggleChanged);
    }

    // =========================================================
    // LANGUAGE CHANGE HANDLER
    // =========================================================

    private void HandleLanguageChanged(string newLang)
    {
        ResolveCurrentAnimalData();

        if (currentAnimalData != null && animalCatalogManager != null)
        {
            animalCatalogManager.UpdateDetailUI(currentAnimalData);
        }

        UpdateAudioToggleInteractable();
    }

    // =========================================================
    // INTRO HAND-OFF
    // =========================================================

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
                if (animalCatalogManager != null)
                {
                    animalCatalogManager.UpdateDetailUI(data);
                }
            }

            ShowARScanPanel();
        }

        UpdateUIState(animal);
    }

    public void SetCurrentAnimal(AnimalData data, bool openInfoPanel = false)
    {
        if (data == null) return;

        StopDescriptionAudio();

        currentAnimalData = data;
        UpdateAudioToggleInteractable();

        if (animalCatalogManager != null)
        {
            animalCatalogManager.UpdateDetailUI(data);
        }

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

    // The AR scan panel reference was removed; this now only closes the catalog.
    public void ShowARScanPanel()
    {
        if (animalCatalogManager != null) animalCatalogManager.CloseCatalogPanel();
    }

    // Kept only so external scripts (e.g. AnimalCatalogManager) still compile.
    public void HideARScanPanel() { }

    // =========================================================
    // UPDATE UI STATE
    // =========================================================

    private void UpdateUIState(IAnimalAction animal)
    {
        bool hasActiveAnimal = introFinished && animal != null;

        if (actionButtonsPanel != null) actionButtonsPanel.SetActive(hasActiveAnimal);

        if (animal != null)
        {
            AnimalData data = animal.GetAnimalData();
            if (data != null)
            {
                currentAnimalData = data;
                UpdateAudioToggleInteractable();

                if (animalCatalogManager != null)
                {
                    animalCatalogManager.UpdateDetailUI(data);
                }

                if (introFinished && autoShowInfoCardOnScan && infoCardPanel != null) ShowInfoCard();
            }
        }

        if (infoCardPanel != null && infoCardPanel.activeSelf)
        {
            SetPreviewDisplayMode(isShowing3DModel);
        }
    }

    // =========================================================
    // SHOW & TOGGLE INFO CARD
    // =========================================================

    public void ShowInfoCard()
    {
        if (infoCardPanel != null) infoCardPanel.SetActive(true);

        ResolveCurrentAnimalData();

        if (currentAnimalData != null)
        {
            UpdateAudioToggleInteractable();

            if (animalCatalogManager != null)
            {
                animalCatalogManager.UpdateDetailUI(currentAnimalData);
            }
        }

        SetPreviewDisplayMode(isShowing3DModel);
        StartCoroutine(RefreshInfoLayout());
    }

    // Rebuilds the layout one frame after opening so the description text is not blank,
    // and makes sure the description starts from its first line.
    private IEnumerator RefreshInfoLayout()
    {
        yield return null;

        if (infoCardPanel == null) yield break;

        Canvas.ForceUpdateCanvases();

        RectTransform rt = infoCardPanel.GetComponent<RectTransform>();
        if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        if (animalCatalogManager != null) animalCatalogManager.ScrollDescriptionToTop();
    }

    private void ResolveCurrentAnimalData()
    {
        if (currentAnimalData != null) return;

        if (ARAnimalManager.Instance != null && ARAnimalManager.Instance.SelectedAnimal != null)
        {
            currentAnimalData = ARAnimalManager.Instance.SelectedAnimal.GetAnimalData();
        }

        if (currentAnimalData == null && animalCatalogManager != null && animalCatalogManager.CurrentSelectedData != null)
        {
            currentAnimalData = animalCatalogManager.CurrentSelectedData;
        }

        if (currentAnimalData == null)
        {
            AnimalData[] all = Resources.LoadAll<AnimalData>("Data");
            if (all == null || all.Length == 0) all = Resources.LoadAll<AnimalData>("");
            if (all != null && all.Length > 0) currentAnimalData = all[0];
        }
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
    // BACK BUTTON
    // =========================================================

    private void OnBackButtonClicked()
    {
        if (backButton != null) AnimateButtonPress(backButton.transform);

        if (infoCardPanel != null && infoCardPanel.activeSelf)
        {
            HideInfoCard();
            return;
        }

        onBackPressed?.Invoke();
    }

    // =========================================================
    // ACTION BUTTON
    // =========================================================

    private void OnActionButtonClicked()
    {
        if (actionButton != null) AnimateButtonPress(actionButton.transform);
        if (ARAnimalManager.Instance != null) ARAnimalManager.Instance.TriggerAction();
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

        if (view2D3DToggle != null) view2D3DToggle.SetIsOnWithoutNotify(show3D);
    }

    // =========================================================
    // SHARE
    // =========================================================

    private void OnShareButtonClicked()
    {
        if (shareButton != null) AnimateButtonPress(shareButton.transform);

        if (isSharing) return;

        ResolveCurrentAnimalData();

        string animalName = "Animal";
        if (currentAnimalData != null)
        {
            string localized = currentAnimalData.GetLocalizedName();
            if (!string.IsNullOrEmpty(localized)) animalName = localized;
        }

        string message = $"Check out {animalName} in {shareSubject}!";
        if (!string.IsNullOrEmpty(shareLink)) message += "\n" + shareLink;

        Debug.Log($"[ARUIManager] Share requested: {message}");

        StartCoroutine(ShareRoutine(shareSubject, message));
    }

    private IEnumerator ShareRoutine(string subject, string message)
    {
        isSharing = true;

#if USE_NATIVE_SHARE
        if (shareScreenshot)
        {
            // Wait until the frame is fully rendered, then capture it.
            yield return new WaitForEndOfFrame();

            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "animal_share.png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);

            new NativeShare()
                .AddFile(path)
                .SetSubject(subject)
                .SetText(message)
                .Share();

            yield return new WaitForSeconds(0.5f);
            isSharing = false;
            yield break;
        }
#endif

        ShareText(subject, message);

        // Small delay so a double tap does not open two share sheets.
        yield return new WaitForSeconds(0.5f);
        isSharing = false;
    }

    // Text-only share using the native share sheet. No plugin needed on Android.
    private void ShareText(string subject, string message)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject);
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), message);

                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share via"))
                {
                    activity.Call("startActivity", chooser);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ARUIManager] Share failed: " + e.Message);
            GUIUtility.systemCopyBuffer = message; // fallback so the user can still paste it
        }
#else
        // Editor / iOS without the plugin / other platforms: copy to clipboard so the action is testable.
        GUIUtility.systemCopyBuffer = message;
        Debug.Log($"[ARUIManager] Share sheet only opens on an Android device build. Message copied to clipboard:\n{message}");
#endif
    }

    private void OnView2D3DToggleChanged(bool is3D)
    {
        SetPreviewDisplayMode(is3D);
    }

    // =========================================================
    // BUTTON PRESS ANIMATION
    // =========================================================

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