// Assets/Scripts/Localization/LocaleFontSwitcher.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Multilingo.Localization
{

    [Serializable]
    public struct NamedFontAsset
    {
        public string name;
        public TMP_FontAsset font;
    }

    [Serializable]
    public struct LocaleFontEntry
    {
        public string localeCode;        // "en", "zh-CN", "ja", "ko", "th", etc.
        public NamedFontAsset[] fonts;
    }

    [DisallowMultipleComponent]
    public class LocaleFontSwitcher : MonoBehaviour
    {
        [Header("Locale font mappings")]
        public LocaleFontEntry[] localeFonts;

        [Header("Prewarm")]
        [Tooltip("Characters to prewarm for chosen fonts so glyphs are available immediately.")]
        public string prewarmChars = "0123456789.,:!?%()[] ";

        // --- internal tracking ---
        readonly HashSet<TMP_Text> trackedTexts = new HashSet<TMP_Text>();
        readonly Dictionary<TMP_Text, TextOriginal> originalMap = new Dictionary<TMP_Text, TextOriginal>();

        // dirty flag: batch multiple RegisterText calls into one ApplyForLocale per frame
        bool applyDirty;
        string lastAppliedCode;

        class TextOriginal
        {
            public TMP_FontAsset originalFont;
        }

        static LocaleFontSwitcher s_instance;
        public static LocaleFontSwitcher Instance => s_instance;

        // Cache the delegate so OnDisable can actually unsubscribe it
        Action<AsyncOperationHandle<LocalizationSettings>> onInitCompleted;

        void Awake()
        {
            if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
            s_instance = this;
            DontDestroyOnLoad(gameObject);
            onInitCompleted = _ => ApplyForLocale(LocalizationSettings.SelectedLocale?.Identifier.Code);
        }

        void OnEnable()
        {
            LocalizationSettings.InitializationOperation.Completed += onInitCompleted;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindAndCaptureTexts(); // initial capture
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LocalizationSettings.InitializationOperation.Completed -= onInitCompleted;
        }

        void OnDestroy() { if (s_instance == this) s_instance = null; }

        void LateUpdate()
        {
            // Batch-apply: if anything was registered this frame, apply fonts once
            if (applyDirty)
            {
                applyDirty = false;
                ApplyForLocaleImmediate(LocalizationSettings.SelectedLocale?.Identifier.Code);
            }
        }

        void OnSceneLoaded(Scene s, LoadSceneMode mode) => FindAndCaptureTexts();
        void OnLocaleChanged(Locale newLocale) => ApplyForLocaleImmediate(newLocale?.Identifier.Code);

        /// <summary>
        /// Scan the scene for TMP_Text instances and capture original font info.
        /// Called once on startup and on scene load.
        /// </summary>
        public void FindAndCaptureTexts()
        {
            var allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTexts)
            {
                if (t == null) continue;
                RegisterTextInternal(t);
            }
            ApplyForLocaleImmediate(LocalizationSettings.SelectedLocale?.Identifier.Code);
        }

        /// <summary>
        /// Re-scan and refresh all texts. Use when you batch-create many texts.
        /// </summary>
        public void Refresh() => FindAndCaptureTexts();
        /// Safe to call multiple times for the same text.
        /// </summary>
        public void RegisterText(TMP_Text t)
        {
            if (t == null) return;
            if (!RegisterTextInternal(t)) return; // already tracked

            // Apply immediately for this one text
            string currentCode = LocalizationSettings.SelectedLocale?.Identifier.Code;
            if (!string.IsNullOrEmpty(currentCode))
            {
                ApplyToText(t, currentCode);
            }
        }

        bool RegisterTextInternal(TMP_Text t)
        {
            if (!trackedTexts.Add(t)) return false; // already tracked
            originalMap[t] = new TextOriginal
            {
                originalFont = t.font
            };
            return true;
        }

        void ApplyToText(TMP_Text t, string code)
        {
            if (t == null || string.IsNullOrEmpty(code)) return;

            // find entry logic (same as ApplyForLocaleImmediate but for one text)
            LocaleFontEntry entry = default;
            bool found = false;
            for (int i = 0; i < localeFonts.Length; i++)
            {
                if (string.Equals(localeFonts[i].localeCode, code, StringComparison.OrdinalIgnoreCase))
                { entry = localeFonts[i]; found = true; break; }
            }
            if (!found)
            {
                string prefix = code.Split('-')[0];
                for (int i = 0; i < localeFonts.Length; i++)
                {
                    if (localeFonts[i].localeCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    { entry = localeFonts[i]; found = true; break; }
                }
            }

            if (!found || entry.fonts == null || entry.fonts.Length == 0) return;

            TMP_FontAsset chosen = null;
            originalMap.TryGetValue(t, out var orig);

            if (orig != null && orig.originalFont != null)
            {
                string origName = orig.originalFont.name;
                
                // First attempt to match by the user-defined name
                foreach (var f in entry.fonts)
                {
                    if (!string.IsNullOrEmpty(f.name) && origName.IndexOf(f.name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        chosen = f.font;
                        break;
                    }
                }

                // Fallback to weight keywords if no explicit name match
                if (chosen == null)
                {
                    string[] weights = { "Bold", "SemiBold", "Light", "Medium", "Black", "Thin", "Italic", "Regular" };
                    string matchedWeight = null;
                    
                    foreach (var w in weights)
                    {
                        if (origName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchedWeight = w;
                            break;
                        }
                    }

                    if (matchedWeight != null)
                    {
                        foreach (var f in entry.fonts)
                        {
                            if (f.font != null && f.font.name.IndexOf(matchedWeight, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                chosen = f.font;
                                break;
                            }
                        }
                    }
                }
            }

            if (chosen == null)
            {
                chosen = entry.fonts[0].font; // fallback to first font
            }

            if (chosen != null && t.font != chosen)
            {
                t.font = chosen;
                if (chosen.material != null) t.fontSharedMaterial = chosen.material;
            }
        }

        /// <summary>
        /// Unregister a runtime-created TMP_Text (optional).
        /// </summary>
        public void UnregisterText(TMP_Text t)
        {
            if (t == null) return;
            trackedTexts.Remove(t);
            originalMap.Remove(t);
        }

        /// <summary>
        /// Apply locale fonts to all registered targets immediately.
        /// </summary>
        void ApplyForLocaleImmediate(string code)
        {
            if (string.IsNullOrEmpty(code)) return;

            // find best match in localeFonts
            LocaleFontEntry entry = default;
            bool found = false;
            for (int i = 0; i < localeFonts.Length; i++)
            {
                if (string.Equals(localeFonts[i].localeCode, code, StringComparison.OrdinalIgnoreCase))
                {
                    entry = localeFonts[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // fallback to prefix match (e.g., "zh" matches "zh-CN")
                string prefix = code.Split('-')[0];
                for (int i = 0; i < localeFonts.Length; i++)
                {
                    if (localeFonts[i].localeCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = localeFonts[i];
                        found = true;
                        break;
                    }
                }
            }

            if (!found || entry.fonts == null || entry.fonts.Length == 0) return;

            // Clean up destroyed texts while iterating
            List<TMP_Text> toRemove = null;

            foreach (var t in trackedTexts)
            {
                if (t == null)
                {
                    if (toRemove == null) toRemove = new List<TMP_Text>();
                    toRemove.Add(t);
                    continue;
                }

                TMP_FontAsset chosen = null;
                originalMap.TryGetValue(t, out var orig);

                if (orig != null && orig.originalFont != null)
                {
                    string origName = orig.originalFont.name;
                    // First attempt to match by the user-defined name
                    foreach (var f in entry.fonts)
                    {
                        if (!string.IsNullOrEmpty(f.name) && origName.IndexOf(f.name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            chosen = f.font;
                            break;
                        }
                    }

                    // Fallback to weight keywords if no explicit name match
                    if (chosen == null)
                    {
                        string[] weights = { "Bold", "SemiBold", "Light", "Medium", "Black", "Thin", "Italic", "Regular" };
                        string matchedWeight = null;
                        
                        foreach (var w in weights)
                        {
                            if (origName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matchedWeight = w;
                                break;
                            }
                        }

                        if (matchedWeight != null)
                        {
                            foreach (var f in entry.fonts)
                            {
                                if (f.font != null && f.font.name.IndexOf(matchedWeight, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    chosen = f.font;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (chosen == null)
                {
                    chosen = entry.fonts[0].font; // fallback
                }

                if (chosen != null && t.font != chosen)
                {
                    t.font = chosen;
                    if (chosen.material != null) t.fontSharedMaterial = chosen.material;
                }
            }

            // Remove destroyed references
            if (toRemove != null)
            {
                foreach (var t in toRemove)
                {
                    trackedTexts.Remove(t);
                    originalMap.Remove(t);
                }
            }

            if (entry.fonts != null)
            {
                foreach (var f in entry.fonts)
                {
                    TryPrewarm(f.font);
                }
            }

            lastAppliedCode = code;
        }

        // kept for backward compat — just calls ApplyForLocaleImmediate
        void ApplyForLocale(string code) => ApplyForLocaleImmediate(code);

        void TryPrewarm(TMP_FontAsset font) { if (font == null || string.IsNullOrEmpty(prewarmChars)) return; font.TryAddCharacters(prewarmChars); }

        /// <summary>
        /// Update or add a locale font entry at runtime.
        /// If the current selected locale matches the localeCode, ApplyForLocale is triggered.
        /// </summary>
        public void SetLocaleFont(string localeCode, NamedFontAsset[] newFonts)
        {
            var idx = Array.FindIndex(localeFonts, e => string.Equals(e.localeCode, localeCode, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) { localeFonts[idx].fonts = newFonts ?? new NamedFontAsset[0]; }
            else
            {
                Array.Resize(ref localeFonts, localeFonts.Length + 1);
                localeFonts[localeFonts.Length - 1] = new LocaleFontEntry { localeCode = localeCode, fonts = newFonts ?? new NamedFontAsset[0] };
            }

            if (LocalizationSettings.SelectedLocale != null && LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith(localeCode, StringComparison.OrdinalIgnoreCase))
                ApplyForLocaleImmediate(LocalizationSettings.SelectedLocale.Identifier.Code);
        }
    }
}
