// Assets/Scripts/Localization/LocaleCodeMap.cs
using System;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace Multilingo.Localization
{
    /// <summary>
    /// Dynamic locale code resolver.
    /// First queries Unity's Localization Settings for available locales,
    /// then falls back to a static override map for edge cases.
    /// 
    /// Eliminates the need to hardcode every language — adding a new locale
    /// in Unity's Localization Settings automatically resolves correctly.
    /// </summary>
    public static class LocaleCodeMap
    {
        // Override map for display-name-to-code lookups that Unity can't resolve
        static readonly Dictionary<string, string> s_overrideMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Chinese (Simplified)(zh)", "zh-CN" },
            { "Chinese (Traditional)(zh-TW)", "zh-TW" },
            { "Korean (South Korea)(ko-KR)", "ko-KR" },
            { "Polish (Poland)(pl-PL)", "pl" },
            { "Spanish (Spain)(es-ES)", "es" },
            { "English (en)", "en" },
            { "German(de)", "de" },
            { "Japanese(ja)", "ja" },
            { "Korean(ko)", "ko" },
            { "Portuguese(pt)", "pt" },
            { "Russian(ru)", "ru" },
            { "Thai(th)", "th" },
        };

        /// <summary>
        /// Resolve any input (display name, locale code, etc.) to a canonical locale code.
        /// Priority:
        ///   1. Exact match in Unity's available locales
        ///   2. CultureInfo name/EnglishName match in Unity's locales
        ///   3. Prefix match (e.g. "ko" matches "ko-KR")
        ///   4. Override map for legacy display names
        ///   5. Assume input is a valid locale code if it looks like one
        ///   6. Fallback to "en"
        /// </summary>
        public static string GetCanonical(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "en";
            string trimmed = input.Trim();

            // 1. Try Unity's Localization Settings
            try
            {
                var locales = LocalizationSettings.AvailableLocales?.Locales;
                if (locales != null && locales.Count > 0)
                {
                    // Exact code match
                    for (int i = 0; i < locales.Count; i++)
                    {
                        if (string.Equals(locales[i].Identifier.Code, trimmed, StringComparison.OrdinalIgnoreCase))
                            return locales[i].Identifier.Code;
                    }

                    // CultureInfo name or EnglishName contains input
                    for (int i = 0; i < locales.Count; i++)
                    {
                        var ci = locales[i].Identifier.CultureInfo;
                        if (ci != null)
                        {
                            if (string.Equals(ci.Name, trimmed, StringComparison.OrdinalIgnoreCase) ||
                                ci.EnglishName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                                return locales[i].Identifier.Code;
                        }
                    }

                    // Prefix match (e.g. "ko" matches "ko-KR")
                    string prefix = trimmed.Split('-')[0];
                    for (int i = 0; i < locales.Count; i++)
                    {
                        if (locales[i].Identifier.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            return locales[i].Identifier.Code;
                    }
                }
            }
            catch
            {
                // Localization not initialized yet — fall through to static map
            }

            // 2. Override map
            if (s_overrideMap.TryGetValue(trimmed, out var v)) return v;

            // 3. Tolerant normalization (strip spaces and parens)
            string normalized = trimmed.Replace(" ", "").Replace("(", "").Replace(")", "");
            if (s_overrideMap.TryGetValue(normalized, out v)) return v;

            // 4. If it looks like a locale code, return it
            if (trimmed.Length >= 2 && trimmed.Length <= 5) return trimmed;

            return "en";
        }

        /// <summary>
        /// Add a custom mapping at runtime. Useful for game-specific locale names.
        /// </summary>
        public static void AddOverride(string displayName, string localeCode)
        {
            s_overrideMap[displayName] = localeCode;
        }
    }
}
