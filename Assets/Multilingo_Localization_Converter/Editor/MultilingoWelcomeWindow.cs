using UnityEngine;
using UnityEditor;

namespace Multilingo.Localization.Editor
{
    [InitializeOnLoad]
    public class MultilingoWelcomeWindow : EditorWindow
    {
        private const string PrefKey = "Multilingo_ShowWelcomeOnStart";
        
        static MultilingoWelcomeWindow()
        {
            EditorApplication.delayCall += ShowWindowOnStart;
        }

        private static void ShowWindowOnStart()
        {
            if (EditorPrefs.GetBool(PrefKey, true))
            {
                ShowWindow();
            }
        }

        [MenuItem("Tools/Multilingo/Welcome Window", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<MultilingoWelcomeWindow>(true, "Welcome to MultiLingo", true);
            window.minSize = new Vector2(450, 450);
            window.maxSize = new Vector2(450, 450);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.15f, 0.15f, 0.18f));

            GUILayout.Space(20);

            // Header
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.5f, 1f) }
            };
            GUILayout.Label("MultiLingo", headerStyle);

            GUIStyle subHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUILayout.Label("Enterprise Localization & Translation Engine", subHeaderStyle);

            GUILayout.Space(20);

            // Welcome Text
            GUIStyle textStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUILayout.Label("Thank you for purchasing MultiLingo!\n\nThis package provides everything you need to manage, translate, and integrate localization data into your Unity project seamlessly.", textStyle);

            GUILayout.Space(30);

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.Width(350));

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40,
                normal = { textColor = Color.white }
            };

            // Needs custom background to look premium
            btnStyle.normal.background = MakeTex(2, 2, new Color(0.3f, 0.4f, 0.7f));
            btnStyle.hover.background = MakeTex(2, 2, new Color(0.4f, 0.5f, 0.8f));

            if (GUILayout.Button(new GUIContent("🌐 Open Translator & Converter", "Translate files into 100+ languages or convert formats."), btnStyle))
            {
                MultilingoConverterWindow.ShowWindow();
                Close();
            }

            GUILayout.Space(10);

            btnStyle.normal.background = MakeTex(2, 2, new Color(0.4f, 0.3f, 0.6f));
            btnStyle.hover.background = MakeTex(2, 2, new Color(0.5f, 0.4f, 0.7f));

            if (GUILayout.Button(new GUIContent("🛠️ Open Unity Localization Tools", "Access advanced utilities like Auto TTS, Font Optimizer, and Sync."), btnStyle))
            {
                MultilingoLocalizationTools.ShowWindow();
                Close();
            }

            GUILayout.Space(10);

            btnStyle.normal.background = MakeTex(2, 2, new Color(0.3f, 0.3f, 0.3f));
            btnStyle.hover.background = MakeTex(2, 2, new Color(0.4f, 0.4f, 0.4f));

            if (GUILayout.Button(new GUIContent("📖 Read Documentation", "Open the comprehensive documentation file."), btnStyle))
            {
                string path = "Assets/Multilingo_Localization_Converter/Documentation.md";
                var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (obj != null)
                    AssetDatabase.OpenAsset(obj);
                else
                    Debug.LogWarning("MultiLingo: Could not find Documentation.md. Ensure it is located in Assets/Multilingo_Localization_Converter/");
            }

            GUILayout.Space(10);

            // Dependency Management Button
            btnStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.45f, 0.4f));
            btnStyle.hover.background = MakeTex(2, 2, new Color(0.3f, 0.55f, 0.5f));

            if (GUILayout.Button(new GUIContent("🔍 Manage Dependencies", "Ensure Unity Localization and other packages are correctly installed."), btnStyle))
            {
                // Use Reflection to avoid a hard assembly reference. 
                // This allows the editor to compile even if dependencies are missing.
                var type = System.Type.GetType("MultilingoSetup.MultilingoDependencyInstaller, Multilingo.Installer");
                if (type != null)
                {
                    var method = type.GetMethod("CheckDependencies", new System.Type[] { typeof(bool) });
                    if (method != null) method.Invoke(null, new object[] { true });
                }
                else
                {
                    Debug.LogWarning("MultiLingo: Installer assembly not found. Please wait for Unity to recompile.");
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // Toggle "Show on start"
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool showOnStart = EditorPrefs.GetBool(PrefKey, true);
            EditorGUI.BeginChangeCheck();
            GUI.contentColor = Color.white;
            showOnStart = GUILayout.Toggle(showOnStart, " Show this window on startup");
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PrefKey, showOnStart);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(15);
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
