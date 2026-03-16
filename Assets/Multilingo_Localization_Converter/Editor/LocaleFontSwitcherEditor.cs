using UnityEngine;
using UnityEditor;
using UnityEngine.Localization.Settings;
using System.Linq;

namespace Multilingo.Localization.Editor
{
    [CustomEditor(typeof(LocaleFontSwitcher))]
    public class LocaleFontSwitcherEditor : UnityEditor.Editor
    {
        private SerializedProperty prewarmCharsProp;
        private SerializedProperty localeFontsProp;

        private void OnEnable()
        {
            prewarmCharsProp = serializedObject.FindProperty("prewarmChars");
            localeFontsProp = serializedObject.FindProperty("localeFonts");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Premium Header
            EditorGUILayout.BeginVertical(new GUIStyle("window") { padding = new RectOffset(10, 10, 10, 10) });
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 0.5f, 1f) } };
            GUILayout.Label("✨ Dynamic Font Switcher", headerStyle);
            GUILayout.Label("Automatically swaps TextMeshPro fonts at runtime based on the active language to prevent missing characters.", new GUIStyle(EditorStyles.wordWrappedMiniLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Prewarm Section
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(new GUIContent("Glyph Prewarming", "Characters to load immediately when the font switches to prevent stuttering."), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(prewarmCharsProp, new GUIContent("Base Characters"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Fetch Locales
            var locales = LocalizationSettings.AvailableLocales?.Locales;
            bool hasLocales = locales != null && locales.Count > 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Font Definitions Mapped To Locales", EditorStyles.boldLabel);
            if (hasLocales)
            {
                if (GUILayout.Button(new GUIContent("Auto-Populate Project Locales", "Fills the list below with every language currently supported in your Unity Localization Settings."), GUILayout.Width(200)))
                {
                    AutoPopulateLocales(locales);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (!hasLocales)
            {
                EditorGUILayout.HelpBox("Could not detect any active Locales in Unity Localization Settings. Please set up your Project Locales first.", MessageType.Warning);
            }

            // Draw the array with a nice UI
            EditorGUI.BeginChangeCheck();
            
            for (int i = 0; i < localeFontsProp.arraySize; i++)
            {
                var entryProp = localeFontsProp.GetArrayElementAtIndex(i);
                var codeProp = entryProp.FindPropertyRelative("localeCode");
                var fontsProp = entryProp.FindPropertyRelative("fonts");
                
                EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox));
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"🌍 {codeProp.stringValue.ToUpper()}", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } }, GUILayout.Width(60));
                
                EditorGUILayout.BeginVertical();
                // Code field
                codeProp.stringValue = EditorGUILayout.TextField(new GUIContent("Locale Code", "The exact language code (e.g. 'en', 'ja', 'fr-FR')."), codeProp.stringValue);
                
                // Fonts
                EditorGUILayout.PropertyField(fontsProp, new GUIContent("Compatible Font Assets", "Add all fonts used by your game here. The script will automatically select the best match (Regular, Bold, etc.) based on the Original English Font names."), true);
                
                EditorGUILayout.EndVertical();
                
                // Remove Button
                if (GUILayout.Button(new GUIContent("X", "Remove Language Mapping"), GUILayout.Width(25), GUILayout.Height(25)))
                {
                    localeFontsProp.DeleteArrayElementAtIndex(i);
                    i--; // Adjust index since we removed an element
                }
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.Space(2);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            // Add Manual Button
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+ Add Custom Override", "Manually add an override for a specific locale code."), GUILayout.Width(200), GUILayout.Height(30)))
            {
                localeFontsProp.arraySize++;
                var newEntry = localeFontsProp.GetArrayElementAtIndex(localeFontsProp.arraySize - 1);
                newEntry.FindPropertyRelative("localeCode").stringValue = "xx";
                newEntry.FindPropertyRelative("fonts").arraySize = 0;
                serializedObject.ApplyModifiedProperties();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void AutoPopulateLocales(System.Collections.Generic.List<UnityEngine.Localization.Locale> locales)
        {
            serializedObject.Update();
            
            // Rebuild the array while keeping assigned fonts if the locale code matches
            var existingEntries = new System.Collections.Generic.Dictionary<string, (string name, Object font)[]>();
            
            for (int i = 0; i < localeFontsProp.arraySize; i++)
            {
                var entry = localeFontsProp.GetArrayElementAtIndex(i);
                string existingCode = entry.FindPropertyRelative("localeCode").stringValue;
                if (!string.IsNullOrEmpty(existingCode) && !existingEntries.ContainsKey(existingCode))
                {
                    var existingFonts = entry.FindPropertyRelative("fonts");
                    var fontsCache = new (string, Object)[existingFonts.arraySize];
                    for (int j = 0; j < existingFonts.arraySize; j++)
                    {
                        var elementProp = existingFonts.GetArrayElementAtIndex(j);
                        fontsCache[j] = (elementProp.FindPropertyRelative("name").stringValue, elementProp.FindPropertyRelative("font").objectReferenceValue);
                    }
                    existingEntries.Add(existingCode, fontsCache);
                }
            }

            localeFontsProp.arraySize = locales.Count;

            for (int i = 0; i < locales.Count; i++)
            {
                var code = locales[i].Identifier.Code;
                var entry = localeFontsProp.GetArrayElementAtIndex(i);
                
                entry.FindPropertyRelative("localeCode").stringValue = code;
                var fontsProp = entry.FindPropertyRelative("fonts");

                if (existingEntries.TryGetValue(code, out var savedFonts))
                {
                    fontsProp.arraySize = savedFonts.Length;
                    for (int j = 0; j < savedFonts.Length; j++)
                    {
                        var elementProp = fontsProp.GetArrayElementAtIndex(j);
                        elementProp.FindPropertyRelative("name").stringValue = savedFonts[j].name;
                        elementProp.FindPropertyRelative("font").objectReferenceValue = savedFonts[j].font;
                    }
                }
                else
                {
                    fontsProp.arraySize = 0;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
