// Assets/Scripts/Localization/LocalizedText.cs
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

namespace Multilingo.Localization
{

    [Serializable]
    public class UnityEventString : UnityEvent<string> { }

    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        [Header("📦 Database Association")]
        [Tooltip("The name of the official Unity Localization String Table (e.g., 'MainMenuStrings').")]
        public string table = "MainMenu";
        
        [Tooltip("The specific key inside that table to retrieve.")]
        public string entryKey;

        [Header("🛠 Formatting & Processing")]
        [Tooltip("Optional variables to inject into your string (e.g. {0}, {1}). Set these here or via script calls.")]
        public string[] formatArgs;

        [Tooltip("When enabled, fetching happens in the background. Highly recommended for performance.")]
        public bool useAsync = true;

        [Header("🔔 Integration Events")]
        [Tooltip("Triggered immediately after the UI text is updated with a new translation.")]
        public UnityEventString OnTextUpdated;
        
        [Tooltip("C# Action version for script-only listeners.")]
        public Action OnTextChange;

        TMP_Text tmp;
        Text ugui;

        // simple versioning to avoid race when multiple async lookups overlap
        int requestVersion;

        void Awake()
        {
            tmp = GetComponent<TMP_Text>();
            ugui = GetComponent<Text>();
            // do NOT refresh here - wait until localization system is ready
        }

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
            // Always attempt refresh on enable.
            // If system isn't ready, Refresh() will handle waiting.
            Refresh();
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
        }

        void LocaleChanged(UnityEngine.Localization.Locale l) => Refresh();

        public void SetKey(string newKey)
        {
            entryKey = newKey;
            Refresh();
        }

        // inspector-friendly. Use this to set string args from other code easily.
        public void SetArgs(params string[] args)
        {
            formatArgs = args;
            Refresh();
        }

        // general-purpose setter for non-string args
        public void SetArgsObjects(params object[] args)
        {
            if (args == null) formatArgs = null;
            else
            {
                formatArgs = new string[args.Length];
                for (int i = 0; i < args.Length; ++i) formatArgs[i] = args[i]?.ToString();
            }
            Refresh();
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(entryKey)) return;

            // If localization still not ready, schedule a delayed refresh
            if (
                !LocalizationSettings.InitializationOperation.IsValid() ||
                !LocalizationSettings.InitializationOperation.IsDone)
            {
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(DelayedRefreshUntilReady());
                }
                return;
            }

            if (useAsync) _ = RefreshAsync(++requestVersion);
            else RefreshSync();
        }

        IEnumerator DelayedRefreshUntilReady()
        {
            while (
                   !LocalizationSettings.InitializationOperation.IsValid() ||
                   !LocalizationSettings.InitializationOperation.IsDone)
                yield return null;
            // ensure one frame for other systems
            yield return new WaitForEndOfFrame();
            Refresh();
        }

        async Task RefreshAsync(int thisRequestVersion)
        {
            object[] argsObj = ConvertStringArgsToObjectArray(formatArgs);

            var mgr = LocalizationManager.Instance;
            if (mgr == null)
            {
                ApplyText(FormatFallback(entryKey, argsObj), thisRequestVersion);
                return;
            }

            try
            {
                var text = await mgr.GetStringAsync(table, entryKey, argsObj);
                ApplyText(text, thisRequestVersion);
            }
            catch (Exception)
            {
                ApplyText(FormatFallback(entryKey, argsObj), thisRequestVersion);
            }
        }

        void RefreshSync()
        {
            object[] argsObj = ConvertStringArgsToObjectArray(formatArgs);

            var mgr = LocalizationManager.Instance;
            if (mgr != null)
            {
                var text = mgr.GetStringCached(table, entryKey, argsObj);
                ApplyText(text, ++requestVersion);
                _ = RefreshAsync(++requestVersion);
            }
            else
            {
                ApplyText(FormatFallback(entryKey, argsObj), ++requestVersion);
            }
        }

        void ApplyText(string value, int thisRequestVersion)
        {
            if (thisRequestVersion != requestVersion) return;

            if (tmp != null)
            {
                tmp.text = value;
                // Force registration with font switcher to ensure correct font is applied to this specific text
                if (LocaleFontSwitcher.Instance != null)
                    LocaleFontSwitcher.Instance.RegisterText(tmp);
            }
            else if (ugui != null)
            {
                ugui.text = value;
            }

            OnTextUpdated?.Invoke(value);
            OnTextChange?.Invoke();
        }

        static object[] ConvertStringArgsToObjectArray(string[] inArgs)
        {
            if (inArgs == null || inArgs.Length == 0) return Array.Empty<object>();
            var outArgs = new object[inArgs.Length];
            for (int i = 0; i < inArgs.Length; ++i) outArgs[i] = inArgs[i];
            return outArgs;
        }

        static string FormatFallback(string template, object[] args)
        {
            if (args == null || args.Length == 0) return template;
            try { return string.Format(template, args); }
            catch { return template; }
        }
    }
}
