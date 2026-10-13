using UnityEngine;
using UnityEngine.UI;

public class GetCardButton : MonoBehaviour
{
    [SerializeField] private string websiteURL = "https://auradyno.netlify.app/";
    [SerializeField] private Button getCardButton;

    private void Awake()
    {
        if (getCardButton == null)
            getCardButton = GetComponent<Button>();
    }

    private void OnEnable()
    {
        getCardButton.onClick.AddListener(OpenWebsite);
    }

    private void OnDisable()
    {
        getCardButton.onClick.RemoveListener(OpenWebsite);
    }

    public void OpenWebsite()
    {
        Application.OpenURL(websiteURL);
    }
}