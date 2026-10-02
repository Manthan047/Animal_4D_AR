using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any Button in Unity to make it automatically switch
/// to the configured language when clicked.
/// </summary>
[RequireComponent(typeof(Button))]
public class LanguageSelectButton : MonoBehaviour
{
    [Tooltip("The language name to set when clicked (e.g. 'English', 'Spanish', 'Hindi', 'French', 'Gujarati', 'German').")]
    [SerializeField] private string targetLanguage = "English";

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.SetLanguage(targetLanguage);
        }
    }
}
