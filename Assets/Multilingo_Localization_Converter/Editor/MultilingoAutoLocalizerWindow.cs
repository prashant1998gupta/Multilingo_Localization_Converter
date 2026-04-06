using UnityEngine;
using UnityEditor;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;
using UnityEngine.Localization.Components;
using UnityEditor.Events;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Multilingo.Localization.Editor
{
    public class MultilingoAutoLocalizerWindow : EditorWindow
    {
        private StringTableCollection targetCollection;
        private GameObject targetRoot;
        private string keyPrefix = "UI_";
        private bool includeInactive = true;
        private bool autoCleanKeys = true;
        
        private Vector2 scrollPos;
        private int localizedCount = 0;

        [MenuItem("Tools/Multilingo/Auto-Localize UI 🪄")]
        public static void ShowWindow()
        {
            var window = GetWindow<MultilingoAutoLocalizerWindow>("Auto Localize");
            window.minSize = new Vector2(450, 550);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.18f, 0.18f, 0.2f));

            GUILayout.Space(10);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            headerStyle.normal.textColor = new Color(1f, 0.6f, 0.4f);
            GUILayout.Label("Auto-Localize UI Components", headerStyle);
            GUILayout.Label("Instantly convert all hardcoded texts in a Canvas to Localized strings.", new GUIStyle(EditorStyles.centeredGreyMiniLabel));
            
            GUILayout.Space(15);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Step 1: Target Collection
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("1. Setup", EditorStyles.boldLabel);
            GUILayout.Space(5);
            targetCollection = EditorGUILayout.ObjectField("String Table", targetCollection, typeof(StringTableCollection), false) as StringTableCollection;
            targetRoot = EditorGUILayout.ObjectField("Root GameObject (Canvas/Panel)", targetRoot, typeof(GameObject), true) as GameObject;
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Step 2: Settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("2. Settings", EditorStyles.boldLabel);
            GUILayout.Space(5);
            keyPrefix = EditorGUILayout.TextField(new GUIContent("Key Prefix", "Added to the start of generated keys. e.g. UI_"), keyPrefix);
            includeInactive = EditorGUILayout.Toggle(new GUIContent("Include Inactive", "Search hidden game objects too."), includeInactive);
            autoCleanKeys = EditorGUILayout.Toggle(new GUIContent("Clean Key Names", "Removes spaces and special chars to make safe keys."), autoCleanKeys);
            EditorGUILayout.EndVertical();

            GUILayout.Space(20);

            if (targetRoot == null)
            {
                EditorGUILayout.HelpBox("Please assign a Root GameObject from your scene to scan.", MessageType.Warning);
            }
            else if (targetCollection == null)
            {
                EditorGUILayout.HelpBox("Please assign a Target String Table Collection.", MessageType.Warning);
            }
            else
            {
                GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, fixedHeight = 50 };
                btnStyle.normal.textColor = Color.white;
                GUI.backgroundColor = new Color(0.1f, 0.6f, 0.9f);
                
                if (GUILayout.Button("✨ Auto-Localize All Texts Now ✨", btnStyle))
                {
                    RunAutoLocalizer();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunAutoLocalizer()
        {
            if (targetRoot == null || targetCollection == null) return;

            localizedCount = 0;
            int skippedCount = 0;

            // 1. Find all Legacy Text elements
            var uiTexts = targetRoot.GetComponentsInChildren<UnityEngine.UI.Text>(includeInactive);
            
            // 2. Find all TextMeshPro elements using reflection to avoid hard compile errors if package is missing
            Component[] tmpTexts = new Component[0];
            var tmpType = System.Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            if (tmpType != null)
            {
                tmpTexts = targetRoot.GetComponentsInChildren(tmpType, includeInactive);
            }

            SharedTableData sharedData = targetCollection.SharedData;
            var englishTable = targetCollection.StringTables.FirstOrDefault(t => 
                t.LocaleIdentifier.Code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase) || 
                t.LocaleIdentifier.Code.Contains("English"));

            Undo.RecordObject(targetRoot, "Auto Localize UI");

            // Process UnityEngine.UI.Text
            foreach (var textComp in uiTexts)
            {
                if (ProcessTextComponent(textComp.gameObject, textComp.text, sharedData, englishTable, typeof(UnityEngine.UI.Text)))
                    localizedCount++;
                else
                    skippedCount++;
            }

            // Process TextMeshPro
            foreach (var tmpComp in tmpTexts)
            {
                var textProp = tmpComp.GetType().GetProperty("text");
                if (textProp != null)
                {
                    string textVal = textProp.GetValue(tmpComp) as string;
                    if (ProcessTextComponent(tmpComp.gameObject, textVal, sharedData, englishTable, tmpType))
                        localizedCount++;
                    else
                        skippedCount++;
                }
            }

            // Save Assets
            EditorUtility.SetDirty(sharedData);
            if (englishTable != null) EditorUtility.SetDirty(englishTable);
            foreach (var table in targetCollection.StringTables) EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Magic Complete! 🪄", 
                $"Successfully Auto-Localized {localizedCount} Text Components.\n\nSkipped: {skippedCount} (Empty or already localized).", "Awesome!");
        }

        private bool ProcessTextComponent(GameObject go, string hardcodedText, SharedTableData sharedData, StringTable englishTable, System.Type componentType)
        {
            if (string.IsNullOrWhiteSpace(hardcodedText)) return false; // Ignore empty text
            
            // Check if already localized
            var existingLoc = go.GetComponent<LocalizeStringEvent>();
            if (existingLoc != null) return false;

            // 1. Generate a Safe Key
            string generatedKey = hardcodedText;
            if (autoCleanKeys)
            {
                generatedKey = Regex.Replace(generatedKey, @"[^a-zA-Z0-9]", "_"); // Replace special chars with underscore
                generatedKey = Regex.Replace(generatedKey, @"_+", "_"); // Remove consecutive underscores
                generatedKey = generatedKey.Trim('_');
                
                // Truncate if too long (e.g., long paragraphs)
                if (generatedKey.Length > 30) generatedKey = generatedKey.Substring(0, 30);
                if (generatedKey.EndsWith("_")) generatedKey = generatedKey.TrimEnd('_');
            }

            string finalKey = $"{keyPrefix}{generatedKey}";

            // Ensure key is unique in table by appending numbers if needed
            string baseFinalKey = finalKey;
            int suffix = 1;
            while (sharedData.Contains(finalKey) && GetTableValue(englishTable, finalKey) != hardcodedText)
            {
                suffix++;
                finalKey = $"{baseFinalKey}_{suffix}";
            }

            // 2. Add to Label System
            if (!sharedData.Contains(finalKey))
            {
                sharedData.AddKey(finalKey);
                if (englishTable != null)
                {
                    englishTable.AddEntry(finalKey, hardcodedText);
                }
            }

            // 3. Attach standard LocalizeStringEvent
            var locEvent = Undo.AddComponent<LocalizeStringEvent>(go);
            var entry = sharedData.GetEntry(finalKey);
            
            locEvent.StringReference.TableReference = targetCollection.SharedData.TableCollectionName;
            locEvent.StringReference.TableEntryReference = entry.Id;

            // 4. Link UnityEvent properly so it changes the UI string
            var targetComponent = go.GetComponent(componentType);
            var methodInfo = componentType.GetProperty("text").GetSetMethod();
            var action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<string>), targetComponent, methodInfo) as UnityEngine.Events.UnityAction<string>;
            
            UnityEventTools.AddPersistentListener(locEvent.OnUpdateString, action);
            
            EditorUtility.SetDirty(go);
            return true;
        }
        
        private string GetTableValue(StringTable table, string key)
        {
            if (table == null) return null;
            var entry = table.GetEntry(key);
            return entry != null ? entry.LocalizedValue : null;
        }
    }
}
