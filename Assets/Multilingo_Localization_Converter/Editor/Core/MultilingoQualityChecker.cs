using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Multilingo.Localization.Editor
{
    /// <summary>
    /// Post-translation quality checker.
    /// Validates translations for placeholder preservation, tag integrity,
    /// length anomalies, and other common machine translation issues.
    /// </summary>
    public static class MultilingoQualityChecker
    {
        [Serializable]
        public class QualityIssue
        {
            public enum Severity { Warning, Error }

            public string Key;
            public string Language;
            public string Message;
            public Severity Level;

            public QualityIssue(string key, string lang, string message, Severity level = Severity.Warning)
            {
                Key = key;
                Language = lang;
                Message = message;
                Level = level;
            }

            public override string ToString()
            {
                string icon = Level == Severity.Error ? "❌" : "⚠️";
                return $"{icon} [{Language.ToUpper()}] '{Key}': {Message}";
            }
        }

        /// <summary>
        /// Run all quality checks on a translation.
        /// </summary>
        public static List<QualityIssue> Validate(
            string key, string source, string translated, string langCode)
        {
            var issues = new List<QualityIssue>();
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(translated)) return issues;

            CheckPlaceholders(key, source, translated, langCode, issues);
            CheckRichTextTags(key, source, translated, langCode, issues);
            CheckLengthAnomaly(key, source, translated, langCode, issues);
            CheckIdenticalToSource(key, source, translated, langCode, issues);
            CheckLeadingTrailingWhitespace(key, translated, langCode, issues);
            CheckEmptyTranslation(key, source, translated, langCode, issues);

            return issues;
        }

        /// <summary>
        /// Validate all translations in a data table.
        /// data[0] = headers, data[1+] = rows.
        /// sourceCol = index of source language column.
        /// </summary>
        public static List<QualityIssue> ValidateTable(
            List<List<string>> data, int keyCol, int sourceCol, string[] headers)
        {
            var allIssues = new List<QualityIssue>();
            if (data.Count < 2 || headers == null) return allIssues;

            for (int r = 1; r < data.Count; r++)
            {
                if (keyCol >= data[r].Count) continue;
                string key = data[r][keyCol];
                string source = sourceCol < data[r].Count ? data[r][sourceCol] : "";

                for (int c = 0; c < headers.Length; c++)
                {
                    if (c == keyCol || c == sourceCol) continue;
                    if (c >= data[r].Count) continue;

                    string translated = data[r][c];
                    string lang = ExtractLangCode(headers[c]);

                    allIssues.AddRange(Validate(key, source, translated, lang));
                }
            }

            return allIssues;
        }

        // =====================
        // Individual Checks
        // =====================

        static void CheckPlaceholders(string key, string source, string translated, string lang, List<QualityIssue> issues)
        {
            var sourcePlaceholders = Regex.Matches(source, @"\{(\d+)\}");
            foreach (Match m in sourcePlaceholders)
            {
                if (!translated.Contains(m.Value))
                {
                    issues.Add(new QualityIssue(key, lang,
                        $"Missing placeholder {m.Value} in translation",
                        QualityIssue.Severity.Error));
                }
            }
        }

        static void CheckRichTextTags(string key, string source, string translated, string lang, List<QualityIssue> issues)
        {
            var sourceOpenTags = Regex.Matches(source, @"<([a-zA-Z]+)[^>]*>");
            var sourceCloseTags = Regex.Matches(source, @"</([a-zA-Z]+)>");

            foreach (Match m in sourceOpenTags)
            {
                string tagName = m.Groups[1].Value.ToLower();
                if (!Regex.IsMatch(translated, $@"<{tagName}[^>]*>", RegexOptions.IgnoreCase))
                {
                    issues.Add(new QualityIssue(key, lang,
                        $"Missing opening tag <{tagName}> in translation",
                        QualityIssue.Severity.Error));
                }
            }

            foreach (Match m in sourceCloseTags)
            {
                string tagName = m.Groups[1].Value.ToLower();
                if (!translated.Contains($"</{tagName}>"))
                {
                    issues.Add(new QualityIssue(key, lang,
                        $"Missing closing tag </{tagName}> in translation",
                        QualityIssue.Severity.Error));
                }
            }
        }

        static void CheckLengthAnomaly(string key, string source, string translated, string lang, List<QualityIssue> issues)
        {
            if (source.Length < 3) return; // Skip very short strings

            float ratio = (float)translated.Length / source.Length;

            // CJK languages can be much shorter
            bool isCJK = lang.StartsWith("zh") || lang.StartsWith("ja") || lang.StartsWith("ko");
            float minRatio = isCJK ? 0.15f : 0.25f;
            float maxRatio = isCJK ? 2.5f : 3.5f;

            if (ratio < minRatio)
            {
                issues.Add(new QualityIssue(key, lang,
                    $"Translation suspiciously short ({ratio:F1}x source length)"));
            }
            else if (ratio > maxRatio)
            {
                issues.Add(new QualityIssue(key, lang,
                    $"Translation suspiciously long ({ratio:F1}x source length)"));
            }
        }

        static void CheckIdenticalToSource(string key, string source, string translated, string lang, List<QualityIssue> issues)
        {
            // Skip if source is a number, single word proper noun, or very short
            if (source.Length <= 2) return;
            if (Regex.IsMatch(source, @"^\d+$")) return; // Pure number

            if (string.Equals(source, translated, StringComparison.Ordinal) && source.Length > 3)
            {
                issues.Add(new QualityIssue(key, lang,
                    "Translation is identical to source — possibly untranslated"));
            }
        }

        static void CheckLeadingTrailingWhitespace(string key, string translated, string lang, List<QualityIssue> issues)
        {
            if (translated.StartsWith(" ") || translated.EndsWith(" ") || translated.EndsWith("\n"))
            {
                issues.Add(new QualityIssue(key, lang,
                    "Translation has leading/trailing whitespace"));
            }
        }

        static void CheckEmptyTranslation(string key, string source, string translated, string lang, List<QualityIssue> issues)
        {
            if (!string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(translated))
            {
                issues.Add(new QualityIssue(key, lang,
                    "Translation is empty",
                    QualityIssue.Severity.Error));
            }
        }

        // =====================
        // Helpers
        // =====================

        static string ExtractLangCode(string header)
        {
            // Try to extract lang code from header like "Japanese(ja)" or "ja"
            var match = Regex.Match(header, @"\(([a-z]{2}(?:-[A-Z]{2})?)\)");
            if (match.Success) return match.Groups[1].Value;

            // If header is itself a lang code
            if (Regex.IsMatch(header.Trim(), @"^[a-z]{2}(-[A-Z]{2})?$"))
                return header.Trim();

            return header;
        }
    }
}
