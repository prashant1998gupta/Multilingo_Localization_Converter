#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;

namespace Multilingo.Localization.Editor
{
    [CustomEditor(typeof(LocalizedText))]
    public class LocalizedTextEditor : UnityEditor.Editor
    {
        SerializedProperty tableProp;
        SerializedProperty entryKeyProp;
        SerializedProperty useAsyncProp;
        SerializedProperty formatArgsProp;

        List<StringTableCollection> tableCollections = new List<StringTableCollection>();
        List<string> tableNames = new List<string>();
        List<string> allEntryKeys = new List<string>();
        List<string> filteredKeys = new List<string>();
        Dictionary<string, string> englishCache = new Dictionary<string, string>();

        int selectedTableIndex = -1;
        int selectedKeyIndex = -1;
        string searchTerm = "";

        Vector2 listScroll;
        bool showCreateEntry = false;
        string newEntryKey = "";
        string newEntryValue = "";

        readonly float rowHeight = 38f;
        readonly Color accent = new Color(0.6f, 0.5f, 1f); // Vibrant Purple consistent with other tools
        readonly Color bg1 = new Color(0.11f, 0.11f, 0.11f, 0.55f);
        readonly Color bg2 = new Color(0.08f, 0.08f, 0.08f, 0.55f);
        readonly Color sel = new Color(0.6f, 0.5f, 1f, 0.20f);

        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle primaryButtonStyle;

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        void OnEnable()
        {
            tableProp = serializedObject.FindProperty("table");
            entryKeyProp = serializedObject.FindProperty("entryKey");
            useAsyncProp = serializedObject.FindProperty("useAsync");
            formatArgsProp = serializedObject.FindProperty("formatArgs");

            RefreshTables();
        }

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = accent } };
                subHeaderStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
                
                primaryButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 30,
                    normal = { textColor = Color.white }
                };
                primaryButtonStyle.normal.background = MakeTex(2, 2, new Color(0.35f, 0.25f, 0.8f));
                primaryButtonStyle.hover.background = MakeTex(2, 2, new Color(0.45f, 0.35f, 0.9f));
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InitializeStyles();

            DrawPremiumHeader();

            EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 10, 10) });
            DrawTopBar();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            DrawCreateEntry();

            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 10, 10) });
            DrawSearchBar();
            EditorGUILayout.Space(8);
            DrawList();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 10, 10) });
            DrawBottomBar();
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawPremiumHeader()
        {
            EditorGUILayout.BeginVertical(new GUIStyle("window") { padding = new RectOffset(10, 10, 10, 10) });
            GUILayout.Label("✨ Localized Text Engine", headerStyle);
            GUILayout.Label("Smart UI Translation & Automatic Asset Binding", subHeaderStyle);
            EditorGUILayout.EndVertical();
        }

        void DrawTopBar()
        {
            GUILayout.Label(new GUIContent("🔑 Key Selection", "Select the table and entry key for this UI element."), EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Selected Key", EditorStyles.miniLabel);
            GUI.enabled = false;
            EditorGUILayout.TextField(entryKeyProp.stringValue, GUILayout.Height(24));
            GUI.enabled = true;
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(130));
            EditorGUILayout.Space(18);
            if (GUILayout.Button(new GUIContent("🔄 Refresh", "Reload Tables from project."), GUILayout.Height(24)))
                RefreshTables();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            if (GUILayout.Button(new GUIContent("📥 Open Official Localization Tables", "Opens the native Unity Localization Tables window."), EditorStyles.miniButton))
                TryOpenLocalizationWindow();

            // Missing Key Warning
            if (!string.IsNullOrEmpty(entryKeyProp.stringValue) && !allEntryKeys.Contains(entryKeyProp.stringValue))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"The key '{entryKeyProp.stringValue}' was not found in table '{tableProp.stringValue}'. It might have been deleted or renamed.", MessageType.Error);
            }
        }

        void DrawCreateEntry()
        {
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            showCreateEntry = EditorGUILayout.Foldout(showCreateEntry, "➕ Create New Translation Key", true, foldoutStyle);

            if (showCreateEntry)
            {
                EditorGUILayout.BeginVertical("box");
                
                newEntryKey = EditorGUILayout.TextField(new GUIContent("New Key Name", "Use snake_case or SCREAM_CASE (e.g. menu_start_btn)."), newEntryKey);
                newEntryValue = EditorGUILayout.TextField(new GUIContent("English Value", "The initial English translation for this key."), newEntryValue);

                EditorGUILayout.Space(5);
                
                GUI.enabled = !string.IsNullOrWhiteSpace(newEntryKey) && selectedTableIndex >= 0;
                if (GUILayout.Button("Create & Assign Key", primaryButtonStyle))
                {
                    CreateAndAssignKey();
                }
                GUI.enabled = true;

                if (selectedTableIndex < 0)
                    EditorGUILayout.HelpBox("Select a table first.", MessageType.Info);

                EditorGUILayout.EndVertical();
            }
        }

        void CreateAndAssignKey()
        {
            if (selectedTableIndex < 0 || selectedTableIndex >= tableCollections.Count) return;

            var col = tableCollections[selectedTableIndex];
            
            // Add to Shared Data
            var sharedEntry = col.SharedData.AddKey(newEntryKey);
            
            // Add to English Table
            if (englishTable != null)
            {
                englishTable.AddEntry(newEntryKey, newEntryValue);
                EditorUtility.SetDirty(englishTable);
            }
            
            EditorUtility.SetDirty(col.SharedData);
            AssetDatabase.SaveAssets();

            entryKeyProp.stringValue = newEntryKey;
            showCreateEntry = false;
            newEntryKey = "";
            newEntryValue = "";
            
            RefreshKeys();
            ApplyPreviewToRuntime();
        }

        void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("🔍 Search Database", "Filter keys by name or English content."), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                searchTerm = "";
                ApplyFilter();
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(4);
            string newText = EditorGUILayout.TextField(searchTerm, (GUIStyle)"SearchTextField");
            if (newText != searchTerm)
            {
                searchTerm = newText;
                ApplyFilter();
            }
        }

        void DrawList()
        {
            int idx = tableNames.IndexOf(tableProp.stringValue);
            if (idx < 0) idx = 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Source Table:", "The String Table Collection to pull from."), GUILayout.Width(100));
            int newIdx = EditorGUILayout.Popup(idx, tableNames.ToArray());
            EditorGUILayout.EndHorizontal();

            if (newIdx != idx)
            {
                tableProp.stringValue = tableNames[newIdx];
                selectedTableIndex = newIdx;
                RefreshKeys();
            }

            EditorGUILayout.Space(8);

            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(250));

            for (int i = 0; i < filteredKeys.Count; i++)
            {
                string key = filteredKeys[i];
                string preview = englishCache.TryGetValue(key, out var v) ? v : "";

                Rect row = GUILayoutUtility.GetRect(0, rowHeight, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(row, (i % 2 == 0) ? bg1 : bg2);

                if (i == selectedKeyIndex)
                    EditorGUI.DrawRect(row, sel);

                Rect keyRect = new Rect(row.x + 10, row.y + 5, row.width * 0.4f, 20);
                Rect prevRect = new Rect(row.x + row.width * 0.42f, row.y + 5, row.width * 0.55f, 20);

                GUIStyle kStyle = (i == selectedKeyIndex)
                    ? new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } }
                    : new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };

                if (GUI.Button(keyRect, ShortKey(key), kStyle))
                    SelectIndex(i);

                // Copy button
                Rect copyRect = new Rect(row.x + row.width * 0.4f + 2, row.y + 10, 20, 15);
                if (GUI.Button(copyRect, new GUIContent("📋", "Copy key to clipboard"), EditorStyles.miniButton))
                {
                    GUIUtility.systemCopyBuffer = key;
                }

                string pv = preview;
                if (!string.IsNullOrEmpty(pv) && pv.Length > 80) pv = pv.Substring(0, 75) + "…";
                GUI.Label(prevRect, pv, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
            
            DrawGlobalTranslationOverview();
        }

        void DrawGlobalTranslationOverview()
        {
            if (string.IsNullOrEmpty(entryKeyProp.stringValue)) return;
            if (selectedTableIndex < 0 || selectedTableIndex >= tableCollections.Count) return;

            EditorGUILayout.Space(10);
            GUILayout.Label(new GUIContent("🌍 Global Translation Overview", "See what this key looks like in every language of your project."), EditorStyles.boldLabel);
            
            var col = tableCollections[selectedTableIndex];
            var tables = col.StringTables;

            EditorGUILayout.BeginVertical("box");
            foreach (var table in tables)
            {
                var entry = table.GetEntry(entryKeyProp.stringValue);
                string val = entry != null ? entry.Value : "[MISSING]";
                Color c = entry != null ? Color.white : new Color(1f, 0.4f, 0.4f);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(table.LocaleIdentifier.Code.ToUpper(), EditorStyles.miniBoldLabel, GUILayout.Width(45));
                GUIStyle vStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = c }, wordWrap = true };
                GUILayout.Label(val, vStyle);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        void DrawBottomBar()
        {
            GUILayout.Label(new GUIContent("⚙️ Logic Configuration", "Configure how this text updates at runtime."), EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(useAsyncProp, new GUIContent("Use Async Loading", "If enabled, text will be fetched asynchronously. Recommended for heavy UI or remote tables."));
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button(new GUIContent("🎯 Apply English Preview to UI", "Instantly push the English translation to the TMP/Text component for editor visualization."), primaryButtonStyle))
                ApplyPreviewToRuntime();

            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginVertical(new GUIStyle("box"));
            GUILayout.Label(new GUIContent("🧩 Dynamic Format Arguments", "Variables to inject into the string (e.g. {0}, {1})."), EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(formatArgsProp, true);
            EditorGUILayout.EndVertical();
        }

        // ------------------------
        // LOGIC
        // ------------------------

        void RefreshTables()
        {
            tableCollections.Clear();
            tableNames.Clear();
            englishCache.Clear();
            allEntryKeys.Clear();
            filteredKeys.Clear();
            selectedKeyIndex = -1;

            var cols = LocalizationEditorSettings.GetStringTableCollections();
            if (cols != null)
            {
                foreach (var c in cols)
                {
                    if (c != null)
                    {
                        tableCollections.Add(c);
                        tableNames.Add(c.TableCollectionName);
                    }
                }
            }

            selectedTableIndex = tableNames.IndexOf(tableProp.stringValue);
            if (selectedTableIndex < 0 && tableNames.Count > 0)
                selectedTableIndex = 0;

            RefreshKeys();
        }

        StringTable englishTable;

        void RefreshKeys()
        {
            allEntryKeys.Clear();
            englishCache.Clear();
            selectedKeyIndex = -1;
            englishTable = null;

            if (selectedTableIndex < 0 || selectedTableIndex >= tableCollections.Count)
                return;

            var col = tableCollections[selectedTableIndex];
            if (col?.SharedData?.Entries == null)
                return;

            foreach (var e in col.SharedData.Entries)
                allEntryKeys.Add(e.Key);

            var guids = AssetDatabase.FindAssets("t:StringTable");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var tbl = AssetDatabase.LoadAssetAtPath<StringTable>(path);
                if (tbl == null) continue;

                if (tbl.TableCollectionName == col.TableCollectionName &&
                    (tbl.LocaleIdentifier.Code == "en" || tbl.LocaleIdentifier.Code == "en-US"))
                {
                    englishTable = tbl;
                    break;
                }
            }

            if (englishTable != null)
            {
                foreach (var k in allEntryKeys)
                {
                    var entry = englishTable.GetEntry(k);
                    englishCache[k] = entry != null ? entry.Value : "";
                }
            }

            ApplyFilter();
        }

        void ApplyFilter()
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                filteredKeys = new List<string>(allEntryKeys);
            }
            else
            {
                string s = searchTerm.ToLowerInvariant();
                filteredKeys = allEntryKeys
                    .Where(k =>
                    {
                        string en = englishCache.TryGetValue(k, out var v) ? v : "";
                        return k.ToLowerInvariant().Contains(s) || en.ToLowerInvariant().Contains(s);
                    })
                    .ToList();
            }

            selectedKeyIndex = filteredKeys.IndexOf(entryKeyProp.stringValue);
        }

        void SelectIndex(int i)
        {
            selectedKeyIndex = i;
            entryKeyProp.stringValue = filteredKeys[i];

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            listScroll.y = Mathf.Max(0, i * rowHeight - rowHeight * 2);
            Repaint();
        }

        void ApplyPreviewToRuntime()
        {
            var comp = (LocalizedText)target;
            string key = entryKeyProp.stringValue;

            string pv = englishCache.TryGetValue(key, out var v) ? v : key;

            var tmp = comp.GetComponent<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.text = pv;
                tmp.ForceMeshUpdate();
                EditorUtility.SetDirty(tmp);
                return;
            }

            var u = comp.GetComponent<UnityEngine.UI.Text>();
            if (u != null)
            {
                u.text = pv;
                EditorUtility.SetDirty(u);
            }
        }

        void TryOpenLocalizationWindow()
        {
            string[] candidates = new[]
            {
                "Window/Localization",
                "Window/Asset Management/Localization Tables",
                "Window/Asset Management/Localization",
                "Window/Asset Management/Localization Tables..."
            };

            foreach (var menu in candidates)
            {
                try
                {
                    if (EditorApplication.ExecuteMenuItem(menu))
                        return;
                }
                catch { }
            }

            EditorUtility.DisplayDialog(
                "Localization window not found",
                "Could not find a Localization window menu item. Open it manually via:\n\n" +
                "Window → Asset Management → Localization Tables\n\n" +
                "Or ensure the Localization package is installed.",
                "OK");
        }

        string ShortKey(string k)
        {
            if (string.IsNullOrEmpty(k)) return "";
            if (k.Length <= 40) return k;
            return k.Substring(0, 30) + "…" + k.Substring(k.Length - 6);
        }
    }
}
#endif
