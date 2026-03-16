using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.Linq;

namespace MultilingoSetup
{
    /// <summary>
    /// Smooth Setup Wizard for MultiLingo that ensures all UPM dependencies are installed.
    /// </summary>
    [InitializeOnLoad]
    public class MultilingoDependencyInstaller : EditorWindow
    {
        private static ListRequest _listRequest;
        private static AddRequest _addRequest;
        private static bool _isChecking = false;
        private static List<PackageInfo> _missingPackages = new List<PackageInfo>();
        private static bool _installationInProgress = false;
        private static string _statusMessage = "Checking dependencies...";
        private static int _currentPackageIndex = -1;

        private struct PackageInfo
        {
            public string Name;
            public string DisplayName;
            public string Version;

            public PackageInfo(string name, string displayName, string version = null)
            {
                Name = name;
                DisplayName = displayName;
                Version = version;
            }
        }

        // --- MAIN DEPENDENCIES DEFINITION ---
        // Add any new required packages to this list.
        private static readonly PackageInfo[] RequiredPackages = new PackageInfo[]
        {
            new PackageInfo("com.unity.localization", "Unity Localization", "1.4.5"),
            new PackageInfo("com.unity.textmeshpro", "TextMeshPro"),
            new PackageInfo("com.unity.addressables", "Addressables")
        };

        static MultilingoDependencyInstaller()
        {
            // Delay call to ensure Unity is ready and hasn't started compilation immediately
            EditorApplication.delayCall += CheckDependencies;
        }

        private static bool _manualCheck = false;

        [MenuItem("Tools/Multilingo/Check Dependencies", priority = 100)]
        public static void CheckDependencies() => CheckDependencies(false);

        public static void CheckDependencies(bool manual)
        {
            if (_isChecking || _installationInProgress) return;

            _manualCheck = manual;
            _missingPackages.Clear();
            _isChecking = true;
            _statusMessage = "Checking dependencies...";
            
            _listRequest = Client.List(true);
            EditorApplication.update += CheckListProgress;
        }

        private static void CheckListProgress()
        {
            if (_listRequest == null)
            {
                EditorApplication.update -= CheckListProgress;
                return;
            }

            if (_listRequest.IsCompleted)
            {
                EditorApplication.update -= CheckListProgress;
                _isChecking = false;

                if (_listRequest.Status == StatusCode.Success)
                {
                    var installed = _listRequest.Result;
                    foreach (var req in RequiredPackages)
                    {
                        if (!installed.Any(p => p.name == req.Name))
                        {
                            _missingPackages.Add(req);
                        }
                    }

                    if (_missingPackages.Count > 0)
                    {
                        ShowWindow();
                    }
                    else if (_manualCheck)
                    {
                        EditorUtility.DisplayDialog("MultiLingo", "All core dependencies (Unity Localization, TMP, Addressables) are already installed and up to date!", "Great!");
                    }
                }
                else
                {
                    if (_manualCheck)
                        EditorUtility.DisplayDialog("MultiLingo", "Could not check dependencies because Package Manager is currently busy. Please try again in a moment.", "OK");
                    
                    Debug.LogFormat("<color=#8866ff><b>MultiLingo:</b></color> Dependency check skipped because Package Manager is busy.");
                }
            }
        }

        public static void ShowWindow()
        {
            // Center the window on the screen
            var window = GetWindow<MultilingoDependencyInstaller>(true, "MultiLingo Setup Wizard", true);
            window.minSize = new Vector2(500, 380);
            window.maxSize = new Vector2(500, 380);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            DrawBackground();

            GUILayout.Space(25);
            DrawHeader();
            GUILayout.Space(25);

            if (_installationInProgress)
            {
                DrawInstallingUI();
            }
            else
            {
                DrawMissingPackagesUI();
            }

            GUILayout.FlexibleSpace();
            DrawFooter();
        }

        private void DrawBackground()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.12f, 0.12f, 0.15f));
            
            // Accent top border
            var topRect = new Rect(0, 0, position.width, 4);
            EditorGUI.DrawRect(topRect, new Color(0.5f, 0.4f, 1f));
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.7f, 1f) }
            };
            GUILayout.Label("MultiLingo", headerStyle);

            GUIStyle subStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUILayout.Label("Welcome! Let's get your environment ready.", subStyle);
        }

        private void DrawMissingPackagesUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(30);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(10);
            
            GUIStyle listHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            GUILayout.Label("Required Core Dependencies:", listHeaderStyle);
            GUILayout.Space(10);

            foreach (var pkg in _missingPackages)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);
                
                GUIStyle entryStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 13,
                    normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
                };
                
                GUILayout.Label($"√ ", new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.4f, 0.8f, 0.4f) } }, GUILayout.Width(20));
                GUILayout.Label($"{pkg.DisplayName}", entryStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{pkg.Name}", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } });
                GUILayout.Space(10);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
            GUILayout.Space(30);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(30);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 50,
                fixedWidth = 280,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            
            // Premium background
            btnStyle.normal.background = MakeTex(2, 2, new Color(0.35f, 0.3f, 0.75f));
            btnStyle.hover.background = MakeTex(2, 2, new Color(0.45f, 0.4f, 0.85f));
            btnStyle.active.background = MakeTex(2, 2, new Color(0.25f, 0.2f, 0.65f));

            if (GUILayout.Button("Install All Dependencies", btnStyle))
            {
                StartInstallation();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInstallingUI()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            
            GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.4f, 0.9f, 1f) }
            };
            GUILayout.Label(_statusMessage, statusStyle);
            
            GUILayout.Space(15);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect rect = GUILayoutUtility.GetRect(350, 24);
            float progress = (_currentPackageIndex + 1) / (float)_missingPackages.Count;
            EditorGUI.ProgressBar(rect, progress, $"Installing {_currentPackageIndex + 1} of {_missingPackages.Count}");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.4f, 0.4f, 0.4f) }
            };
            GUILayout.Label("Note: This will restart compilation after installation.", footerStyle);
            GUILayout.Space(15);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        private void StartInstallation()
        {
            _installationInProgress = true;
            _currentPackageIndex = 0;
            InstallNextPackage();
        }

        private static void InstallNextPackage()
        {
            if (_currentPackageIndex < _missingPackages.Count)
            {
                var pkg = _missingPackages[_currentPackageIndex];
                _statusMessage = $"Preparing to install {pkg.DisplayName}...";
                
                string target = string.IsNullOrEmpty(pkg.Version) ? pkg.Name : $"{pkg.Name}@{pkg.Version}";
                _addRequest = Client.Add(target);
                EditorApplication.update += CheckAddProgress;
            }
            else
            {
                FinishInstallation();
            }
        }

        private static void CheckAddProgress()
        {
            if (_addRequest.IsCompleted)
            {
                EditorApplication.update -= CheckAddProgress;

                if (_addRequest.Status == StatusCode.Success)
                {
                    _currentPackageIndex++;
                    InstallNextPackage();
                }
                else
                {
                    _statusMessage = $"Error installing {_missingPackages[_currentPackageIndex].DisplayName}";
                    Debug.LogError($"MultiLingo: Failed to install package {_missingPackages[_currentPackageIndex].Name}: {_addRequest.Error.message}");
                    _installationInProgress = false; // Stop on error
                }
            }
        }

        private static void FinishInstallation()
        {
            _statusMessage = "All dependencies installed successfully!";
            Debug.Log("<color=#8866ff><b>MultiLingo:</b></color> All dependencies installed successfully.");
            
            EditorApplication.delayCall += () =>
            {
                if (HasOpenInstances<MultilingoDependencyInstaller>())
                {
                    GetWindow<MultilingoDependencyInstaller>().Close();
                }
                AssetDatabase.Refresh();
            };
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
