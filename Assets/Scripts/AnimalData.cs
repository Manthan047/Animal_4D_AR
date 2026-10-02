using UnityEngine;

/// <summary>
/// A single piece of text stored in English, Spanish and Hindi.
/// Falls back to English if the current language's field is left blank.
/// </summary>
[System.Serializable]
public class LocalizedField
{
    [TextArea(1, 12)] public string english;
    [TextArea(1, 12)] public string spanish;
    [TextArea(1, 12)] public string hindi;

    /// <summary>
    /// Returns the text for the given language (use LanguageManager.LANG_* constants),
    /// falling back to English if that language's field is empty.
    /// </summary>
    public string Get(string language)
    {
        switch (language)
        {
            case LanguageManager.LANG_SPANISH:
                return string.IsNullOrEmpty(spanish) ? english : spanish;

            case LanguageManager.LANG_HINDI:
                return string.IsNullOrEmpty(hindi) ? english : hindi;

            default:
                return english;
        }
    }

    // Lets you keep using `myField` like a plain string almost everywhere,
    // e.g. string s = animalData.animalName; (returns English).
    public static implicit operator string(LocalizedField field) => field?.english ?? string.Empty;

    // Makes Debug.Log(myField), string + myField, etc. behave sensibly.
    public override string ToString() => english ?? string.Empty;
}

[CreateAssetMenu(fileName = "NewAnimalData", menuName = "AR Animals/Animal Data", order = 1)]
public class AnimalData : ScriptableObject
{
    [Header("General Info")]
    public LocalizedField animalName;
    public string scientificName; // scientific names are usually the same across languages
    public LocalizedField classification;
    public LocalizedField era;

    [Header("Physical Attributes")]
    public string averageLength;
    public string averageWeight;

    [Header("Educational Information")]
    public LocalizedField description;
    public LocalizedField funFact;

    [Header("Visuals")]
    [Tooltip("The small icon sprite shown in the bottom animal-picker row.")]
    public Sprite thumbnail;

    [Tooltip("The full 2D illustration shown when the user toggles to 2D view.")]
    public Sprite image2D;

    [Header("Audio")]
    public AudioClip roarClip;
    public AudioClip ambientClip;

    [Tooltip("Narration/voiceover audio that reads out the description text aloud.")]
    public AudioClip descriptionAudioClip;

    [Header("AR Configuration")]
    public Vector3 targetScale = Vector3.one;
    public Vector3 targetRotationOffset = Vector3.zero;

    [Header("Preview Stage")]
    [Tooltip("Standalone model prefab shown in the rotatable mini 3D viewer, independent of AR marker tracking.")]
    public GameObject previewModelPrefab;

    // ---------------------------------------------------------
    // Convenience accessors for the language currently active in LanguageManager
    // ---------------------------------------------------------

    public string GetLocalizedName()
    {
        string lang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : LanguageManager.LANG_ENGLISH;
        string result = animalName != null ? animalName.Get(lang) : "";

        if (LanguageManager.Instance != null && (string.IsNullOrEmpty(result) || (lang != LanguageManager.LANG_ENGLISH && result == (animalName?.english ?? ""))))
        {
            string key = animalName != null && !string.IsNullOrEmpty(animalName.english) ? animalName.english : name;
            string dictName = LanguageManager.Instance.GetLocalizedAnimalName(key);
            if (!string.IsNullOrEmpty(dictName) && dictName != key)
            {
                return dictName;
            }
        }

        return !string.IsNullOrEmpty(result) ? result : name;
    }


    public string GetLocalizedClassification()
    {
        string lang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : LanguageManager.LANG_ENGLISH;
        return classification != null ? classification.Get(lang) : "";
    }

    public string GetLocalizedEra()
    {
        string lang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : LanguageManager.LANG_ENGLISH;
        return era != null ? era.Get(lang) : "";
    }

    public string GetLocalizedDescription()
    {
        string lang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : LanguageManager.LANG_ENGLISH;
        return description != null ? description.Get(lang) : "";
    }

    public string GetLocalizedFunFact()
    {
        string lang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : LanguageManager.LANG_ENGLISH;
        return funFact != null ? funFact.Get(lang) : "";
    }

    /// <summary>
    /// Returns a full, formatted biography suitable for educational display in the Info Panel.
    /// </summary>
    public string GetFormattedBio()
    {
        if (LanguageManager.Instance != null)
        {
            return LanguageManager.Instance.GetFormattedAnimalBio(GetLocalizedName(), this);
        }

        string desc = GetLocalizedDescription();
        string fact = GetLocalizedFunFact();
        if (!string.IsNullOrEmpty(fact))
        {
            return desc + "\n\n💡 Fun Fact: " + fact;
        }
        return desc;
    }
}