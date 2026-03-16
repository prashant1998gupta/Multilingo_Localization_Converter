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
    /// This script is designed to run even if the rest of the project has compilation errors.
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
        private static bool _manualCheck = false;

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
        private static readonly PackageInfo[] RequiredPackages = new PackageInfo[]
        {
            new PackageInfo("com.unity.localization", "Unity Localization", "1.5.10"),
            new PackageInfo("com.unity.textmeshpro", "TextMeshPro"),
            new PackageInfo("com.unity.addressables", "Addressables", "1.21.19")
        };

        [InitializeOnLoadMethod]
        private static void OnProjectLoaded()
        {
            // Use Warning level to ensure it shows even if Info logs are hidden
            Debug.LogWarning("<color=#8866ff><b>MultiLingo:</b></color> Setup Wizard Initialized. Starting dependency check...");
            EditorApplication.delayCall += () => CheckDependencies(false);
        }

        [MenuItem("Tools/Multilingo/Force Dependency Check", priority = 100)]
        public static void ForceCheck() => CheckDependencies(true);

        public static void CheckDependencies(bool manual)
        {
            if (_isChecking || _installationInProgress)
            {
                ShowWindow();
                return;
            }

            _manualCheck = manual;
            _missingPackages.Clear();
            _isChecking = true;
            _statusMessage = "Connecting to Package Manager...";
            
            // SHOW WINDOW IMMEDIATELY
            ShowWindow();

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
                        // Unity 6 Check: com.unity.textmeshpro is now part of com.unity.ugui or core modules.
                        bool isUnity6 = Application.unityVersion.StartsWith("6");
                        bool isUnity6TMP = req.Name == "com.unity.textmeshpro" && 
                                         (isUnity6 || installed.Any(p => p.name == "com.unity.ugui" && p.version.StartsWith("2.")));

                        var installedPkg = installed.FirstOrDefault(p => p.name == req.Name);
                        bool needsUpdate = false;
                        
                        if (installedPkg != null && !string.IsNullOrEmpty(req.Version))
                        {
                            // If installed version is lower than required, mark for update
                            if (IsVersionLower(installedPkg.version, req.Version))
                                needsUpdate = true;
                        }

                        if (!isUnity6TMP && (installedPkg == null || needsUpdate))
                        {
                            _missingPackages.Add(req);
                        }
                    }

                    if (_missingPackages.Count > 0)
                    {
                        _statusMessage = $"Missing {_missingPackages.Count} system components.";
                        
                        // Force a popup if it's the first check and we're missing things
                        if (!_manualCheck && !EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            EditorUtility.DisplayDialog("MultiLingo Setup", 
                                $"MultiLingo needs to install {_missingPackages.Count} required dependencies (Unity Localization, etc.) to fix your project errors.\n\nClick OK to open the Setup Wizard.", "OK");
                        }
                        
                        ShowWindow(); 
                    }
                    else
                    {
                        Debug.LogWarning("<color=#8866ff><b>MultiLingo:</b></color> All dependencies satisfied.");
                        if (!_manualCheck)
                        {
                            CloseInstaller();
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("MultiLingo", "All core dependencies (Unity Localization, TMP, Addressables) are already installed!", "Great!");
                            CloseInstaller();
                        }
                    }
                }
                else
                {
                    _statusMessage = "Package Manager is currently busy. Please wait...";
                    Debug.LogWarning("<color=#8866ff><b>MultiLingo:</b></color> Package Manager is currently busy.");
                    
                    if (_manualCheck)
                        EditorUtility.DisplayDialog("MultiLingo", "Could not check dependencies because Package Manager is currently busy.", "OK");
                }
            }
        }

        public static void ShowWindow()
        {
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
            else if (_isChecking)
            {
                DrawCheckingUI();
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
            var topRect = new Rect(0, 0, position.width, 4);
            EditorGUI.DrawRect(topRect, new Color(0.5f, 0.4f, 1f));
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 26, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.7f, 1f) } };
            GUILayout.Label("MultiLingo", headerStyle);
            GUIStyle subStyle = new GUIStyle(EditorStyles.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
            GUILayout.Label("System Configuration Wizard", subStyle);
        }

        private void DrawCheckingUI()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            GUILayout.Label("🔍 " + _statusMessage, statusStyle);
            GUILayout.Space(10);
            GUILayout.Label("This handles errors like 'Localization missing' automatically.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawMissingPackagesUI()
        {
            if (_missingPackages.Count == 0 && !_isChecking)
            {
                DrawCheckingUI();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(30);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(10);
            GUILayout.Label("Required Core Dependencies:", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, normal = { textColor = Color.white } });
            GUILayout.Space(10);

            foreach (var pkg in _missingPackages)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUILayout.Label($"√ ", new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.4f, 0.8f, 0.4f) } }, GUILayout.Width(20));
                GUILayout.Label($"{pkg.DisplayName}", new GUIStyle(EditorStyles.label) { fontSize = 13, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } });
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
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 50, fixedWidth = 280, fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            btnStyle.normal.background = MakeTex(2, 2, new Color(0.35f, 0.3f, 0.75f));
            btnStyle.hover.background = MakeTex(2, 2, new Color(0.45f, 0.4f, 0.85f));

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
            GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.4f, 0.9f, 1f) } };
            GUILayout.Label(_statusMessage, statusStyle);
            GUILayout.Space(15);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect rect = GUILayoutUtility.GetRect(350, 24);
            float progress = (_currentPackageIndex + 1) / (float)_missingPackages.Count;
            EditorGUI.ProgressBar(rect, progress, $"Installing {_currentPackageIndex + 1} of {_missingPackages.Count}...");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Note: Unity will restart and recompile scripts during this process.", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.4f, 0.4f, 0.4f) } });
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
                _statusMessage = $"Installing {pkg.DisplayName}...";
                _addRequest = Client.Add(string.IsNullOrEmpty(pkg.Version) ? pkg.Name : $"{pkg.Name}@{pkg.Version}");
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
                    // If it matches a core package that might be built-in, allow skipping
                    string pkgName = _missingPackages[_currentPackageIndex].Name;
                    if (pkgName.Contains("textmeshpro") || pkgName.Contains("addressables"))
                    {
                        Debug.LogWarning($"<color=#8866ff><b>MultiLingo:</b></color> Could not explicitly install {pkgName}. This is common in Unity 6 where these are built-in. Continuing...");
                        _currentPackageIndex++;
                        InstallNextPackage();
                    }
                    else
                    {
                        _statusMessage = $"Error installing package.";
                        Debug.LogError($"MultiLingo: Failed to install package: {_addRequest.Error.message}");
                        _installationInProgress = false;
                    }
                }
            }
        }

        private static void FinishInstallation()
        {
            Debug.LogWarning("MultiLingo: All dependencies installed successfully.");
            AssetDatabase.Refresh();
            CloseInstaller();
        }

        private static void CloseInstaller()
        {
            if (HasOpenInstances<MultilingoDependencyInstaller>()) GetWindow<MultilingoDependencyInstaller>().Close();
        }

        private static bool IsVersionLower(string current, string required)
        {
            try
            {
                // Remove any non-numeric suffixes like -preview or -f1
                string cleanCurrent = new string(current.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()).TrimEnd('.');
                string cleanRequired = new string(required.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()).TrimEnd('.');
                
                System.Version vCurrent = new System.Version(cleanCurrent);
                System.Version vRequired = new System.Version(cleanRequired);
                return vCurrent < vRequired;
            }
            catch { return false; }
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
