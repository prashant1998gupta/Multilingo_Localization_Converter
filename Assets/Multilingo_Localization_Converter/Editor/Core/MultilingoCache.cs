using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Multilingo.Localization.Editor
{
    /// <summary>
    /// Translation cache with:
    /// - Exact match lookup
    /// - Fuzzy matching (Levenshtein distance) for similar strings
    /// - Persistent storage to disk
    /// - Source text hash tracking for stale detection
    /// </summary>
    public class MultilingoCache
    {
        private Dictionary<string, string> _cache = new Dictionary<string, string>();
        private Dictionary<string, string> _sourceHashes = new Dictionary<string, string>();
        private string _cacheFilePath;
        private string _hashFilePath;

        public int Count => _cache.Count;

        public MultilingoCache(string cacheDir = null)
        {
            string dir = cacheDir ?? Application.temporaryCachePath;
            _cacheFilePath = Path.Combine(dir, "MultilingoCache.json");
            _hashFilePath = Path.Combine(dir, "MultilingoSourceHashes.json");
            Load();
        }

        // =====================
        // Core Operations
        // =====================

        public bool TryGet(string key, out string value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void Set(string key, string value)
        {
            _cache[key] = value;
        }

        /// <summary>
        /// Store a source text hash for stale detection.
        /// Key format: "entryKey_langCode", hash = MD5 of source text.
        /// </summary>
        public void SetSourceHash(string entryKey, string sourceText)
        {
            _sourceHashes[entryKey] = ComputeHash(sourceText);
        }

        /// <summary>
        /// Check if a translation is stale (source text changed since last translation).
        /// </summary>
        public bool IsStale(string entryKey, string currentSourceText)
        {
            if (!_sourceHashes.TryGetValue(entryKey, out string storedHash)) return false;
            return storedHash != ComputeHash(currentSourceText);
        }

        /// <summary>
        /// Find a fuzzy-matched cached translation for similar source text.
        /// Returns null if no match above threshold is found.
        /// </summary>
        public string FindFuzzyMatch(string text, string langCode, float threshold = 0.85f)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 3) return null;

            string suffix = $"_{langCode}";
            string bestValue = null;
            float bestSimilarity = threshold;

            foreach (var kvp in _cache)
            {
                if (!kvp.Key.EndsWith(suffix)) continue;
                string cachedSource = kvp.Key.Substring(0, kvp.Key.Length - suffix.Length);

                // Quick length check to avoid expensive Levenshtein on very different strings
                float lengthRatio = (float)Math.Min(text.Length, cachedSource.Length) /
                                    Math.Max(text.Length, cachedSource.Length);
                if (lengthRatio < threshold) continue;

                float similarity = ComputeSimilarity(text, cachedSource);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestValue = kvp.Value;
                }
            }

            return bestValue;
        }

        public void Clear()
        {
            _cache.Clear();
            _sourceHashes.Clear();
        }

        // =====================
        // Persistence
        // =====================

        public void Save()
        {
            try
            {
                CacheData data = new CacheData();
                foreach (var kv in _cache)
                {
                    data.keys.Add(kv.Key);
                    data.values.Add(kv.Value);
                }
                File.WriteAllText(_cacheFilePath, JsonUtility.ToJson(data));

                // Save source hashes
                CacheData hashData = new CacheData();
                foreach (var kv in _sourceHashes)
                {
                    hashData.keys.Add(kv.Key);
                    hashData.values.Add(kv.Value);
                }
                File.WriteAllText(_hashFilePath, JsonUtility.ToJson(hashData));
            }
            catch { }
        }

        public void Load()
        {
            _cache.Clear();
            _sourceHashes.Clear();

            if (File.Exists(_cacheFilePath))
            {
                try
                {
                    CacheData data = JsonUtility.FromJson<CacheData>(File.ReadAllText(_cacheFilePath));
                    if (data != null)
                    {
                        for (int i = 0; i < data.keys.Count && i < data.values.Count; i++)
                            _cache[data.keys[i]] = data.values[i];
                    }
                }
                catch { }
            }

            if (File.Exists(_hashFilePath))
            {
                try
                {
                    CacheData data = JsonUtility.FromJson<CacheData>(File.ReadAllText(_hashFilePath));
                    if (data != null)
                    {
                        for (int i = 0; i < data.keys.Count && i < data.values.Count; i++)
                            _sourceHashes[data.keys[i]] = data.values[i];
                    }
                }
                catch { }
            }
        }

        // =====================
        // Levenshtein Similarity
        // =====================

        static float ComputeSimilarity(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal)) return 1f;
            int dist = LevenshteinDistance(a, b);
            return 1f - (float)dist / Math.Max(a.Length, b.Length);
        }

        static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            // Use two-row optimization to reduce memory
            var prev = new int[m + 1];
            var curr = new int[m + 1];

            for (int j = 0; j <= m; j++) prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                var tmp = prev; prev = curr; curr = tmp;
            }
            return prev[m];
        }

        static string ComputeHash(string text)
        {
            // Simple hash for change detection
            int hash = 17;
            unchecked
            {
                foreach (char c in text)
                    hash = hash * 31 + c;
            }
            return hash.ToString("X8");
        }

        [Serializable]
        class CacheData
        {
            public List<string> keys = new List<string>();
            public List<string> values = new List<string>();
        }
    }
}
