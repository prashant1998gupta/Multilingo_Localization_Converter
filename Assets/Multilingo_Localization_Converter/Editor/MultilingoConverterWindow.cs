using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Text;
using System.Linq;
using System.Xml;

namespace Multilingo.Localization.Editor
{
    public class MultilingoConverterWindow : EditorWindow
    {
        private string inputFilePath = "";
        private List<string> batchInputFiles = new List<string>();
        private bool isBatchMode = false;
        private bool isConverterMode = false;  // false = Translator, true = Converter
        private int charLimit = 0;  // 0 = no limit
        private bool showValidation = false;

        private List<List<string>> tableData = new List<List<string>>();
        private string[] tableHeaders = new string[0];
        private int selectedSourceColumnIndex = 0;
        private int detectedContextColumn = -1; // Auto-detected context/notes column
        
        private Vector2 scrollPos;
        private Vector2 langScrollPos;
        private string languageSearch = "";

        private bool isProcessing = false;
        private float progress = 0f;
        private string progressStatus = "";
        private float currentSpeed = 0f;
        private float currentEta = 0f;
        private int cacheHits = 0;
        private int totalRequested = 0;
        
        // Progress explicit tracking
        private float currentFileProgress = 0f;
        private string currentFileProgressStatus = "";
        private int batchCurrentFileIndex = 0;
        private bool cancelRequest = false;

        // Quality checker results
        private List<MultilingoQualityChecker.QualityIssue> qualityIssues = new List<MultilingoQualityChecker.QualityIssue>();
        private bool showQualityReport = false;

        public enum OutputFormat { SameAsInput, CSV, XLSX, JSON, XML, YAML }
        private OutputFormat selectedOutputFormat = OutputFormat.SameAsInput;

        public enum TranslationProvider { GoogleFree, GoogleCloud, DeepL, OpenAI }
        private TranslationProvider currentProvider = TranslationProvider.GoogleFree;
        private string googleCloudApiKey = "";
        private string deeplApiKey = "";
        private string openAiApiKey = "";
        private string openAiModel = "gpt-4o-mini";

        // Advanced Core Systems
        private MultilingoCache _cache;
        private MultilingoTranslationEngine _engine;
        private MultilingoGlossary _glossary;

        // Detected languages in the current file
        private HashSet<string> autoDetectedLanguages = new HashSet<string>();

        public static readonly Dictionary<string, string> SupportedLanguages = new Dictionary<string, string>
        {
            {"Afrikaans", "af"}, {"Albanian", "sq"}, {"Amharic", "am"}, {"Arabic", "ar"}, {"Armenian", "hy"}, 
            {"Azerbaijani", "az"}, {"Basque", "eu"}, {"Belarusian", "be"}, {"Bengali", "bn"}, {"Bosnian", "bs"}, 
            {"Bulgarian", "bg"}, {"Catalan", "ca"}, {"Cebuano", "ceb"}, {"Chichewa", "ny"}, {"Chinese (Simplified)", "zh-CN"}, 
            {"Chinese (Traditional)", "zh-TW"}, {"Corsican", "co"}, {"Croatian", "hr"}, {"Czech", "cs"}, {"Danish", "da"}, 
            {"Dutch", "nl"}, {"English", "en"}, {"Esperanto", "eo"}, {"Estonian", "et"}, {"Filipino", "tl"}, {"Finnish", "fi"}, 
            {"French", "fr"}, {"Frisian", "fy"}, {"Galician", "gl"}, {"Georgian", "ka"}, {"German", "de"}, {"Greek", "el"}, 
            {"Gujarati", "gu"}, {"Haitian Creole", "ht"}, {"Hausa", "ha"}, {"Hawaiian", "haw"}, {"Hebrew", "iw"}, {"Hindi", "hi"}, 
            {"Hmong", "hmn"}, {"Hungarian", "hu"}, {"Icelandic", "is"}, {"Igbo", "ig"}, {"Indonesian", "id"}, {"Irish", "ga"}, 
            {"Italian", "it"}, {"Japanese", "ja"}, {"Javanese", "jw"}, {"Kannada", "kn"}, {"Kazakh", "kk"}, {"Khmer", "km"}, 
            {"Kinyarwanda", "rw"}, {"Korean", "ko"}, {"Kurdish (Kurmanji)", "ku"}, {"Kyrgyz", "ky"}, {"Lao", "lo"}, {"Latin", "la"}, 
            {"Latvian", "lv"}, {"Lithuanian", "lt"}, {"Luxembourgish", "lb"}, {"Macedonian", "mk"}, {"Malagasy", "mg"}, 
            {"Malay", "ms"}, {"Malayalam", "ml"}, {"Maltese", "mt"}, {"Maori", "mi"}, {"Marathi", "mr"}, {"Mongolian", "mn"}, 
            {"Myanmar (Burmese)", "my"}, {"Nepali", "ne"}, {"Norwegian", "no"}, {"Odia (Oriya)", "or"}, {"Pashto", "ps"}, 
            {"Persian", "fa"}, {"Polish", "pl"}, {"Portuguese", "pt"}, {"Punjabi", "pa"}, {"Romanian", "ro"}, {"Russian", "ru"}, 
            {"Samoan", "sm"}, {"Scots Gaelic", "gd"}, {"Serbian", "sr"}, {"Sesotho", "st"}, {"Shona", "sn"}, {"Sindhi", "sd"}, 
            {"Sinhala", "si"}, {"Slovak", "sk"}, {"Slovenian", "sl"}, {"Somali", "so"}, {"Spanish", "es"}, {"Sundanese", "su"}, 
            {"Swahili", "sw"}, {"Swedish", "sv"}, {"Tajik", "tg"}, {"Tamil", "ta"}, {"Tatar", "tt"}, {"Telugu", "te"}, 
            {"Thai", "th"}, {"Turkish", "tr"}, {"Turkmen", "tk"}, {"Ukrainian", "uk"}, {"Urdu", "ur"}, {"Uyghur", "ug"}, 
            {"Uzbek", "uz"}, {"Vietnamese", "vi"}, {"Welsh", "cy"}, {"Xhosa", "xh"}, {"Yiddish", "yi"}, {"Yoruba", "yo"}, {"Zulu", "zu"}
        };

        private string GetLanguageLabel(string langName)
        {
            string code = SupportedLanguages[langName];
            return $"{langName}  •  {code}";
        }

        private Dictionary<string, bool> selectedLanguages = new Dictionary<string, bool>();

        [MenuItem("Tools/Multilingo/Localization Converter")]
        public static void ShowWindow()
        {
            var window = GetWindow<MultilingoConverterWindow>("Multilingo Pro");
            window.minSize = new Vector2(600, 750);
            window.InitializeLanguages();
            window.InitCoreSystems();
            window.Show();
        }

        private void InitializeLanguages()
        {
            if (selectedLanguages.Count == 0)
            {
                foreach (var lang in SupportedLanguages.Keys)
                {
                    selectedLanguages[lang] = false;
                }
            }
        }

        void OnEnable()
        {
            InitializeLanguages();
            InitCoreSystems();
            LoadPrefs();
            // Reset cached styles (they become invalid after domain reload)
            _headerStyle = _subHeaderStyle = _sectionTitleStyle = _dropAreaStyle = _primaryButtonStyle = null;
        }

        private void InitCoreSystems()
        {
            if (_cache == null) _cache = new MultilingoCache();
            if (_glossary == null) { _glossary = MultilingoGlossary.LoadFromDisk(); }
            if (_engine == null)
            {
                _engine = new MultilingoTranslationEngine(_cache);
                _engine.Glossary = _glossary;
            }
        }

        void OnDisable()
        {
            SavePrefs();
        }

        private void LoadPrefs()
        {
            isBatchMode = EditorPrefs.GetBool("Multilingo_IsBatchMode", false);
            isConverterMode = EditorPrefs.GetBool("Multilingo_IsConverterMode", false);
            selectedOutputFormat = (OutputFormat)EditorPrefs.GetInt("Multilingo_OutputFormat", 0);
            charLimit = EditorPrefs.GetInt("Multilingo_CharLimit", 0);
            
            currentProvider = (TranslationProvider)EditorPrefs.GetInt("Multilingo_Provider", 0);
            googleCloudApiKey = EditorPrefs.GetString("Multilingo_GoogleApiKey", "");
            deeplApiKey = EditorPrefs.GetString("Multilingo_DeeplApiKey", "");
            openAiApiKey = EditorPrefs.GetString("Multilingo_OpenAiApiKey", "");
            openAiModel = EditorPrefs.GetString("Multilingo_OpenAiModel", "gpt-4o-mini");

            string savedLangs = EditorPrefs.GetString("Multilingo_SelectedLangs", "");
            if (!string.IsNullOrEmpty(savedLangs))
            {
                var langs = savedLangs.Split(',');
                foreach (var l in langs)
                {
                    if (selectedLanguages.ContainsKey(l))
                        selectedLanguages[l] = true;
                }
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetBool("Multilingo_IsBatchMode", isBatchMode);
            EditorPrefs.SetBool("Multilingo_IsConverterMode", isConverterMode);
            EditorPrefs.SetInt("Multilingo_OutputFormat", (int)selectedOutputFormat);
            EditorPrefs.SetInt("Multilingo_CharLimit", charLimit);

            EditorPrefs.SetInt("Multilingo_Provider", (int)currentProvider);
            EditorPrefs.SetString("Multilingo_GoogleApiKey", googleCloudApiKey);
            EditorPrefs.SetString("Multilingo_DeeplApiKey", deeplApiKey);
            EditorPrefs.SetString("Multilingo_OpenAiApiKey", openAiApiKey);
            EditorPrefs.SetString("Multilingo_OpenAiModel", openAiModel);

            var activeLangs = selectedLanguages.Where(k => k.Value).Select(k => k.Key);
            EditorPrefs.SetString("Multilingo_SelectedLangs", string.Join(",", activeLangs));
        }

        // Custom Styles (cached to avoid re-creation every OnGUI call)
        private GUIStyle _headerStyle, _subHeaderStyle, _sectionTitleStyle, _dropAreaStyle, _primaryButtonStyle;

        private GUIStyle HeaderStyle { get {
            if (_headerStyle == null) { _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 28, alignment = TextAnchor.MiddleCenter, margin = new RectOffset(0, 0, 15, 5) }; _headerStyle.normal.textColor = new Color(0.6f, 0.5f, 1f); }
            return _headerStyle;
        }}
        private GUIStyle SubHeaderStyle { get {
            if (_subHeaderStyle == null) { _subHeaderStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, margin = new RectOffset(0, 0, 0, 15) }; _subHeaderStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f); }
            return _subHeaderStyle;
        }}
        private GUIStyle SectionTitleStyle { get {
            if (_sectionTitleStyle == null) _sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, margin = new RectOffset(5, 5, 5, 10) };
            return _sectionTitleStyle;
        }}
        private GUIStyle DropAreaStyle { get {
            if (_dropAreaStyle == null) { _dropAreaStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold }; _dropAreaStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f); }
            return _dropAreaStyle;
        }}

        // Texture cache to prevent creating new Texture2D objects every frame (causes black windows)
        private static Dictionary<Color, Texture2D> _texCache = new Dictionary<Color, Texture2D>();
        private Texture2D MakeTex(int width, int height, Color col)
        {
            if (_texCache.TryGetValue(col, out Texture2D cached) && cached != null)
                return cached;
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.hideFlags = HideFlags.HideAndDontSave;
            result.SetPixels(pix);
            result.Apply();
            _texCache[col] = result;
            return result;
        }

        private GUIStyle PrimaryButtonStyle { get {
            if (_primaryButtonStyle == null)
            {
                _primaryButtonStyle = new GUIStyle(GUI.skin.button);
                _primaryButtonStyle.fontSize = 15;
                _primaryButtonStyle.fontStyle = FontStyle.Bold;
                _primaryButtonStyle.fixedHeight = 45;
                _primaryButtonStyle.normal.textColor = Color.white;
                _primaryButtonStyle.normal.background = MakeTex(2, 2, new Color(0.45f, 0.35f, 0.9f));
                _primaryButtonStyle.hover.background = MakeTex(2, 2, new Color(0.55f, 0.45f, 1.0f));
                _primaryButtonStyle.active.background = MakeTex(2, 2, new Color(0.35f, 0.25f, 0.8f));
            }
            return _primaryButtonStyle;
        }}

        void OnGUI()
        {
            // Darker background tint
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.15f, 0.15f, 0.18f));

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.Space(10);

            // HEADER
            GUILayout.Label("MultiLingo", HeaderStyle);
            GUILayout.Label("Translate files or convert between formats", SubHeaderStyle);

            EditorGUILayout.Space(10);
            // TOP-LEVEL MODE: Translator / Converter
            GUI.enabled = !isProcessing;
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle topToggleStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 35 };
            topToggleStyle.normal.textColor = Color.white;
            int topMode = isConverterMode ? 1 : 0;
            EditorGUI.BeginChangeCheck();
            GUIContent[] topModes = {
                new GUIContent("🌐 Translator", "Translate content into multiple languages using AI Providers. Preserves layout, HTML, and rich text variables."),
                new GUIContent("🔄 Converter", "Convert one localization file format directly into another (CSV <> JSON <> XML).")
            };
            int newTopMode = GUILayout.Toolbar(topMode, topModes, topToggleStyle, GUILayout.Width(350), GUILayout.Height(35));
            if (EditorGUI.EndChangeCheck() && newTopMode != topMode)
            {
                isConverterMode = newTopMode == 1;
                inputFilePath = "";
                batchInputFiles.Clear();
                tableData.Clear();
                tableHeaders = new string[0];
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;
            EditorGUILayout.Space(5);

            if (!isConverterMode)
            {
                // === TRANSLATOR MODE ===
                GUI.enabled = !isProcessing;
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                int currentMode = isBatchMode ? 1 : 0;
                GUIStyle toggleStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
                EditorGUI.BeginChangeCheck();
                GUIContent[] modes = {
                    new GUIContent("📄 Single File", "Translate or convert one specific file at a time."),
                    new GUIContent("📁 Batch Mode", "Select an entire folder and translate / convert every compatible file inside it automatically.")
                };
                int newMode = GUILayout.Toolbar(currentMode, modes, toggleStyle, GUILayout.Width(300), GUILayout.Height(30));
                if (EditorGUI.EndChangeCheck() && newMode != currentMode)
                {
                    isBatchMode = newMode == 1;
                    inputFilePath = "";
                    batchInputFiles.Clear();
                    tableData.Clear();
                    tableHeaders = new string[0];
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUI.enabled = true;
                EditorGUILayout.Space(10);

                // CACHE SECTION
                RenderCacheSection();

                if (!isProcessing)
                {
                    // FILE DROP AREA
                    RenderFileDropArea();

                    if (tableData != null && tableData.Count > 0)
                    {
                        // CONFIGURATION
                        RenderConfigurationSection();

                        // DATA VALIDATION
                        RenderValidationSection();

                        // QUALITY REPORT (post-translation)
                        if (showQualityReport && qualityIssues.Count > 0)
                            RenderQualityReport();

                        // Start Translation Button
                        EditorGUILayout.Space(20);
                        GUI.enabled = (selectedLanguages.Any(x => x.Value) || selectedOutputFormat != OutputFormat.SameAsInput);
                        
                        if (GUILayout.Button(new GUIContent("Start Processing", "Begin the translation engine for all selected languages and output to unity Assets."), PrimaryButtonStyle))
                        {
                            RunTranslationProcess();
                        }
                        GUI.enabled = true;
                    }
                }
                else
                {
                    // PROGRESS SECTION (Web View Style)
                    RenderProgressSection();
                }
            }
            else
            {
                // === CONVERTER MODE ===
                RenderConverterMode();
            }

            EditorGUILayout.EndScrollView();
        }

        private void RenderConverterMode()
        {
            EditorGUILayout.Space(10);

            // FILE DROP AREA
            RenderFileDropArea();

            if (tableData != null && tableData.Count > 0)
            {
                EditorGUILayout.Space(10);
                RenderStyleBox(() =>
                {
                    GUILayout.Label("Convert To", SectionTitleStyle);
                    EditorGUILayout.Space(5);

                    // Format selection buttons
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    string[] formats = { "CSV", "XLSX", "JSON", "XML", "YAML" };
                    OutputFormat[] formatValues = { OutputFormat.CSV, OutputFormat.XLSX, OutputFormat.JSON, OutputFormat.XML, OutputFormat.YAML };

                    GUIStyle formatBtnStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontSize = 13,
                        fontStyle = FontStyle.Bold,
                        fixedHeight = 35
                    };
                    formatBtnStyle.normal.textColor = Color.white;

                    for (int i = 0; i < formats.Length; i++)
                    {
                        bool isSelected = selectedOutputFormat == formatValues[i];
                        GUIStyle btnStyle = new GUIStyle(formatBtnStyle);
                        if (isSelected)
                        {
                            btnStyle.normal.background = MakeTex(2, 2, new Color(0.45f, 0.35f, 0.9f));
                            btnStyle.normal.textColor = Color.white;
                        }
                        else
                        {
                            btnStyle.normal.background = MakeTex(2, 2, new Color(0.25f, 0.25f, 0.3f));
                            btnStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                        }

                        if (GUILayout.Button(formats[i], btnStyle, GUILayout.Width(80)))
                        {
                            selectedOutputFormat = formatValues[i];
                        }
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                });

                // Convert Button
                EditorGUILayout.Space(15);
                GUI.enabled = selectedOutputFormat != OutputFormat.SameAsInput;
                if (GUILayout.Button("🔄 Convert & Save", PrimaryButtonStyle))
                {
                    RunConversionProcess();
                }
                GUI.enabled = true;

                // Preview Table
                if (tableData.Count > 1)
                {
                    EditorGUILayout.Space(10);
                    RenderStyleBox(() =>
                    {
                        GUILayout.Label($"Preview  ({tableData.Count - 1} rows, {tableHeaders.Length} columns)", SectionTitleStyle);
                        EditorGUILayout.Space(5);

                        // Show first few rows
                        int previewRows = Mathf.Min(tableData.Count, 6);
                        EditorGUILayout.BeginHorizontal();
                        for (int c = 0; c < Mathf.Min(tableHeaders.Length, 5); c++)
                        {
                            GUILayout.Label(tableHeaders[c], EditorStyles.boldLabel, GUILayout.Width(position.width / Mathf.Min(tableHeaders.Length, 5) - 20));
                        }
                        if (tableHeaders.Length > 5) GUILayout.Label("...", EditorStyles.boldLabel);
                        EditorGUILayout.EndHorizontal();

                        for (int r = 1; r < previewRows; r++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            for (int c = 0; c < Mathf.Min(tableData[r].Count, 5); c++)
                            {
                                string val = tableData[r][c];
                                if (val.Length > 30) val = val.Substring(0, 27) + "...";
                                GUILayout.Label(val, GUILayout.Width(position.width / Mathf.Min(tableHeaders.Length, 5) - 20));
                            }
                            if (tableData[r].Count > 5) GUILayout.Label("...");
                            EditorGUILayout.EndHorizontal();
                        }
                        if (tableData.Count > 6)
                            GUILayout.Label($"  ... and {tableData.Count - 6} more rows", EditorStyles.miniLabel);
                    });
                }
            }
        }

        private void RunConversionProcess()
        {
            if (isBatchMode && batchInputFiles.Count > 0)
            {
                string folder = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (string.IsNullOrEmpty(folder)) return;

                foreach (var file in batchInputFiles)
                {
                    try
                    {
                        var data = MultilingoFileParser.ParseFile(file);
                        string ext = selectedOutputFormat.ToString().ToLower();
                        string outFile = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(file)}.{ext}");
                        MultilingoFileParser.SaveFile(outFile, (MultilingoFileParser.OutputFormat)(int)selectedOutputFormat, data);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to convert {file}: {e.Message}");
                    }
                }
                EditorUtility.DisplayDialog("Conversion Complete", $"Converted {batchInputFiles.Count} files to {selectedOutputFormat} format.\nSaved to: {folder}", "OK");
                AssetDatabase.Refresh();
            }
            else
            {
                string ext = selectedOutputFormat.ToString().ToLower();
                string defaultName = $"{Path.GetFileNameWithoutExtension(inputFilePath)}.{ext}";
                string savePath = EditorUtility.SaveFilePanel("Save Converted File", "Assets", defaultName, ext);
                if (!string.IsNullOrEmpty(savePath))
                {
                    MultilingoFileParser.SaveFile(savePath, (MultilingoFileParser.OutputFormat)(int)selectedOutputFormat, tableData);
                    EditorUtility.DisplayDialog("Conversion Complete", $"File converted to {selectedOutputFormat} and saved to:\n{savePath}", "OK");
                    AssetDatabase.Refresh();
                }
            }
        }

        private static readonly HashSet<string> RTLLanguages = new HashSet<string>
        { "Arabic", "Hebrew", "Persian", "Urdu", "Sindhi", "Pashto", "Kurdish (Kurmanji)", "Yiddish", "Uyghur" };

        private void RenderValidationSection()
        {
            if (tableData == null || tableData.Count < 2 || tableHeaders.Length == 0) return;

            EditorGUILayout.Space(10);
            RenderStyleBox(() =>
            {
                EditorGUILayout.BeginHorizontal();
                showValidation = EditorGUILayout.Foldout(showValidation, "Data Validation & Tools", true, EditorStyles.foldoutHeader);
                EditorGUILayout.EndHorizontal();

                if (!showValidation) return;

                EditorGUILayout.Space(5);

                // --- Missing Translations Report ---
                GUILayout.Label("Missing Translations", EditorStyles.boldLabel);
                EditorGUILayout.Space(3);

                bool allComplete = true;
                for (int c = 0; c < tableHeaders.Length; c++)
                {
                    if (c == selectedSourceColumnIndex) continue;
                    int missing = 0;
                    for (int r = 1; r < tableData.Count; r++)
                    {
                        if (c >= tableData[r].Count || string.IsNullOrWhiteSpace(tableData[r][c]))
                            missing++;
                    }
                    if (missing > 0)
                    {
                        allComplete = false;
                        float pct = (float)(tableData.Count - 1 - missing) / (tableData.Count - 1);
                        EditorGUILayout.BeginHorizontal();
                        
                        bool isRTL = RTLLanguages.Any(rtl => tableHeaders[c].IndexOf(rtl, System.StringComparison.OrdinalIgnoreCase) >= 0);
                        string rtlTag = isRTL ? "  [RTL]" : "";
                        
                        GUILayout.Label($"  {tableHeaders[c]}{rtlTag}", GUILayout.Width(180));
                        Rect barRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                        EditorGUI.ProgressBar(barRect, pct, $"{(int)(pct * 100)}% complete  ({missing} missing)");
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(2);
                    }
                }
                if (allComplete)
                {
                    EditorGUILayout.HelpBox("All columns are fully populated. No missing data!", MessageType.Info);
                }

                EditorGUILayout.Space(10);

                // --- Character Limit Check ---
                GUILayout.Label("Character Limit Check", EditorStyles.boldLabel);
                EditorGUILayout.Space(3);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Max characters per cell:", GUILayout.Width(160));
                charLimit = EditorGUILayout.IntField(charLimit, GUILayout.Width(60));
                GUILayout.Label("(0 = disabled)", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                if (charLimit > 0)
                {
                    int violations = 0;
                    for (int r = 1; r < tableData.Count; r++)
                    {
                        for (int c = 0; c < tableData[r].Count; c++)
                        {
                            if ((tableData[r][c]?.Length ?? 0) > charLimit)
                                violations++;
                        }
                    }
                    if (violations > 0)
                        EditorGUILayout.HelpBox($"{violations} cells exceed {charLimit} characters. These may overflow UI elements.", MessageType.Warning);
                    else
                        EditorGUILayout.HelpBox($"All cells are within {charLimit} character limit.", MessageType.Info);
                }

                EditorGUILayout.Space(10);

                // --- Pseudo-Localization ---
                GUILayout.Label("Pseudo-Localization", EditorStyles.boldLabel);
                GUILayout.Label("Generate fake accented text to test if your UI handles special characters and longer strings.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(3);
                if (GUILayout.Button("Generate Pseudo-Localized Column", GUILayout.Height(28)))
                {
                    GeneratePseudoLocalization();
                }

                // --- RTL Language Info ---
                var rtlInFile = tableHeaders.Where(h => RTLLanguages.Any(rtl => h.IndexOf(rtl, System.StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                if (rtlInFile.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox($"RTL languages detected: {string.Join(", ", rtlInFile)}\nEnsure your UI supports right-to-left text rendering for these.", MessageType.Warning);
                }
            });
        }

        private void GeneratePseudoLocalization()
        {
            if (tableData == null || tableData.Count < 2) return;

            string pseudoHeader = "Pseudo-Loc";
            if (!tableData[0].Contains(pseudoHeader))
                tableData[0].Add(pseudoHeader);

            int pseudoCol = tableData[0].IndexOf(pseudoHeader);

            for (int r = 1; r < tableData.Count; r++)
            {
                while (tableData[r].Count <= pseudoCol) tableData[r].Add("");
                string source = selectedSourceColumnIndex < tableData[r].Count ? tableData[r][selectedSourceColumnIndex] : "";
                tableData[r][pseudoCol] = PseudoLocalize(source);
            }

            tableHeaders = tableData[0].ToArray();
            EditorUtility.DisplayDialog("Pseudo-Localization", $"Generated pseudo-localized text in column '{pseudoHeader}'.\nThis helps test if your UI handles accented characters and ~30% longer strings.", "OK");
        }

        private string PseudoLocalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder();
            sb.Append('[');
            foreach (char c in input)
            {
                switch (char.ToLower(c))
                {
                    case 'a': sb.Append('\u00e4'); break;
                    case 'c': sb.Append('\u00e7'); break;
                    case 'd': sb.Append('\u00f0'); break;
                    case 'e': sb.Append('\u00e9'); break;
                    case 'g': sb.Append('\u011f'); break;
                    case 'h': sb.Append('\u0125'); break;
                    case 'i': sb.Append('\u00ee'); break;
                    case 'k': sb.Append('\u0137'); break;
                    case 'l': sb.Append('\u013a'); break;
                    case 'n': sb.Append('\u00f1'); break;
                    case 'o': sb.Append('\u00f6'); break;
                    case 'r': sb.Append('\u0157'); break;
                    case 's': sb.Append('\u0161'); break;
                    case 't': sb.Append('\u0163'); break;
                    case 'u': sb.Append('\u00fc'); break;
                    case 'w': sb.Append('\u0175'); break;
                    case 'y': sb.Append('\u00fd'); break;
                    case 'z': sb.Append('\u017e'); break;
                    default: sb.Append(c); break;
                }
            }
            int extra = (int)(input.Length * 0.3f);
            for (int i = 0; i < extra; i++) sb.Append('~');
            sb.Append(']');
            return sb.ToString();
        }

        private void RenderStyleBox(System.Action renderContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical();
            renderContent?.Invoke();
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void RenderCacheSection()
        {
            InitCoreSystems();
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Space(5);
            GUILayout.Label("⚡ Cache", EditorStyles.boldLabel, GUILayout.Width(55));
            
            float hitRate = totalRequested > 0 ? ((float)cacheHits / totalRequested) * 100f : 0f;
            GUILayout.Label($"Entries: {_cache.Count}  |  Hit Rate: {hitRate:F1}%  |  Fuzzy Match: ON", EditorStyles.label);
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60))) 
            {
                _cache.Clear();
                _cache.Save();
                totalRequested = 0;
                cacheHits = 0;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void RenderQualityReport()
        {
            EditorGUILayout.Space(10);
            RenderStyleBox(() =>
            {
                GUILayout.Label($"Translation Quality Report ({qualityIssues.Count} issues)", SectionTitleStyle);
                int errors = qualityIssues.FindAll(i => i.Level == MultilingoQualityChecker.QualityIssue.Severity.Error).Count;
                int warnings = qualityIssues.Count - errors;
                EditorGUILayout.HelpBox($"{errors} errors, {warnings} warnings detected in translations.", errors > 0 ? MessageType.Error : MessageType.Warning);

                int showMax = Mathf.Min(qualityIssues.Count, 20);
                for (int i = 0; i < showMax; i++)
                    EditorGUILayout.LabelField(qualityIssues[i].ToString(), EditorStyles.wordWrappedMiniLabel);
                if (qualityIssues.Count > 20)
                    GUILayout.Label($"  ... and {qualityIssues.Count - 20} more issues", EditorStyles.miniLabel);
            });
        }

        private void RenderFileDropArea()
        {
            bool hasFile = !isBatchMode ? !string.IsNullOrEmpty(inputFilePath) : batchInputFiles.Count > 0;
            float height = (!isBatchMode || !hasFile) ? 100f : 55f;

            string text = "";
            if (!isBatchMode) text = !hasFile ? "📂 Drag & Drop your CSV or Excel file here\n" : $"✅ Loaded: {Path.GetFileName(inputFilePath)}\n";
            else text = !hasFile ? "📂\nDrag & Drop Multiple Files\nor click below to browse folder\nMaximum 10 files (CSV or Excel)" : "📂 Drag & Drop More Files Here";

            // Draw custom drop area box using EditorGUILayout groups
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(2, 2, !hasFile ? new Color(0.2f, 0.2f, 0.25f) : new Color(0.25f, 0.35f, 0.25f));

            EditorGUILayout.BeginVertical(boxStyle, GUILayout.Height(height));
            GUILayout.FlexibleSpace();
            
            GUIStyle dropStyle = new GUIStyle(DropAreaStyle);
            if(isBatchMode) dropStyle.fontSize = 13;
            GUILayout.Label(text, dropStyle);
            
            if (!isBatchMode || !hasFile)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Browse Files", GUILayout.Width(140), GUILayout.Height(22)))
                {
                    if (!isBatchMode)
                    {
                        string path = EditorUtility.OpenFilePanel("Select CSV or Excel File", "Assets", "csv,xlsx");
                        if (!string.IsNullOrEmpty(path)) LoadFile(path);
                    }
                    else
                    {
                        string folder = EditorUtility.OpenFolderPanel("Select Folder with CSV/Excel files", "Assets", "");
                        if (!string.IsNullOrEmpty(folder))
                        {
                            var files = Directory.GetFiles(folder).Where(f => f.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (files.Length > 0)
                            {
                                batchInputFiles.Clear();
                                batchInputFiles.AddRange(files.Take(10));
                                LoadFile(batchInputFiles[0]);
                            }
                        }
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            // Handle Drag/Drop events on the rect we just drew
            Rect dropRect = GUILayoutUtility.GetLastRect();

            if (isBatchMode && hasFile)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Selected Files ({batchInputFiles.Count} / 10):", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear All", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    batchInputFiles.Clear();
                    tableData.Clear();
                    tableHeaders = new string[0];
                    GUIUtility.ExitGUI(); // Add this line to handle layout interrupts safely
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);

                int indexToRemove = -1;
                for (int i = 0; i < batchInputFiles.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUILayout.Label("📄 " + Path.GetFileName(batchInputFiles[i]), EditorStyles.label);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        indexToRemove = i;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (indexToRemove != -1)
                {
                    batchInputFiles.RemoveAt(indexToRemove);
                    if (batchInputFiles.Count > 0) LoadFile(batchInputFiles[0]);
                    else { tableData.Clear(); tableHeaders = new string[0]; }
                    GUIUtility.ExitGUI(); // Add this line to safely exit GUI scope immediately after modifying
                }
                
                EditorGUILayout.EndVertical();
            }

            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropRect.Contains(evt.mousePosition))
                        break;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        var validPaths = DragAndDrop.paths.Where(p => p.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase) || p.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase)).ToList();

                        if (validPaths.Count > 0)
                        {
                            if (!isBatchMode)
                            {
                                LoadFile(validPaths[0]);
                            }
                            else
                            {
                                batchInputFiles.AddRange(validPaths);
                                if (batchInputFiles.Count > 10) batchInputFiles = batchInputFiles.Take(10).ToList();
                                
                                if (tableData == null || tableData.Count == 0 || (batchInputFiles.Count > 0 && inputFilePath != batchInputFiles[0]))
                                {
                                    LoadFile(batchInputFiles[0]);
                                }
                            }
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Error", "Please drop .csv or .xlsx files.", "OK");
                        }
                    }
                    Event.current.Use();
                    break;
            }
        }

        private void RenderConfigurationSection()
        {
            RenderStyleBox(() =>
            {
                GUILayout.Label("Configuration", SectionTitleStyle);

                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.BeginVertical();
                GUILayout.Label(new GUIContent("Source Column:", "The main column containing the text you want to translate from. Usually English."), EditorStyles.boldLabel);
                if (tableHeaders.Length > 0)
                {
                    selectedSourceColumnIndex = EditorGUILayout.Popup(selectedSourceColumnIndex, tableHeaders);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(10);
                
                EditorGUILayout.BeginVertical();
                GUILayout.Label(new GUIContent("Output Format:", "Choose how you want your parsed data exported. CSV is usually recommended."), EditorStyles.boldLabel);
                selectedOutputFormat = (OutputFormat)EditorGUILayout.EnumPopup(selectedOutputFormat);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);
                
                // --- API Provider Config ---
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("AI Translation Engine", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Provider:", "Choose which Translation Engine runs the process."), GUILayout.Width(130));
                currentProvider = (TranslationProvider)EditorGUILayout.EnumPopup(currentProvider);
                EditorGUILayout.EndHorizontal();

                if (currentProvider == TranslationProvider.GoogleCloud) {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Cloud API Key:", "Found in your Google Cloud Console under APIs & Services."), GUILayout.Width(130));
                    googleCloudApiKey = EditorGUILayout.PasswordField(googleCloudApiKey);
                    EditorGUILayout.EndHorizontal();
                } else if (currentProvider == TranslationProvider.DeepL) {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("DeepL Auth Key:", "Your DeepL Developer Authentication Key. Works with both Free and Pro."), GUILayout.Width(130));
                    deeplApiKey = EditorGUILayout.PasswordField(deeplApiKey);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.HelpBox("Supports DeepL API Free and Pro.", MessageType.Info);
                } else if (currentProvider == TranslationProvider.OpenAI) {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("OpenAI API Key:", "Standard OpenAI Secret Key starting with 'sk-'."), GUILayout.Width(130));
                    openAiApiKey = EditorGUILayout.PasswordField(openAiApiKey);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent("Model:", "We recommend 'gpt-4o' or 'gpt-3.5-turbo'."), GUILayout.Width(130));
                    openAiModel = EditorGUILayout.TextField(openAiModel);
                    EditorGUILayout.EndHorizontal();
                } else {
                    EditorGUILayout.HelpBox("Google Free uses an unofficial endpoint. Use DeepL or OpenAI for reliable bulk translation.", MessageType.Warning);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(10);
                GUILayout.Label("Target Languages:", EditorStyles.boldLabel);
                
                if (autoDetectedLanguages.Count > 0)
                {
                    EditorGUILayout.HelpBox($"Auto-detected {autoDetectedLanguages.Count} languages already in the file.", MessageType.Info);
                }

                GUI.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
                languageSearch = EditorGUILayout.TextField("Search:", languageSearch, GUI.skin.textField);
                GUI.backgroundColor = Color.white;

                langScrollPos = EditorGUILayout.BeginScrollView(langScrollPos, "box", GUILayout.Height(180));
                
                var filteredLangs = SupportedLanguages.Keys.Where(k => string.IsNullOrEmpty(languageSearch) || k.IndexOf(languageSearch, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            
                // Show auto-detected ones first
                var detected = filteredLangs.Where(l => autoDetectedLanguages.Contains(l)).ToList();
                var nonDetected = filteredLangs.Where(l => !autoDetectedLanguages.Contains(l)).ToList();

                int cols = 2;

                if (detected.Count > 0)
                {
                    GUILayout.Label("ALREADY IN FILE (Updating empty rows):", EditorStyles.boldLabel);
                    int detCount = 0;
                    EditorGUILayout.BeginHorizontal();
                    foreach (var lang in detected)
                    {
                        if (detCount > 0 && detCount % cols == 0)
                        {
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                        }
                        selectedLanguages[lang] = EditorGUILayout.ToggleLeft(GetLanguageLabel(lang), selectedLanguages[lang], GUILayout.Width(position.width / cols - 15));
                        detCount++;
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(5);
                    GUILayout.Label("NEW LANGUAGES TO ADD:", EditorStyles.boldLabel);
                }

                int count = 0;
                EditorGUILayout.BeginHorizontal();
                foreach (var lang in nonDetected)
                {
                    if (count > 0 && count % cols == 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                    }
                    selectedLanguages[lang] = EditorGUILayout.ToggleLeft(GetLanguageLabel(lang), selectedLanguages[lang], GUILayout.Width(position.width / cols - 15));
                    count++;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft)) ToggleAllLanguages(true);
                if (GUILayout.Button("Deselect All", EditorStyles.miniButtonRight)) ToggleAllLanguages(false);
                EditorGUILayout.EndHorizontal();
            });
        }

        private void RenderProgressSection()
        {
            RenderStyleBox(() =>
            {
                GUILayout.Label(isBatchMode ? "Batch Translation Progress" : "Processing", SectionTitleStyle);
                
                GUIStyle progressTextStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

                if (isBatchMode)
                {
                    GUILayout.Label(progressStatus, EditorStyles.miniLabel);
                    Rect rBatch = EditorGUILayout.GetControlRect(false, 15);
                    EditorGUI.DrawRect(rBatch, new Color(0.1f, 0.1f, 0.1f, 1f)); 
                    Rect fillBatch = new Rect(rBatch.x, rBatch.y, rBatch.width * progress, rBatch.height);
                    EditorGUI.DrawRect(fillBatch, new Color(0.3f, 0.8f, 0.8f, 1f)); 
                    EditorGUILayout.Space(10);
                }

                // File specific progress
                EditorGUILayout.BeginVertical("box");
                
                if (isBatchMode) 
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(currentFileProgressStatus, EditorStyles.label);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{(currentFileProgress)*100f:F0}%", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                }

                Rect rFile = EditorGUILayout.GetControlRect(false, 25);
                EditorGUI.DrawRect(rFile, new Color(0.1f, 0.1f, 0.1f, 1f)); 
                float fillAmount = isBatchMode ? currentFileProgress : progress;
                Rect fillFile = new Rect(rFile.x, rFile.y, rFile.width * fillAmount, rFile.height);
                EditorGUI.DrawRect(fillFile, new Color(0.35f, 0.55f, 0.95f, 1f)); 
                
                if (!isBatchMode)
                {
                    GUI.Label(rFile, progressStatus, progressTextStyle);
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Speed: {currentSpeed:F1} rows/s", EditorStyles.miniBoldLabel);
                GUILayout.Label($"ETA: {(int)currentEta}s", EditorStyles.miniBoldLabel);
                float hitRate = totalRequested > 0 ? ((float)cacheHits / totalRequested) * 100f : 0f;
                GUILayout.Label($"Cache Hit: {hitRate:F0}%", EditorStyles.miniBoldLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(20);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel Processing", GUILayout.Width(200), GUILayout.Height(30)))
                {
                    cancelRequest = true;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            });
        }

        private void ToggleAllLanguages(bool state)
        {
            var keys = new List<string>(selectedLanguages.Keys);
            foreach(var key in keys) selectedLanguages[key] = state;
        }

        private void LoadFile(string path)
        {
            inputFilePath = path;
            try
            {
                // Use centralized parser (supports CSV, XLSX, JSON, XML)
                tableData = MultilingoFileParser.ParseFile(path);
                autoDetectedLanguages.Clear();
                qualityIssues.Clear();
                showQualityReport = false;

                if (tableData.Count > 0)
                {
                    tableHeaders = tableData[0].ToArray();
                    
                    // Smart source column detection (checks English, en, Source, Original, Base, etc.)
                    selectedSourceColumnIndex = MultilingoFileParser.DetectSourceColumn(tableData, tableHeaders);

                    // Auto-detect context/notes column for AI translation context
                    detectedContextColumn = MultilingoFileParser.DetectContextColumn(tableHeaders);
                    if (detectedContextColumn >= 0)
                        Debug.Log($"[Multilingo] Context column detected: '{tableHeaders[detectedContextColumn]}' — will be used for AI translation context.");

                    // Auto-detect existing target languages
                    for (int i = 0; i < tableHeaders.Length; i++)
                    {
                        string headerLower = tableHeaders[i].ToLower();
                        foreach (var kvp in SupportedLanguages)
                        {
                            if (headerLower.Contains(kvp.Key.ToLower()) || headerLower.EndsWith($"({kvp.Value.ToLower()})"))
                            {
                                autoDetectedLanguages.Add(kvp.Key);
                                selectedLanguages[kvp.Key] = true;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load file: {e.Message}");
                tableData = new List<List<string>>();
                tableHeaders = new string[0];
            }
        }

        private void PromptSaveAndEnd()
        {
            string ext = selectedOutputFormat == OutputFormat.SameAsInput ? "csv" : selectedOutputFormat.ToString().ToLower();
            string defaultName = $"translated_{Path.GetFileNameWithoutExtension(inputFilePath)}";
            
            string savePath = EditorUtility.SaveFilePanel("Download / Save Target File", "Assets", defaultName, ext);
            
            if (!string.IsNullOrEmpty(savePath))
            {
                SaveFile(savePath, selectedOutputFormat, tableData);
                EditorUtility.DisplayDialog("Download Complete", $"File successfully generated and saved to:\n{savePath}", "OK");
                AssetDatabase.Refresh();
            }
            else
            {
                EditorUtility.DisplayDialog("Cancelled", "File processing completed, but saving was cancelled. The processed data is held in memory. Click 'Start Processing' again to initiate save.", "OK");
            }
        }

        private async void RunTranslationProcess()
        {
            cancelRequest = false;
            isProcessing = true;
            progress = 0f;
            progressStatus = "Initializing...";
            totalRequested = 0;
            cacheHits = 0;
            qualityIssues.Clear();
            showQualityReport = false;

            InitCoreSystems();

            // Configure the engine from current UI settings
            _engine.Provider = (MultilingoTranslationEngine.TranslationProvider)(int)currentProvider;
            _engine.GoogleCloudApiKey = googleCloudApiKey;
            _engine.DeepLApiKey = deeplApiKey;
            _engine.OpenAiApiKey = openAiApiKey;
            _engine.OpenAiModel = openAiModel;
            _engine.Glossary = _glossary;
            _engine.CancelRequested = false;
            _engine.ResetStats();
            
            string currentContextPrefix = "";
            float currentGlobalProgressBase = 0f;
            float progressPerStep = 1f;

            _engine.OnProgress = (p, msg) => { 
                progress = currentGlobalProgressBase + (p * progressPerStep);
                string cleanMsg = msg.Replace("Translating...", "").Trim();
                progressStatus = $"{currentContextPrefix} {cleanMsg}";
                Repaint(); 
            };

            var targetLanguages = selectedLanguages.Where(kv => kv.Value).Select(kv => new { Name = kv.Key, Code = SupportedLanguages[kv.Key] }).ToList();
            
            string batchOutputFolder = "";
            if (isBatchMode)
            {
                batchOutputFolder = EditorUtility.OpenFolderPanel("Select Output Directory for Batch", "Assets", "");
                if (string.IsNullOrEmpty(batchOutputFolder))
                {
                    isProcessing = false;
                    return;
                }
            }

            if (targetLanguages.Count == 0 && selectedOutputFormat != OutputFormat.SameAsInput)
            {
                progressStatus = "Converting format...";
                progress = 1.0f;
                Repaint();
                await Task.Delay(100);
                
                if (isBatchMode)
                {
                    for (int bi = 0; bi < batchInputFiles.Count; bi++)
                    {
                        if (cancelRequest) break;
                        var file = batchInputFiles[bi];
                        progressStatus = $"Converting {Path.GetFileName(file)} ({bi + 1}/{batchInputFiles.Count})...";
                        progress = (float)bi / batchInputFiles.Count;
                        Repaint();
                        await Task.Delay(10); // yield for UI repaint

                        var data = MultilingoFileParser.ParseFile(file);
                        string ext = selectedOutputFormat == OutputFormat.SameAsInput ? "csv" : selectedOutputFormat.ToString().ToLower();
                        string outFile = Path.Combine(batchOutputFolder, $"converted_{Path.GetFileNameWithoutExtension(file)}.{ext}");
                        MultilingoFileParser.SaveFile(outFile, (MultilingoFileParser.OutputFormat)(int)selectedOutputFormat, data);
                    }
                    if (!cancelRequest)
                    {
                        EditorUtility.DisplayDialog("Batch Complete", $"Processed {batchInputFiles.Count} files.", "OK");
                        AssetDatabase.Refresh();
                    }
                }
                else
                {
                    PromptSaveAndEnd();
                }
                
                isProcessing = false;
                return;
            }

            float startTime = Time.realtimeSinceStartup;
            List<string> filesToProcess = isBatchMode ? batchInputFiles : new List<string> { inputFilePath };
            
            List<List<List<string>>> multiFileData = new List<List<List<string>>>();
            
            foreach (var file in filesToProcess)
            {
                if (file != inputFilePath || tableData.Count == 0)
                {
                    try { multiFileData.Add(MultilingoFileParser.ParseFile(file)); } catch { continue; }
                }
                else
                {
                    multiFileData.Add(tableData);
                }
            }

            for (int fIndex = 0; fIndex < filesToProcess.Count; fIndex++)
            {
                if (cancelRequest) break;
                batchCurrentFileIndex = fIndex;
                string currentFile = filesToProcess[fIndex];
                var currentData = multiFileData[fIndex];
                if (currentData.Count < 2) continue;

                // Detect context column for this file
                string[] fileHeaders = currentData[0].ToArray();
                int ctxCol = MultilingoFileParser.DetectContextColumn(fileHeaders);

                List<int> targetColumnIndices = new List<int>();

                // Header preparation
                foreach (var lang in targetLanguages)
                {
                    string expectedHeader = $"{lang.Name} ({lang.Code})";
                    int colIndex = currentData[0].FindIndex(h => h.Equals(expectedHeader, System.StringComparison.OrdinalIgnoreCase) || h.IndexOf(lang.Name, System.StringComparison.OrdinalIgnoreCase) >= 0);

                    if (colIndex == -1)
                    {
                        currentData[0].Add(expectedHeader);
                        colIndex = currentData[0].Count - 1;
                        for (int i = 1; i < currentData.Count; i++) while (currentData[i].Count <= colIndex) currentData[i].Add("");
                    }
                    targetColumnIndices.Add(colIndex);
                }

                // Use the new batch translation engine for each language
                for (int i = 0; i < targetLanguages.Count; i++)
                {
                    if (cancelRequest) { _engine.CancelRequested = true; break; }
                    var lang = targetLanguages[i];
                    int targetCol = targetColumnIndices[i];

                    currentContextPrefix = isBatchMode
                        ? $"[{fIndex + 1}/{filesToProcess.Count}] {Path.GetFileName(currentFile)} → {lang.Name}"
                        : $"Translating to {lang.Name}";
                    
                    int totalSteps = filesToProcess.Count * targetLanguages.Count;
                    currentGlobalProgressBase = (float)(fIndex * targetLanguages.Count + i) / totalSteps;
                    progressPerStep = 1f / totalSteps;

                    progressStatus = $"{currentContextPrefix}...";
                    currentFileProgressStatus = $"{Path.GetFileName(currentFile)} → {lang.Name}";
                    Repaint();

                    // Collect texts that need translation
                    var textsToTranslate = new List<string>();
                    var contextTexts = new List<string>();
                    var rowIndices = new List<int>();

                    for (int rowIndex = 1; rowIndex < currentData.Count; rowIndex++)
                    {
                        var row = currentData[rowIndex];
                        string sourceText = row.Count > selectedSourceColumnIndex ? row[selectedSourceColumnIndex] : "";
                        while (row.Count <= targetCol) row.Add("");

                        if (!string.IsNullOrWhiteSpace(sourceText) && string.IsNullOrWhiteSpace(row[targetCol]))
                        {
                            textsToTranslate.Add(sourceText);
                            string ctx = (ctxCol >= 0 && ctxCol < row.Count) ? row[ctxCol] : "";
                            contextTexts.Add(ctx);
                            rowIndices.Add(rowIndex);
                        }
                    }

                    if (textsToTranslate.Count == 0) continue;

                    // Batch translate with concurrency, retry, and context
                    var translated = await _engine.TranslateBatchAsync(
                        textsToTranslate.ToArray(), "auto", lang.Code, contextTexts.ToArray());

                    // Apply results
                    for (int j = 0; j < rowIndices.Count && j < translated.Length; j++)
                    {
                        currentData[rowIndices[j]][targetCol] = translated[j];
                        // Store source hash for stale detection
                        _cache.SetSourceHash($"{currentData[rowIndices[j]][0]}_{lang.Code}", textsToTranslate[j]);
                    }

                    totalRequested = _engine.TotalRequested;
                    cacheHits = _engine.CacheHits;
                    currentSpeed = _engine.CurrentSpeed;
                    currentEta = _engine.CurrentEta;
                    currentFileProgress = (float)(i + 1) / targetLanguages.Count;
                    progress = currentGlobalProgressBase + progressPerStep;
                    Repaint();
                }

                // Run quality check on translated data
                fileHeaders = currentData[0].ToArray();
                qualityIssues.AddRange(MultilingoQualityChecker.ValidateTable(currentData, 0, selectedSourceColumnIndex, fileHeaders));

                // Save file immediately in batch mode
                if (isBatchMode && !cancelRequest)
                {
                    string ext = selectedOutputFormat == OutputFormat.SameAsInput ? "csv" : selectedOutputFormat.ToString().ToLower();
                    string outFile = Path.Combine(batchOutputFolder, $"translated_{Path.GetFileNameWithoutExtension(currentFile)}.{ext}");
                    MultilingoFileParser.SaveFile(outFile, (MultilingoFileParser.OutputFormat)(int)selectedOutputFormat, currentData);
                }
            }

            if (cancelRequest)
            {
                progressStatus = "Processing cancelled.";
                Repaint();
                await Task.Delay(1000);
            }
            else
            {
                progressStatus = "Translations complete! Preparing file...";
                progress = 1.0f;
                Repaint();
                _cache.Save();
                
                showQualityReport = qualityIssues.Count > 0;
                
                await Task.Delay(100);
                
                if (isBatchMode)
                {
                    string qMsg = qualityIssues.Count > 0 ? $"\n\n⚠️ {qualityIssues.Count} quality issues detected." : "";
                    EditorUtility.DisplayDialog("Batch Complete", $"Processed {filesToProcess.Count} files and saved to:\n{batchOutputFolder}{qMsg}", "OK");
                    AssetDatabase.Refresh();
                }
                else
                {
                    tableData = multiFileData.FirstOrDefault() ?? tableData;
                    PromptSaveAndEnd();
                }
            }

            isProcessing = false;
            cancelRequest = false;
        }

        private async Task<string> TranslateTextAsync(string text, string sourceLang, string targetLang)
        {
            if (currentProvider == TranslationProvider.DeepL) return await TranslateDeepLAsync(text, sourceLang, targetLang);
            if (currentProvider == TranslationProvider.OpenAI) return await TranslateOpenAIAsync(text, sourceLang, targetLang);
            if (currentProvider == TranslationProvider.GoogleCloud) return await TranslateGoogleCloudAsync(text, sourceLang, targetLang);

            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={UnityWebRequest.EscapeURL(text)}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    return ExtractTranslationFromGoogleResponse(req.downloadHandler.text, text);
                }
                return text; 
            }
        }

        private async Task<string> TranslateDeepLAsync(string text, string sourceLang, string targetLang)
        {
            if (string.IsNullOrEmpty(deeplApiKey)) return text;
            string deepLLang = targetLang.ToUpper(); 
            if (deepLLang.StartsWith("ZH")) deepLLang = "ZH"; 
            if (deepLLang.StartsWith("EN")) deepLLang = "EN-US";

            string endpoint = deeplApiKey.EndsWith(":fx") ? "https://api-free.deepl.com/v2/translate" : "https://api.deepl.com/v2/translate";
            
            WWWForm form = new WWWForm();
            form.AddField("text", text);
            form.AddField("target_lang", deepLLang);
            
            using (UnityWebRequest req = UnityWebRequest.Post(endpoint, form))
            {
                req.SetRequestHeader("Authorization", "DeepL-Auth-Key " + deeplApiKey);
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();
                
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var match = Regex.Match(req.downloadHandler.text, @"""text"":""(.*?)""");
                    if (match.Success) return Regex.Unescape(match.Groups[1].Value);
                }
            }
            return text;
        }

        private async Task<string> TranslateOpenAIAsync(string text, string sourceLang, string targetLang)
        {
            if (string.IsNullOrEmpty(openAiApiKey)) return text;
            string endpoint = "https://api.openai.com/v1/chat/completions";
            
            string jsonBody = "{\"model\": \"" + openAiModel + "\", \"messages\": [{\"role\": \"system\", \"content\": \"You are a professional game translator. Translate the given text directly into " + targetLang + " without any additional commentary. Preserve any formatting placeholders (like {0}).\"}, {\"role\": \"user\", \"content\": \"" + text.Replace("\"", "\\\"").Replace("\n", "\\n") + "\"}]}";

            using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
                
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();
                
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var match = Regex.Match(req.downloadHandler.text, @"""content"":\s*""(.*?)""");
                    if (match.Success) return Regex.Unescape(match.Groups[1].Value);
                }
            }
            return text;
        }

        private async Task<string> TranslateGoogleCloudAsync(string text, string sourceLang, string targetLang)
        {
            if (string.IsNullOrEmpty(googleCloudApiKey)) return text;
            string url = $"https://translation.googleapis.com/language/translate/v2?key={googleCloudApiKey}";
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
            }
            return text;
        }

        private string ExtractTranslationFromGoogleResponse(string json, string originalText)
        {
            try 
            {
                StringBuilder fullTranslation = new StringBuilder();
                int endOfFirstArray = json.IndexOf(",null,");
                if (endOfFirstArray == -1) endOfFirstArray = json.Length;
                string arrayData = json.Substring(0, endOfFirstArray);
                
                MatchCollection matches = Regex.Matches(arrayData, @"\[""((?:[^""\\]|\\.)*)"",""(?:[^""\\]|\\.)*""");
                if (matches.Count > 0)
                {
                    foreach(Match m in matches) fullTranslation.Append(m.Groups[1].Value);
                    return Regex.Unescape(fullTranslation.ToString());
                }
                return originalText;
            }
            catch { return originalText; }
        }

        // --- Data Formats (delegated to centralized MultilingoFileParser) ---

        private void SaveFile(string path, OutputFormat format, List<List<string>> data)
        {
            MultilingoFileParser.SaveFile(path, (MultilingoFileParser.OutputFormat)(int)format, data);
        }
    }
}
