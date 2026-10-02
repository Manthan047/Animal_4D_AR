using UnityEngine;
using UnityEngine.UI;
using LightSide; // UniText namespace

public class AnimalCard : MonoBehaviour
{
    [Header("UI Elements")]
    public Image cardImage;
    public UniText cardNameText;
    public Button cardButton;
    public Image selectionBorder;

    [HideInInspector]
    public AnimalData animalData;

    private System.Action<AnimalData> onSelectCallback;


    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnEnable()
    {
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
            LanguageManager.Instance.OnLanguageChanged += HandleLanguageChanged;
        }

        UpdateCardText();
    }

    private void OnDisable()
    {
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
        }
    }

    private void HandleLanguageChanged(string newLang)
    {
        UpdateCardText();
    }

    public void UpdateCardText()
    {
        if (cardNameText == null)
            AutoFindReferences();

        if (cardNameText != null && animalData != null)
        {
            cardNameText.Text = animalData.GetLocalizedName();
        }
    }


    // =========================================================
    // FIND REFERENCES
    // =========================================================

    private void AutoFindReferences()
    {
        // SKETCH IMAGE
        if (cardImage == null)
        {
            Transform imageTransform = transform.Find("SketchImage");

            if (imageTransform != null)
                cardImage = imageTransform.GetComponent<Image>();
        }

        // CARD NAME
        if (cardNameText == null)
        {
            Transform nameTransform = transform.Find("CardName");

            if (nameTransform != null)
                cardNameText = nameTransform.GetComponent<UniText>();

            if (cardNameText == null)
                cardNameText = GetComponentInChildren<UniText>(true);
        }

        // BUTTON
        if (cardButton == null)
        {
            cardButton = GetComponent<Button>();

            if (cardButton == null)
                cardButton = GetComponentInChildren<Button>();
        }

        // SELECTION BORDER
        if (selectionBorder == null)
        {
            Transform borderTransform = transform.Find("SelectionBorder");

            if (borderTransform != null)
                selectionBorder = borderTransform.GetComponent<Image>();
        }

        // WARNINGS
        if (cardImage == null)
            Debug.LogWarning("[AnimalCard] SketchImage Image not found on " + gameObject.name);

        if (cardNameText == null)
            Debug.LogWarning("[AnimalCard] CardName UniText not found on " + gameObject.name);

        if (cardButton == null)
            Debug.LogWarning("[AnimalCard] Button not found on " + gameObject.name);
    }


    // =========================================================
    // SETUP CARD
    // =========================================================

    public void SetupCard(
        AnimalData data,
        System.Action<AnimalData> onClickCallback)
    {
        animalData = data;
        onSelectCallback = onClickCallback;

        AutoFindReferences();

        if (data == null)
            return;


        // NAME
        UpdateCardText();


        // IMAGE
        if (cardImage != null)
        {
            Sprite targetSprite = data.thumbnail;

            // FALLBACK SKETCH
            if (targetSprite == null)
            {
                // Use the English name as the stable lookup key,
                // regardless of which language is currently displayed.
                string name = data.animalName.english.ToLower();

                if (name.Contains("rex"))
                    targetSprite = Resources.Load<Sprite>("Sketches/rex_sketch");
                else if (name.Contains("ankylo"))
                    targetSprite = Resources.Load<Sprite>("Sketches/ankylosaurus_sketch");
                else if (name.Contains("stego"))
                    targetSprite = Resources.Load<Sprite>("Sketches/stegosaurus_sketch");
            }

            // APPLY IMAGE
            if (targetSprite != null)
            {
                cardImage.sprite = targetSprite;
                cardImage.preserveAspect = true;
                cardImage.color = Color.white;
                cardImage.enabled = true;
            }
            else
            {
                Debug.LogWarning("[AnimalCard] No thumbnail found for " + data.animalName.english);
                cardImage.enabled = false;
            }
        }


        // BUTTON CLICK
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(HandleCardClicked);
        }

        // Start unselected
        SetSelected(false);
    }


    // =========================================================
    // CARD CLICK
    // =========================================================

    private void HandleCardClicked()
    {
        if (animalData == null)
        {
            Debug.LogWarning("[AnimalCard] No AnimalData assigned.");
            return;
        }

        Debug.Log("[AnimalCard] Clicked: " + animalData.animalName.english);

        if (onSelectCallback != null)
            onSelectCallback.Invoke(animalData);
    }


    // =========================================================
    // SELECTION
    // =========================================================

    public void SetSelected(bool isSelected)
    {
        if (selectionBorder != null)
            selectionBorder.gameObject.SetActive(isSelected);

        transform.localScale = isSelected
            ? new Vector3(1.05f, 1.05f, 1f)
            : Vector3.one;
    }
}