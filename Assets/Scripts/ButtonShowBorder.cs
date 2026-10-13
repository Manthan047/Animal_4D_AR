using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any GameObject (e.g. an empty "Manager" object).
/// Assign the Button and the Image (sprite) in the Inspector.
/// Clicking the button shows a border around the sprite.
/// </summary>
public class ButtonShowBorder : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Button button;          // The button that will be clicked
    public Image sprite;           // The UI Image (sprite) that gets the border

    [Header("Border Settings")]
    public Color borderColor = Color.yellow;
    public float borderThickness = 5f;
    public bool toggleOnClick = true;   // true = click again to hide, false = stays visible

    private Outline outline;

    private void Awake()
    {
        if (button == null || sprite == null)
        {
            Debug.LogError("ButtonShowBorder: Please assign both the Button and the Sprite (Image) in the Inspector.", this);
            enabled = false;
            return;
        }

        // Reuse an existing Outline or add one to the sprite
        outline = sprite.GetComponent<Outline>();
        if (outline == null)
            outline = sprite.gameObject.AddComponent<Outline>();

        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(borderThickness, borderThickness);
        outline.enabled = false;   // border hidden at start
    }

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
      
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        outline.enabled = toggleOnClick ? !outline.enabled : true;
    }
}