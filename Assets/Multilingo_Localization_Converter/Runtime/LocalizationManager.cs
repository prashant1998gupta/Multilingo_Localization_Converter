// Assets/Scripts/Localization/LocalizationManager.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Multilingo.Localization
{

    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }
        readonly ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>();
        const string DefaultLocaleCode = "en";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        void OnLocaleChanged(Locale l) => cache.Clear();

        string CacheKey(string table, string entry, Locale locale) =>
            $"{table}|{entry}|{(locale != null ? locale.Identifier.Code : "null")}";

        // Set locale using any of the display strings you provided
        public void SetLocaleByDisplayName(string displayName)
        {
            var code = LocaleCodeMap.GetCanonical(displayName);
            SetLocaleByCode(code);
        }

        // Set locale using canonical code (e.g. "en", "zh-CN", "ko-KR", "pt")
        public void SetLocaleByCode(string code)
        {
            if (string.IsNullOrEmpty(code)) code = DefaultLocaleCode;

            // Best-effort matching against available locales (no LINQ to avoid allocations)
            var locales = LocalizationSettings.AvailableLocales.Locales;
            Locale target = null;

            // Try exact code match first (Identifier.Code)
            for (int i = 0; i < locales.Count; i++)
            {
                if (string.Equals(locales[i].Identifier.Code, code, StringComparison.OrdinalIgnoreCase))
                { target = locales[i]; break; }
            }

            if (target == null)
            {
                // Try culture name or prefix match (e.g. "ko-KR" vs "ko")
                string prefix = code.Split('-')[0];
                for (int i = 0; i < locales.Count; i++)
                {
                    var l = locales[i];
                    if (string.Equals(l.Identifier.CultureInfo?.Name, code, StringComparison.OrdinalIgnoreCase)
                        || l.Identifier.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    { target = l; break; }
                }
            }

            if (target != null)
            {
                LocalizationSettings.SelectedLocale = target;
                return;
            }

            // fallback to DefaultLocaleCode
            for (int i = 0; i < locales.Count; i++)
            {
                if (string.Equals(locales[i].Identifier.Code, DefaultLocaleCode, StringComparison.OrdinalIgnoreCase))
                { LocalizationSettings.SelectedLocale = locales[i]; return; }
            }
        }

        // Async fetch with caching
        public async Task<string> GetStringAsync(string table, string entry, params object[] args)
        {
            if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(entry)) return string.Empty;
            var locale = LocalizationSettings.SelectedLocale;
            var key = CacheKey(table, entry, locale);
            if (cache.TryGetValue(key, out var cached)) return Format(cached, args);

            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, entry);
            var result = await op.Task;
            if (string.IsNullOrEmpty(result)) result = entry; // fallback to key
            cache[key] = result;
            return Format(result, args);
        }

        // Synchronous best-effort getter (returns key if not cached)
        public string GetStringCached(string table, string entry, params object[] args)
        {
            var locale = LocalizationSettings.SelectedLocale;
            var key = CacheKey(table, entry, locale);
            if (cache.TryGetValue(key, out var cached)) return Format(cached, args);
            return Format(entry, args);
        }

        string Format(string tpl, object[] args)
        {
            if (args == null || args.Length == 0) return tpl;
            try { return string.Format(tpl, args); }
            catch { return tpl; }
        }

        // Pre-warm a list of keys for faster runtime display (parallel)
        public async Task PrewarmKeys(string table, params string[] keys)
        {
            if (keys == null || keys.Length == 0) return;
            var tasks = new Task[keys.Length];
            for (int i = 0; i < keys.Length; i++) tasks[i] = GetStringAsync(table, keys[i]);
            await Task.WhenAll(tasks);
        }
    }
}
