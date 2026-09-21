using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    public TMP_Text animalNameText;
    public TMP_Text classificationText;
    public TMP_Text descriptionText;
    public TMP_Text funFactText;

    [Header("Detail Buttons")]
    public Button backButton;
    public Button wikipediaButton;
    public Button shareButton;

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


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        SetupAudioSource();
        LoadAllAvailableAnimalData();
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (modelPreviewController == null)
        {
            modelPreviewController = FindObjectOfType<ModelPreviewController>(true);
        }

        if (arUIManager == null)
        {
            arUIManager = FindObjectOfType<ARUIManager>(true);
        }

        SetupButtons();
        LoadAllAvailableAnimalData();
        PopulateCatalog();

        HideDetailView();

        // Catalog panel starts hidden — the AR scan flow is the default entry point.
        if (catalogPanel != null)
            catalogPanel.SetActive(false);
    }


    private void SetupButtons()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(ShowCardsView);
        }

        if (wikipediaButton != null)
        {
            wikipediaButton.onClick.RemoveAllListeners();
            wikipediaButton.onClick.AddListener(OpenWikipedia);
        }

        if (shareButton != null)
        {
            shareButton.onClick.RemoveAllListeners();
            shareButton.onClick.AddListener(ShareAnimal);
        }
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
                if (data != null && !animals.Exists(x => x != null && (x == data || x.animalName == data.animalName)))
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
                if (data != null && !animals.Exists(x => x != null && (x == data || x.animalName == data.animalName)))
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
            cardObject.name = "Card_" + data.animalName;

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

    public void OpenCatalogPanel()
    {
        if (catalogPanel != null)
            catalogPanel.SetActive(true);

        // Close AR scan panel so nothing overlaps.
        if (arUIManager != null)
            arUIManager.HideARScanPanel();

        OpenCatalog();
    }

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

        if (arUIManager != null)
        {
            arUIManager.HidePreviews();
        }
        else if (modelPreviewController != null)
        {
            modelPreviewController.ClearModel();
            modelPreviewController.gameObject.SetActive(false);
        }

        foreach (AnimalCard card in spawnedCards)
        {
            if (card != null)
            {
                card.SetSelected(false);
            }
        }
    }


    // =========================================================
    // SELECT ANIMAL CARD
    // =========================================================

    public void SelectAnimal(AnimalData data)
    {
        if (data == null)
            return;

        currentSelectedData = data;

        if (arUIManager != null)
        {
            arUIManager.SetCurrentAnimal(data, false);
        }
        else if (modelPreviewController != null)
        {
            modelPreviewController.gameObject.SetActive(true);
            modelPreviewController.ShowModel(data);
        }

        Debug.Log("[AnimalCatalogManager] Selected: " + data.animalName);

        foreach (AnimalCard card in spawnedCards)
        {
            if (card != null)
            {
                bool selected = card.animalData == data ||
                    (card.animalData != null && card.animalData.animalName == data.animalName);

                card.SetSelected(selected);
            }
        }

        if (animalNameText != null)
        {
            animalNameText.text = data.animalName;
        }

        if (classificationText != null)
        {
            string classText = !string.IsNullOrEmpty(data.classification) ? data.classification : "";

            if (!string.IsNullOrEmpty(data.era))
            {
                classText = !string.IsNullOrEmpty(classText) ? classText + " • " + data.era : data.era;
            }

            classificationText.text = classText;
        }

        if (descriptionText != null)
        {
            descriptionText.text = data.description;
        }

        if (funFactText != null)
        {
            if (!string.IsNullOrEmpty(data.funFact))
            {
                funFactText.text = "Fun Fact: " + data.funFact;
                funFactText.gameObject.SetActive(true);
            }
            else
            {
                funFactText.text = "";
                funFactText.gameObject.SetActive(false);
            }
        }

        if (cardsView != null)
        {
            cardsView.SetActive(false);
        }

        if (detailView != null)
        {
            detailView.SetActive(true);
        }

        if (wikipediaButton != null)
        {
            wikipediaButton.gameObject.SetActive(true);
            wikipediaButton.interactable = true;
        }

        if (shareButton != null)
        {
            shareButton.gameObject.SetActive(true);
            shareButton.interactable = true;
        }

        if (Application.isPlaying && playRoarOnSelect && data.roarClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(data.roarClip);
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

        if (arUIManager != null)
        {
            arUIManager.HidePreviews();
        }
        else if (modelPreviewController != null)
        {
            modelPreviewController.ClearModel();
            modelPreviewController.gameObject.SetActive(false);
        }

        foreach (AnimalCard card in spawnedCards)
        {
            if (card != null)
            {
                card.SetSelected(false);
            }
        }
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

        string animalName = currentSelectedData.animalName;
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

        string shareText = "Check out " + currentSelectedData.animalName + " in Animal 4D+ AR!";

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
            AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent");

            intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
            intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), shareText);
            intent.Call<AndroidJavaObject>("setType", "text/plain");

            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share Animal");
            activity.Call("startActivity", chooser);
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