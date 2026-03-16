using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Multilingo.Localization.Editor
{
    /// <summary>
    /// Glossary / Terminology Lock system.
    /// Ensures game-specific terms are translated consistently or kept untranslated.
    /// Supports per-language overrides and "Do Not Translate" flags.
    /// </summary>
    [Serializable]
    public class MultilingoGlossary
    {
        [Serializable]
        public class GlossaryEntry
        {
            public string source = "";       // Source term (e.g., "Mana", "HP")
            public string target = "";       // Target translation (e.g., "マナ")
            public string lang = "";         // Target language code (e.g., "ja"). Empty = all languages.
            public bool doNotTranslate;      // If true, keep source term as-is in all languages
        }

        public List<GlossaryEntry> Entries = new List<GlossaryEntry>();

        static string GlossaryFilePath => Path.Combine(Application.temporaryCachePath, "MultilingoGlossary.json");

        // =====================
        // Core API
        // =====================

        /// <summary>
        /// Get the glossary translation for a term in a specific language.
        /// Returns null if no glossary entry exists.
        /// </summary>
        public string GetTranslation(string sourceTerm, string targetLang)
        {
            foreach (var entry in Entries)
            {
                if (!string.Equals(entry.source, sourceTerm, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.doNotTranslate) return entry.source;

                if (string.IsNullOrEmpty(entry.lang) ||
                    string.Equals(entry.lang, targetLang, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.target;
                }
            }
            return null;
        }

        /// <summary>
        /// Add or update a glossary entry.
        /// </summary>
        public void AddEntry(string source, string target, string lang = "", bool doNotTranslate = false)
        {
            // Update existing entry if found
            for (int i = 0; i < Entries.Count; i++)
            {
                if (string.Equals(Entries[i].source, source, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Entries[i].lang, lang, StringComparison.OrdinalIgnoreCase))
                {
                    Entries[i].target = target;
                    Entries[i].doNotTranslate = doNotTranslate;
                    return;
                }
            }

            Entries.Add(new GlossaryEntry
            {
                source = source,
                target = target,
                lang = lang,
                doNotTranslate = doNotTranslate
            });
        }

        public void RemoveEntry(int index)
        {
            if (index >= 0 && index < Entries.Count)
                Entries.RemoveAt(index);
        }

        // =====================
        // Import / Export
        // =====================

        /// <summary>
        /// Import glossary entries from a CSV file.
        /// Expected format: Source,Target,LanguageCode,DoNotTranslate
        /// </summary>
        public void ImportFromCSV(string filePath)
        {
            var data = MultilingoFileParser.ParseCSV(File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            if (data.Count < 2) return;

            for (int r = 1; r < data.Count; r++)
            {
                if (data[r].Count < 1 || string.IsNullOrWhiteSpace(data[r][0])) continue;

                string source = data[r][0];
                string target = data[r].Count > 1 ? data[r][1] : "";
                string lang = data[r].Count > 2 ? data[r][2] : "";
                bool dnt = data[r].Count > 3 && (data[r][3].ToLower() == "true" || data[r][3] == "1");

                AddEntry(source, target, lang, dnt);
            }
        }

        /// <summary>
        /// Export glossary to CSV.
        /// </summary>
        public void ExportToCSV(string filePath)
        {
            var data = new List<List<string>>();
            data.Add(new List<string> { "Source", "Target", "LanguageCode", "DoNotTranslate" });

            foreach (var entry in Entries)
            {
                data.Add(new List<string>
                {
                    entry.source,
                    entry.target,
                    entry.lang,
                    entry.doNotTranslate ? "true" : "false"
                });
            }

            MultilingoFileParser.SaveCSV(filePath, data);
        }

        // =====================
        // Persistence
        // =====================

        [Serializable]
        private class GlossaryData
        {
            public List<GlossaryEntry> entries = new List<GlossaryEntry>();
        }

        public void Save()
        {
            try
            {
                var data = new GlossaryData { entries = Entries };
                File.WriteAllText(GlossaryFilePath, JsonUtility.ToJson(data, true));
            }
            catch { }
        }

        public void Load()
        {
            if (File.Exists(GlossaryFilePath))
            {
                try
                {
                    var data = JsonUtility.FromJson<GlossaryData>(File.ReadAllText(GlossaryFilePath));
                    if (data != null) Entries = data.entries ?? new List<GlossaryEntry>();
                }
                catch { }
            }
        }

        public static MultilingoGlossary LoadFromDisk()
        {
            var glossary = new MultilingoGlossary();
            glossary.Load();
            return glossary;
        }
    }
}
