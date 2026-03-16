using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Multilingo.Localization
{
    /// <summary>
    /// Dynamically populates a TMP_Dropdown from Unity's Localization Settings.
    /// No more hardcoded language lists — adding a new locale in Localization Settings
    /// automatically appears in the dropdown.
    /// 
    /// Supports:
    /// - Native language names (English, 日本語, 한국어, Deutsch, etc.)
    /// - Automatic selection of the current locale
    /// - Persistence of language preference via PlayerPrefs
    /// - Graceful fallback if Localization hasn't initialized yet
    /// </summary>
    public class LanguageDropdownUI : MonoBehaviour
    {
        [Header("UI Reference")]
        public TMP_Dropdown dropdown;

        [Header("Display Settings")]
        [Tooltip("Show native language names (e.g. '日本語' instead of 'Japanese')")]
        public bool useNativeNames = false;

        [Tooltip("Show language code in parentheses (e.g. 'Japanese (ja)')")]
        public bool showLanguageCode = false;

        [Header("Persistence")]
        [Tooltip("Save the user's language preference and restore it on next launch")]
        public bool rememberChoice = true;

        const string PREF_KEY = "Multilingo_SelectedLocale";

        // Runtime references to available locales (ordered same as dropdown)
        readonly List<Locale> _locales = new List<Locale>();

        // Display name map for known languages
        static readonly Dictionary<string, string> NativeNames = new Dictionary<string, string>
        {
            {"en",    "English"},
            {"zh-CN", "中文(简体)"},
            {"zh-TW", "中文(繁體)"},
            {"ja",    "日本語"},
            {"ko",    "한국어"},
            {"ko-KR", "한국어"},
            {"de",    "Deutsch"},
            {"fr",    "Français"},
            {"fr-FR", "Français"},
            {"es",    "Español"},
            {"es-ES", "Español"},
            {"it",    "Italiano"},
            {"pt",    "Português"},
            {"pt-BR", "Português (Brasil)"},
            {"ru",    "Русский"},
            {"pl",    "Polski"},
            {"pl-PL", "Polski"},
            {"th",    "ไทย"},
            {"vi",    "Tiếng Việt"},
            {"ar",    "العربية"},
            {"hi",    "हिन्दी"},
            {"tr",    "Türkçe"},
            {"nl",    "Nederlands"},
            {"sv",    "Svenska"},
            {"da",    "Dansk"},
            {"no",    "Norsk"},
            {"fi",    "Suomi"},
            {"cs",    "Čeština"},
            {"hu",    "Magyar"},
            {"ro",    "Română"},
            {"uk",    "Українська"},
            {"id",    "Bahasa Indonesia"},
            {"ms",    "Bahasa Melayu"},
        };

        IEnumerator Start()
        {
            // Wait for localization to initialize
            yield return LocalizationSettings.InitializationOperation;

            PopulateDropdown();

            // Restore saved preference
            if (rememberChoice)
            {
                string savedCode = PlayerPrefs.GetString(PREF_KEY, "");
                if (!string.IsNullOrEmpty(savedCode))
                {
                    for (int i = 0; i < _locales.Count; i++)
                    {
                        if (_locales[i].Identifier.Code == savedCode)
                        {
                            dropdown.SetValueWithoutNotify(i);
                            LocalizationManager.Instance?.SetLocaleByCode(savedCode);
                            break;
                        }
                    }
                }
            }

            dropdown.onValueChanged.AddListener(OnLanguageSelected);
        }

        void PopulateDropdown()
        {
            dropdown.ClearOptions();
            _locales.Clear();

            var available = LocalizationSettings.AvailableLocales?.Locales;
            if (available == null || available.Count == 0)
            {
                Debug.LogWarning("[Multilingo] No locales found in Localization Settings.");
                return;
            }

            var options = new List<TMP_Dropdown.OptionData>();

            foreach (var locale in available)
            {
                _locales.Add(locale);
                string displayName = GetDisplayName(locale);
                options.Add(new TMP_Dropdown.OptionData(displayName));
            }

            dropdown.AddOptions(options);

            // Auto-select current locale
            var current = LocalizationSettings.SelectedLocale;
            if (current != null)
            {
                int index = _locales.IndexOf(current);
                if (index >= 0) dropdown.SetValueWithoutNotify(index);
            }
        }

        void OnLanguageSelected(int index)
        {
            if (index < 0 || index >= _locales.Count) return;

            string code = _locales[index].Identifier.Code;

            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.SetLocaleByCode(code);
            else
                LocalizationSettings.SelectedLocale = _locales[index];

            if (rememberChoice)
                PlayerPrefs.SetString(PREF_KEY, code);
        }

        string GetDisplayName(Locale locale)
        {
            string code = locale.Identifier.Code;
            string name;

            if (useNativeNames && NativeNames.TryGetValue(code, out string native))
                name = native;
            else
            {
                // Use CultureInfo EnglishName if available, fallback to code
                name = locale.Identifier.CultureInfo?.EnglishName ?? locale.name;
                // Clean up "()" region info if present: "Japanese (Japan)" -> "Japanese"
                int parenIdx = name.IndexOf('(');
                if (parenIdx > 0 && !showLanguageCode) name = name.Substring(0, parenIdx).Trim();
            }

            if (showLanguageCode) name += $" ({code})";
            return name;
        }
    }
}
