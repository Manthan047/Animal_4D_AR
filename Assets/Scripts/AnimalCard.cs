using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnimalCard : MonoBehaviour
{
    [Header("UI Elements")]
    public Image cardImage;
    public TMP_Text cardNameText;
    public Button cardButton;
    public Image selectionBorder;

    [HideInInspector]
    public AnimalData animalData;

    private System.Action<AnimalData> onSelectCallback;


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        AutoFindReferences();
    }


    // =========================================================
    // FIND REFERENCES
    // =========================================================

    private void AutoFindReferences()
    {
        // -----------------------------------------------------
        // SKETCH IMAGE
        // -----------------------------------------------------

        if (cardImage == null)
        {
            Transform imageTransform =
                transform.Find("SketchImage");

            if (imageTransform != null)
            {
                cardImage =
                    imageTransform.GetComponent<Image>();
            }
        }


        // -----------------------------------------------------
        // CARD NAME
        // -----------------------------------------------------

        if (cardNameText == null)
        {
            Transform nameTransform =
                transform.Find("CardName");

            if (nameTransform != null)
            {
                cardNameText =
                    nameTransform.GetComponent<TMP_Text>();
            }
        }


        // -----------------------------------------------------
        // BUTTON
        // -----------------------------------------------------

        if (cardButton == null)
        {
            cardButton =
                GetComponent<Button>();

            if (cardButton == null)
            {
                cardButton =
                    GetComponentInChildren<Button>();
            }
        }


        // -----------------------------------------------------
        // SELECTION BORDER
        // -----------------------------------------------------

        if (selectionBorder == null)
        {
            Transform borderTransform =
                transform.Find("SelectionBorder");

            if (borderTransform != null)
            {
                selectionBorder =
                    borderTransform.GetComponent<Image>();
            }
        }


        // -----------------------------------------------------
        // WARNINGS
        // -----------------------------------------------------

        if (cardImage == null)
        {
            Debug.LogWarning(
                "[AnimalCard] SketchImage Image not found on " +
                gameObject.name
            );
        }

        if (cardNameText == null)
        {
            Debug.LogWarning(
                "[AnimalCard] CardName TMP_Text not found on " +
                gameObject.name
            );
        }

        if (cardButton == null)
        {
            Debug.LogWarning(
                "[AnimalCard] Button not found on " +
                gameObject.name
            );
        }
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
        {
            return;
        }


        // -----------------------------------------------------
        // NAME
        // -----------------------------------------------------

        if (cardNameText != null)
        {
            cardNameText.text =
                data.animalName;
        }


        // -----------------------------------------------------
        // IMAGE
        // -----------------------------------------------------

        if (cardImage != null)
        {
            Sprite targetSprite =
                data.thumbnail;


            // -------------------------------------------------
            // FALLBACK SKETCH
            // -------------------------------------------------

            if (targetSprite == null)
            {
                string name =
                    data.animalName.ToLower();


                if (name.Contains("rex"))
                {
                    targetSprite =
                        Resources.Load<Sprite>(
                            "Sketches/rex_sketch"
                        );
                }
                else if (name.Contains("ankylo"))
                {
                    targetSprite =
                        Resources.Load<Sprite>(
                            "Sketches/ankylosaurus_sketch"
                        );
                }
                else if (name.Contains("stego"))
                {
                    targetSprite =
                        Resources.Load<Sprite>(
                            "Sketches/stegosaurus_sketch"
                        );
                }
            }


            // -------------------------------------------------
            // APPLY IMAGE
            // -------------------------------------------------

            if (targetSprite != null)
            {
                cardImage.sprite =
                    targetSprite;

                cardImage.preserveAspect =
                    true;

                cardImage.color =
                    Color.white;

                cardImage.enabled =
                    true;
            }
            else
            {
                Debug.LogWarning(
                    "[AnimalCard] No thumbnail found for " +
                    data.animalName
                );

                cardImage.enabled =
                    false;
            }
        }


        // -----------------------------------------------------
        // BUTTON CLICK
        // -----------------------------------------------------

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();

            cardButton.onClick.AddListener(
                HandleCardClicked
            );
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
            Debug.LogWarning(
                "[AnimalCard] No AnimalData assigned."
            );

            return;
        }


        Debug.Log(
            "[AnimalCard] Clicked: " +
            animalData.animalName
        );


        if (onSelectCallback != null)
        {
            onSelectCallback.Invoke(
                animalData
            );
        }
    }


    // =========================================================
    // SELECTION
    // =========================================================

    public void SetSelected(bool isSelected)
    {
        if (selectionBorder != null)
        {
            selectionBorder.gameObject.SetActive(
                isSelected
            );
        }


        transform.localScale =
            isSelected
                ? new Vector3(1.05f, 1.05f, 1f)
                : Vector3.one;
    }
}