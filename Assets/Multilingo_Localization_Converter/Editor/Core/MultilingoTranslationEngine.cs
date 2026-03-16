using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Multilingo.Localization.Editor
{
    /// <summary>
    /// Centralized translation engine with:
    /// - Multi-provider support (Google Free, Google Cloud, DeepL, OpenAI)
    /// - Batch bundling (20-50x fewer API calls)
    /// - Concurrent translation with semaphore throttling
    /// - Exponential backoff retry logic
    /// - Translation context injection
    /// - Glossary enforcement
    /// </summary>
    public class MultilingoTranslationEngine
    {
        public enum TranslationProvider { GoogleFree, GoogleCloud, DeepL, OpenAI }

        // Configuration
        public TranslationProvider Provider { get; set; } = TranslationProvider.GoogleFree;
        public string GoogleCloudApiKey { get; set; } = "";
        public string DeepLApiKey { get; set; } = "";
        public string OpenAiApiKey { get; set; } = "";
        public string OpenAiModel { get; set; } = "gpt-4o-mini";

        // Batch settings
        public int BatchSize { get; set; } = 25;
        public int MaxConcurrency { get; set; } = 4;
        public int MaxRetries { get; set; } = 3;

        // Context
        public string ProjectName { get; set; } = "";
        public MultilingoGlossary Glossary { get; set; }

        // Callbacks
        public Action<float, string> OnProgress;
        public bool CancelRequested { get; set; }

        // Statistics
        public int TotalRequested { get; private set; }
        public int CacheHits { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float CurrentEta { get; private set; }

        private MultilingoCache _cache;

        public MultilingoTranslationEngine(MultilingoCache cache)
        {
            _cache = cache ?? new MultilingoCache();
        }

        public void ResetStats()
        {
            TotalRequested = 0;
            CacheHits = 0;
            CurrentSpeed = 0;
            CurrentEta = 0;
        }

        // =====================================================
        // PUBLIC API: Translate a batch of texts to one language
        // =====================================================

        /// <summary>
        /// Translate multiple source texts to a target language.
        /// Uses batching, caching, concurrency, and retry logic automatically.
        /// </summary>
        /// <param name="texts">Source texts to translate</param>
        /// <param name="sourceLang">Source language code ("auto" for auto-detect)</param>
        /// <param name="targetLang">Target language code</param>
        /// <param name="contexts">Optional context strings per text (same length as texts, or null)</param>
        /// <returns>Translated texts in the same order</returns>
        public async Task<string[]> TranslateBatchAsync(
            string[] texts, string sourceLang, string targetLang, string[] contexts = null)
        {
            var results = new string[texts.Length];
            var uncachedIndices = new List<int>();

            // Step 1: Check cache (exact + fuzzy)
            for (int i = 0; i < texts.Length; i++)
            {
                TotalRequested++;
                string cacheKey = $"{texts[i]}_{targetLang}";

                if (_cache.TryGet(cacheKey, out string cached))
                {
                    results[i] = cached;
                    CacheHits++;
                }
                else
                {
                    // Try fuzzy match
                    string fuzzy = _cache.FindFuzzyMatch(texts[i], targetLang);
                    if (fuzzy != null)
                    {
                        results[i] = fuzzy;
                        CacheHits++;
                    }
                    else
                    {
                        uncachedIndices.Add(i);
                    }
                }
            }

            if (uncachedIndices.Count == 0) return results;

            // Step 2: Translate uncached texts using batched API calls
            DateTime startTime = DateTime.UtcNow;
            int completed = 0;

            if (Provider == TranslationProvider.OpenAI && !string.IsNullOrEmpty(OpenAiApiKey))
            {
                // OpenAI supports batch bundling — send multiple texts per API call
                var batches = SplitIntoBatches(uncachedIndices, BatchSize);
                var semaphore = new SemaphoreSlim(MaxConcurrency);
                var tasks = new List<Task>();

                foreach (var batch in batches)
                {
                    if (CancelRequested) break;
                    await semaphore.WaitAsync();

                    var batchCopy = batch.ToList();
                    Func<Task> processTask = async () =>
                    {
                        try
                        {
                            var batchTexts = batchCopy.Select(idx => texts[idx]).ToArray();
                            var batchContexts = contexts != null
                                ? batchCopy.Select(idx => idx < contexts.Length ? contexts[idx] : "").ToArray()
                                : null;

                            var translated = await TranslateBatchOpenAI(batchTexts, sourceLang, targetLang, batchContexts);
                            for (int i = 0; i < batchCopy.Count && i < translated.Length; i++)
                            {
                                int origIdx = batchCopy[i];
                                results[origIdx] = translated[i];
                                _cache.Set($"{texts[origIdx]}_{targetLang}", translated[i]);
                            }

                            Interlocked.Add(ref completed, batchCopy.Count);
                            UpdateStats(completed, uncachedIndices.Count, startTime);
                        }
                        finally { semaphore.Release(); }
                    };
                    tasks.Add(processTask());
                }
                await Task.WhenAll(tasks);
            }
            else
            {
                // For Google/DeepL: concurrent individual or small-batch requests
                var semaphore = new SemaphoreSlim(MaxConcurrency);
                var tasks = new List<Task>();

                foreach (int idx in uncachedIndices)
                {
                    if (CancelRequested) break;
                    await semaphore.WaitAsync();

                    int capturedIdx = idx;
                    Func<Task> processTask = async () =>
                    {
                        try
                        {
                            string ctx = contexts != null && capturedIdx < contexts.Length ? contexts[capturedIdx] : null;
                            string translated = await TranslateSingleWithRetry(texts[capturedIdx], sourceLang, targetLang, ctx);
                            results[capturedIdx] = translated;
                            _cache.Set($"{texts[capturedIdx]}_{targetLang}", translated);

                            Interlocked.Increment(ref completed);
                            UpdateStats(completed, uncachedIndices.Count, startTime);
                        }
                        finally { semaphore.Release(); }
                    };
                    tasks.Add(processTask());
                }
                await Task.WhenAll(tasks);
            }

            _cache.Save();

            // Fill any remaining nulls with original text
            for (int i = 0; i < results.Length; i++)
                if (results[i] == null) results[i] = texts[i];

            return results;
        }

        /// <summary>
        /// Translate a single text with retry and exponential backoff.
        /// </summary>
        public async Task<string> TranslateSingleWithRetry(
            string text, string sourceLang, string targetLang, string context = null)
        {
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                try
                {
                    string result = await TranslateSingleAsync(text, sourceLang, targetLang, context);
                    return result;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxRetries - 1)
                    {
                        Debug.LogWarning($"[Multilingo] Translation failed after {MaxRetries} retries: {ex.Message}");
                        return text;
                    }
                    int delay = (int)(Math.Pow(2, attempt) * 1000); // 1s, 2s, 4s
                    await Task.Delay(delay);
                }
            }
            return text;
        }

        // =====================================================
        // PRIVATE: Provider-specific translation
        // =====================================================

        async Task<string> TranslateSingleAsync(string text, string sourceLang, string targetLang, string context)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            switch (Provider)
            {
                case TranslationProvider.DeepL: return await TranslateDeepLAsync(text, targetLang);
                case TranslationProvider.OpenAI: return await TranslateOpenAISingle(text, sourceLang, targetLang, context);
                case TranslationProvider.GoogleCloud: return await TranslateGoogleCloudAsync(text, targetLang);
                default: return await TranslateGoogleFreeAsync(text, sourceLang, targetLang);
            }
        }

        /// <summary>
        /// OpenAI batch: send multiple strings in one API call.
        /// Dramatically reduces API calls (20-50x fewer).
        /// </summary>
        async Task<string[]> TranslateBatchOpenAI(string[] texts, string sourceLang, string targetLang, string[] contexts)
        {
            if (string.IsNullOrEmpty(OpenAiApiKey)) return texts;

            string glossaryInstruction = BuildGlossaryInstruction(targetLang);
            string contextBlock = "";
            if (contexts != null && contexts.Any(c => !string.IsNullOrEmpty(c)))
            {
                contextBlock = "\nContext for each line:\n";
                for (int i = 0; i < contexts.Length; i++)
                {
                    if (!string.IsNullOrEmpty(contexts[i]))
                        contextBlock += $"  Line {i + 1}: {contexts[i]}\n";
                }
            }

            var sb = new StringBuilder();
            for (int i = 0; i < texts.Length; i++)
                sb.AppendLine($"{i + 1}. {texts[i]}");

            string systemPrompt = $"You are a professional game translator." +
                (string.IsNullOrEmpty(ProjectName) ? "" : $" The game is called \"{ProjectName}\".") +
                $"\nTranslate each numbered line below to {targetLang}." +
                $"\nReturn ONLY the translations, numbered exactly the same way." +
                $"\nPreserve any formatting placeholders like {{0}}, {{1}}, <b>, <color>, etc." +
                $"\nDo not add explanations or notes." +
                glossaryInstruction + contextBlock;

            string jsonBody = BuildOpenAIRequest(systemPrompt, sb.ToString());

            try
            {
                string response = await SendOpenAIRequest(jsonBody);
                return ParseBatchOpenAIResponse(response, texts.Length, texts);
            }
            catch
            {
                // Fallback: translate individually
                var results = new string[texts.Length];
                for (int i = 0; i < texts.Length; i++)
                    results[i] = await TranslateOpenAISingle(texts[i], sourceLang, targetLang, contexts?[i]);
                return results;
            }
        }

        async Task<string> TranslateOpenAISingle(string text, string sourceLang, string targetLang, string context)
        {
            if (string.IsNullOrEmpty(OpenAiApiKey)) return text;

            string glossaryInstruction = BuildGlossaryInstruction(targetLang);
            string contextPart = string.IsNullOrEmpty(context) ? "" : $"\nContext: {context}";

            string systemPrompt = $"You are a professional game translator." +
                (string.IsNullOrEmpty(ProjectName) ? "" : $" The game is called \"{ProjectName}\".") +
                $"\nTranslate the given text directly into {targetLang} without any additional commentary." +
                $"\nPreserve any formatting placeholders (like {{0}})." +
                glossaryInstruction + contextPart;

            string jsonBody = BuildOpenAIRequest(systemPrompt, text);
            try
            {
                string response = await SendOpenAIRequest(jsonBody);
                var match = Regex.Match(response, @"""content"":\s*""(.*?)""");
                if (match.Success) return Regex.Unescape(match.Groups[1].Value);
            }
            catch (Exception e) { Debug.LogWarning($"[Multilingo] OpenAI error: {e.Message}"); }
            return text;
        }

        async Task<string> TranslateDeepLAsync(string text, string targetLang)
        {
            if (string.IsNullOrEmpty(DeepLApiKey)) return text;
            string deepLLang = targetLang.ToUpper();
            if (deepLLang.StartsWith("ZH")) deepLLang = "ZH";
            if (deepLLang.StartsWith("EN")) deepLLang = "EN-US";

            string endpoint = DeepLApiKey.EndsWith(":fx")
                ? "https://api-free.deepl.com/v2/translate"
                : "https://api.deepl.com/v2/translate";

            WWWForm form = new WWWForm();
            form.AddField("text", text);
            form.AddField("target_lang", deepLLang);

            using (UnityWebRequest req = UnityWebRequest.Post(endpoint, form))
            {
                req.SetRequestHeader("Authorization", "DeepL-Auth-Key " + DeepLApiKey);
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var match = Regex.Match(req.downloadHandler.text, @"""text"":""(.*?)""");
                    if (match.Success) return Regex.Unescape(match.Groups[1].Value);
                }
                else throw new Exception($"DeepL {req.responseCode}: {req.error}");
            }
            return text;
        }

        async Task<string> TranslateGoogleCloudAsync(string text, string targetLang)
        {
            if (string.IsNullOrEmpty(GoogleCloudApiKey)) return text;
            string url = $"https://translation.googleapis.com/language/translate/v2?key={GoogleCloudApiKey}";
            string jsonBody = $"{{\"q\": \"{text.Replace("\"", "\\\"").Replace("\n", "\\n")}\", \"target\": \"{targetLang}\", \"format\": \"text\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var match = Regex.Match(req.downloadHandler.text, @"""translatedText"":\s*""(.*?)""");
                    if (match.Success) return Regex.Unescape(match.Groups[1].Value);
                }
                else throw new Exception($"Google Cloud {req.responseCode}: {req.error}");
            }
            return text;
        }

        async Task<string> TranslateGoogleFreeAsync(string text, string sourceLang, string targetLang)
        {
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={UnityWebRequest.EscapeURL(text)}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    return ExtractGoogleFreeTranslation(req.downloadHandler.text, text);
                }
                else throw new Exception($"Google Free {req.responseCode}: {req.error}");
            }
        }

        // =====================================================
        // PRIVATE: Helpers
        // =====================================================

        string BuildGlossaryInstruction(string targetLang)
        {
            if (Glossary == null || Glossary.Entries.Count == 0) return "";

            var sb = new StringBuilder("\n\nIMPORTANT GLOSSARY - Use these exact translations:");
            foreach (var entry in Glossary.Entries)
            {
                if (entry.doNotTranslate)
                    sb.Append($"\n  \"{entry.source}\" → keep as \"{entry.source}\" (do NOT translate)");
                else if (string.Equals(entry.lang, targetLang, StringComparison.OrdinalIgnoreCase) ||
                         string.IsNullOrEmpty(entry.lang))
                    sb.Append($"\n  \"{entry.source}\" → \"{entry.target}\"");
            }
            return sb.ToString();
        }

        string BuildOpenAIRequest(string systemPrompt, string userContent)
        {
            string escSystem = systemPrompt.Replace("\"", "\\\"").Replace("\n", "\\n");
            string escUser = userContent.Replace("\"", "\\\"").Replace("\n", "\\n");
            return "{\"model\": \"" + OpenAiModel + "\", \"messages\": [" +
                   "{\"role\": \"system\", \"content\": \"" + escSystem + "\"}, " +
                   "{\"role\": \"user\", \"content\": \"" + escUser + "\"}]}";
        }

        async Task<string> SendOpenAIRequest(string jsonBody)
        {
            using (UnityWebRequest req = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + OpenAiApiKey);
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                    return req.downloadHandler.text;
                else
                    throw new Exception($"OpenAI {req.responseCode}: {req.downloadHandler.text}");
            }
        }

        string[] ParseBatchOpenAIResponse(string response, int expectedCount, string[] originals)
        {
            var results = new string[expectedCount];
            var match = Regex.Match(response, @"""content"":\s*""(.*?)""(?=\s*[,}])");
            if (!match.Success)
            {
                Array.Copy(originals, results, expectedCount);
                return results;
            }

            string content = Regex.Unescape(match.Groups[1].Value);
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            int parsed = 0;
            foreach (var line in lines)
            {
                if (parsed >= expectedCount) break;
                // Remove numbering: "1. translated text" → "translated text"
                string cleaned = Regex.Replace(line.Trim(), @"^\d+\.\s*", "");
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    results[parsed] = cleaned;
                    parsed++;
                }
            }

            // Fill remaining with originals
            for (int i = parsed; i < expectedCount; i++)
                results[i] = originals[i];

            return results;
        }

        string ExtractGoogleFreeTranslation(string json, string originalText)
        {
            try
            {
                StringBuilder fullTranslation = new StringBuilder();
                int endOfFirstArray = json.IndexOf(",null,");
                if (endOfFirstArray == -1) endOfFirstArray = json.Length;
                string arrayData = json.Substring(0, endOfFirstArray);

                MatchCollection matches = Regex.Matches(arrayData, @"\[""((?:[^""\\]|\\.)*)""," + @"""(?:[^""\\]|\\.)*""");
                if (matches.Count > 0)
                {
                    foreach (Match m in matches) fullTranslation.Append(m.Groups[1].Value);
                    return Regex.Unescape(fullTranslation.ToString());
                }
                return originalText;
            }
            catch { return originalText; }
        }

        List<List<int>> SplitIntoBatches(List<int> indices, int batchSize)
        {
            var batches = new List<List<int>>();
            for (int i = 0; i < indices.Count; i += batchSize)
            {
                batches.Add(indices.GetRange(i, Math.Min(batchSize, indices.Count - i)));
            }
            return batches;
        }

        void UpdateStats(int completed, int total, DateTime startTime)
        {
            float elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            CurrentSpeed = elapsed > 0 ? completed / elapsed : 0;
            CurrentEta = CurrentSpeed > 0 ? (total - completed) / CurrentSpeed : 0;
            OnProgress?.Invoke((float)completed / total, $"Translating... ({completed}/{total})");
        }
    }
}
