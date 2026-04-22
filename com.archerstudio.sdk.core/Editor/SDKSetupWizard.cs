using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Consolidated SDK Wizard for configuration, symbols, package sources and validation.
    /// Menu: ArcherStudio > SDK > Setup Wizard
    /// </summary>
    public class SDKSetupWizard : EditorWindow {
        private const string Tag = "SDKSetupWizard";
        private const string ResourcesPath = "Assets/Resources";
        private const string PackagePrefix = "com.archerstudio.sdk.";

        // ─── Wizard State ───
        private Vector2 _scrollPos;
        private int _currentTab;
        private readonly string[] _tabNames = {
            "Quick Setup", "Configs", "Symbols", "Sources", "Validate"
        };

        // ─── Quick Setup toggles ───
        private bool _createConfigs = true;
        private SDKEnvironment _environment = SDKEnvironment.Development;
        private string _appId = "";
        private string _adjustToken = "";
        private string _adSdkKey = "";

        // ─── Symbols tab state ───
        private List<SymbolRow> _symbolRows;
        private string _customSymbol = "";
        private bool _showCustomSection = true;
        private SDKSymbolDetector.SymbolScope _symbolScope = SDKSymbolDetector.SymbolScope.ActiveProfile;
        private readonly List<string> _pendingCustomAdds = new List<string>();
        private readonly List<string> _pendingCustomRemoves = new List<string>();

        // ─── Sources tab state ───
        private string _gitRepoUrl;
        private string _gitRef;
        private string _localRelativePath;
        private List<PackageEntry> _packages = new List<PackageEntry>();
        private SourceMode _currentSourceMode = SourceMode.Unknown;
        private string _manifestPath;

        private enum SourceMode { Unknown, Local, Git, Mixed }
        private enum PendingAction { None, Add, Remove }

        // ─── Module toggles ───
        private bool _enableConsent = true;
        private bool _enableLogin;
        private bool _enableTracking = true;
        private bool _enableAnalytics = true;
        private bool _enableAds = true;
        private bool _enableIAP = true;
        private bool _enableRemoteConfig = true;
        private bool _enablePush;
        private bool _enableDeepLink;
        private bool _enableTestLab;
        private bool _enableCloudSave;

        [MenuItem("ArcherStudio/SDK/Setup Wizard", false, 0)]
        public static void ShowWindow() {
            var window = GetWindow<SDKSetupWizard>("SDK Setup Wizard");
            window.minSize = new Vector2(600, 650);
            window.Show();
        }

        public static void ShowTab(int tabIndex) {
            var window = GetWindow<SDKSetupWizard>("SDK Setup Wizard");
            window._currentTab = tabIndex;
            window.Show();
            window.Repaint();
        }

        private void OnEnable() {
            LoadFromExistingConfigs();
            InitializeSourceSwitcher();
        }

        private void OnFocus() {
            if (_currentTab == 2) RefreshSymbolRows();
            if (_currentTab == 3) RefreshPackageSources();
        }

        private void OnGUI() {
            DrawHeader();

            _currentTab = GUILayout.Toolbar(_currentTab, _tabNames);
            EditorGUILayout.Space(8);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_currentTab) {
                case 0: DrawQuickSetup(); break;
                case 1: DrawConfigsTab(); break;
                case 2: DrawSymbolsTab(); break;
                case 3: DrawSourcesTab(); break;
                case 4: DrawValidateTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader() {
            EditorGUILayout.Space(4);
            var style = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("ArcherStudio SDK Setup Wizard", style);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        // ═══════════════════════════════════════════════════════
        //  TAB 0: Quick Setup
        // ═══════════════════════════════════════════════════════

        private void DrawQuickSetup() {
            EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);

            bool coreExists = AssetDatabase.LoadAssetAtPath<SDKCoreConfig>($"{ResourcesPath}/SDKCoreConfig.asset") != null;
            EditorGUILayout.HelpBox(
                coreExists
                    ? "SDKCoreConfig đã tồn tại — giá trị đang được load từ disk. Thay đổi trên wizard và nhấn 'Run Setup' để cập nhật."
                    : "SDKCoreConfig chưa tồn tại — nhấn 'Run Setup' để tạo mới các config trong Assets/Resources/.",
                coreExists ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(coreExists ? "Status: existing configs loaded" : "Status: no configs detected",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reload from Existing", GUILayout.Width(160), GUILayout.Height(20))) {
                LoadFromExistingConfigs();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _environment = (SDKEnvironment)EditorGUILayout.EnumPopup("Target Environment", _environment);
            if (EditorGUI.EndChangeCheck()) {
                SDKFirebaseSwitcher.SwitchEnvironment(_environment);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("App Settings", EditorStyles.boldLabel);
            _appId = EditorGUILayout.TextField("App ID", _appId);
            _adjustToken = EditorGUILayout.TextField("Adjust App Token", _adjustToken);
            _adSdkKey = EditorGUILayout.TextField("Ad SDK Key (MAX/IS)", _adSdkKey);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);
            _enableConsent = EditorGUILayout.Toggle("Consent (GDPR/ATT)", _enableConsent);
            _enableLogin = EditorGUILayout.Toggle("Login (Google Play Games)", _enableLogin);
            _enableTracking = EditorGUILayout.Toggle("Tracking (Firebase + Adjust)", _enableTracking);
            _enableAnalytics = EditorGUILayout.Toggle("Analytics", _enableAnalytics);
            _enableAds = EditorGUILayout.Toggle("Ads (Mediation)", _enableAds);
            _enableIAP = EditorGUILayout.Toggle("In-App Purchase", _enableIAP);
            _enableRemoteConfig = EditorGUILayout.Toggle("Remote Config", _enableRemoteConfig);
            _enablePush = EditorGUILayout.Toggle("Push Notifications", _enablePush);
            _enableDeepLink = EditorGUILayout.Toggle("Deep Linking", _enableDeepLink);
            _enableTestLab = EditorGUILayout.Toggle("Firebase Test Lab", _enableTestLab);
            _enableCloudSave = EditorGUILayout.Toggle("Cloud Save (Firestore)", _enableCloudSave);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            _createConfigs = EditorGUILayout.Toggle("Create Config Assets", _createConfigs);

            EditorGUILayout.Space(16);

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Run Setup", GUILayout.Height(40))) {
                RunQuickSetup();
            }
            GUI.backgroundColor = Color.white;
        }

        // ═══════════════════════════════════════════════════════
        //  TAB 1: Configs
        // ═══════════════════════════════════════════════════════

        private void DrawConfigsTab() {
            EditorGUILayout.LabelField("Config Assets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Create individual config assets in Assets/Resources/. Existing configs will NOT be overwritten.\n" +
                "Nhấn 'Import Lib' để cài thư viện bên thứ ba (UPM hoặc mở trang tải .unitypackage).",
                MessageType.Info);

            EditorGUILayout.Space(8);
            DrawConfigButton<SDKCoreConfig>("SDKCoreConfig", "Core Config (required)");
            DrawConfigButton<SDKBootstrapConfig>("SDKBootstrapConfig", "Bootstrap Config (required)");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Module Configs", EditorStyles.boldLabel);
            DrawModuleConfigButton("ConsentConfig", "Consent Config", "com.archerstudio.sdk.consent");
            DrawModuleConfigButton("LoginConfig", "Login Config", "com.archerstudio.sdk.login");
            DrawModuleConfigButton("TrackingConfig", "Tracking Config", "com.archerstudio.sdk.tracking");
            DrawModuleConfigButton("AdConfig", "Ad Config", "com.archerstudio.sdk.ads");
            DrawModuleConfigButton("IAPConfig", "IAP Config", "com.archerstudio.sdk.iap");
            DrawModuleConfigButton("RemoteConfigConfig", "Remote Config", "com.archerstudio.sdk.remoteconfig");
            DrawModuleConfigButton("PushConfig", "Push Config", "com.archerstudio.sdk.push");
            DrawModuleConfigButton("DeepLinkConfig", "Deep Link Config", "com.archerstudio.sdk.deeplink");
            DrawModuleConfigButton("TestLabConfig", "Test Lab Config", "com.archerstudio.sdk.testlab");
            DrawModuleConfigButton("CloudSaveConfig", "Cloud Save Config", "com.archerstudio.sdk.cloudsave");
        }

        // ═══════════════════════════════════════════════════════
        //  TAB 2: Symbols
        // ═══════════════════════════════════════════════════════

        private void DrawSymbolsTab() {
            EditorGUILayout.LabelField("SDK Scripting Define Symbols", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Apply to:", GUILayout.Width(60));
            _symbolScope = (SDKSymbolDetector.SymbolScope)EditorGUILayout.EnumPopup(_symbolScope);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            var targetLabel = SDKSymbolDetector.GetScopeLabel(_symbolScope);
            EditorGUILayout.LabelField($"Target: {targetLabel}", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("Green=OK | Red=Missing | Yellow=Orphan | Gray=Not Installed", MessageType.None);
            if (GUILayout.Button("Refresh", GUILayout.Width(70), GUILayout.Height(38))) RefreshSymbolRows();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Auto-Detect All SDKs & Sync Symbols", GUILayout.Height(28))) {
                SDKSymbolDetector.RunDetection(_symbolScope);
                RefreshSymbolRows();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            DrawBulkSymbolsToolbar();
            EditorGUILayout.Space(8);
            DrawSymbolTableHeader();

            if (_symbolRows == null) RefreshSymbolRows();
            foreach (var row in _symbolRows) DrawSymbolRow(row);
        }

        // ═══════════════════════════════════════════════════════
        //  TAB 3: Sources (Package Source Switcher)
        // ═══════════════════════════════════════════════════════

        private void DrawSourcesTab() {
            EditorGUILayout.LabelField("SDK Source Switcher", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Switch all com.archerstudio.sdk.* packages between local (file:) and git sources.", MessageType.Info);

            EditorGUILayout.Space(8);
            DrawSourceStatus();
            EditorGUILayout.Space(8);
            DrawSourceSettings();
            EditorGUILayout.Space(8);
            DrawSourceBulkButtons();
            EditorGUILayout.Space(12);
            DrawPackageSourceList();
        }

        private void DrawSourceStatus() {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Mode:", EditorStyles.boldLabel, GUILayout.Width(100));
            var (label, color) = _currentSourceMode switch {
                SourceMode.Local => ("LOCAL (file:)", new Color(0.3f, 0.8f, 0.3f)),
                SourceMode.Git => ("GIT (remote)", new Color(0.4f, 0.7f, 1f)),
                SourceMode.Mixed => ("MIXED", new Color(0.9f, 0.8f, 0.2f)),
                _ => ("Unknown", Color.gray)
            };
            var statusStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color }, fontSize = 14 };
            EditorGUILayout.LabelField(label, statusStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSourceSettings() {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Git Source", EditorStyles.miniLabel);
            _gitRepoUrl = EditorGUILayout.TextField("Repository URL", _gitRepoUrl);
            _gitRef = EditorGUILayout.TextField("Branch / Tag", _gitRef);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Local Source", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            _localRelativePath = EditorGUILayout.TextField("Relative Path", _localRelativePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60))) {
                var abs = EditorUtility.OpenFolderPanel("Select SDK Root Folder", "", "");
                if (!string.IsNullOrEmpty(abs)) {
                    var packagesDir = Path.GetFullPath("Packages");
                    _localRelativePath = GetRelativePath(packagesDir, abs).Replace('\\', '/');
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // Save settings to EditorPrefs
            EditorPrefs.SetString("ArcherSDK_GitRepoUrl", _gitRepoUrl);
            EditorPrefs.SetString("ArcherSDK_GitRef", _gitRef);
            EditorPrefs.SetString("ArcherSDK_LocalPath", _localRelativePath);
        }

        private void DrawSourceBulkButtons() {
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _currentSourceMode != SourceMode.Local;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Switch All to LOCAL", GUILayout.Height(35))) {
                if (EditorUtility.DisplayDialog("Switch to Local", "Unity will reimport packages. Continue?", "Switch", "Cancel")) SwitchAllSources(toLocal: true);
            }
            GUI.enabled = _currentSourceMode != SourceMode.Git;
            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Switch All to GIT", GUILayout.Height(35))) {
                if (EditorUtility.DisplayDialog("Switch to Git", "Unity will reimport packages. Continue?", "Switch", "Cancel")) SwitchAllSources(toLocal: false);
            }
            GUI.enabled = true; GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPackageSourceList() {
            EditorGUILayout.LabelField($"Packages ({_packages.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("", GUILayout.Width(16));
            GUILayout.Label("Package", EditorStyles.miniLabel, GUILayout.Width(120));
            GUILayout.Label("Ver", EditorStyles.miniLabel, GUILayout.Width(45));
            GUILayout.Label("Git Ref", EditorStyles.miniLabel, GUILayout.Width(80));
            GUILayout.Label("Source", EditorStyles.miniLabel);
            GUILayout.Label("", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            foreach (var pkg in _packages) {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.contentColor = pkg.IsLocal ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.4f, 0.7f, 1f);
                GUILayout.Label(pkg.IsLocal ? "L" : "G", EditorStyles.boldLabel, GUILayout.Width(16));
                GUI.contentColor = Color.white;
                EditorGUILayout.LabelField(pkg.Name.Replace(PackagePrefix, ""), EditorStyles.boldLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField(pkg.InstalledVersion, EditorStyles.miniLabel, GUILayout.Width(45));
                pkg.GitRef = EditorGUILayout.TextField(pkg.GitRef, GUILayout.Width(80));
                string disp = pkg.CurrentSource; if (disp.Length > 30) disp = "..." + disp.Substring(disp.Length - 27);
                EditorGUILayout.LabelField(disp, EditorStyles.miniLabel);
                if (GUILayout.Button(pkg.IsLocal ? "-> Git" : "-> Local", GUILayout.Width(70))) SwitchSingleSource(pkg, !pkg.IsLocal);
                EditorGUILayout.EndHorizontal();
            }
        }

        // ═══════════════════════════════════════════════════════
        //  TAB 4: Validate
        // ═══════════════════════════════════════════════════════

        private void DrawValidateTab() {
            EditorGUILayout.LabelField("Validate Setup", EditorStyles.boldLabel);
            if (GUILayout.Button("Run Validation", GUILayout.Height(30))) _validationResults = ValidateSetup();
            if (_validationResults != null) {
                foreach (var issue in _validationResults) EditorGUILayout.HelpBox(issue, MessageType.Warning);
                if (_validationResults.Count == 0) EditorGUILayout.HelpBox("All checks passed!", MessageType.Info);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  LOGIC: Configs
        // ═══════════════════════════════════════════════════════

        private void LoadFromExistingConfigs() {
            var coreConfig = AssetDatabase.LoadAssetAtPath<SDKCoreConfig>($"{ResourcesPath}/SDKCoreConfig.asset");
            if (coreConfig != null) {
                _appId = coreConfig.AppId ?? ""; _environment = coreConfig.Environment;
                _enableConsent = coreConfig.EnableConsent; _enableLogin = coreConfig.EnableLogin;
                _enableTracking = coreConfig.EnableTracking; _enableAnalytics = coreConfig.EnableAnalytics;
                _enableAds = coreConfig.EnableAds; _enableIAP = coreConfig.EnableIAP;
                _enableRemoteConfig = coreConfig.EnableRemoteConfig; _enablePush = coreConfig.EnablePush;
                _enableDeepLink = coreConfig.EnableDeepLink; _enableTestLab = coreConfig.EnableTestLab;
                _enableCloudSave = coreConfig.EnableCloudSave;
            }

            var trackingAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{ResourcesPath}/TrackingConfig.asset");
            if (trackingAsset != null) {
                var field = trackingAsset.GetType().GetField("AdjustAppToken");
                if (field != null) _adjustToken = field.GetValue(trackingAsset) as string ?? "";
            }

            var adAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{ResourcesPath}/AdConfig.asset");
            if (adAsset != null) {
                var field = adAsset.GetType().GetField("SdkKey");
                if (field != null) _adSdkKey = field.GetValue(adAsset) as string ?? "";
            }
        }

        private void RunQuickSetup() {
            EnsureDirectoryExists(ResourcesPath);
            if (_createConfigs) {
                CreateConfigIfMissing<SDKCoreConfig>("SDKCoreConfig");
                CreateConfigIfMissing<SDKBootstrapConfig>("SDKBootstrapConfig");
                var toggles = GetModuleToggles();
                if (toggles.Consent) CreateModuleConfig("ConsentConfig");
                if (toggles.Login) CreateModuleConfig("LoginConfig");
                if (toggles.Tracking) CreateModuleConfig("TrackingConfig");
                if (toggles.Ads) CreateModuleConfig("AdConfig");
                if (toggles.IAP) CreateModuleConfig("IAPConfig");
                if (toggles.RemoteConfig) CreateModuleConfig("RemoteConfigConfig");
                if (toggles.Push) CreateModuleConfig("PushConfig");
                if (toggles.DeepLink) CreateModuleConfig("DeepLinkConfig");
                if (toggles.TestLab) CreateModuleConfig("TestLabConfig");
                if (toggles.CloudSave) CreateModuleConfig("CloudSaveConfig");
                ApplyConfigValues(toggles);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("SDK Setup", "Setup complete. Configs created/updated in Assets/Resources/.", "OK");
        }

        private void ApplyConfigValues(ModuleToggles toggles) {
            var coreConfig = AssetDatabase.LoadAssetAtPath<SDKCoreConfig>($"{ResourcesPath}/SDKCoreConfig.asset");
            if (coreConfig != null) {
                coreConfig.AppId = _appId; coreConfig.Environment = _environment;
                coreConfig.EnableConsent = toggles.Consent; coreConfig.EnableLogin = toggles.Login;
                coreConfig.EnableTracking = toggles.Tracking; coreConfig.EnableAnalytics = toggles.Analytics;
                coreConfig.EnableAds = toggles.Ads; coreConfig.EnableIAP = toggles.IAP;
                coreConfig.EnableRemoteConfig = toggles.RemoteConfig; coreConfig.EnablePush = toggles.Push;
                coreConfig.EnableDeepLink = toggles.DeepLink; coreConfig.EnableTestLab = toggles.TestLab;
                coreConfig.EnableCloudSave = toggles.CloudSave;
                EditorUtility.SetDirty(coreConfig);
            }
            UpdateModuleField("TrackingConfig", "AdjustAppToken", _adjustToken);
            UpdateModuleField("AdConfig", "SdkKey", _adSdkKey);
        }

        private void UpdateModuleField(string configName, string fieldName, string value) {
            if (string.IsNullOrEmpty(value)) return;
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{ResourcesPath}/{configName}.asset");
            if (asset == null) return;
            var field = asset.GetType().GetField(fieldName);
            if (field != null) { field.SetValue(asset, value); EditorUtility.SetDirty(asset); }
        }

        // ═══════════════════════════════════════════════════════
        //  LOGIC: Symbols
        // ═══════════════════════════════════════════════════════

        private void RefreshSymbolRows() {
            _symbolRows = new List<SymbolRow>();
            foreach (var entry in SDKSymbolDetector.Entries) {
                _symbolRows.Add(new SymbolRow {
                    Symbol = entry.Symbol, DisplayName = entry.DisplayName, DetectionType = entry.DetectionType,
                    IsDetected = SDKSymbolDetector.IsSDKDetected(entry.Symbol),
                    IsDefined = SDKSymbolDetector.IsSymbolDefined(entry.Symbol, _symbolScope),
                });
            }
        }

        private void DrawSymbolRow(SymbolRow row) {
            Color statusColor = (row.IsDetected && row.IsDefined) ? Color.green : (row.IsDetected ? Color.red : (row.IsDefined ? Color.yellow : Color.gray));
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.contentColor = statusColor; GUILayout.Label("\u2022", GUILayout.Width(15)); GUI.contentColor = Color.white;
            EditorGUILayout.LabelField(row.Symbol, GUILayout.Width(180));
            GUILayout.Label(row.DisplayName, GUILayout.Width(140));
            GUILayout.FlexibleSpace();
            if (row.IsDefined) { if (GUILayout.Button("Remove", GUILayout.Width(80))) SDKSymbolDetector.RemoveSymbol(row.Symbol, _symbolScope); }
            else { if (GUILayout.Button("Add", GUILayout.Width(80))) SDKSymbolDetector.AddSymbol(row.Symbol, _symbolScope); }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBulkSymbolsToolbar() {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add All Missing")) SDKSymbolDetector.RunDetection(_symbolScope);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSymbolTableHeader() {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("", GUILayout.Width(16));
            GUILayout.Label("Symbol", EditorStyles.miniLabel, GUILayout.Width(180));
            GUILayout.Label("SDK", EditorStyles.miniLabel, GUILayout.Width(140));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Action", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════
        //  LOGIC: Sources
        // ═══════════════════════════════════════════════════════

        private void InitializeSourceSwitcher() {
            _gitRepoUrl = EditorPrefs.GetString("ArcherSDK_GitRepoUrl", "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git");
            _gitRef = EditorPrefs.GetString("ArcherSDK_GitRef", "main");
            _localRelativePath = EditorPrefs.GetString("ArcherSDK_LocalPath", "../../archer-core-sdk-unity");
            _manifestPath = Path.GetFullPath("Packages/manifest.json");
            RefreshPackageSources();
        }

        private void RefreshPackageSources() {
            _packages.Clear(); if (!File.Exists(_manifestPath)) return;
            var content = File.ReadAllText(_manifestPath);
            var regex = new Regex($@"""({Regex.Escape(PackagePrefix)}[^""]+)""\s*:\s*""([^""]+)""");
            foreach (Match match in regex.Matches(content)) {
                var entry = new PackageEntry { Name = match.Groups[1].Value, CurrentSource = match.Groups[2].Value };
                entry.IsLocal = entry.CurrentSource.StartsWith("file:");
                entry.IsGit = entry.CurrentSource.Contains(".git");
                entry.GitRef = entry.IsGit ? (entry.CurrentSource.Contains("#") ? entry.CurrentSource.Split('#').Last() : _gitRef) : _gitRef;
                entry.InstalledVersion = ReadPackageVersion(entry.Name);
                _packages.Add(entry);
            }
            if (_packages.Count == 0) _currentSourceMode = SourceMode.Unknown;
            else if (_packages.All(p => p.IsLocal)) _currentSourceMode = SourceMode.Local;
            else if (_packages.All(p => p.IsGit)) _currentSourceMode = SourceMode.Git;
            else _currentSourceMode = SourceMode.Mixed;
        }

        private void SwitchAllSources(bool toLocal) {
            var content = File.ReadAllText(_manifestPath);
            foreach (var pkg in _packages) {
                string ns = toLocal ? $"file:{_localRelativePath}/{pkg.Name}" : $"{_gitRepoUrl}?path={pkg.Name}#{pkg.GitRef}";
                content = content.Replace($"\"{pkg.Name}\": \"{pkg.CurrentSource}\"", $"\"{pkg.Name}\": \"{ns}\"");
            }
            File.WriteAllText(_manifestPath, content); RefreshPackageSources(); UnityEditor.PackageManager.Client.Resolve();
        }

        private void SwitchSingleSource(PackageEntry pkg, bool toLocal) {
            var content = File.ReadAllText(_manifestPath);
            string ns = toLocal ? $"file:{_localRelativePath}/{pkg.Name}" : $"{_gitRepoUrl}?path={pkg.Name}#{pkg.GitRef}";
            content = content.Replace($"\"{pkg.Name}\": \"{pkg.CurrentSource}\"", $"\"{pkg.Name}\": \"{ns}\"");
            File.WriteAllText(_manifestPath, content); RefreshPackageSources(); UnityEditor.PackageManager.Client.Resolve();
        }

        // ═══════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════

        private static string ReadPackageVersion(string pkgName) {
            string p = Path.GetFullPath($"Packages/{pkgName}/package.json");
            if (!File.Exists(p)) p = Path.GetFullPath($"Library/PackageCache/{pkgName}/package.json");
            if (!File.Exists(p)) return "?";
            var m = Regex.Match(File.ReadAllText(p), @"""version""\s*:\s*""([^""]+)""");
            return m.Success ? m.Groups[1].Value : "?";
        }

        private static string GetRelativePath(string from, string to) {
            var f = new Uri(from.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var t = new Uri(to.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(f.MakeRelativeUri(t).ToString()).TrimEnd('/');
        }

        private static bool CreateConfigIfMissing<T>(string name) where T : ScriptableObject {
            string path = $"{ResourcesPath}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return false;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path); return true;
        }

        private static bool CreateModuleConfig(string name) {
            string path = $"{ResourcesPath}/{name}.asset"; if (File.Exists(Path.GetFullPath(path))) return false;
            System.Type t = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
                t = a.GetType($"ArcherStudio.SDK.Consent.{name}") ?? a.GetType($"ArcherStudio.SDK.Login.{name}") ?? a.GetType($"ArcherStudio.SDK.Tracking.{name}") ?? a.GetType($"ArcherStudio.SDK.Ads.{name}") ?? a.GetType($"ArcherStudio.SDK.IAP.{name}") ?? a.GetType($"ArcherStudio.SDK.RemoteConfig.{name}") ?? a.GetType($"ArcherStudio.SDK.Push.{name}") ?? a.GetType($"ArcherStudio.SDK.DeepLink.{name}") ?? a.GetType($"ArcherStudio.SDK.TestLab.{name}") ?? a.GetType($"ArcherStudio.SDK.CloudSave.{name}");
                if (t != null) break;
            }
            if (t == null) return false;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance(t), path); return true;
        }

        private static void EnsureDirectoryExists(string path) {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder)) {
                if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectoryExists(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private void DrawConfigButton<T>(string n, string l) where T : ScriptableObject {
            EditorGUILayout.BeginHorizontal(); string p = $"{ResourcesPath}/{n}.asset"; bool ex = AssetDatabase.LoadAssetAtPath<T>(p) != null;
            EditorGUILayout.LabelField($"  {l}{(ex ? "" : " (missing)")}", ex ? EditorStyles.label : EditorStyles.boldLabel);
            if (ex) { if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeObject = AssetDatabase.LoadAssetAtPath<T>(p); }
            else { if (GUILayout.Button("Create", GUILayout.Width(60))) { EnsureDirectoryExists(ResourcesPath); CreateConfigIfMissing<T>(n); AssetDatabase.SaveAssets(); } }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModuleConfigButton(string n, string l, string pn) {
            EditorGUILayout.BeginHorizontal(); string p = $"{ResourcesPath}/{n}.asset"; bool ex = File.Exists(Path.GetFullPath(p)); bool pi = File.Exists(Path.GetFullPath($"Packages/{pn}/package.json"));
            EditorGUILayout.LabelField($"  {l}{(!pi ? " (pkg not installed)" : (ex ? "" : " (missing)"))}", ex && pi ? EditorStyles.label : EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(!pi);
            if (ex) { if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(p); }
            else { if (GUILayout.Button("Create", GUILayout.Width(60))) { EnsureDirectoryExists(ResourcesPath); CreateModuleConfig(n); AssetDatabase.SaveAssets(); } }
            EditorGUI.EndDisabledGroup();
            if (SDKLibraryImporter.HasInfo(n)) { if (GUILayout.Button(SDKLibraryImporter.IsInstalled(n) ? "Installed" : "Import Lib", GUILayout.Width(100))) SDKLibraryImporter.ImportLibrary(n); } else GUILayout.Space(104);
            EditorGUILayout.EndHorizontal();
        }

        private static List<string> ValidateSetup() {
            var res = new List<string>(); if (Resources.Load<SDKCoreConfig>("SDKCoreConfig") == null) res.Add("SDKCoreConfig missing");
            if (Resources.Load<SDKBootstrapConfig>("SDKBootstrapConfig") == null) res.Add("SDKBootstrapConfig missing"); return res;
        }

        private List<string> _validationResults;

        private struct ModuleToggles {
            public bool Consent, Login, Tracking, Analytics, Ads, IAP, RemoteConfig, Push, DeepLink, TestLab, CloudSave;
        }

        private ModuleToggles GetModuleToggles() => new ModuleToggles {
            Consent = _enableConsent, Login = _enableLogin, Tracking = _enableTracking, Analytics = _enableAnalytics,
            Ads = _enableAds, IAP = _enableIAP, RemoteConfig = _enableRemoteConfig, Push = _enablePush,
            DeepLink = _enableDeepLink, TestLab = _enableTestLab, CloudSave = _enableCloudSave
        };

        private class PackageEntry { public string Name, CurrentSource, GitRef, InstalledVersion; public bool IsLocal, IsGit; }
        private class SymbolRow { public string Symbol, DisplayName, DetectionType; public bool IsDetected, IsDefined; }
    }
}
