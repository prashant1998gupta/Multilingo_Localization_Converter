using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using UnityEngine.Networking;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Multilingo.Localization.Editor
{
    public class MultilingoLocalizationTools : EditorWindow
    {
        private Vector2 scrollPos;
        private int selectedTab = 0;
        private string[] tabs = { "⌨️ C# Keys", "🔍 Scene Scanner", "📦 SO Export", "⚡ One-Click", "🔄 Table Sync", "🤖 Auto Translate", "📊 Sheets Sync", "🅰️ Font Optimizer", "🎙️ Auto TTS", "🔠 Pseudo-Localize", "🩺 Table Doctor", "📈 Progress" };

        // C# Keys Generator
        private string keysInputFilePath = "";
        private List<List<string>> keysTableData = new List<List<string>>();
        private string[] keysHeaders = new string[0];
        private int keysColumnIndex = 0;
        private string keysClassName = "LocalizationKeys";
        private string keysNamespace = "";
        private bool keysPascalCase = true;
        private string keysOutputPath = "";
        private string keysPreview = "";

        // Scene Scanner
        private List<ScannedText> scannedTexts = new List<ScannedText>();
        private Vector2 scanScrollPos;
        private bool includeInactive = true;
        private bool scanPrefabs = false;
        private string scanExportPath = "";

        // SO Export
        private string soInputFilePath = "";
        private List<List<string>> soTableData = new List<List<string>>();
        private string[] soHeaders = new string[0];
        private string soOutputFolder = "";

        // StringTable Sync
        private string stInputFilePath = "";
        private List<List<string>> stTableData = new List<List<string>>();
        private string[] stHeaders = new string[0];
        private StringTableCollection selectedTableCollection;

        // Auto Translate
        private StringTableCollection atTargetCollection;
        private string atStatusInfo = "";
        private bool atIsProcessing = false;

        // Google Sheets Sync
        private string gsUrl = "";
        private StringTableCollection gsTargetCollection;
        private string gsStatus = "";

        // Font Optimizer
        private string fontInputFilePath = "";
        private List<List<string>> fontTableData = new List<List<string>>();
        private string fontUniqueChars = "";

        // Auto Voice-Over (TTS)
        private StringTableCollection voStringCollection;
        private AssetTableCollection voAssetCollection;
        private string voOutputFolder = "Assets/LocalizedAudio";
        private int voSelectedVoiceIndex = 0;
        private string[] voVoices = { "alloy", "echo", "fable", "onyx", "nova", "shimmer" };
        private string voStatusInfo = "";
        private bool voIsProcessing = false;

        // Pseudo-Localize
        private StringTableCollection plSourceCollection;
        private string plStatusInfo = "";

        // Table Doctor
        private LocalizationTableCollection tdTargetCollection;
        private string tdStatusInfo = "";
        private Vector2 tdScrollPos;
        private List<string> tdReportLogs = new List<string>();

        // Progress Report
        private StringTableCollection prTargetCollection;
        private string prStatusInfo = "";
        private Vector2 prScrollPos;
        private List<string> prReportLogs = new List<string>();

        private class ScannedText
        {
            public string objectPath;
            public string componentType;
            public string text;
            public GameObject gameObject;
        }

        [MenuItem("Tools/Multilingo/Unity Localization Tools")]
        public static void ShowWindow()
        {
            var window = GetWindow<MultilingoLocalizationTools>("Localization Tools");
            window.minSize = new Vector2(650, 500);
            window.Show();
        }

        // --- Styles ---
        private GUIStyle _headerStyle;
        private GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 22,
                        alignment = TextAnchor.MiddleCenter
                    };
                    _headerStyle.normal.textColor = new Color(0.55f, 0.45f, 1f);
                }
                return _headerStyle;
            }
        }

        private GUIStyle _subHeaderStyle;
        private GUIStyle SubHeaderStyle
        {
            get
            {
                if (_subHeaderStyle == null)
                {
                    _subHeaderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    {
                        fontSize = 12
                    };
                }
                return _subHeaderStyle;
            }
        }

        void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.15f, 0.15f, 0.18f));

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.Space(10);

            GUILayout.Label("MultiLingo", HeaderStyle);
            GUILayout.Label("Unity Localization Tools", SubHeaderStyle);

            EditorGUILayout.Space(10);

            // Modern UI Tab Grid
            EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(5, 5, 5, 5) });
            int columns = 4;
            int numRows = Mathf.CeilToInt((float)tabs.Length / columns);
            for (int r = 0; r < numRows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < columns; c++)
                {
                    int index = r * columns + c;
                    if (index < tabs.Length)
                    {
                        bool isSelected = selectedTab == index;

                        GUIStyle modernTabStyle = new GUIStyle(GUI.skin.button);
                        modernTabStyle.fontSize = 11;
                        modernTabStyle.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
                        modernTabStyle.alignment = TextAnchor.MiddleCenter;
                        modernTabStyle.wordWrap = true;
                        
                        // Clean Text Color
                        modernTabStyle.normal.textColor = isSelected ? new Color(1f, 1f, 1f) : new Color(0.7f, 0.7f, 0.7f);
                        modernTabStyle.hover.textColor = new Color(0.9f, 0.9f, 0.9f);

                        // Premium Flat Backgrounds
                        if (isSelected) 
                        {
                            modernTabStyle.normal.background = MakeTex(2, 2, new Color(0.25f, 0.4f, 0.55f));
                            modernTabStyle.hover.background = MakeTex(2, 2, new Color(0.3f, 0.45f, 0.6f));
                        }
                        else 
                        {
                            modernTabStyle.normal.background = MakeTex(2, 2, new Color(0.18f, 0.18f, 0.18f));
                            modernTabStyle.hover.background = MakeTex(2, 2, new Color(0.25f, 0.25f, 0.25f));
                        }

                        if (GUILayout.Button(tabs[index], modernTabStyle, GUILayout.MinHeight(32)))
                        {
                            selectedTab = index;
                            GUI.FocusControl(null); // Clear keyboard focus
                        }
                    }
                    else
                    {
                        // Fill empty grid slot if the number of tabs isn't perfectly divisible
                        GUILayout.FlexibleSpace(); 
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (r < numRows - 1) GUILayout.Space(2); // Subtle row gap
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);

            switch (selectedTab)
            {
                case 0: RenderKeysGenerator(); break;
                case 1: RenderSceneScanner(); break;
                case 2: RenderSOExport(); break;
                case 3: RenderOneClickSetup(); break;
                case 4: RenderStringTableSync(); break;
                case 5: RenderAutoTranslate(); break;
                case 6: RenderGoogleSheetsSync(); break;
                case 7: RenderFontOptimizer(); break;
                case 8: RenderAutoVoiceOver(); break;
                case 9: RenderPseudoLocalize(); break;
                case 10: RenderTableDoctor(); break;
                case 11: RenderProgressReport(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ===========================
        // TAB 1: C# Keys Generator
        // ===========================
        private void RenderKeysGenerator()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            GUILayout.Label("Generate a static C# class with localization key constants.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5);

            // File Selection
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source File:", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label(string.IsNullOrEmpty(keysInputFilePath) ? "None selected" : Path.GetFileName(keysInputFilePath));
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFilePanel("Select CSV or Excel File", "Assets", "csv,xlsx");
                if (!string.IsNullOrEmpty(path))
                {
                    keysInputFilePath = path;
                    LoadKeysFile(path);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (keysHeaders.Length > 0)
            {
                EditorGUILayout.Space(10);

                // Key Column Selection
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Key Column:", EditorStyles.boldLabel, GUILayout.Width(100));
                keysColumnIndex = EditorGUILayout.Popup(keysColumnIndex, keysHeaders);
                EditorGUILayout.EndHorizontal();

                // Class Settings
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Class Name:", EditorStyles.boldLabel, GUILayout.Width(100));
                keysClassName = EditorGUILayout.TextField(keysClassName);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Namespace:", EditorStyles.boldLabel, GUILayout.Width(100));
                keysNamespace = EditorGUILayout.TextField(keysNamespace);
                GUILayout.Label("(optional)", EditorStyles.miniLabel, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();

                keysPascalCase = EditorGUILayout.ToggleLeft("  Convert keys to UPPER_SNAKE_CASE constants", keysPascalCase);

                EditorGUILayout.Space(10);

                // Preview
                if (GUILayout.Button("Preview Generated Code", GUILayout.Height(28)))
                {
                    keysPreview = GenerateKeysCode();
                }

                if (!string.IsNullOrEmpty(keysPreview))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
                    EditorGUILayout.BeginVertical("box");
                    int lines = keysPreview.Split('\n').Length;
                    string displayPreview = keysPreview;
                    if (lines > 25)
                    {
                        var previewLines = keysPreview.Split('\n');
                        displayPreview = string.Join("\n", previewLines.Take(20)) + $"\n    // ... ({lines - 20} more lines)\n}}";
                        if (!string.IsNullOrEmpty(keysNamespace)) displayPreview += "\n}";
                    }
                    EditorGUILayout.TextArea(displayPreview, EditorStyles.textArea, GUILayout.MinHeight(200));
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.Space(10);

                // Generate Button
                GUIStyle genBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 40
                };
                genBtnStyle.normal.textColor = Color.white;
                genBtnStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.7f, 0.3f));

                if (GUILayout.Button("Generate & Save C# File", genBtnStyle))
                {
                    string code = GenerateKeysCode();
                    string defaultPath = $"Assets/Scripts/Localization/{keysClassName}.cs";
                    string savePath = EditorUtility.SaveFilePanel("Save C# Keys File", "Assets", keysClassName, "cs");
                    if (!string.IsNullOrEmpty(savePath))
                    {
                        File.WriteAllText(savePath, code, Encoding.UTF8);
                        AssetDatabase.Refresh();
                        EditorUtility.DisplayDialog("Success", $"Generated {keysClassName}.cs with {keysTableData.Count - 1} key constants.\nSaved to: {savePath}", "OK");
                    }
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void LoadKeysFile(string path)
        {
            try
            {
                if (path.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Use the converter window's parser if available, otherwise load as CSV
                    var converterWindow = GetWindow<MultilingoConverterWindow>("Multilingo Pro", false);
                    if (converterWindow != null)
                    {
                        // Fall back to CSV approach
                    }
                }
                string content = File.ReadAllText(path, Encoding.UTF8);
                keysTableData = ParseCSVSimple(content);
                if (keysTableData.Count > 0)
                {
                    keysHeaders = keysTableData[0].ToArray();
                    // Auto-detect key column
                    for (int i = 0; i < keysHeaders.Length; i++)
                    {
                        string h = keysHeaders[i].ToLower();
                        if (h == "key" || h == "id" || h == "keys" || h.Contains("key"))
                        {
                            keysColumnIndex = i;
                            break;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load file: {e.Message}");
            }
        }

        private string GenerateKeysCode()
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated by MultiLingo Localization Tools");
            sb.AppendLine("// Do not edit manually - regenerate from source file");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(keysNamespace))
            {
                sb.AppendLine($"namespace {keysNamespace}");
                sb.AppendLine("{");
            }

            string indent = string.IsNullOrEmpty(keysNamespace) ? "" : "    ";
            sb.AppendLine($"{indent}public static class {keysClassName}");
            sb.AppendLine($"{indent}{{");

            HashSet<string> usedNames = new HashSet<string>();

            for (int r = 1; r < keysTableData.Count; r++)
            {
                if (keysColumnIndex >= keysTableData[r].Count) continue;
                string key = keysTableData[r][keysColumnIndex];
                if (string.IsNullOrWhiteSpace(key)) continue;

                string constName = keysPascalCase ? ToUpperSnakeCase(key) : SanitizeIdentifier(key);
                
                // Handle duplicates
                string originalName = constName;
                int suffix = 2;
                while (usedNames.Contains(constName))
                {
                    constName = originalName + "_" + suffix++;
                }
                usedNames.Add(constName);

                string escapedKey = key.Replace("\"", "\\\"");
                sb.AppendLine($"{indent}    public const string {constName} = \"{escapedKey}\";");
            }

            sb.AppendLine($"{indent}}}");
            if (!string.IsNullOrEmpty(keysNamespace))
                sb.AppendLine("}");

            return sb.ToString();
        }

        private string ToUpperSnakeCase(string input)
        {
            string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9_]", "_");
            sanitized = Regex.Replace(sanitized, @"([a-z])([A-Z])", "$1_$2");
            sanitized = Regex.Replace(sanitized, @"_+", "_");
            sanitized = sanitized.Trim('_').ToUpper();
            if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
                sanitized = "_" + sanitized;
            return string.IsNullOrEmpty(sanitized) ? "_EMPTY" : sanitized;
        }

        private string SanitizeIdentifier(string input)
        {
            string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9_]", "_");
            if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
                sanitized = "_" + sanitized;
            return string.IsNullOrEmpty(sanitized) ? "_empty" : sanitized;
        }

        // ===========================
        // TAB 2: Scene Text Scanner
        // ===========================
        private void RenderSceneScanner()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            GUILayout.Label("Scan your scene for hardcoded text in UI components.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5);

            includeInactive = EditorGUILayout.ToggleLeft("  Include inactive GameObjects", includeInactive);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUIStyle scanBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 35
            };
            scanBtnStyle.normal.textColor = Color.white;
            scanBtnStyle.normal.background = MakeTex(2, 2, new Color(0.3f, 0.5f, 0.9f));

            if (GUILayout.Button(new GUIContent("Scan Current Scene", "Searches every active and inactive GameObject in the scene for Text and TextMeshPro components to extract hardcoded strings."), scanBtnStyle))
            {
                ScanScene();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (scannedTexts.Count > 0)
            {
                EditorGUILayout.LabelField($"Found {scannedTexts.Count} text components:", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                scanScrollPos = EditorGUILayout.BeginScrollView(scanScrollPos, GUILayout.MaxHeight(350));

                // Header
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("GameObject", EditorStyles.boldLabel, GUILayout.Width(200));
                GUILayout.Label("Component", EditorStyles.boldLabel, GUILayout.Width(120));
                GUILayout.Label("Text Content", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                foreach (var item in scannedTexts)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    
                    // Clickable object name
                    if (GUILayout.Button(item.objectPath, EditorStyles.linkLabel, GUILayout.Width(200)))
                    {
                        if (item.gameObject != null)
                        {
                            Selection.activeGameObject = item.gameObject;
                            EditorGUIUtility.PingObject(item.gameObject);
                        }
                    }
                    
                    GUILayout.Label(item.componentType, GUILayout.Width(120));
                    
                    string displayText = item.text.Length > 60 ? item.text.Substring(0, 57) + "..." : item.text;
                    GUILayout.Label(displayText);
                    
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(10);

                // Export Button
                if (GUILayout.Button("Export to CSV", GUILayout.Height(30)))
                {
                    ExportScannedTexts();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Click 'Scan Current Scene' to find all hardcoded text strings in your UI components (Text, TextMeshPro).", MessageType.Info);
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void ScanScene()
        {
            scannedTexts.Clear();

            // Scan UnityEngine.UI.Text
            var textComponents = GetSceneObjectsOfType<UnityEngine.UI.Text>(includeInactive);
            foreach (var t in textComponents)
            {
                if (!string.IsNullOrWhiteSpace(t.text))
                {
                    scannedTexts.Add(new ScannedText
                    {
                        objectPath = GetGameObjectPath(t.gameObject),
                        componentType = "UI.Text",
                        text = t.text,
                        gameObject = t.gameObject
                    });
                }
            }

            // Scan TMPro if available
            var tmpType = System.Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            if (tmpType != null)
            {
                var tmpComponents = GetSceneObjectsOfType(tmpType, includeInactive);
                foreach (var obj in tmpComponents)
                {
                    var component = obj as Component;
                    if (component == null) continue;
                    
                    var textProp = tmpType.GetProperty("text");
                    if (textProp != null)
                    {
                        string text = textProp.GetValue(component) as string;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            scannedTexts.Add(new ScannedText
                            {
                                objectPath = GetGameObjectPath(component.gameObject),
                                componentType = "TextMeshPro",
                                text = text,
                                gameObject = component.gameObject
                            });
                        }
                    }
                }
            }

            if (scannedTexts.Count == 0)
            {
                EditorUtility.DisplayDialog("Scan Complete", "No hardcoded text found in the current scene.", "OK");
            }
        }

        private string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private void ExportScannedTexts()
        {
            string savePath = EditorUtility.SaveFilePanel("Export Scanned Texts", "Assets", "scanned_texts", "csv");
            if (string.IsNullOrEmpty(savePath)) return;

            var sb = new StringBuilder();
            sb.AppendLine("Key,English,GameObject,Component");
            int counter = 1;
            foreach (var item in scannedTexts)
            {
                string key = ToUpperSnakeCase(item.text.Length > 30 ? item.text.Substring(0, 30) : item.text);
                if (string.IsNullOrEmpty(key)) key = $"TEXT_{counter}";
                string escapedText = item.text.Replace("\"", "\"\"").Replace("\n", "\\n");
                string escapedPath = item.objectPath.Replace("\"", "\"\"");
                sb.AppendLine($"\"{key}\",\"{escapedText}\",\"{escapedPath}\",\"{item.componentType}\"");
                counter++;
            }
            File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(true));
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Export Complete", $"Exported {scannedTexts.Count} text entries to:\n{savePath}", "OK");
        }

        // ===========================
        // TAB 3: ScriptableObject Export
        // ===========================
        private void RenderSOExport()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            GUILayout.Label("Generate ScriptableObject assets from your localization data.\nCreates one SO per language with all key-value pairs.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(10);

            // File Selection
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source File:", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label(string.IsNullOrEmpty(soInputFilePath) ? "None selected" : Path.GetFileName(soInputFilePath));
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFilePanel("Select CSV or Excel File", "Assets", "csv,xlsx");
                if (!string.IsNullOrEmpty(path))
                {
                    soInputFilePath = path;
                    try
                    {
                        string content = File.ReadAllText(path, Encoding.UTF8);
                        soTableData = ParseCSVSimple(content);
                        if (soTableData.Count > 0) soHeaders = soTableData[0].ToArray();
                    }
                    catch (System.Exception e) { Debug.LogError(e.Message); }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (soHeaders.Length > 0 && soTableData.Count > 1)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox($"Found {soHeaders.Length} columns and {soTableData.Count - 1} rows.\nOne ScriptableObject will be created per language column.", MessageType.Info);

                EditorGUILayout.Space(10);

                GUIStyle soBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 40
                };
                soBtnStyle.normal.textColor = Color.white;
                soBtnStyle.normal.background = MakeTex(2, 2, new Color(0.8f, 0.5f, 0.2f));

                if (GUILayout.Button(new GUIContent("Generate ScriptableObjects", "Creates one ScriptableObject per language containing a highly optimized mapped dictionary of all Translations for fast runtime lookup."), soBtnStyle))
                {
                    GenerateScriptableObjects();
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void GenerateScriptableObjects()
        {
            string folder = EditorUtility.SaveFolderPanel("Select Output Folder for SO Assets", "Assets", "Localization");
            if (string.IsNullOrEmpty(folder)) return;

            // Make path relative to project
            if (folder.StartsWith(Application.dataPath))
                folder = "Assets" + folder.Substring(Application.dataPath.Length);

            // First ensure the SO script exists
            string soScriptPath = folder + "/LocalizationData.cs";
            if (!File.Exists(soScriptPath))
            {
                string soScript = @"using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = ""LocalizationData"", menuName = ""Multilingo/Localization Data"")]
public class LocalizationData : ScriptableObject
{
    public string languageName;
    public string languageCode;
    public List<LocalizationEntry> entries = new List<LocalizationEntry>();

    [System.Serializable]
    public class LocalizationEntry
    {
        public string key;
        public string value;
    }

    public string GetValue(string key)
    {
        var entry = entries.Find(e => e.key == key);
        return entry != null ? entry.value : key;
    }
}
";
                File.WriteAllText(soScriptPath, soScript, Encoding.UTF8);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Script Created",
                    $"Created LocalizationData.cs at:\n{soScriptPath}\n\nPlease wait for Unity to compile, then click 'Generate ScriptableObjects' again to create the assets.",
                    "OK");
                return;
            }

            // Generate one SO per language column (skip first column = keys)
            int keyCol = 0; // Assume first column is keys
            int created = 0;

            for (int c = 1; c < soHeaders.Length; c++)
            {
                string langName = soHeaders[c].Trim();
                if (string.IsNullOrEmpty(langName)) continue;

                string assetPath = $"{folder}/Lang_{langName.Replace(" ", "_").Replace("(", "").Replace(")", "")}.asset";

                // Create the SO
                var so = ScriptableObject.CreateInstance("LocalizationData");
                if (so == null)
                {
                    EditorUtility.DisplayDialog("Error", "LocalizationData ScriptableObject type not found.\nPlease wait for Unity to compile the script first.", "OK");
                    return;
                }

                // Set fields using reflection
                var langField = so.GetType().GetField("languageName");
                var entriesField = so.GetType().GetField("entries");
                
                if (langField != null) langField.SetValue(so, langName);

                if (entriesField != null)
                {
                    var entriesList = entriesField.GetValue(so) as System.Collections.IList;
                    var entryType = System.Type.GetType("LocalizationData+LocalizationEntry, Assembly-CSharp");
                    
                    if (entryType != null && entriesList != null)
                    {
                        for (int r = 1; r < soTableData.Count; r++)
                        {
                            string key = keyCol < soTableData[r].Count ? soTableData[r][keyCol] : "";
                            string val = c < soTableData[r].Count ? soTableData[r][c] : "";
                            if (string.IsNullOrEmpty(key)) continue;

                            var entry = System.Activator.CreateInstance(entryType);
                            entryType.GetField("key")?.SetValue(entry, key);
                            entryType.GetField("value")?.SetValue(entry, val);
                            entriesList.Add(entry);
                        }
                    }
                }

                AssetDatabase.CreateAsset(so, assetPath);
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Export Complete", $"Created {created} ScriptableObject assets in:\n{folder}", "OK");
        }

        // ===========================
        // TAB 4: One-Click Setup
        // ===========================
        private void RenderOneClickSetup()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            GUILayout.Label("Auto-generate the LocalizationSystem GameObject in your scene with all required runtime scripts attached.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(15);

            GUIStyle setupBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                fixedHeight = 50
            };
            setupBtnStyle.normal.textColor = Color.white;
            setupBtnStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.6f, 0.8f));

            if (GUILayout.Button("Create Localization System in Scene", setupBtnStyle))
            {
                CreateLocalizationSystem();
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.HelpBox("This will:\n1. Create a 'LocalizationSystem' GameObject if it doesn't exist.\n2. Attach 'LocalizationManager' and 'LocaleFontSwitcher' components automatically.\n3. Make it ready for your UI localized texts.", MessageType.Info);

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void CreateLocalizationSystem()
        {
            // Find existing
            GameObject locSys = GameObject.Find("LocalizationSystem");
            bool isNew = false;
            if (locSys == null)
            {
                locSys = new GameObject("LocalizationSystem");
                isNew = true;
            }

            // Try to add LocalizationManager
            var locManagerType = typeof(Multilingo.Localization.LocalizationManager);
            if (locManagerType != null)
            {
                if (locSys.GetComponent(locManagerType) == null)
                    locSys.AddComponent(locManagerType);
            }
            else
            {
                Debug.LogWarning("LocalizationManager script not found.");
            }

            // Try to add LocaleFontSwitcher
            var fontSwitcherType = typeof(Multilingo.Localization.LocaleFontSwitcher);
            if (fontSwitcherType != null)
            {
                if (locSys.GetComponent(fontSwitcherType) == null)
                    locSys.AddComponent(fontSwitcherType);
            }
            else
            {
                Debug.LogWarning("LocaleFontSwitcher script not found.");
            }

            Selection.activeGameObject = locSys;
            EditorGUIUtility.PingObject(locSys);

            if (isNew)
                EditorUtility.DisplayDialog("Setup Complete", "Created 'LocalizationSystem' GameObject and attached available runtime scripts.", "OK");
            else
                EditorUtility.DisplayDialog("Setup Complete", "Updated existing 'LocalizationSystem' GameObject with missing runtime scripts.", "OK");
        }

        // ===========================
        // TAB 5: StringTable Sync
        // ===========================
        private void RenderStringTableSync()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            GUILayout.Label("Import translations from CSV/Excel directly into Unity's StringTableCollection.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(10);

            // File Selection
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source CSV/Excel:", EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label(string.IsNullOrEmpty(stInputFilePath) ? "None selected" : Path.GetFileName(stInputFilePath));
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFilePanel("Select CSV or Excel File", "Assets", "csv,xlsx");
                if (!string.IsNullOrEmpty(path))
                {
                    stInputFilePath = path;
                    try
                    {
                        string content = File.ReadAllText(path, Encoding.UTF8);
                        stTableData = ParseCSVSimple(content);
                        if (stTableData.Count > 0) stHeaders = stTableData[0].ToArray();
                    }
                    catch (System.Exception e) { Debug.LogError(e.Message); }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Target Table
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Target StringTable:", EditorStyles.boldLabel, GUILayout.Width(150));
            selectedTableCollection = (StringTableCollection)EditorGUILayout.ObjectField(selectedTableCollection, typeof(StringTableCollection), false);
            EditorGUILayout.EndHorizontal();

            if (stHeaders.Length > 0 && stTableData.Count > 1 && selectedTableCollection != null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox($"Ready to sync {stTableData.Count - 1} entries to '{selectedTableCollection.TableCollectionName}'", MessageType.Info);
                
                GUIStyle syncBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 40
                };
                syncBtnStyle.normal.textColor = Color.white;
                syncBtnStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.7f, 0.4f));

                if (GUILayout.Button("Sync to StringTableCollection", syncBtnStyle))
                {
                    SyncStringTable();
                }
            }

            EditorGUILayout.Space(20);
            GUILayout.Label("Export (Two-Way Sync):", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Export an existing StringTableCollection to a CSV file for external translation.", MessageType.None);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Target StringTable:", EditorStyles.boldLabel, GUILayout.Width(150));
            selectedTableCollection = (StringTableCollection)EditorGUILayout.ObjectField(selectedTableCollection, typeof(StringTableCollection), false);
            EditorGUILayout.EndHorizontal();

            if (selectedTableCollection != null)
            {
                EditorGUILayout.Space(10);
                GUIStyle exportBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 35
                };
                exportBtnStyle.normal.textColor = Color.white;
                exportBtnStyle.normal.background = MakeTex(2, 2, new Color(0.8f, 0.4f, 0.2f));

                if (GUILayout.Button("Export StringTable to CSV", exportBtnStyle))
                {
                    ExportStringTableToCSV();
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void ExportStringTableToCSV()
        {
            if (selectedTableCollection == null) return;
            string savePath = EditorUtility.SaveFilePanel("Export StringTable", "Assets", selectedTableCollection.TableCollectionName + "_Export", "csv");
            if (string.IsNullOrEmpty(savePath)) return;

            var sharedData = selectedTableCollection.SharedData;
            var tables = selectedTableCollection.StringTables;

            var sb = new StringBuilder();
            
            // Build Headers
            var headers = new List<string> { "Key" };
            foreach (var table in tables)
            {
                headers.Add($"{table.LocaleIdentifier.CultureInfo?.EnglishName ?? "Unknown"} ({table.LocaleIdentifier.Code})");
            }

            sb.AppendLine(string.Join(",", headers.Select(h => "\"" + h + "\"")));

            // Rows
            foreach (var entry in sharedData.Entries)
            {
                var rowFields = new List<string>();
                rowFields.Add("\"" + entry.Key.Replace("\"", "\"\"") + "\""); // Key

                foreach (var table in tables)
                {
                    var stringEntry = table.GetEntry(entry.Id);
                    string val = stringEntry != null ? stringEntry.Value : "";
                    rowFields.Add("\"" + val.Replace("\"", "\"\"").Replace("\n", "\\n").Replace("\r", "") + "\"");
                }
                sb.AppendLine(string.Join(",", rowFields));
            }

            File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(true));
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Export Complete", $"Exported {sharedData.Entries.Count} keys to:\n{savePath}", "OK");
        }

        private void RenderAutoTranslate()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            GUILayout.Label("Auto-Translate Missing Keys", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = new Color(0.8f, 0.7f, 1f) } });
            GUILayout.Label("Find empty translation entries in your StringTableCollection and automatically translate them using the AI connected in Converter Mode.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(15);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Target Collection:", EditorStyles.boldLabel, GUILayout.Width(130));
            atTargetCollection = (StringTableCollection)EditorGUILayout.ObjectField(atTargetCollection, typeof(StringTableCollection), false);
            EditorGUILayout.EndHorizontal();

            if (atTargetCollection != null)
            {
                EditorGUILayout.Space(10);
                if (!atIsProcessing && GUILayout.Button(new GUIContent("Scan & Auto-Translate Missing", "Queries the AI provider configured in the main MultiLingo tool to seamlessly detect and automatically inject matching translations for empty rows."), GUILayout.Height(40)))
                {
                    _ = AutoTranslateMissingKeys();
                }
                
                if (atIsProcessing)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.HelpBox(atStatusInfo, MessageType.Info);
                    if (GUILayout.Button("Cancel Processing", GUILayout.Height(30)))
                    {
                        atIsProcessing = false;
                        atStatusInfo = "Cancelled by user.";
                    }
                }
                else if (!string.IsNullOrEmpty(atStatusInfo))
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.HelpBox(atStatusInfo, MessageType.Info);
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private async Task AutoTranslateMissingKeys()
        {
            atIsProcessing = true;
            atStatusInfo = "Scanning for missing translations...";
            Repaint();

            var sharedData = atTargetCollection.SharedData;
            var tables = atTargetCollection.StringTables;
            
            // Find English source table
            var enTable = tables.FirstOrDefault(t => t.LocaleIdentifier.Code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase));
            if (enTable == null)
            {
                atStatusInfo = "Error: StringTableCollection must have an English ('en') locale to act as the source language.";
                atIsProcessing = false;
                Repaint();
                return;
            }

            int translatedCount = 0;
            int totalMissing = 0;

            foreach (var entry in sharedData.Entries)
            {
                var enString = enTable.GetEntry(entry.Id);
                if (enString == null || string.IsNullOrWhiteSpace(enString.Value)) continue;

                foreach (var table in tables)
                {
                    if (table == enTable) continue;
                    var tEntry = table.GetEntry(entry.Id);
                    if (tEntry == null || string.IsNullOrWhiteSpace(tEntry.Value)) totalMissing++;
                }
            }

            if (totalMissing == 0)
            {
                atStatusInfo = "All keys are fully translated! Nothing to do.";
                atIsProcessing = false;
                Repaint();
                return;
            }

            int provider = EditorPrefs.GetInt("Multilingo_Provider", 0);
            string googleApiKey = EditorPrefs.GetString("Multilingo_GoogleApiKey", "");
            string deeplApiKey = EditorPrefs.GetString("Multilingo_DeeplApiKey", "");
            string openAiApiKey = EditorPrefs.GetString("Multilingo_OpenAiApiKey", "");
            string openAiModel = EditorPrefs.GetString("Multilingo_OpenAiModel", "gpt-4o-mini");

            foreach (var entry in sharedData.Entries)
            {
                if (!atIsProcessing) break;
                
                var enString = enTable.GetEntry(entry.Id);
                if (enString == null || string.IsNullOrWhiteSpace(enString.Value)) continue;
                string sourceText = enString.Value;

                foreach (var table in tables)
                {
                    if (!atIsProcessing) break;
                    if (table == enTable) continue;
                    
                    var tEntry = table.GetEntry(entry.Id);
                    if (tEntry != null && !string.IsNullOrWhiteSpace(tEntry.Value)) continue;

                    string targetCode = table.LocaleIdentifier.Code;
                    atStatusInfo = $"Translating '{entry.Key}' to {targetCode}... ({translatedCount}/{totalMissing})";
                    Repaint();

                    string translated = sourceText;

                    try
                    {
                        if (provider == 2 && !string.IsNullOrEmpty(deeplApiKey)) // DeepL
                        {
                            string deepLLang = targetCode.ToUpper();
                            if (deepLLang.StartsWith("ZH")) deepLLang = "ZH";
                            if (deepLLang.StartsWith("EN")) deepLLang = "EN-US";
                            string endpoint = deeplApiKey.EndsWith(":fx") ? "https://api-free.deepl.com/v2/translate" : "https://api.deepl.com/v2/translate";
                            WWWForm form = new WWWForm();
                            form.AddField("text", sourceText);
                            form.AddField("target_lang", deepLLang);
                            using (UnityWebRequest req = UnityWebRequest.Post(endpoint, form))
                            {
                                req.SetRequestHeader("Authorization", "DeepL-Auth-Key " + deeplApiKey);
                                var op = req.SendWebRequest();
                                while (!op.isDone) await Task.Yield();
                                if (req.result == UnityWebRequest.Result.Success) {
                                    var m = Regex.Match(req.downloadHandler.text, @"""text"":""(.*?)""");
                                    if (m.Success) translated = Regex.Unescape(m.Groups[1].Value);
                                }
                            }
                        }
                        else if (provider == 3 && !string.IsNullOrEmpty(openAiApiKey)) // OpenAI
                        {
                            string endpoint = "https://api.openai.com/v1/chat/completions";
                            string jsonBody = "{\"model\": \"" + openAiModel + "\", \"messages\": [{\"role\": \"system\", \"content\": \"Translate the given text directly into " + targetCode + " without any additional commentary. Preserve any formatting placeholders (like {0}).\"}, {\"role\": \"user\", \"content\": \"" + sourceText.Replace("\"", "\\\"").Replace("\n", "\\n") + "\"}]}";
                            using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
                            {
                                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                                req.downloadHandler = new DownloadHandlerBuffer();
                                req.SetRequestHeader("Content-Type", "application/json");
                                req.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
                                var op = req.SendWebRequest();
                                while (!op.isDone) await Task.Yield();
                                if (req.result == UnityWebRequest.Result.Success) {
                                    var m = Regex.Match(req.downloadHandler.text, @"""content"":\s*""(.*?)""");
                                    if (m.Success) translated = Regex.Unescape(m.Groups[1].Value);
                                }
                            }
                        }
                        else if (provider == 1 && !string.IsNullOrEmpty(googleApiKey)) // Google Cloud
                        {
                            string url = $"https://translation.googleapis.com/language/translate/v2?key={googleApiKey}";
                            string jsonBody = $"{{\"q\": \"{sourceText.Replace("\"", "\\\"").Replace("\n", "\\n")}\", \"target\": \"{targetCode}\", \"format\": \"text\"}}";
                            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
                            {
                                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                                req.downloadHandler = new DownloadHandlerBuffer();
                                req.SetRequestHeader("Content-Type", "application/json");
                                var op = req.SendWebRequest();
                                while (!op.isDone) await Task.Yield();
                                if (req.result == UnityWebRequest.Result.Success) {
                                    var m = Regex.Match(req.downloadHandler.text, @"""translatedText"":\s*""(.*?)""");
                                    if (m.Success) translated = Regex.Unescape(m.Groups[1].Value);
                                }
                            }
                        }
                        else // Google Free
                        {
                            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetCode}&dt=t&q={UnityWebRequest.EscapeURL(sourceText)}";
                            using (UnityWebRequest req = UnityWebRequest.Get(url))
                            {
                                var op = req.SendWebRequest();
                                while (!op.isDone) await Task.Yield();
                                if (req.result == UnityWebRequest.Result.Success)
                                {
                                    StringBuilder fullT = new StringBuilder();
                                    string json = req.downloadHandler.text;
                                    int endOfFirstArray = json.IndexOf(",null,");
                                    if (endOfFirstArray == -1) endOfFirstArray = json.Length;
                                    string arrayData = json.Substring(0, endOfFirstArray);
                                    MatchCollection matches = Regex.Matches(arrayData, @"\[""((?:[^""\\]|\\.)*)"",""(?:[^""\\]|\\.)*""");
                                    foreach(Match m in matches) fullT.Append(m.Groups[1].Value);
                                    if (fullT.Length > 0) translated = Regex.Unescape(fullT.ToString());
                                }
                            }
                        }
                    } catch { } 

                    if (tEntry == null) table.AddEntry(entry.Id, translated);
                    else tEntry.Value = translated;

                    EditorUtility.SetDirty(table);
                    translatedCount++;
                    await Task.Delay(150); 
                }
            }

            if (atIsProcessing)
            {
                AssetDatabase.SaveAssets();
                atStatusInfo = $"Successfully translated {translatedCount} missing keys!";
                atIsProcessing = false;
                Repaint();
                EditorUtility.DisplayDialog("Auto-Translate Complete", atStatusInfo, "OK");
            }
        }

        private void SyncStringTable()
        {
            if (selectedTableCollection == null || stTableData.Count < 2) return;

            int keyCol = 0; // Assume keys are in first column
            var sharedData = selectedTableCollection.SharedData;
            var locales = LocalizationEditorSettings.GetLocales();

            int addedOrUpdated = 0;

            for (int r = 1; r < stTableData.Count; r++)
            {
                if (keyCol >= stTableData[r].Count) continue;
                string key = stTableData[r][keyCol];
                if (string.IsNullOrWhiteSpace(key)) continue;

                var sharedEntry = sharedData.GetEntry(key) ?? sharedData.AddKey(key);

                for (int c = 1; c < stHeaders.Length; c++)
                {
                    string headerName = stHeaders[c].Trim();
                    string value = c < stTableData[r].Count ? stTableData[r][c] : "";

                    // Find matching locale table
                    foreach (var table in selectedTableCollection.StringTables)
                    {
                        var locale = table.LocaleIdentifier;
                        if (locale.Code.Contains(headerName) || headerName.Contains(locale.Code) || 
                            (locale.CultureInfo != null && locale.CultureInfo.EnglishName.Contains(headerName)))
                        {
                            var entry = table.GetEntry(sharedEntry.Id);
                            if (entry == null) table.AddEntry(key, value);
                            else entry.Value = value;
                        }
                    }
                }
                addedOrUpdated++;
            }

            EditorUtility.SetDirty(selectedTableCollection);
            foreach (var table in selectedTableCollection.StringTables)
            {
                EditorUtility.SetDirty(table);
            }
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Sync Complete", $"Successfully synced {addedOrUpdated} keys to '{selectedTableCollection.TableCollectionName}'.", "OK");
        }

        // ===========================
        // TAB 6: Google Sheets Sync
        // ===========================
        private void RenderGoogleSheetsSync()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            GUILayout.Label("Sync directly from a published Google Sheet CSV URL.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5);

            GUILayout.Label("Google Sheets CSV URL:", EditorStyles.boldLabel);
            gsUrl = EditorGUILayout.TextField(gsUrl);
            GUILayout.Label("Tip: In Google Sheets, go to File > Share > Publish to web. Choose 'CSV' format.", EditorStyles.miniLabel);

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Target StringTable:", EditorStyles.boldLabel, GUILayout.Width(150));
            gsTargetCollection = (StringTableCollection)EditorGUILayout.ObjectField(gsTargetCollection, typeof(StringTableCollection), false);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(gsUrl) && gsTargetCollection != null)
            {
                EditorGUILayout.Space(15);
                
                GUIStyle syncBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 40
                };
                syncBtnStyle.normal.textColor = Color.white;
                syncBtnStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.5f, 0.8f));

                if (GUILayout.Button("Download & Sync Google Sheet", syncBtnStyle))
                {
                    _ = DownloadAndSyncGoogleSheet();
                }
            }

            if (!string.IsNullOrEmpty(gsStatus))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(gsStatus, MessageType.Info);
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private async Task DownloadAndSyncGoogleSheet()
        {
            gsStatus = "Downloading...";
            Repaint();
            try
            {
                using (var client = new HttpClient())
                {
                    string content = await client.GetStringAsync(gsUrl);
                    gsStatus = "Parsing CSV...";
                    Repaint();
                    var data = ParseCSVSimple(content);
                    if (data.Count < 2)
                    {
                        gsStatus = "Error: CSV data is empty or invalid.";
                        Repaint();
                        return;
                    }
                    
                    stTableData = data;
                    stHeaders = data[0].ToArray();
                    selectedTableCollection = gsTargetCollection;
                    
                    gsStatus = "Applying to Unity Localization...";
                    Repaint();
                    SyncStringTable();
                    gsStatus = $"Successfully synced {data.Count - 1} keys from Google Sheets!";
                    Repaint();
                }
            }
            catch (System.Exception ex)
            {
                gsStatus = "Failed: " + ex.Message;
                Debug.LogError("Google Sheets Sync Error: " + ex);
                Repaint();
            }
        }

        // ===========================
        // TAB 7: Font Optimizer
        // ===========================
        private void RenderFontOptimizer()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            GUILayout.Label("Extract all unique characters from translations for TMP Font Asset creation.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source CSV/Excel:", EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label(string.IsNullOrEmpty(fontInputFilePath) ? "None selected" : Path.GetFileName(fontInputFilePath));
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFilePanel("Select File", "Assets", "csv,xlsx");
                if (!string.IsNullOrEmpty(path))
                {
                    fontInputFilePath = path;
                    fontUniqueChars = "";
                    try
                    {
                        string content = File.ReadAllText(path, Encoding.UTF8);
                        fontTableData = ParseCSVSimple(content);
                    }
                    catch (System.Exception e) { Debug.LogError(e.Message); }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (fontTableData.Count > 0)
            {
                EditorGUILayout.Space(10);
                if (GUILayout.Button("Extract Unique Characters (All Languages)", GUILayout.Height(30)))
                {
                    ExtractFontChars();
                }
            }

            if (!string.IsNullOrEmpty(fontUniqueChars))
            {
                EditorGUILayout.Space(10);
                GUILayout.Label($"Unique Characters: {fontUniqueChars.Length}", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(fontUniqueChars, GUILayout.Height(100));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(30)))
                {
                    GUIUtility.systemCopyBuffer = fontUniqueChars;
                    EditorUtility.DisplayDialog("Copied", "Characters copied to clipboard! Paste this into the TextMeshPro Font Asset Creator.", "OK");
                }
                if (GUILayout.Button("Save as .txt", GUILayout.Height(30)))
                {
                    string savePath = EditorUtility.SaveFilePanel("Save Unique Characters", "Assets", "UniqueCharacters", "txt");
                    if (!string.IsNullOrEmpty(savePath))
                    {
                        File.WriteAllText(savePath, fontUniqueChars, Encoding.UTF8);
                        AssetDatabase.Refresh();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void ExtractFontChars()
        {
            var unique = new HashSet<char>();
            for (int r = 1; r < fontTableData.Count; r++)
            {
                for (int c = 1; c < fontTableData[r].Count; c++)
                {
                    string text = fontTableData[r][c];
                    foreach (char ch in text)
                    {
                        if (!char.IsControl(ch)) unique.Add(ch);
                    }
                }
            }
            // Ensure space is always included
            unique.Add(' ');
            
            var sorted = unique.ToList();
            sorted.Sort();
            fontUniqueChars = new string(sorted.ToArray());
        }

        // ===========================
        // TAB 9: Auto Voice-Over (TTS)
        // ===========================
        private void RenderAutoVoiceOver()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            GUILayout.Label("Auto Voice-Over Generator (TTS)", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = new Color(0.9f, 0.6f, 0.4f) } });
            GUILayout.Label("Use OpenAI's TTS to generate synthesized localized voice dialogue from your StringTableCollection and map them directly into an AssetTableCollection.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(15);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source StringTable:", EditorStyles.boldLabel, GUILayout.Width(140));
            voStringCollection = (StringTableCollection)EditorGUILayout.ObjectField(voStringCollection, typeof(StringTableCollection), false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Target AssetTable:", EditorStyles.boldLabel, GUILayout.Width(140));
            voAssetCollection = (AssetTableCollection)EditorGUILayout.ObjectField(voAssetCollection, typeof(AssetTableCollection), false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("OpenAI Voice:", EditorStyles.boldLabel, GUILayout.Width(140));
            voSelectedVoiceIndex = EditorGUILayout.Popup(voSelectedVoiceIndex, voVoices);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Save Folder:", EditorStyles.boldLabel, GUILayout.Width(140));
            voOutputFolder = EditorGUILayout.TextField(voOutputFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (path.StartsWith(Application.dataPath))
                {
                    voOutputFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            
            string apiKey = EditorPrefs.GetString("Multilingo_OpenAiApiKey", "");
            if (string.IsNullOrEmpty(apiKey))
            {
                EditorGUILayout.HelpBox("OpenAI API Key is required. Please set it in the Converter Mode UI.", MessageType.Error);
            }
            else if (voStringCollection != null && voAssetCollection != null)
            {
                if (!voIsProcessing && GUILayout.Button(new GUIContent("Generate Voice-Overs & Map Assets", "Uses OpenAI's TTS to generate audio dialogue specifically corresponding to each Translation entry, seamlessly storing them into Unity's AssetTable."), GUILayout.Height(40)))
                {
                    _ = GenerateAutoVoiceOvers();
                }

                if (voIsProcessing)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.HelpBox(voStatusInfo, MessageType.Info);
                    if (GUILayout.Button("Cancel Processing", GUILayout.Height(30)))
                    {
                        voIsProcessing = false;
                        voStatusInfo = "Cancelled by user.";
                    }
                }
                else if (!string.IsNullOrEmpty(voStatusInfo))
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.HelpBox(voStatusInfo, MessageType.Info);
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private async Task GenerateAutoVoiceOvers()
        {
            voIsProcessing = true;
            voStatusInfo = "Initializing Voice-Over Generation...";
            Repaint();

            if (!Directory.Exists(voOutputFolder))
            {
                Directory.CreateDirectory(voOutputFolder);
                AssetDatabase.Refresh();
            }

            var stringData = voStringCollection.SharedData;
            var stringTables = voStringCollection.StringTables;
            var assetTables = voAssetCollection.AssetTables;
            var assetShared = voAssetCollection.SharedData;

            string apiKey = EditorPrefs.GetString("Multilingo_OpenAiApiKey", "");
            string selectedVoice = voVoices[voSelectedVoiceIndex];

            int generatedCount = 0;

            foreach (var sEntry in stringData.Entries)
            {
                if (!voIsProcessing) break;
                
                var aEntry = assetShared.GetEntry(sEntry.Key) ?? assetShared.AddKey(sEntry.Key);

                foreach (var sTable in stringTables)
                {
                    if (!voIsProcessing) break;

                    string targetCode = sTable.LocaleIdentifier.Code;
                    var localString = sTable.GetEntry(sEntry.Id);

                    if (localString == null || string.IsNullOrWhiteSpace(localString.Value)) continue;

                    string text = localString.Value;

                    var aTable = assetTables.FirstOrDefault(t => t.LocaleIdentifier.Code == targetCode);
                    if (aTable == null) continue;

                    var localAsset = aTable.GetEntry(aEntry.Id);
                    if (localAsset != null && !localAsset.IsEmpty) continue;

                    voStatusInfo = $"TTS: '{sEntry.Key}' to {targetCode}... ({generatedCount} done)";
                    Repaint();

                    string safeKey = SanitizeIdentifier(sEntry.Key);
                    string fileName = $"{safeKey}_{targetCode}.mp3";
                    string filePath = Path.Combine(voOutputFolder, fileName);
                    string safePath = filePath.Replace("\\", "/");

                    bool success = false;
                    try
                    {
                        string endpoint = "https://api.openai.com/v1/audio/speech";
                        string jsonBody = "{\"model\": \"tts-1\", \"input\": \"" + text.Replace("\"", "\\\"").Replace("\n", " ") + "\", \"voice\": \"" + selectedVoice + "\"}";
                        
                        using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
                        {
                            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                            req.downloadHandler = new DownloadHandlerBuffer();
                            req.SetRequestHeader("Content-Type", "application/json");
                            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                            
                            var op = req.SendWebRequest();
                            while (!op.isDone) await Task.Yield();

                            if (req.result == UnityWebRequest.Result.Success)
                            {
                                File.WriteAllBytes(safePath, req.downloadHandler.data);
                                success = true;
                            }
                            else
                            {
                                Debug.LogError($"TTS failed for {targetCode} '{sEntry.Key}': {req.error}");
                            }
                        }
                    }
                    catch (System.Exception e) { Debug.LogError("Error generating TTS: " + e.Message); }

                    if (success)
                    {
                        AssetDatabase.ImportAsset(safePath, ImportAssetOptions.ForceUpdate);
                        await Task.Delay(500);
                        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(safePath);

                        if (clip != null)
                        {
                            voAssetCollection.AddAssetToTable(aTable, aEntry.Id, clip);
                            EditorUtility.SetDirty(aTable);
                        }
                        
                        generatedCount++;
                    }
                    await Task.Delay(250); 
                }
            }

            if (voIsProcessing)
            {
                EditorUtility.SetDirty(voAssetCollection);
                AssetDatabase.SaveAssets();
                voStatusInfo = $"Successfully generated and mapped {generatedCount} voice-over clips!";
                voIsProcessing = false;
                Repaint();
                EditorUtility.DisplayDialog("Auto Voice-Over Complete", voStatusInfo, "OK");
            }
        }

        // ===========================
        // TAB 10: Pseudo-Localize
        // ===========================
        private void RenderPseudoLocalize()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            GUILayout.Label("Pseudo-Localization Generator", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = new Color(0.4f, 0.9f, 0.6f) } });
            GUILayout.Label("Instantly create a Pseudo-Locale (e.g., [Šţäŕţ Ğäɱé~~~~~]) from your English StringTable to stress-test your UI layouts for text expansion.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(15);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source Collection:", EditorStyles.boldLabel, GUILayout.Width(130));
            plSourceCollection = (StringTableCollection)EditorGUILayout.ObjectField(plSourceCollection, typeof(StringTableCollection), false);
            EditorGUILayout.EndHorizontal();

            if (plSourceCollection != null)
            {
                EditorGUILayout.Space(10);
                if (GUILayout.Button(new GUIContent("Generate Pseudo-Locale Table", "Translates all default English text into an intentionally expanded fake language to verify UI element constraints."), GUILayout.Height(40)))
                {
                    GeneratePseudoLocalization();
                }

                if (!string.IsNullOrEmpty(plStatusInfo))
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.HelpBox(plStatusInfo, MessageType.Info);
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void GeneratePseudoLocalization()
        {
            if (plSourceCollection == null) return;
            var tables = plSourceCollection.StringTables;
            var sharedData = plSourceCollection.SharedData;

            var enTable = tables.FirstOrDefault(t => t.LocaleIdentifier.Code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase));
            if (enTable == null)
            {
                plStatusInfo = "Error: Could not find an English ('en') source table in the collection.";
                return;
            }

            // Pseudo-Locale Code
            string pseudoCode = "qps-ploc";
            var pseudoTable = tables.FirstOrDefault(t => t.LocaleIdentifier.Code == pseudoCode);
            
            if (pseudoTable == null)
            {
                plStatusInfo = $"Error: Please add the Pseudo-Locale (Code: {pseudoCode}) to your Localization Settings first.";
                return;
            }

            int generatedCount = 0;
            string charMap = "AÅBßCÇDÐEÉFƑGĞHĤIÎJĴKĶLĻMɱNÑOÖPÞQɊRŔSŠTŢUÜVṼWŴXӾYÝZŽaåbßcçdðeéfƒgğhĥiîjĵkķlļmɱnñoöpþqɋrŕsštţuüvṽwŵxӿyýzž";
            
            foreach (var entry in sharedData.Entries)
            {
                var enString = enTable.GetEntry(entry.Id);
                if (enString == null || string.IsNullOrWhiteSpace(enString.Value)) continue;

                string sourceText = enString.Value;
                StringBuilder pText = new StringBuilder("[");
                
                bool inTag = false;
                bool inPlaceholder = false;

                foreach (char c in sourceText)
                {
                    if (c == '<') inTag = true;
                    if (c == '{') inPlaceholder = true;

                    if (inTag || inPlaceholder)
                    {
                        pText.Append(c);
                    }
                    else
                    {
                        int mapIndex = charMap.IndexOf(c);
                        if (mapIndex != -1 && mapIndex % 2 == 0 && mapIndex + 1 < charMap.Length)
                        {
                            pText.Append(charMap[mapIndex + 1]);
                        }
                        else
                        {
                            pText.Append(c);
                        }
                    }

                    if (c == '>') inTag = false;
                    if (c == '}') inPlaceholder = false;
                }

                // Add expansion (simulate 30-40% longer strings like German/Russian)
                int expansionLength = Mathf.Max(3, (int)(sourceText.Length * 0.35f));
                pText.Append(new string('~', expansionLength));
                pText.Append("]");

                var tEntry = pseudoTable.GetEntry(entry.Id);
                if (tEntry == null) pseudoTable.AddEntry(entry.Id, pText.ToString());
                else tEntry.Value = pText.ToString();

                generatedCount++;
            }

            EditorUtility.SetDirty(pseudoTable);
            AssetDatabase.SaveAssets();

            plStatusInfo = $"Successfully generated {generatedCount} pseudo-localized keys!";
            EditorUtility.DisplayDialog("Pseudo-Localization Complete", plStatusInfo, "OK");
        }

        // ===========================
        // TAB 11: Table Doctor
        // ===========================
        private void RenderTableDoctor()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            GUILayout.Label("Table Doctor & Integrity Checker", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = new Color(1f, 0.4f, 0.4f) } });
            GUILayout.Label("Automatically scan your entire Translation Collection to find completely empty keys, missing regional translations, and trailing whitespace bugs.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(15);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Target Collection:", EditorStyles.boldLabel, GUILayout.Width(130));
            tdTargetCollection = (LocalizationTableCollection)EditorGUILayout.ObjectField(tdTargetCollection, typeof(LocalizationTableCollection), false);
            EditorGUILayout.EndHorizontal();

            if (tdTargetCollection != null)
            {
                EditorGUILayout.Space(10);
                if (GUILayout.Button(new GUIContent("Run Health Diagnosis", "Checks the entire dictionary structure to detect empty values, leading/trailing whitespace bugs, and completely unused keys."), GUILayout.Height(40)))
                {
                    RunTableDoctor();
                }

                if (tdReportLogs.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    GUILayout.Label($"Diagnosis Report: ({tdReportLogs.Count} Issues Found)", EditorStyles.boldLabel);
                    
                    tdScrollPos = EditorGUILayout.BeginScrollView(tdScrollPos, "box", GUILayout.Height(200));
                    foreach (var log in tdReportLogs)
                    {
                        EditorGUILayout.LabelField(log, EditorStyles.wordWrappedMiniLabel);
                    }
                    EditorGUILayout.EndScrollView();
                }
                else if (!string.IsNullOrEmpty(tdStatusInfo))
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.HelpBox(tdStatusInfo, MessageType.Info);
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void RunTableDoctor()
        {
            if (tdTargetCollection == null) return;
            
            tdReportLogs.Clear();
            var sharedData = tdTargetCollection.SharedData;
            
            int totalEmptyKeys = 0;
            int missingTranslations = 0;
            int whitespaceIssues = 0;

            bool isStringCollection = tdTargetCollection is StringTableCollection;
            
            if (isStringCollection)
            {
                var tables = ((StringTableCollection)tdTargetCollection).StringTables;
                foreach (var entry in sharedData.Entries)
                {
                    bool isCompletelyEmpty = true;
                    foreach (var table in tables)
                    {
                        var sEntry = table.GetEntry(entry.Id);
                        if (sEntry == null || string.IsNullOrEmpty(sEntry.Value))
                        {
                            tdReportLogs.Add($"[Missing] Key '{entry.Key}': Missing in {table.LocaleIdentifier.Code}");
                            missingTranslations++;
                        }
                        else
                        {
                            isCompletelyEmpty = false;
                            if (sEntry.Value.StartsWith(" ") || sEntry.Value.EndsWith(" ") || sEntry.Value.EndsWith("\n"))
                            {
                                tdReportLogs.Add($"[Whitespace] Key '{entry.Key}': Trailing/leading whitespace found in {table.LocaleIdentifier.Code}");
                                whitespaceIssues++;
                            }
                        }
                    }
                    if (isCompletelyEmpty)
                    {
                        tdReportLogs.Insert(0, $"[DEAD KEY] '{entry.Key}': No data found in ANY locale.");
                        totalEmptyKeys++;
                    }
                }
            }
            else
            {
                var tables = ((AssetTableCollection)tdTargetCollection).AssetTables;
                foreach (var entry in sharedData.Entries)
                {
                    bool isCompletelyEmpty = true;
                    foreach (var table in tables)
                    {
                        var aEntry = table.GetEntry(entry.Id);
                        if (aEntry == null || aEntry.IsEmpty)
                        {
                            tdReportLogs.Add($"[Missing Asset] Key '{entry.Key}': Missing asset in {table.LocaleIdentifier.Code}");
                            missingTranslations++;
                        }
                        else
                        {
                            isCompletelyEmpty = false;
                        }
                    }
                    if (isCompletelyEmpty)
                    {
                        tdReportLogs.Insert(0, $"[DEAD KEY] '{entry.Key}': No data found in ANY locale.");
                        totalEmptyKeys++;
                    }
                }
            }

            if (tdReportLogs.Count == 0)
            {
                tdStatusInfo = "Table is perfectly healthy! No missing entries or whitespace issues found.";
            }
            else
            {
                tdStatusInfo = $"Diagnosis complete. Found {totalEmptyKeys} Dead Keys, {missingTranslations} Missing Entries, and {whitespaceIssues} Whitespace Issues.";
            }

            Repaint();
        }

        // ===========================
        // TAB 12: Progress Report
        // ===========================
        private void RenderProgressReport()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            GUILayout.Label("Project Progress Report", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = new Color(0.4f, 0.7f, 1f) } });
            GUILayout.Label("Calculate the exact completion percentage of translations across all your string tables. Excellent for producers tracking localization milestone progress.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(15);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Target Collection:", EditorStyles.boldLabel, GUILayout.Width(130));
            prTargetCollection = (StringTableCollection)EditorGUILayout.ObjectField(prTargetCollection, typeof(StringTableCollection), false);
            EditorGUILayout.EndHorizontal();

            if (prTargetCollection != null)
            {
                EditorGUILayout.Space(10);
                if (GUILayout.Button(new GUIContent("Generate Progress Report", "Iterates through the translations of all languages and returns the absolute percentage of completely localized entries."), GUILayout.Height(40)))
                {
                    GenerateProgressReport();
                }

                if (prReportLogs.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    GUILayout.Label("Translation Progress:", EditorStyles.boldLabel);
                    
                    prScrollPos = EditorGUILayout.BeginScrollView(prScrollPos, "box", GUILayout.Height(200));
                    foreach (var log in prReportLogs)
                    {
                        EditorGUILayout.LabelField(log, EditorStyles.wordWrappedMiniLabel);
                    }
                    EditorGUILayout.EndScrollView();
                }
                else if (!string.IsNullOrEmpty(prStatusInfo))
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.HelpBox(prStatusInfo, MessageType.Info);
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void GenerateProgressReport()
        {
            if (prTargetCollection == null) return;
            
            prReportLogs.Clear();
            var sharedData = prTargetCollection.SharedData;
            var tables = prTargetCollection.StringTables;

            int totalKeysCount = sharedData.Entries.Count;

            if (totalKeysCount == 0)
            {
                prStatusInfo = "Table is completely empty. 0 Keys found.";
                return;
            }

            prReportLogs.Add($"Total Project Localization Keys: {totalKeysCount}\n");

            foreach (var table in tables)
            {
                int translatedCount = 0;
                
                foreach (var entry in sharedData.Entries)
                {
                    var sEntry = table.GetEntry(entry.Id);
                    if (sEntry != null && !string.IsNullOrWhiteSpace(sEntry.Value))
                    {
                        translatedCount++;
                    }
                }

                float percentage = ((float)translatedCount / totalKeysCount) * 100f;
                string localeName = table.LocaleIdentifier.Code.ToUpper();
                string status = percentage >= 100f ? "✅ (Complete)" : "⚠️ (In Progress)";

                prReportLogs.Add($"[{localeName}] - {percentage:F1}% Completed  ({translatedCount}/{totalKeysCount}) {status}");
            }

            prStatusInfo = "Report Generated!";
            Repaint();
        }

        // --- Utilities ---
        private List<List<string>> ParseCSVSimple(string content)
        {
            var result = new List<List<string>>();
            var currentLine = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < content.Length && content[i + 1] == '"') { currentField.Append('"'); i++; }
                    else if (c == '"') inQuotes = false;
                    else currentField.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { currentLine.Add(currentField.ToString()); currentField.Clear(); }
                    else if (c == '\n' || (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n'))
                    {
                        currentLine.Add(currentField.ToString()); currentField.Clear();
                        result.Add(currentLine); currentLine = new List<string>();
                        if (c == '\r') i++;
                    }
                    else if (c != '\r') currentField.Append(c);
                }
            }
            if (currentField.Length > 0 || currentLine.Count > 0)
            {
                currentLine.Add(currentField.ToString());
                result.Add(currentLine);
            }
            if (result.Count > 0 && result.Last().Count == 1 && string.IsNullOrWhiteSpace(result.Last()[0]))
                result.RemoveAt(result.Count - 1);
            return result;
        }

        // Texture cache to prevent creating new Texture2D objects every frame (causes black windows)
        private static Dictionary<Color, Texture2D> _texCache = new Dictionary<Color, Texture2D>();
        private static Texture2D MakeTex(int width, int height, Color col)
        {
            if (_texCache.TryGetValue(col, out Texture2D cached) && cached != null)
                return cached;
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.hideFlags = HideFlags.HideAndDontSave;
            result.SetPixels(pix);
            result.Apply();
            _texCache[col] = result;
            return result;
        }

        private Object[] GetSceneObjectsOfType(System.Type type, bool includeInactive)
        {
            if (includeInactive)
            {
                var all = Resources.FindObjectsOfTypeAll(type);
                return all.Where(o => o is Component c && c.gameObject.scene.isLoaded).ToArray();
            }
            return UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
        }

        private T[] GetSceneObjectsOfType<T>(bool includeInactive) where T : Component
        {
            if (includeInactive)
            {
                var all = Resources.FindObjectsOfTypeAll<T>();
                return all.Where(c => c.gameObject.scene.isLoaded).ToArray();
            }
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        }
    }
}
