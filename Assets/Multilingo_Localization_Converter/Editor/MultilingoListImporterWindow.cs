using UnityEngine;
using UnityEditor;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Multilingo.Localization.Editor
{
    public class MultilingoListImporterWindow : EditorWindow
    {
        private StringTableCollection targetCollection;
        private string nameList = "";
        private bool stripNumbers = true;
        private bool populateEnglish = true;
        private bool clearTableFirst = false;
        private Vector2 scrollPos;

        [MenuItem("Tools/Multilingo/Raw List Importer 🚀")]
        public static void ShowWindow()
        {
            var window = GetWindow<MultilingoListImporterWindow>("List Importer");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.18f, 0.18f, 0.2f));

            GUILayout.Space(10);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            headerStyle.normal.textColor = new Color(0.55f, 0.45f, 1f);
            GUILayout.Label("Raw List Importer", headerStyle);
            GUILayout.Label("Paste a list of names to instantly add them to a Localization Table.", new GUIStyle(EditorStyles.centeredGreyMiniLabel));
            
            GUILayout.Space(15);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("1. Select String Table Collection", EditorStyles.boldLabel);
            targetCollection = EditorGUILayout.ObjectField(targetCollection, typeof(StringTableCollection), false) as StringTableCollection;
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("2. Options", EditorStyles.boldLabel);
            stripNumbers = EditorGUILayout.ToggleLeft(" Strip leading numbers (e.g. '1. Bathroom' -> 'Bathroom')", stripNumbers);
            populateEnglish = EditorGUILayout.ToggleLeft(" Auto-populate 'English' column with the name", populateEnglish);
            GUI.color = new Color(1f, 0.7f, 0.7f);
            clearTableFirst = EditorGUILayout.ToggleLeft(" Clear Target Table before importing (WARNING: Deletes all current keys)", clearTableFirst);
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("3. Paste Names (One per line)", EditorStyles.boldLabel);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(250));
            GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            nameList = EditorGUILayout.TextArea(nameList, textAreaStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 40 };
            btnStyle.normal.textColor = Color.white;
            
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUILayout.Button("Import to Selected Table", btnStyle))
            {
                ImportData();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);
        }

        private void ImportData()
        {
            if (targetCollection == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a String Table Collection first.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(nameList))
            {
                EditorUtility.DisplayDialog("Error", "List is empty. Please paste some names.", "OK");
                return;
            }

            var lines = nameList.Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToArray();
            int addedCount = 0;
            int skippedCount = 0;

            SharedTableData sharedData = targetCollection.SharedData;

            StringTable englishTable = null;
            if (populateEnglish)
            {
                englishTable = targetCollection.StringTables.FirstOrDefault(t => 
                    t.LocaleIdentifier.Code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase) || 
                    t.LocaleIdentifier.Code.Contains("English"));
            }

            if (clearTableFirst)
            {
                bool confirm = EditorUtility.DisplayDialog("Clear Table", 
                    $"Are you sure you want to permanently clear ALL entries from '{targetCollection.name}' before importing?", 
                    "Yes, Clear It", "Cancel");
                if (!confirm) return;

                // Clear all keys from SharedData
                sharedData.Entries.Clear();
                
                // Clear all translated strings
                foreach (var table in targetCollection.StringTables)
                {
                    table.Clear();
                }
            }

            foreach (var line in lines)
            {
                string keyName = line;

                if (stripNumbers)
                {
                    // Regex to remove leading numbers like "1. ", "2)", "3-", etc.
                    keyName = Regex.Replace(keyName, @"^\d+[\.\)\-]?\s*", "").Trim();
                }

                // Make sure it generates a valid string
                if (string.IsNullOrEmpty(keyName)) continue;

                // Create the Key in the Shared Data
                if (sharedData.GetEntry(keyName) == null)
                {
                    var newKey = sharedData.AddKey(keyName);
                    addedCount++;

                    // Populate English table
                    if (englishTable != null)
                    {
                        englishTable.AddEntry(keyName, keyName);
                    }
                }
                else
                {
                    skippedCount++;
                }
            }

            // Save changes
            EditorUtility.SetDirty(sharedData);
            if (englishTable != null) EditorUtility.SetDirty(englishTable);
            foreach (var table in targetCollection.StringTables) EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Import Complete", 
                $"Added {addedCount} new entries.\nSkipped {skippedCount} existing entries.\n\nTarget: {targetCollection.name}", "OK");
        }
    }
}
