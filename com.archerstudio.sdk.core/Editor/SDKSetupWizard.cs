using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Editor wizard to auto-create SDK configs and provide setup guidance.
    /// Menu: ArcherStudio > SDK > Setup Wizard
    /// </summary>
    public class SDKSetupWizard : EditorWindow {
        private const string Tag = "SDKSetupWizard";
        private const string ResourcesPath = "Assets/Resources";

        // ─── Wizard State ───
        private Vector2 _scrollPos;
        private int _currentTab;
        private readonly string[] _tabNames = {
            "Quick Setup", "Configs", "Symbols", "Validate"
        };

        // ─── Quick Setup toggles ───
        private bool _createConfigs = true;
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

        // ─── Module toggles ───
        private bool _enableConsent = true;
        private bool _enableTracking = true;
        private bool _enableAnalytics = true;
        private bool _enableAds = true;
        private bool _enableIAP = true;
        private bool _enableRemoteConfig = true;
        private bool _enablePush;
        private bool _enableDeepLink;
        private bool _enableTestLab;

        [MenuItem("ArcherStudio/SDK/Setup Wizard", false, 0)]
        public static void ShowWindow() {
            var window = GetWindow<SDKSetupWizard>("SDK Setup Wizard");
            window.minSize = new Vector2(560, 620);
            window.Show();
        }

        public static void ShowTab(int tabIndex) {
            var window = GetWindow<SDKSetupWizard>("SDK Setup Wizard");
            window.minSize = new Vector2(560, 620);
            window._currentTab = tabIndex;
            window.Show();
            window.Repaint();
        }

        private void OnFocus() {
            if (_currentTab == 2) {
                RefreshSymbolRows();
            }
        }

        [MenuItem("ArcherStudio/SDK/Create All Configs", false, 21)]
        public static void MenuCreateConfigs() {
            CreateAllConfigs(new ModuleToggles());
        }

        [MenuItem("ArcherStudio/SDK/Validate Setup", false, 40)]
        public static void MenuValidate() {
            var issues = ValidateSetup();
            if (issues.Count == 0) {
                EditorUtility.DisplayDialog("SDK Validation", "All checks passed!", "OK");
            } else {
                string msg = string.Join("\n", issues);
                EditorUtility.DisplayDialog("SDK Validation", $"Issues found:\n\n{msg}", "OK");
            }
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
                case 3: DrawValidateTab(); break;
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

        private void DrawQuickSetup() {
            EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates all required config assets in Assets/Resources/.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("App Settings", EditorStyles.boldLabel);
            _appId = EditorGUILayout.TextField("App ID", _appId);
            _adjustToken = EditorGUILayout.TextField("Adjust App Token", _adjustToken);
            _adSdkKey = EditorGUILayout.TextField("Ad SDK Key (MAX/IS)", _adSdkKey);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);
            _enableConsent = EditorGUILayout.Toggle("Consent (GDPR/ATT)", _enableConsent);
            _enableTracking = EditorGUILayout.Toggle("Tracking (Firebase + Adjust)", _enableTracking);
            _enableAnalytics = EditorGUILayout.Toggle("Analytics", _enableAnalytics);
            _enableAds = EditorGUILayout.Toggle("Ads (Mediation)", _enableAds);
            _enableIAP = EditorGUILayout.Toggle("In-App Purchase", _enableIAP);
            _enableRemoteConfig = EditorGUILayout.Toggle("Remote Config", _enableRemoteConfig);
            _enablePush = EditorGUILayout.Toggle("Push Notifications", _enablePush);
            _enableDeepLink = EditorGUILayout.Toggle("Deep Linking", _enableDeepLink);
            _enableTestLab = EditorGUILayout.Toggle("Firebase Test Lab", _enableTestLab);

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

        private void DrawConfigsTab() {
            EditorGUILayout.LabelField("Config Assets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Create individual config assets in Assets/Resources/. " +
                "Existing configs will NOT be overwritten.",
                MessageType.Info);

            EditorGUILayout.Space(8);

            DrawConfigButton<SDKCoreConfig>("SDKCoreConfig", "Core Config (required)");
            DrawConfigButton<SDKBootstrapConfig>("SDKBootstrapConfig", "Bootstrap Config (required)");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Module Configs", EditorStyles.boldLabel);

            DrawModuleConfigButton("ConsentConfig", "Consent Config", "com.archerstudio.sdk.consent");
            DrawModuleConfigButton("TrackingConfig", "Tracking Config", "com.archerstudio.sdk.tracking");
            DrawModuleConfigButton("AdConfig", "Ad Config", "com.archerstudio.sdk.ads");
            DrawModuleConfigButton("IAPConfig", "IAP Config", "com.archerstudio.sdk.iap");
            DrawModuleConfigButton("RemoteConfigConfig", "Remote Config", "com.archerstudio.sdk.remoteconfig");
            DrawModuleConfigButton("PushConfig", "Push Config", "com.archerstudio.sdk.push");
            DrawModuleConfigButton("DeepLinkConfig", "Deep Link Config", "com.archerstudio.sdk.deeplink");
            DrawModuleConfigButton("TestLabConfig", "Test Lab Config", "com.archerstudio.sdk.testlab");
        }

        private void DrawSymbolsTab() {
            EditorGUILayout.LabelField("SDK Scripting Define Symbols", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Apply to:", GUILayout.Width(60));
            _symbolScope = (SDKSymbolDetector.SymbolScope)EditorGUILayout.EnumPopup(_symbolScope);
            EditorGUILayout.EndHorizontal();

            string scopeDesc = "";
            switch (_symbolScope) {
                case SDKSymbolDetector.SymbolScope.ActiveProfile:
                    scopeDesc = "Symbols are applied to the Active Build Profile only.";
                    break;
                case SDKSymbolDetector.SymbolScope.ActivePlatform:
                    scopeDesc = "Symbols are applied to the active platform's PlayerSettings.";
                    break;
                case SDKSymbolDetector.SymbolScope.AllMobilePlatforms:
                    scopeDesc = "Symbols are applied to both Android and iOS PlayerSettings.";
                    break;
            }
            EditorGUILayout.HelpBox(scopeDesc, MessageType.Info);

            EditorGUILayout.Space(4);
            var targetLabel = SDKSymbolDetector.GetScopeLabel(_symbolScope);
            EditorGUILayout.LabelField($"Target: {targetLabel}", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(
                "Green = OK  |  Red = SDK found, symbol missing  |  " +
                "Yellow = symbol defined, SDK removed  |  Gray = not installed",
                MessageType.None);
            if (GUILayout.Button("Refresh", GUILayout.Width(70), GUILayout.Height(38))) {
                RefreshSymbolRows();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Auto-Detect All SDKs & Sync Symbols", GUILayout.Height(28))) {
                SDKSymbolDetector.RunDetection(_symbolScope);
                RefreshSymbolRows();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            DrawBulkActionsToolbar();

            EditorGUILayout.Space(8);
            DrawSymbolTableHeader();

            if (_symbolRows == null) RefreshSymbolRows();
            foreach (var row in _symbolRows) DrawSymbolRow(row);

            DrawPendingChangesBar();
            EditorGUILayout.Space(8);
            DrawCustomSymbolSection();
        }

        private void RefreshSymbolRows() {
            _symbolRows = new List<SymbolRow>();
            foreach (var entry in SDKSymbolDetector.Entries) {
                _symbolRows.Add(new SymbolRow {
                    Symbol = entry.Symbol,
                    DisplayName = entry.DisplayName,
                    DetectionType = entry.DetectionType,
                    IsDetected = SDKSymbolDetector.IsSDKDetected(entry.Symbol),
                    IsDefined = SDKSymbolDetector.IsSymbolDefined(entry.Symbol, _symbolScope),
                });
            }
        }

        private static void DrawSymbolTableHeader() {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("", GUILayout.Width(18));
            GUILayout.Label("Symbol", EditorStyles.miniLabel, GUILayout.Width(180));
            GUILayout.Label("SDK", EditorStyles.miniLabel, GUILayout.Width(140));
            GUILayout.Label("Detected", EditorStyles.miniLabel, GUILayout.Width(55));
            GUILayout.Label("Defined", EditorStyles.miniLabel, GUILayout.Width(55));
            GUILayout.Label("Pending", EditorStyles.miniLabel, GUILayout.Width(55));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Action", EditorStyles.miniLabel, GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSymbolRow(SymbolRow row) {
            Color statusColor;
            string statusIcon;
            if (row.IsDetected && row.IsDefined) {
                statusColor = new Color(0.3f, 0.8f, 0.3f); statusIcon = "\u2714";
            } else if (row.IsDetected && !row.IsDefined) {
                statusColor = new Color(0.9f, 0.3f, 0.3f); statusIcon = "\u2718";
            } else if (!row.IsDetected && row.IsDefined) {
                statusColor = new Color(0.9f, 0.8f, 0.2f); statusIcon = "\u26A0";
            } else {
                statusColor = new Color(0.5f, 0.5f, 0.5f); statusIcon = "\u2014";
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var prevColor = GUI.contentColor;
            GUI.contentColor = statusColor;
            GUILayout.Label(statusIcon, GUILayout.Width(18));
            GUI.contentColor = prevColor;

            EditorGUILayout.SelectableLabel(row.Symbol, EditorStyles.label, GUILayout.Width(180), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            GUILayout.Label(row.DisplayName, GUILayout.Width(140));

            prevColor = GUI.contentColor;
            GUI.contentColor = row.IsDetected ? Color.green : Color.gray;
            GUILayout.Label(row.IsDetected ? "Yes" : "No", GUILayout.Width(55));
            GUI.contentColor = row.IsDefined ? Color.green : Color.gray;
            GUILayout.Label(row.IsDefined ? "Yes" : "No", GUILayout.Width(55));

            switch (row.Pending) {
                case PendingAction.Add: GUI.contentColor = new Color(0.3f, 0.9f, 0.3f); GUILayout.Label("+Add", GUILayout.Width(55)); break;
                case PendingAction.Remove: GUI.contentColor = new Color(0.9f, 0.4f, 0.3f); GUILayout.Label("-Rem", GUILayout.Width(55)); break;
                default: GUI.contentColor = Color.gray; GUILayout.Label("--", GUILayout.Width(55)); break;
            }
            GUI.contentColor = prevColor;

            GUILayout.FlexibleSpace();
            if (row.Pending != PendingAction.None) {
                if (GUILayout.Button("Undo", GUILayout.Width(60))) row.Pending = PendingAction.None;
            }

            if (row.IsDefined) {
                GUI.enabled = row.Pending != PendingAction.Remove;
                if (GUILayout.Button("Queue Remove", GUILayout.Width(90))) row.Pending = PendingAction.Remove;
                GUI.enabled = true;
            } else {
                GUI.enabled = row.Pending != PendingAction.Add;
                if (GUILayout.Button("Queue Add", GUILayout.Width(90))) row.Pending = PendingAction.Add;
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBulkActionsToolbar() {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Queue Add All Missing", GUILayout.Height(22))) {
                if (_symbolRows == null) RefreshSymbolRows();
                foreach (var row in _symbolRows) if (row.IsDetected && !row.IsDefined) row.Pending = PendingAction.Add;
            }
            if (GUILayout.Button("Queue Remove All Orphaned", GUILayout.Height(22))) {
                if (_symbolRows == null) RefreshSymbolRows();
                foreach (var row in _symbolRows) if (!row.IsDetected && row.IsDefined) row.Pending = PendingAction.Remove;
            }
            if (GUILayout.Button("Clear All Pending", GUILayout.Height(22))) {
                if (_symbolRows != null) foreach (var row in _symbolRows) row.Pending = PendingAction.None;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPendingChangesBar() {
            if (_symbolRows == null) return;
            var pendingChanges = new List<SDKSymbolDetector.SymbolChange>();
            foreach (var row in _symbolRows) {
                if (row.Pending == PendingAction.Add) pendingChanges.Add(new SDKSymbolDetector.SymbolChange(row.Symbol, true, row.DisplayName));
                else if (row.Pending == PendingAction.Remove) pendingChanges.Add(new SDKSymbolDetector.SymbolChange(row.Symbol, false, row.DisplayName));
            }
            foreach (var sym in _pendingCustomAdds) pendingChanges.Add(new SDKSymbolDetector.SymbolChange(sym, true, "Custom"));
            foreach (var sym in _pendingCustomRemoves) pendingChanges.Add(new SDKSymbolDetector.SymbolChange(sym, false, "Custom"));

            if (pendingChanges.Count == 0) return;

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button($"Apply All ({pendingChanges.Count} changes)", GUILayout.Height(30))) {
                SDKSymbolDetector.ApplyBulkChanges(pendingChanges, _symbolScope);
                _pendingCustomAdds.Clear(); _pendingCustomRemoves.Clear();
                RefreshSymbolRows();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawCustomSymbolSection() {
            _showCustomSection = EditorGUILayout.Foldout(_showCustomSection, "Custom Symbol", true, EditorStyles.foldoutHeader);
            if (!_showCustomSection) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Symbol:", GUILayout.Width(55));
            _customSymbol = EditorGUILayout.TextField(_customSymbol);
            GUI.enabled = !string.IsNullOrWhiteSpace(_customSymbol);
            if (GUILayout.Button("Queue Add", GUILayout.Width(80))) {
                var sym = _customSymbol.Trim();
                if (!_pendingCustomAdds.Contains(sym)) { _pendingCustomAdds.Add(sym); _pendingCustomRemoves.Remove(sym); }
                _customSymbol = "";
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawValidateTab() {
            EditorGUILayout.LabelField("Validate Setup", EditorStyles.boldLabel);
            if (GUILayout.Button("Run Validation", GUILayout.Height(30))) _validationResults = ValidateSetup();
            if (_validationResults != null) {
                foreach (var issue in _validationResults) EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
        }

        private List<string> _validationResults;

        private void RunQuickSetup() {
            if (_createConfigs) {
                var toggles = GetModuleToggles();
                CreateAllConfigs(toggles);
                ApplyConfigValues(toggles);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("SDK Setup Complete", "Config assets created/verified!", "OK");
        }

        private struct ModuleToggles {
            public bool Consent, Tracking, Analytics, Ads, IAP, RemoteConfig, Push, DeepLink, TestLab;
            public ModuleToggles(bool allOn = true) { Consent = Tracking = Analytics = Ads = IAP = RemoteConfig = allOn; Push = DeepLink = TestLab = false; }
        }

        private ModuleToggles GetModuleToggles() {
            return new ModuleToggles {
                Consent = _enableConsent, Tracking = _enableTracking, Analytics = _enableAnalytics, Ads = _enableAds, IAP = _enableIAP,
                RemoteConfig = _enableRemoteConfig, Push = _enablePush, DeepLink = _enableDeepLink, TestLab = _enableTestLab
            };
        }

        private static int CreateAllConfigs(ModuleToggles toggles) {
            EnsureDirectoryExists(ResourcesPath);
            int count = 0;
            count += CreateConfigIfMissing<SDKCoreConfig>("SDKCoreConfig") ? 1 : 0;
            count += CreateConfigIfMissing<SDKBootstrapConfig>("SDKBootstrapConfig") ? 1 : 0;
            if (toggles.Consent) count += CreateModuleConfig("ConsentConfig") ? 1 : 0;
            if (toggles.Tracking) count += CreateModuleConfig("TrackingConfig") ? 1 : 0;
            if (toggles.Ads) count += CreateModuleConfig("AdConfig") ? 1 : 0;
            if (toggles.IAP) count += CreateModuleConfig("IAPConfig") ? 1 : 0;
            if (toggles.RemoteConfig) count += CreateModuleConfig("RemoteConfigConfig") ? 1 : 0;
            if (toggles.Push) count += CreateModuleConfig("PushConfig") ? 1 : 0;
            if (toggles.DeepLink) count += CreateModuleConfig("DeepLinkConfig") ? 1 : 0;
            if (toggles.TestLab) count += CreateModuleConfig("TestLabConfig") ? 1 : 0;
            return count;
        }

        private static bool CreateConfigIfMissing<T>(string name) where T : ScriptableObject {
            string path = $"{ResourcesPath}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return false;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return true;
        }

        private static bool CreateModuleConfig(string configName) {
            string path = $"{ResourcesPath}/{configName}.asset";
            if (File.Exists(Path.GetFullPath(path))) return false;
            System.Type configType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) {
                configType = assembly.GetType($"ArcherStudio.SDK.Consent.{configName}")
                          ?? assembly.GetType($"ArcherStudio.SDK.Tracking.{configName}")
                          ?? assembly.GetType($"ArcherStudio.SDK.Ads.{configName}")
                          ?? assembly.GetType($"ArcherStudio.SDK.IAP.{configName}")
                          ?? assembly.GetType($"ArcherStudio.SDK.RemoteConfig.{configName}")
                          ?? assembly.GetType($"ArcherStudio.SDK.Push.{configName}")
                          ?? assembly.GetType($"ArcherStudio.SDK.DeepLink.{configName}")
                          ?? assembly.GetType($"ArcherStudio.SDK.TestLab.{configName}");
                if (configType != null) break;
            }
            if (configType == null) return false;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance(configType), path);
            return true;
        }

        private void ApplyConfigValues(ModuleToggles toggles) {
            var coreConfig = AssetDatabase.LoadAssetAtPath<SDKCoreConfig>($"{ResourcesPath}/SDKCoreConfig.asset");
            if (coreConfig != null) {
                coreConfig.AppId = _appId; coreConfig.EnableConsent = toggles.Consent; coreConfig.EnableTracking = toggles.Tracking;
                coreConfig.EnableAnalytics = toggles.Analytics; coreConfig.EnableAds = toggles.Ads; coreConfig.EnableIAP = toggles.IAP;
                coreConfig.EnableRemoteConfig = toggles.RemoteConfig;
                coreConfig.EnablePush = toggles.Push; coreConfig.EnableDeepLink = toggles.DeepLink; coreConfig.EnableTestLab = toggles.TestLab;
                EditorUtility.SetDirty(coreConfig);
            }

            var trackingAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{ResourcesPath}/TrackingConfig.asset");
            if (trackingAsset != null && !string.IsNullOrEmpty(_adjustToken)) {
                var field = trackingAsset.GetType().GetField("AdjustAppToken");
                if (field != null) { field.SetValue(trackingAsset, _adjustToken); EditorUtility.SetDirty(trackingAsset); }
            }

            var adAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{ResourcesPath}/AdConfig.asset");
            if (adAsset != null && !string.IsNullOrEmpty(_adSdkKey)) {
                var field = adAsset.GetType().GetField("SdkKey");
                if (field != null) { field.SetValue(adAsset, _adSdkKey); EditorUtility.SetDirty(adAsset); }
            }
        }

        private static List<string> ValidateSetup() {
            var issues = new List<string>();
            if (Resources.Load<SDKCoreConfig>("SDKCoreConfig") == null) issues.Add("SDKCoreConfig missing");
            if (Resources.Load<SDKBootstrapConfig>("SDKBootstrapConfig") == null) issues.Add("SDKBootstrapConfig missing");
            return issues;
        }

        private static void EnsureDirectoryExists(string path) {
            if (!AssetDatabase.IsValidFolder(path)) {
                string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                string folder = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder)) {
                    if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectoryExists(parent);
                    AssetDatabase.CreateFolder(parent, folder);
                }
            }
        }

        private void DrawConfigButton<T>(string name, string label) where T : ScriptableObject {
            EditorGUILayout.BeginHorizontal();
            string path = $"{ResourcesPath}/{name}.asset";
            bool exists = AssetDatabase.LoadAssetAtPath<T>(path) != null;
            EditorGUILayout.LabelField($"  {label}{(exists ? "" : " (missing)")}", exists ? EditorStyles.label : EditorStyles.boldLabel);
            if (exists) { if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeObject = AssetDatabase.LoadAssetAtPath<T>(path); }
            else { if (GUILayout.Button("Create", GUILayout.Width(60))) { EnsureDirectoryExists(ResourcesPath); CreateConfigIfMissing<T>(name); AssetDatabase.SaveAssets(); } }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModuleConfigButton(string name, string label, string packageName) {
            EditorGUILayout.BeginHorizontal();
            string path = $"{ResourcesPath}/{name}.asset";
            bool exists = File.Exists(Path.GetFullPath(path));
            bool packageInstalled = File.Exists(Path.GetFullPath($"Packages/{packageName}/package.json"));
            EditorGUI.BeginDisabledGroup(!packageInstalled);
            EditorGUILayout.LabelField($"  {label}{(!packageInstalled ? " (package not installed)" : (exists ? "" : " (missing)"))}", exists && packageInstalled ? EditorStyles.label : EditorStyles.boldLabel);
            if (exists) { if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path); }
            else { if (GUILayout.Button("Create", GUILayout.Width(60))) { EnsureDirectoryExists(ResourcesPath); CreateModuleConfig(name); AssetDatabase.SaveAssets(); } }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private enum PendingAction { None, Add, Remove }
        private class SymbolRow { public string Symbol, DisplayName, DetectionType; public bool IsDetected, IsDefined; public PendingAction Pending; }
    }
}
