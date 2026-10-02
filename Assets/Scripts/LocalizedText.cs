using System.Collections.Generic;
using UnityEngine;
using LightSide; // UniText namespace

/// <summary>
/// Localizes a UniText component using LanguageManager's string table.
/// </summary>
[RequireComponent(typeof(UniText))]
public class LocalizedText : MonoBehaviour
{
    // Enabled instances only, so LanguageManager doesn't need to scan the scene.
    private static readonly List<LocalizedText> active = new List<LocalizedText>();
    public static IReadOnlyList<LocalizedText> Active => active;

    [Tooltip("Key that maps into LanguageManager's StringTable, e.g. \"lbl_loading\".")]
    [SerializeField] private string localizationKey;

    [Tooltip("Optional wrapper, use {0} as insertion point, e.g. \"<b>{0}</b>\".")]
    [SerializeField] private string formatWrapper = "{0}";

    private UniText uniText;

    public string LocalizationKey
    {
        get => localizationKey;
        set
        {
            if (localizationKey == value) return;
            localizationKey = value;

            // If disabled, OnEnable will apply it later.
            if (isActiveAndEnabled) UpdateText();
        }
    }

    private void Awake()
    {
        uniText = GetComponent<UniText>();
    }

    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        UpdateText();
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    public void UpdateText()
    {
        if (uniText == null) uniText = GetComponent<UniText>();
        if (uniText == null || string.IsNullOrEmpty(localizationKey))
            return;

        string localized = LanguageManager.Instance != null
            ? LanguageManager.Instance.GetText(localizationKey)
            : localizationKey;

        string formatted = string.IsNullOrEmpty(formatWrapper) || formatWrapper == "{0}"
            ? localized
            : string.Format(formatWrapper, localized);

        // Skip the re-shape if nothing changed.
        if (uniText.Text != formatted)
            uniText.Text = formatted;
    }
}