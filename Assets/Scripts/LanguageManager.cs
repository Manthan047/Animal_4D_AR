using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LightSide; // UniText namespace

public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance { get; private set; }

    public const string LANG_ENGLISH = "English";
    public const string LANG_SPANISH = "Spanish";
    public const string LANG_HINDI = "Hindi";

    private const string PREF_KEY_LANGUAGE = "App_Selected_Language";

    [Header("Current Language")]
    [SerializeField] private string currentLanguage = LANG_ENGLISH;

    public string CurrentLanguage => currentLanguage;

    // Language the user has tapped but NOT yet confirmed with Submit
    private string pendingLanguage = LANG_ENGLISH;

    public event Action<string> OnLanguageChanged;

    [Header("UI Reference (Auto-bound if empty)")]
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button englishButton;
    [SerializeField] private Button spanishButton;
    [SerializeField] private Button hindiButton;
    [SerializeField] private Button submitButton;

    [Header("Flag Display")]
    [SerializeField] private Image flagImage;      // the Image in the logo box
    [SerializeField] private Sprite englishFlag;   // e.g. UK / US flag
    [SerializeField] private Sprite spanishFlag;   // Spain flag
    [SerializeField] private Sprite hindiFlag;     // India flag

    // Localized strings dictionary: [Key] -> { [Language] -> Value }
    private static readonly Dictionary<string, Dictionary<string, string>> StringTable = new Dictionary<string, Dictionary<string, string>>
    {
        // ---------------- Caution Panel ----------------
        ["lbl_parental_supervision"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Parental Supervision Required: Children must be supervised by an adult at all times while using this app to prevent stumbles, falls, or collisions with objects.",
            [LANG_SPANISH] = "Supervisión de los Padres Requerida:\nAsegúrese de que un adulto supervise a los niños mientras usan esta aplicación de Realidad Aumentada.",
            [LANG_HINDI] = "अभिभावक पर्यवेक्षण आवश्यक:\nकृपया सुनिश्चित करें कि इस ऑगमेंटेड रियलिटी ऐप का उपयोग करते समय बच्चे किसी वयस्क की देखरेख में हों।"
        },
        ["btn_ok"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "OK",
            [LANG_SPANISH] = "Aceptar",
            [LANG_HINDI] = "ठीक है"
        },

        // ---------------- Loading Panel ----------------
        ["lbl_loading"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Loading...",
            [LANG_SPANISH] = "Cargando...",
            [LANG_HINDI] = "लोड हो रहा है..."
        },

        // ---------------- Instructions Panel ----------------
        ["title_instructions"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Instructions",
            [LANG_SPANISH] = "Cómo Jugar",
            [LANG_HINDI] = "कैसे खेलें"
        },
        ["instruction_step1"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "1. Place the card facing the camera,and it will automatically scan the image.",
            [LANG_SPANISH] = "1. Apunta la cámara directamente a una tarjeta Animal 4D+ para darle vida al animal.",
            [LANG_HINDI] = "1. जानवर को 4D AR में देखने के लिए अपने कैमरे को एनिमल 4D+ कार्ड की ओर रखें।"
        },
        ["instruction_step2"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "2. If you dont have  Animal  4D+ card set.you can have it here.",
            [LANG_SPANISH] = "2. ¿No tienes tarjetas físicas? Toca 'Obtener Tarjetas' para descargarlas e imprimirlas.",
            [LANG_HINDI] = "2. क्या आपके पास कार्ड नहीं हैं? कार्ड डाउनलोड और प्रिंट करने के लिए 'कार्ड प्राप्त करें' दबाएं।"
        },
        ["btn_get_card"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Get Cards",
            [LANG_SPANISH] = "Obtener Tarjetas",
            [LANG_HINDI] = "कार्ड प्राप्त करें"
        },

        // ---------------- Setting Panel ----------------
        ["lbl_language"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Select Language",
            [LANG_SPANISH] = "Seleccionar Idioma",
            [LANG_HINDI] = "भाषा चुनें"
        },
        ["btn_lang_english"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "English",
            [LANG_SPANISH] = "English",
            [LANG_HINDI] = "English (अंग्रेज़ी)"
        },
        ["btn_lang_spanish"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Español",
            [LANG_SPANISH] = "Español",
            [LANG_HINDI] = "Español (स्पैनिश)"
        },
        ["btn_lang_hindi"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "हिन्दी",
            [LANG_SPANISH] = "Hindi",
            [LANG_HINDI] = "हिन्दी (Hindi)"
        },
        ["btn_back"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Back",
            [LANG_SPANISH] = "Atrás",
            [LANG_HINDI] = "वापस"
        },
        ["btn_submit"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Submit",
            [LANG_SPANISH] = "Enviar",
            [LANG_HINDI] = "सबमिट करें"
        },
         ["Guide_Text"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "You can always change the langauge by accesseing Tools > Set Langauge",
            [LANG_SPANISH] = "Siempre puedes cambiar el idioma accediendo a Herramientas > Establecer idioma",
            [LANG_HINDI] = "आप टूल्स > सेट लैंग्वेज पर जाकर भाषा बदल सकते हैं।"
        },
        ["change_langauge"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Change Langauge",
            [LANG_SPANISH] = "Cambiar idioma",
            [LANG_HINDI] = "भाषा बदलें"
        },

        // ---------------- AR HUD & Action Buttons ----------------
        ["btn_action"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Action",
            [LANG_SPANISH] = "Acción",
            [LANG_HINDI] = "एक्शन"
        },
        ["btn_move"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Walk",
            [LANG_SPANISH] = "Caminar",
            [LANG_HINDI] = "चलें"
        },
        ["btn_idle"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Idle",
            [LANG_SPANISH] = "Reposo",
            [LANG_HINDI] = "शांत"
        },
        ["btn_audio"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Roar",
            [LANG_SPANISH] = "Rugido",
            [LANG_HINDI] = "आवाज़"
        },
        ["btn_fast_audio"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Fast Audio",
            [LANG_SPANISH] = "Audio Rápido",
            [LANG_HINDI] = "फास्ट ऑडियो"
        },
        ["btn_share"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Share",
            [LANG_SPANISH] = "Compartir",
            [LANG_HINDI] = "साझा करें"
        },
        ["btn_wikipedia"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Wikipedia",
            [LANG_SPANISH] = "Wikipedia",
            [LANG_HINDI] = "विकिपीडिया"
        },
        ["mode_3d"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "3D Mode",
            [LANG_SPANISH] = "Modo 3D",
            [LANG_HINDI] = "3D मोड"
        },
        ["mode_2d"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "2D Mode",
            [LANG_SPANISH] = "Modo 2D",
            [LANG_HINDI] = "2D मोड"
        },
        ["guidance_scan"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Point camera at an Animal card to begin 4D AR experience",
            [LANG_SPANISH] = "Apunta la cámara a una tarjeta de animal para comenzar la experiencia 4D AR",
            [LANG_HINDI] = "4D AR अनुभव शुरू करने के लिए कैमरे को एनिमल कार्ड की ओर करें"
        },
        ["info_title_header"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Animal Encyclopedia",
            [LANG_SPANISH] = "Enciclopedia Animal",
            [LANG_HINDI] = "जानवर की जानकारी"
        },
        ["fun_fact_prefix"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "💡 Fun Fact: ",
            [LANG_SPANISH] = "💡 Dato Curioso: ",
            [LANG_HINDI] = "💡 रोचक तथ्य: "
        },
        ["lbl_classification"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Classification",
            [LANG_SPANISH] = "Clasificación",
            [LANG_HINDI] = "वर्गीकरण"
        },
        ["lbl_era"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Era / Period",
            [LANG_SPANISH] = "Era / Período",
            [LANG_HINDI] = "काल / युग"
        },
        ["lbl_dimensions"] = new Dictionary<string, string>
        {
            [LANG_ENGLISH] = "Dimensions",
            [LANG_SPANISH] = "Dimensiones",
            [LANG_HINDI] = "आकार व वजन"
        }
    };

    public struct LocalizedAnimalInfo
    {
        public string Name;
        public string ScientificName;
        public string Classification;
        public string Era;
        public string Length;
        public string Weight;
        public string Description;
        public string FunFact;
    }

    // Localized animal profiles: [AnimalKey] -> { [Language] -> Info }
    private static readonly Dictionary<string, Dictionary<string, LocalizedAnimalInfo>> AnimalDataTable = new Dictionary<string, Dictionary<string, LocalizedAnimalInfo>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Tyrannosaurus Rex"] = new Dictionary<string, LocalizedAnimalInfo>
        {
            [LANG_ENGLISH] = new LocalizedAnimalInfo
            {
                Name = "Tyrannosaurus Rex",
                ScientificName = "Tyrannosaurus rex",
                Classification = "Apex Carnivore (Theropod)",
                Era = "Late Cretaceous (~68 – 66 Million Years Ago)",
                Length = "12.3 meters (40 ft)",
                Weight = "8,400 kg (8.4 metric tons)",
                Description = "Tyrannosaurus Rex ('King of the Tyrant Lizards') was one of the most formidable land carnivores in Earth's history. It possessed massive serrated teeth up to 12 inches long, an exceptional sense of stereoscopic vision, and an acute olfactory sense capable of detecting prey from miles away.",
                FunFact = "T-Rex possessed the strongest bite force of any terrestrial animal in history — over 12,800 pounds of crushing force, easily shattering solid bone!"
            },
            [LANG_SPANISH] = new LocalizedAnimalInfo
            {
                Name = "Tiranosaurio Rex",
                ScientificName = "Tyrannosaurus rex",
                Classification = "Superdepredador Carnívoro (Terópodo)",
                Era = "Cretácico Tardío (~68 – 66 millones de años atrás)",
                Length = "12.3 metros (40 pies)",
                Weight = "8,400 kg (8.4 toneladas métricas)",
                Description = "El Tiranosaurio Rex ('Rey de los Lagartos Tiranos') fue uno de los mayores carnívoros terrestres de todos los tiempos. Poseía dientes aserrados de hasta 30 cm, visión estereoscópica avanzada y un olfato extraordinario para rastrear presas a gran distancia.",
                FunFact = "¡El T-Rex tenía la mordida más poderosa de cualquier animal terrestre: más de 12,800 libras de fuerza, capaz de triturar huesos con facilidad!"
            },
            [LANG_HINDI] = new LocalizedAnimalInfo
            {
                Name = "टायरानोसॉरस रेक्स",
                ScientificName = "Tyrannosaurus rex",
                Classification = "शीर्ष मांसाहारी (थेरोपोड)",
                Era = "उत्तर क्रिटेशियस काल (~6.8 – 6.6 करोड़ वर्ष पूर्व)",
                Length = "12.3 मीटर (40 फीट)",
                Weight = "8,400 किग्रा (8.4 मीट्रिक टन)",
                Description = "टायरानोसॉरस रेक्स ('तानाशाह छिपकलियों का राजा') सर्वकालिक सबसे शक्तिशाली मांसाहारी जीवों में से एक था। इसके पास 12 इंच तक लंबे दांतेदार दांत, बेहतरीन दूरबीन दृष्टि और मीलों दूर से शिकार सूंघने की असाधारण क्षमता थी।",
                FunFact = "टी-रेक्स के पास इतिहास में किसी भी स्थलीय जीव की तुलना में सबसे मजबूत काटने का बल (12,800 पाउंड से अधिक) था, जो हड्डियों को आसानी से चकनाचूर कर देता था!"
            }
        },

        ["Stegosaurus"] = new Dictionary<string, LocalizedAnimalInfo>
        {
            [LANG_ENGLISH] = new LocalizedAnimalInfo
            {
                Name = "Stegosaurus",
                ScientificName = "Stegosaurus stenops",
                Classification = "Armored Herbivore (Thyreophoran)",
                Era = "Late Jurassic (~155 – 150 Million Years Ago)",
                Length = "9.0 meters (30 ft)",
                Weight = "5,000 kg (5 metric tons)",
                Description = "Stegosaurus ('Roofed Lizard') is famous for its distinctive upright diamond-shaped bony plates along its arched back and four lethal tail spikes called a thagomizer, used for deadly defense against apex predators like Allosaurus.",
                FunFact = "Despite weighing around 5 metric tons, the brain of a Stegosaurus was remarkably small — only about the size of a walnut!"
            },
            [LANG_SPANISH] = new LocalizedAnimalInfo
            {
                Name = "Estegosaurio",
                ScientificName = "Stegosaurus stenops",
                Classification = "Herbívoro Acorazado (Tireóforo)",
                Era = "Jurásico Tardío (~155 – 150 millones de años atrás)",
                Length = "9.0 metros (30 pies)",
                Weight = "5,000 kg (5 toneladas métricas)",
                Description = "El Estegosaurio ('Lagarto con Techo') es reconocido por sus distintivas placas óseas erguidas a lo largo de su columna y cuatro púas defensivas mortales en la cola conocidas como 'thagomizer', con las que se defendía de depredadores como el Alosaurio.",
                FunFact = "¡A pesar de pesar unas 5 toneladas, el cerebro de un estegosaurio era increíblemente pequeño, aproximadamente del tamaño de una nuez!"
            },
            [LANG_HINDI] = new LocalizedAnimalInfo
            {
                Name = "स्टेगोसॉरस",
                ScientificName = "Stegosaurus stenops",
                Classification = "कवचधारी शाकाहारी (थायरोफोरा)",
                Era = "उत्तर जुरासिक काल (~15.5 – 15 करोड़ वर्ष पूर्व)",
                Length = "9.0 मीटर (30 फीट)",
                Weight = "5,000 किग्रा (5 मीट्रिक टन)",
                Description = "स्टेगोसॉरस अपनी पीठ पर लगी विशिष्ट हीरों जैसी सीधी हड्डियों की प्लेटों और पूंछ पर चार घातक नुकीले कांटों (थैगोमाइज़र) के लिए प्रसिद्ध है, जिसका उपयोग वह एलोसॉरस जैसे शिकारियों से अपनी रक्षा के लिए करता था।",
                FunFact = "लगभग 5 मीट्रिक टन वजन होने के बावजूद, स्टेगोसॉरस का दिमाग आश्चर्यजनक रूप से केवल एक अखरोट के आकार का था!"
            }
        },

        ["Ankylosaurus"] = new Dictionary<string, LocalizedAnimalInfo>
        {
            [LANG_ENGLISH] = new LocalizedAnimalInfo
            {
                Name = "Ankylosaurus",
                ScientificName = "Ankylosaurus magniventris",
                Classification = "Armored Herbivore (Living Fortress)",
                Era = "Late Cretaceous (~68 – 66 Million Years Ago)",
                Length = "8.0 meters (26 ft)",
                Weight = "6,000 kg (6 metric tons)",
                Description = "Ankylosaurus was a heavily armored walking tank covered with thick bony plates, protective oval osteoderms, and reinforced spikes. At the tip of its stiffened tail was a massive bony club capable of shattering predator limbs.",
                FunFact = "Even the eyelids of an Ankylosaurus had armored bone protection, making it virtually impervious to attacks from large predators like T-Rex!"
            },
            [LANG_SPANISH] = new LocalizedAnimalInfo
            {
                Name = "Anquilosaurio",
                ScientificName = "Ankylosaurus magniventris",
                Classification = "Herbívoro Acorazado (Fortaleza Viviente)",
                Era = "Cretácico Tardío (~68 – 66 millones de años atrás)",
                Length = "8.0 metros (26 pies)",
                Weight = "6,000 kg (6 toneladas métricas)",
                Description = "El Anquilosaurio era un auténtico tanque blindado viviente cubierto por gruesas placas óseas y osteodermos protectores. En la punta de su cola poseía un pesado mazo óseo capaz de fracturar huesos de grandes depredadores.",
                FunFact = "¡Incluso los párpados del anquilosaurio tenían placas de hueso protectoras, haciéndolo casi invencible frente a los ataques del T-Rex!"
            },
            [LANG_HINDI] = new LocalizedAnimalInfo
            {
                Name = "एंकाइलोसॉरस",
                ScientificName = "Ankylosaurus magniventris",
                Classification = "बख्तरबंद शाकाहारी (जीवित किला)",
                Era = "उत्तर क्रिटेशियस काल (~6.8 – 6.6 करोड़ वर्ष पूर्व)",
                Length = "8.0 मीटर (26 फीट)",
                Weight = "6,000 किग्रा (6 मीट्रिक टन)",
                Description = "एंकाइलोसॉरस एक अत्यंत सुरक्षित जीवित टैंक था, जो मोटी हड्डियों की कवचदार प्लेटों और सुरक्षात्मक नुकीलों से ढका था। इसकी पूंछ के सिरे पर एक भारी हड्डी की गदा थी जो शिकारी डायनासोरों की हड्डियों को तोड़ सकती थी।",
                FunFact = "एंकाइलोसॉरस की पलकों पर भी सुरक्षात्मक हड्डी की परत थी, जिससे टी-रेक्स जैसे शिकारी भी इसे नुकसान नहीं पहुंचा पाते थे!"
            }
        }
    };

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Destroy only this component, not the whole GameObject
            // (PanelController adds this component to its own GameObject).
            Destroy(this);
            return;
        }

        Instance = this;

        // Default language when starting / playing the game is always English.
        currentLanguage = LANG_ENGLISH;
        pendingLanguage = LANG_ENGLISH;
        PlayerPrefs.SetString(PREF_KEY_LANGUAGE, LANG_ENGLISH);
        PlayerPrefs.Save();

        // One single scene scan for both binding and localization setup.
        BindSceneAndSetupLocalization();
    }

    private void Start()
    {
        UpdateLanguageButtonVisuals();
        NotifyLanguageChanged();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // LANGUAGE API
    // =========================================================

    /// <summary>
    /// Changes the application's active language and notifies all listeners.
    /// </summary>
    public void SetLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
            return;

        currentLanguage = language;
        pendingLanguage = language;
        PlayerPrefs.SetString(PREF_KEY_LANGUAGE, currentLanguage);
        PlayerPrefs.Save();

        Debug.Log($"[LanguageManager] Language switched to: {currentLanguage}\nCalled from:\n{System.Environment.StackTrace}");

        UpdateLanguageButtonVisuals();
        NotifyLanguageChanged();
    }

    /// <summary>
    /// Step 1: tapping a language box only SELECTS it (no language change yet).
    /// </summary>
    private void SelectPendingLanguage(string language)
    {
        pendingLanguage = language;
        UpdateLanguageButtonVisuals();
    }

    /// <summary>
    /// Step 2: pressing Submit APPLIES the selected language.
    /// </summary>
    private void ConfirmLanguage()
    {
        if (pendingLanguage == currentLanguage) return;
        SetLanguage(pendingLanguage);
    }

    /// <summary>
    /// Switches language between English, Spanish, and Hindi cyclically.
    /// </summary>
    public void CycleLanguage()
    {
        switch (currentLanguage)
        {
            case LANG_ENGLISH:
                SetLanguage(LANG_SPANISH);
                break;
            case LANG_SPANISH:
                SetLanguage(LANG_HINDI);
                break;
            default:
                SetLanguage(LANG_ENGLISH);
                break;
        }
    }

    /// <summary>
    /// Returns the localized string for a given key in the current language.
    /// </summary>
    public string GetText(string key, string fallback = "")
    {
        if (string.IsNullOrEmpty(key))
            return fallback;

        if (StringTable.TryGetValue(key, out var langDict))
        {
            if (langDict.TryGetValue(currentLanguage, out var val))
                return val;

            if (langDict.TryGetValue(LANG_ENGLISH, out var engVal))
                return engVal;
        }

        return string.IsNullOrEmpty(fallback) ? key : fallback;
    }

    /// <summary>
    /// Returns localized animal information for the current language.
    /// </summary>
    public bool TryGetLocalizedAnimalInfo(string rawAnimalName, out LocalizedAnimalInfo info)
    {
        info = default;
        if (string.IsNullOrEmpty(rawAnimalName))
            return false;

        string normalized = rawAnimalName.Trim();

        // Handle variations (e.g. Rex vs Tyrannosaurus Rex)
        if (normalized.IndexOf("rex", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.IndexOf("tyranno", StringComparison.OrdinalIgnoreCase) >= 0)
            normalized = "Tyrannosaurus Rex";
        else if (normalized.IndexOf("stego", StringComparison.OrdinalIgnoreCase) >= 0)
            normalized = "Stegosaurus";
        else if (normalized.IndexOf("ankylo", StringComparison.OrdinalIgnoreCase) >= 0)
            normalized = "Ankylosaurus";

        if (AnimalDataTable.TryGetValue(normalized, out var langMap))
        {
            if (langMap.TryGetValue(currentLanguage, out info))
                return true;

            if (langMap.TryGetValue(LANG_ENGLISH, out info))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the localized name of an animal in the active language.
    /// </summary>
    public string GetLocalizedAnimalName(string rawAnimalName)
    {
        if (TryGetLocalizedAnimalInfo(rawAnimalName, out var info))
        {
            return info.Name;
        }
        return rawAnimalName;
    }

    /// <summary>
    /// Builds a structured biography text for an animal in the active language.
    /// NOTE: uses <b> tags. Verify that UniText renders them (otherwise they show as literal text).
    /// </summary>
    public string GetFormattedAnimalBio(string rawAnimalName, AnimalData data = null)
    {
        TryGetLocalizedAnimalInfo(rawAnimalName, out LocalizedAnimalInfo info);

        string classification = !string.IsNullOrEmpty(info.Classification) ? info.Classification : (data != null ? data.GetLocalizedClassification() : "");
        string era = !string.IsNullOrEmpty(info.Era) ? info.Era : (data != null ? data.GetLocalizedEra() : "");
        string length = !string.IsNullOrEmpty(info.Length) ? info.Length : (data != null ? data.averageLength : "");
        string weight = !string.IsNullOrEmpty(info.Weight) ? info.Weight : (data != null ? data.averageWeight : "");
        string description = !string.IsNullOrEmpty(info.Description) ? info.Description : (data != null ? data.GetLocalizedDescription() : "");
        string funFact = !string.IsNullOrEmpty(info.FunFact) ? info.FunFact : (data != null ? data.GetLocalizedFunFact() : "");

        string funFactPrefix = GetText("fun_fact_prefix", "💡 Fun Fact: ");

        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(classification))
        {
            sb.AppendLine($"• <b>{GetText("lbl_classification", "Classification")}:</b> {classification}");
        }

        if (!string.IsNullOrEmpty(era))
        {
            sb.AppendLine($"• <b>{GetText("lbl_era", "Era")}:</b> {era}");
        }

        if (!string.IsNullOrEmpty(length) || !string.IsNullOrEmpty(weight))
        {
            string dims = "";
            if (!string.IsNullOrEmpty(length)) dims += length;
            if (!string.IsNullOrEmpty(weight)) dims += (dims.Length > 0 ? " | " : "") + weight;
            sb.AppendLine($"• <b>{GetText("lbl_dimensions", "Dimensions")}:</b> {dims}");
        }

        if (sb.Length > 0)
        {
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine(description);
        }

        if (!string.IsNullOrEmpty(funFact))
        {
            sb.AppendLine();
            if (!funFact.StartsWith("💡") && !funFact.StartsWith("Fun Fact", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"<b>{funFactPrefix}</b>{funFact}");
            }
            else
            {
                sb.AppendLine($"<b>{funFact}</b>");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public void NotifyLanguageChanged()
    {
        OnLanguageChanged?.Invoke(currentLanguage);

        // Only currently-enabled LocalizedText instances are refreshed here.
        // Inactive ones refresh themselves in OnEnable when they get activated.
        var list = LocalizedText.Active;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] != null) list[i].UpdateText();
        }
    }

    // =========================================================
    // SETTING PANEL
    // =========================================================

    public void OpenSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
            pendingLanguage = currentLanguage; // discard any old unsubmitted choice
            UpdateLanguageButtonVisuals();
        }
    }

    public void CloseSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
            pendingLanguage = currentLanguage; // discard unsubmitted choice
            UpdateLanguageButtonVisuals();
        }
    }

    public void ToggleSettingPanel()
    {
        if (settingPanel != null)
        {
            bool isOpening = !settingPanel.activeSelf;
            settingPanel.SetActive(isOpening);
            pendingLanguage = currentLanguage; // always start/end from the active language
            UpdateLanguageButtonVisuals();
        }
    }

    // =========================================================
    // UI AUTO-BINDING & LOCALIZATION SETUP (single scene scan)
    // =========================================================

    private void BindSceneAndSetupLocalization()
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);

        foreach (Transform t in allTransforms)
        {
            string tName = t.name.Trim();

            // ---- UI binding ----
            if (settingPanel == null && (tName == "Setting_panel" || tName == "Setting_Panel"))
                settingPanel = t.gameObject;

            if (settingButton == null && tName == "setting_button")
                settingButton = t.GetComponent<Button>();

            if (spanishButton == null && tName == "Spanish_button")
                spanishButton = t.GetComponent<Button>();

            if (hindiButton == null && tName == "Hindi_button")
                hindiButton = t.GetComponent<Button>();

            if (englishButton == null && tName == "English_button")
                englishButton = t.GetComponent<Button>();

            if (submitButton == null && (tName == "Submit_button" || tName == "Confirm_button"))
                submitButton = t.GetComponent<Button>();

            // ---- Localization attach ----
            // NOTE: "ProgressText" is intentionally NOT localized here.
            // PanelController builds that string itself ("Loading... 45%").
            if (tName == "Caution_text")
                AttachLocalizedText(t.gameObject, "lbl_parental_supervision");
            else if(tName=="infotext")
                AttachLocalizedText(t.gameObject,"Guide_Text");

            else if(tName=="Langauge_Title")
                AttachLocalizedText(t.gameObject,"change_langauge");
    
            else if (tName == "ok_button")
                AttachLocalizedTextToChild(t.gameObject, "btn_ok");

            else if (tName == "Titletext" && t.parent != null && t.parent.name.Contains("Instruction"))
                AttachLocalizedText(t.gameObject, "title_instructions");

            else if (tName == "Instruction_text01")
                AttachLocalizedText(t.gameObject, "instruction_step1");

            else if (tName == "Instruction_text02")
                AttachLocalizedText(t.gameObject, "instruction_step2");

            else if (tName == "Get_the_card_button")
                AttachLocalizedTextToChild(t.gameObject, "btn_get_card");

            else if (tName == "Language_text")
                AttachLocalizedText(t.gameObject, "lbl_language");

            else if (tName == "Action Button")
                AttachLocalizedTextToChild(t.gameObject, "btn_action");

            else if (tName == "Audio Button")
                AttachLocalizedTextToChild(t.gameObject, "btn_audio");

            else if (tName == "Fast_Audio_button")
                AttachLocalizedTextToChild(t.gameObject, "btn_fast_audio");

            else if (tName == "Share_button")
                AttachLocalizedTextToChild(t.gameObject, "btn_share");

            else if (tName == "3d_text")
                AttachLocalizedText(t.gameObject, "mode_3d");

            else if (tName == "2d_text")
                AttachLocalizedText(t.gameObject, "mode_2d");
        }

        // Resolve back button AFTER the loop so it doesn't depend on iteration order.
        if (backButton == null && settingPanel != null)
        {
            foreach (Button b in settingPanel.GetComponentsInChildren<Button>(true))
            {
                if (b.name.Trim().ToLowerInvariant() == "back_button")
                {
                    backButton = b;
                    break;
                }
            }
        }

        // If English button doesn't exist in scene, clone it from the Spanish button
        if (englishButton == null && spanishButton != null && spanishButton.transform.parent != null)
        {
            GameObject engGo = Instantiate(spanishButton.gameObject, spanishButton.transform.parent);
            engGo.name = "English_button";
            engGo.transform.SetAsFirstSibling();

            UniText unitext = engGo.GetComponentInChildren<UniText>(true);
            if (unitext != null) unitext.Text = "English";

            englishButton = engGo.GetComponent<Button>();
        }

        // If Submit button doesn't exist in scene, clone it from the Back button
        // (reposition the clone in your Setting panel layout as needed)
        if (submitButton == null && backButton != null && backButton.transform.parent != null)
        {
            GameObject subGo = Instantiate(backButton.gameObject, backButton.transform.parent);
            subGo.name = "Submit_button";
            submitButton = subGo.GetComponent<Button>();
        }

        // Localize the Submit button label
        if (submitButton != null)
            AttachLocalizedTextToChild(submitButton.gameObject, "btn_submit");

        WireButtonListeners();
    }

    private void WireButtonListeners()
    {
        // LanguageManager is the single owner of the setting button and back button.
        // PanelController must NOT add its own listeners to them.
        if (settingButton != null)
        {
            settingButton.onClick.RemoveListener(ToggleSettingPanel);
            settingButton.onClick.AddListener(ToggleSettingPanel);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(CloseSettingPanel);
            backButton.onClick.AddListener(CloseSettingPanel);
        }

        // Language buttons only SELECT a language
        // "new ButtonClickedEvent()" clears Inspector listeners too (RemoveAllListeners does not)
        if (englishButton != null)
        {
            englishButton.onClick = new Button.ButtonClickedEvent();
            englishButton.onClick.AddListener(() => SelectPendingLanguage(LANG_ENGLISH));
        }

        if (spanishButton != null)
        {
            spanishButton.onClick = new Button.ButtonClickedEvent();
            spanishButton.onClick.AddListener(() => SelectPendingLanguage(LANG_SPANISH));
        }

        if (hindiButton != null)
        {
            hindiButton.onClick = new Button.ButtonClickedEvent();
            hindiButton.onClick.AddListener(() => SelectPendingLanguage(LANG_HINDI));
        }

        // Submit button APPLIES the selected language
        if (submitButton != null)
        {
            submitButton.onClick = new Button.ButtonClickedEvent();
            submitButton.onClick.AddListener(ConfirmLanguage);
        }
    }
  
    private void UpdateLanguageButtonVisuals()
    {
    SetButtonHighlight(englishButton, pendingLanguage == LANG_ENGLISH);
    SetButtonHighlight(spanishButton, pendingLanguage == LANG_SPANISH);
    SetButtonHighlight(hindiButton, pendingLanguage == LANG_HINDI);

    UpdateFlag(); // <-- add this

    if (submitButton != null)
        submitButton.interactable = (pendingLanguage != currentLanguage);
    }

    private void SetButtonHighlight(Button btn, bool isSelected)
{
    if (btn == null) return;

    // Keep the button background normal (no green fill)
    Image img = btn.GetComponent<Image>();
    if (img != null)
        img.color = Color.white;

    // Border only on the selected button
    Outline outline = btn.GetComponent<Outline>();
    if (outline == null)
    {
        outline = btn.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.8f, 0.4f, 1f); // border color
        outline.effectDistance = new Vector2(4f, -4f);         // border thickness
        outline.useGraphicAlpha = false;
    }

    outline.enabled = isSelected;
}

    private void AttachLocalizedText(GameObject go, string key)
    {
        if (go == null) return;

        // LocalizedText requires a UniText on the same object.
        if (go.GetComponent<UniText>() == null) return;

        try
        {
            LocalizedText lt = go.GetComponent<LocalizedText>();
            if (lt == null) lt = go.AddComponent<LocalizedText>();
            if (lt != null)
            {
                lt.LocalizationKey = key;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LanguageManager] Could not attach LocalizedText to {go.name}: {ex.Message}");
        }
    }

    private void AttachLocalizedTextToChild(GameObject go, string key)
    {
        if (go == null) return;

        UniText unitext = go.GetComponentInChildren<UniText>(true);
        if (unitext != null)
        {
            AttachLocalizedText(unitext.gameObject, key);
        }
    }

    private void UpdateFlag()
   {
    if (flagImage == null) return;

    Sprite flag = null;
    switch (currentLanguage)
    {
        case LANG_ENGLISH: flag = englishFlag; break;
        case LANG_SPANISH: flag = spanishFlag; break;
        case LANG_HINDI:   flag = hindiFlag;   break;
    }

    if (flag != null)
        flagImage.sprite = flag;
    }

   
}