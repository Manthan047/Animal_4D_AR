using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LightSide; // UniText's actual namespace — the component class is LightSide.UniText

[ExecuteAlways]
public class AnimalCatalogManager : MonoBehaviour
{
    [Header("Panel Root")]
    [Tooltip("The whole catalog panel (cards + detail). Hidden whenever AR scan is active.")]
    [SerializeField] private GameObject catalogPanel;

    [Header("Cross-Reference (for mutual exclusivity)")]
    [SerializeField] private ARUIManager arUIManager;

    [Header("UI ScrollView References")]
    public Transform contentContainer;
    public GameObject cardPrefab;

    [Header("Catalog / Detail Views")]
    public GameObject cardsView;
    public GameObject detailView;

    [Header("Animal Detail UI")]
    public UniText animalNameText;
    public UniText classificationText;
    public UniText descriptionText;
    public UniText funFactText;

    [Header("Description Scroll")]
    [Tooltip("ScrollRect that contains the description text. Auto-found from the description text if left empty.")]
    [SerializeField] private ScrollRect descriptionScrollRect;

    [Header("3D Mini Viewer")]
    public ModelPreviewController modelPreviewController;

    [Header("Data")]
    public List<AnimalData> animals = new List<AnimalData>();

    [Header("Options")]
    [Tooltip("Keep this OFF. User should select an animal manually.")]
    public bool autoSelectFirstAnimal = false;
    public bool playRoarOnSelect = true;

    private readonly List<AnimalCard> spawnedCards = new List<AnimalCard>();
    private AudioSource audioSource;
    private AnimalData currentSelectedData;

    // ---- Performance caches: UniText re-shapes text every time .Text is set,
    // so only assign when the value actually changed. ----
    private string lastName;
    private string lastClassText;
    private string lastDescription;
    private string lastFunFact;
    private AnimalData lastHighlightedData;

    private bool detailRefsResolved = false;
    private Coroutine scrollTopRoutine;

    public AnimalData CurrentSelectedData => currentSelectedData;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        SetupAudioSource();
        LoadAllAvailableAnimalData();
    }

    private void OnEnable()
    {
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            LanguageManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }
    }

    private void OnDisable()
    {
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        }

        scrollTopRoutine = null;
    }

    private void HandleLanguageChanged(string newLang)
    {
        // Re-localize spawned cards in catalog
        foreach (var card in spawnedCards)
        {
            if (card != null && card.animalData != null && card.cardNameText != null)
            {
                card.cardNameText.Text = card.animalData.GetLocalizedName();
            }
        }

        // Immediately update detail/info UI text in the new language regardless of visibility
        UpdateDetailUI(currentSelectedData);
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            LanguageManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }

        if (modelPreviewController == null)
        {
            modelPreviewController = FindAnyObjectByType<ModelPreviewController>(FindObjectsInactive.Include);
        }

        if (arUIManager == null)
        {
            arUIManager = FindAnyObjectByType<ARUIManager>(FindObjectsInactive.Include);
        }

        // Animal data is already loaded in Awake(); no need to scan Resources a second time.
        PopulateCatalog();

        if (currentSelectedData == null && animals != null && animals.Count > 0)
        {
            currentSelectedData = animals[0];
        }
        UpdateDetailUI(currentSelectedData);

        HideDetailView();

        // Catalog panel starts hidden — the AR scan flow is the default entry point.
        if (catalogPanel != null)
            catalogPanel.SetActive(false);
    }


    private void SetupAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null && Application.isPlaying)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
    }


    // =========================================================
    // LOAD ANIMAL DATA
    // =========================================================

    public void LoadAllAvailableAnimalData()
    {
        if (animals == null)
        {
            animals = new List<AnimalData>();
        }

        AnimalData[] loadedFromData = Resources.LoadAll<AnimalData>("Data");

        if (loadedFromData != null)
        {
            foreach (AnimalData data in loadedFromData)
            {
                if (data != null && !animals.Exists(x => x != null && (x == data || x.animalName.english == data.animalName.english)))
                {
                    animals.Add(data);
                }
            }
        }

        AnimalData[] loadedFromRoot = Resources.LoadAll<AnimalData>("");

        if (loadedFromRoot != null)
        {
            foreach (AnimalData data in loadedFromRoot)
            {
                if (data != null && !animals.Exists(x => x != null && (x == data || x.animalName.english == data.animalName.english)))
                {
                    animals.Add(data);
                }
            }
        }
    }


    // =========================================================
    // POPULATE CATALOG
    // =========================================================

    public void PopulateCatalog()
    {
        if (contentContainer == null)
        {
            GameObject contentObject = GameObject.Find("Content");

            if (contentObject != null)
            {
                contentContainer = contentObject.transform;
            }
        }

        if (contentContainer == null)
        {
            Debug.LogWarning("[AnimalCatalogManager] Content not found.");
            return;
        }

        if (cardPrefab == null || cardPrefab.scene.IsValid() || cardPrefab.name == "ContentCard")
        {
            GameObject resourcePrefab = Resources.Load<GameObject>("AnimalCard");

            if (resourcePrefab != null)
            {
                cardPrefab = resourcePrefab;
            }
        }

        if (cardPrefab == null)
        {
            Debug.LogWarning("[AnimalCatalogManager] AnimalCard prefab not found.");
            return;
        }

        RectTransform contentRect = contentContainer as RectTransform;

        if (contentRect != null)
        {
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
        }

        HorizontalLayoutGroup layout = contentContainer.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
        {
            layout = contentContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        layout.padding = new RectOffset(16, 16, 10, 10);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentContainer.GetComponent<ContentSizeFitter>();

        if (fitter == null)
        {
            fitter = contentContainer.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        for (int i = contentContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = contentContainer.GetChild(i).gameObject;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        spawnedCards.Clear();
        lastHighlightedData = null;

        if (animals == null || animals.Count == 0)
        {
            Debug.LogWarning("[AnimalCatalogManager] No AnimalData found.");
            return;
        }

        foreach (AnimalData data in animals)
        {
            if (data == null)
                continue;

            GameObject cardObject = Instantiate(cardPrefab, contentContainer);
            cardObject.name = "Card_" + data.animalName.english;

            RectTransform cardRect = cardObject.GetComponent<RectTransform>();

            if (cardRect != null)
            {
                cardRect.sizeDelta = new Vector2(140f, 160f);
                cardRect.localScale = Vector3.one;
            }

            LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();

            if (layoutElement == null)
            {
                layoutElement = cardObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredWidth = 140f;
            layoutElement.preferredHeight = 160f;
            layoutElement.minWidth = 140f;
            layoutElement.minHeight = 160f;

            AnimalCard card = cardObject.GetComponent<AnimalCard>();

            if (card != null)
            {
                card.SetupCard(data, SelectAnimal);
                spawnedCards.Add(card);
            }
        }

        Canvas.ForceUpdateCanvases();

        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }
    }


    // =========================================================
    // OPEN / CLOSE CATALOG PANEL (mutual exclusivity with AR)
    // =========================================================

    public void CloseCatalogPanel()
    {
        if (catalogPanel != null)
            catalogPanel.SetActive(false);
    }


    // =========================================================
    // OPEN CATALOG (cards view, reset selection)
    // =========================================================

    public void OpenCatalog()
    {
        Debug.Log("[AnimalCatalogManager] Opening catalog.");

        currentSelectedData = null;

        if (cardsView != null)
        {
            cardsView.SetActive(true);
        }

        if (detailView != null)
        {
            detailView.SetActive(false);
        }

        HidePreviewsInternal();
        ClearCardSelection();
    }


    // =========================================================
    // SELECT ANIMAL CARD
    // =========================================================

    // Called by the cards (must keep this exact signature: void (AnimalData))
    public void SelectAnimal(AnimalData data)
    {
        ShowAnimal(data, playRoarOnSelect);
    }

    private void ShowAnimal(AnimalData data, bool playRoar)
    {
        if (data == null)
            return;

        currentSelectedData = data;

        // Updates the detail texts once. ARUIManager.SetCurrentAnimal calls UpdateDetailUI too,
        // but the cache below makes the repeated calls free.
        if (arUIManager != null)
        {
            arUIManager.SetCurrentAnimal(data, false);
        }
        else if (modelPreviewController != null)
        {
            modelPreviewController.gameObject.SetActive(true);
            modelPreviewController.ShowModel(data);
        }

        Debug.Log("[AnimalCatalogManager] Selected: " + data.animalName.english);

        UpdateDetailUI(data);

        if (detailView != null)
            detailView.SetActive(true);

        // The detail view was just made visible, so make sure the description starts at line 1.
        ScrollDescriptionToTop();

        if (Application.isPlaying && playRoar && data.roarClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(data.roarClip);
        }
    }


    // =========================================================
    // UPDATE DETAIL UI
    // =========================================================

    /// <summary>
    /// Updates all info/detail texts (name, classification, description, fun fact)
    /// immediately in the active language without changing model preview or playing sound.
    /// Text is only assigned when it actually changed, because every UniText assignment
    /// triggers a full re-shape and layout rebuild.
    /// </summary>
    public void UpdateDetailUI(AnimalData data = null)
    {
        if (data == null)
            data = currentSelectedData;

        if (data == null && animals != null && animals.Count > 0)
            data = animals[0];

        if (data == null)
            return;

        currentSelectedData = data;
        AutoResolveDetailReferences();

        string animalName = data.GetLocalizedName();
        string classification = data.GetLocalizedClassification();
        string era = data.GetLocalizedEra();
        string description = data.GetLocalizedDescription();
        string funFact = data.GetLocalizedFunFact();

        // Fallback to LanguageManager data table if description field in asset is empty
        if (string.IsNullOrEmpty(description) && LanguageManager.Instance != null)
        {
            if (LanguageManager.Instance.TryGetLocalizedAnimalInfo(data.animalName.english, out var fallbackInfo))
            {
                if (string.IsNullOrEmpty(animalName)) animalName = fallbackInfo.Name;
                if (string.IsNullOrEmpty(classification)) classification = fallbackInfo.Classification;
                if (string.IsNullOrEmpty(era)) era = fallbackInfo.Era;
                description = fallbackInfo.Description;
                if (string.IsNullOrEmpty(funFact)) funFact = fallbackInfo.FunFact;
            }
        }

        // ---- Name ----
        if (animalNameText != null && animalName != lastName)
        {
            animalNameText.Text = animalName;
            lastName = animalName;
        }

        // ---- Classification ----
        if (classificationText != null)
        {
            string classText = !string.IsNullOrEmpty(classification) ? classification : "";

            if (!string.IsNullOrEmpty(era))
            {
                classText = !string.IsNullOrEmpty(classText) ? classText + " • " + era : era;
            }

            if (classText != lastClassText)
            {
                classificationText.Text = classText;
                lastClassText = classText;
            }
        }

        // ---- Description (always restart from the first line when it changes) ----
        if (descriptionText != null && description != lastDescription)
        {
            descriptionText.Text = description;
            lastDescription = description;
            ScrollDescriptionToTop();
        }

        // ---- Fun fact ----
        if (funFactText != null)
        {
            string currentLang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : LanguageManager.LANG_ENGLISH;
            bool hasFunFact = !string.IsNullOrEmpty(data.funFact != null ? data.funFact.Get(currentLang) : "") || !string.IsNullOrEmpty(funFact);

            string funFactToShow = "";
            if (hasFunFact)
            {
                funFactToShow = !string.IsNullOrEmpty(funFact) ? funFact : data.GetLocalizedFunFact();
            }

            if (funFactToShow != lastFunFact)
            {
                funFactText.Text = funFactToShow;
                lastFunFact = funFactToShow;
            }

            if (funFactText.gameObject.activeSelf != hasFunFact)
            {
                funFactText.gameObject.SetActive(hasFunFact);
            }
        }

        // ---- Card highlight (only when the selection actually changed) ----
        if (lastHighlightedData != data)
        {
            lastHighlightedData = data;

            foreach (AnimalCard card in spawnedCards)
            {
                if (card != null)
                {
                    bool selected = card.animalData == data ||
                        (card.animalData != null && card.animalData.animalName.english == data.animalName.english);

                    card.SetSelected(selected);
                }
            }
        }
    }

    // Only scans the scene ONCE. The old version scanned every Transform in the scene
    // on every call whenever one of the references was missing, which caused lag.
    private void AutoResolveDetailReferences()
    {
        if (detailRefsResolved)
            return;

        if (animalNameText != null && descriptionText != null)
        {
            detailRefsResolved = true;
            return;
        }

        detailRefsResolved = true;

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform t in allTransforms)
        {
            string tName = t.name.Trim();

            if (animalNameText == null && (tName == "Info Title Text" || tName == "Animal Name Text" || tName == "Title Text"))
            {
                animalNameText = t.GetComponent<UniText>();
            }

            if (descriptionText == null && (tName == "Discription_text" || tName == "Description_text" || tName == "DescriptionText" || tName == "DiscriptionText"))
            {
                descriptionText = t.GetComponent<UniText>();
            }
        }

        if (descriptionText == null)
            Debug.LogWarning("[AnimalCatalogManager] Description text not found. Assign it in the Inspector.", this);
    }


    // =========================================================
    // DESCRIPTION SCROLL RESET
    // =========================================================

    /// <summary>
    /// Scrolls the description back to the first line.
    /// Called automatically when the description changes. You can also call it
    /// when the info card is opened (see ARUIManager.RefreshInfoLayout).
    /// </summary>
    public void ScrollDescriptionToTop()
    {
        if (!Application.isPlaying) return;

        if (descriptionScrollRect == null && descriptionText != null)
        {
            descriptionScrollRect = descriptionText.GetComponentInParent<ScrollRect>(true);
        }

        if (descriptionScrollRect == null) return;

        ApplyScrollTop();

        // The layout is rebuilt a frame later, so reset once more after it settles.
        if (isActiveAndEnabled)
        {
            if (scrollTopRoutine != null) StopCoroutine(scrollTopRoutine);
            scrollTopRoutine = StartCoroutine(ScrollTopNextFrame());
        }
    }

    private IEnumerator ScrollTopNextFrame()
    {
        yield return null;

        if (descriptionScrollRect == null) yield break;

        Canvas.ForceUpdateCanvases();
        ApplyScrollTop();
        scrollTopRoutine = null;
    }

    private void ApplyScrollTop()
    {
        if (descriptionScrollRect == null) return;

        descriptionScrollRect.StopMovement(); // kills leftover inertia from the previous scroll
        descriptionScrollRect.verticalNormalizedPosition = 1f;

        RectTransform content = descriptionScrollRect.content;
        if (content != null)
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }
    }


    // =========================================================
    // BACK TO CARDS
    // =========================================================

    public void ShowCardsView()
    {
        Debug.Log("[AnimalCatalogManager] Back to cards.");

        currentSelectedData = null;

        if (detailView != null)
        {
            detailView.SetActive(false);
        }

        if (cardsView != null)
        {
            cardsView.SetActive(true);
        }

        HidePreviewsInternal();
        ClearCardSelection();
    }


    // =========================================================
    // HIDE DETAIL
    // =========================================================

    private void HideDetailView()
    {
        if (detailView != null)
        {
            detailView.SetActive(false);
        }

        HidePreviewsInternal();
    }


    // =========================================================
    // SHARED HELPERS
    // =========================================================

    private void HidePreviewsInternal()
    {
        if (arUIManager != null)
        {
            arUIManager.HidePreviews();
        }
        else if (modelPreviewController != null)
        {
            modelPreviewController.ClearModel();
            modelPreviewController.gameObject.SetActive(false);
        }
    }

    private void ClearCardSelection()
    {
        lastHighlightedData = null;

        foreach (AnimalCard card in spawnedCards)
        {
            if (card != null)
            {
                card.SetSelected(false);
            }
        }
    }


    // =========================================================
    // WIKIPEDIA
    // =========================================================

    public void OpenWikipedia()
    {
        if (currentSelectedData == null)
        {
            Debug.LogWarning("[AnimalCatalogManager] No animal selected.");
            return;
        }

        string animalName = currentSelectedData.animalName.english;
        string encodedName = UnityEngine.Networking.UnityWebRequest.EscapeURL(animalName);
        string url = "https://en.wikipedia.org/wiki/" + encodedName;

        Debug.Log("[AnimalCatalogManager] Wikipedia: " + url);
        Application.OpenURL(url);
    }


    // =========================================================
    // SHARE
    // =========================================================

    public void ShareAnimal()
    {
        if (currentSelectedData == null)
        {
            Debug.LogWarning("[AnimalCatalogManager] No animal selected.");
            return;
        }

        string shareText = "Check out " + currentSelectedData.GetLocalizedName() + " in Animal 4D+ AR!";

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), shareText);
                intent.Call<AndroidJavaObject>("setType", "text/plain");

                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share Animal"))
                {
                    activity.Call("startActivity", chooser);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[AnimalCatalogManager] Share failed: " + e.Message);
        }
#else
        GUIUtility.systemCopyBuffer = shareText;
        Debug.Log("[AnimalCatalogManager] Share text copied: " + shareText);
#endif
    }
}