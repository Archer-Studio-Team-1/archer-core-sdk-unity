using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Editor window to verify and fix AndroidManifest.xml settings
    /// for privacy/consent compliance, permissions, and SDK configuration.
    ///
    /// Menu: ArcherStudio > SDK > Verify Android Manifest
    /// </summary>
    public class AndroidManifestVerifier : EditorWindow {
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string ToolsNs = "http://schemas.android.com/tools";

        private Vector2 _scrollPos;
        private List<VerifyResult> _results;
        private int _passCount;
        private int _failCount;
        private int _warnCount;
        private bool _hasFixableIssues;

        [MenuItem("ArcherStudio/SDK/Verify Android Manifest", false, 200)]
        public static void ShowWindow() {
            var window = GetWindow<AndroidManifestVerifier>("Manifest Verifier");
            window.minSize = new Vector2(560, 450);
            window.RunVerification();
        }

        private void OnGUI() {
            GUILayout.Space(6);
            EditorGUILayout.LabelField("Android Manifest Verifier", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Checks AndroidManifest.xml for privacy/consent defaults, " +
                "required permissions, and SDK configuration.\n" +
                "Use 'Fix All Issues' to auto-fix FAIL and WARN items.",
                MessageType.Info);

            GUILayout.Space(4);

            // ─── Action Buttons ───
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Verification", GUILayout.Height(28))) {
                RunVerification();
            }
            GUI.enabled = _hasFixableIssues;
            if (GUILayout.Button("Fix All Issues", GUILayout.Width(120), GUILayout.Height(28))) {
                if (EditorUtility.DisplayDialog("Fix Manifest",
                    "This will modify AndroidManifest.xml to fix all detected issues:\n\n" +
                    "- Set privacy meta-data to correct values\n" +
                    "- Add missing permissions\n" +
                    "- Fix launcher activity attributes\n" +
                    "- Add tools namespace if missing\n\n" +
                    "A backup will be created. Continue?", "Fix", "Cancel")) {
                    FixAllIssues();
                }
            }
            GUI.enabled = true;
            if (GUILayout.Button("Open Manifest", GUILayout.Width(120), GUILayout.Height(28))) {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
                if (asset != null) AssetDatabase.OpenAsset(asset);
                else EditorUtility.DisplayDialog("Not Found", $"{ManifestPath} not found.", "OK");
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (_results == null) {
                EditorGUILayout.HelpBox("Click 'Run Verification' to check manifest.", MessageType.None);
                return;
            }

            // Summary
            EditorGUILayout.LabelField(
                $"Results: <color=green>{_passCount} PASS</color>  " +
                $"<color=red>{_failCount} FAIL</color>  " +
                $"<color=yellow>{_warnCount} WARN</color>",
                new GUIStyle(EditorStyles.label) { richText = true, fontSize = 13 });

            GUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            string currentSection = "";
            foreach (var result in _results) {
                if (result.Section != currentSection) {
                    currentSection = result.Section;
                    GUILayout.Space(6);
                    EditorGUILayout.LabelField($"── {currentSection} ──", EditorStyles.boldLabel);
                }

                DrawResult(result);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawResult(VerifyResult result) {
            string icon;
            Color color;
            switch (result.Status) {
                case ResultStatus.Pass:
                    icon = "\u2713"; color = new Color(0.2f, 0.8f, 0.2f); break;
                case ResultStatus.Fail:
                    icon = "\u2717"; color = new Color(1f, 0.3f, 0.3f); break;
                default:
                    icon = "!"; color = new Color(1f, 0.85f, 0.2f); break;
            }

            EditorGUILayout.BeginHorizontal();
            var prevColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(icon, GUILayout.Width(16));
            GUI.color = prevColor;

            EditorGUILayout.LabelField(result.Label,
                new GUIStyle(EditorStyles.label) { richText = true },
                GUILayout.MinWidth(200));

            if (!string.IsNullOrEmpty(result.Detail)) {
                EditorGUILayout.LabelField(result.Detail,
                    new GUIStyle(EditorStyles.miniLabel) { richText = true, wordWrap = true });
            }
            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════
        //  Verification
        // ═══════════════════════════════════════════════

        private void RunVerification() {
            _results = new List<VerifyResult>();
            _passCount = 0;
            _failCount = 0;
            _warnCount = 0;
            _hasFixableIssues = false;

            if (!File.Exists(ManifestPath)) {
                AddResult("General", ResultStatus.Fail,
                    "AndroidManifest.xml not found",
                    $"Expected at: {ManifestPath}");
                _hasFixableIssues = true;
                Repaint();
                return;
            }

            var doc = new XmlDocument();
            doc.Load(ManifestPath);

            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("android", AndroidNs);
            nsManager.AddNamespace("tools", ToolsNs);

            VerifyPrivacyDefaults(doc, nsManager);
            VerifyPermissions(doc, nsManager);
            VerifyLauncherActivity(doc, nsManager);
            VerifyFacebookSdk(doc, nsManager);
            VerifyAdjustSdk(doc, nsManager);
            VerifyGeneral(doc, nsManager);

            _hasFixableIssues = _failCount > 0 || _warnCount > 0;
            Repaint();
        }

        // ─── Privacy / Consent Defaults ───

        private void VerifyPrivacyDefaults(XmlDocument doc, XmlNamespaceManager ns) {
            const string section = "Privacy / Consent Defaults";

            VerifyMetaData(doc, ns, section,
                "com.facebook.sdk.AutoLogAppEventsEnabled", "false",
                "Facebook auto-logs events before consent → GDPR violation");

            VerifyMetaData(doc, ns, section,
                "com.facebook.sdk.AdvertiserIDCollectionEnabled", "false",
                "Facebook collects Advertising ID before consent");

            VerifyMetaData(doc, ns, section,
                "firebase_analytics_collection_enabled", "false",
                "Firebase collects analytics before consent");

            VerifyMetaData(doc, ns, section,
                "google_analytics_default_allow_ad_storage", "false",
                "Google Analytics allows ad storage before consent");

            VerifyMetaData(doc, ns, section,
                "google_analytics_default_allow_analytics_storage", "false",
                "Google Analytics allows analytics storage before consent");

            VerifyMetaData(doc, ns, section,
                "google_analytics_default_allow_ad_user_data", "false",
                "Google DMA: Allows ad-related user data before consent");

            VerifyMetaData(doc, ns, section,
                "google_analytics_default_allow_ad_personalization_signals", "false",
                "Google DMA: Allows ad personalization signals before consent");
        }

        // ─── Permissions ───

        private void VerifyPermissions(XmlDocument doc, XmlNamespaceManager ns) {
            const string section = "Permissions";

            VerifyPermission(doc, ns, section,
                "android.permission.INTERNET", true,
                "Required for all network SDKs");

            VerifyPermission(doc, ns, section,
                "com.google.android.gms.permission.AD_ID", true,
                "Required for Adjust/Firebase to read Google Advertising ID on Android 13+");

            VerifyPermission(doc, ns, section,
                "com.google.android.finsky.permission.BIND_GET_INSTALL_REFERRER_SERVICE", true,
                "Required for Adjust install referrer attribution");

            VerifyPermission(doc, ns, section,
                "android.permission.ACCESS_NETWORK_STATE", true,
                "Allows SDKs to check connection type");

            VerifyPermission(doc, ns, section,
                "android.permission.WAKE_LOCK", true,
                "Allows Firebase/Push to wake device for notifications");

            // Warn about dangerous permissions that shouldn't be there
            VerifyPermissionAbsent(doc, ns, section,
                "android.permission.READ_PHONE_STATE",
                "Unnecessary — triggers Play Store privacy warning and potentially blocks approval");

            VerifyPermissionAbsent(doc, ns, section,
                "android.permission.ACCESS_FINE_LOCATION",
                "Unnecessary unless game needs GPS — triggers privacy warning");
        }

        // ─── Launcher Activity ───

        private void VerifyLauncherActivity(XmlDocument doc, XmlNamespaceManager ns) {
            const string section = "Launcher Activity";

            var activity = doc.SelectSingleNode(
                "//activity[@android:name='com.unity3d.player.UnityPlayerActivity']", ns) as XmlElement;

            if (activity == null) {
                AddResult(section, ResultStatus.Fail,
                    "UnityPlayerActivity not found",
                    "App will not launch without this activity");
                return;
            }

            AddResult(section, ResultStatus.Pass, "UnityPlayerActivity present");

            var exported = activity.GetAttribute("exported", AndroidNs);
            if (exported == "true") {
                AddResult(section, ResultStatus.Pass, "android:exported=\"true\"");
            } else {
                AddResult(section, ResultStatus.Fail,
                    "android:exported missing or not \"true\"",
                    "Required for launcher activity on Android 12+");
            }

            var hwAccel = activity.GetAttribute("hardwareAccelerated", AndroidNs);
            if (hwAccel == "true") {
                AddResult(section, ResultStatus.Pass, "android:hardwareAccelerated=\"true\"");
            } else {
                AddResult(section, ResultStatus.Warn,
                    "android:hardwareAccelerated missing or not \"true\"",
                    "Recommended for Ad SDKs (video performance)");
            }

            var toolsNode = activity.GetAttribute("node", ToolsNs);
            if (toolsNode == "replace") {
                AddResult(section, ResultStatus.Pass, "tools:node=\"replace\"",
                    "Prevents enabled=false conflict in Unity 6");
            } else {
                AddResult(section, ResultStatus.Warn,
                    "tools:node=\"replace\" not set",
                    "Unity 6 may set enabled=false causing launch failure");
            }

            var intentFilter = activity.SelectSingleNode(
                "intent-filter/action[@android:name='android.intent.action.MAIN']", ns);
            var launcherCategory = activity.SelectSingleNode(
                "intent-filter/category[@android:name='android.intent.category.LAUNCHER']", ns);

            if (intentFilter != null && launcherCategory != null) {
                AddResult(section, ResultStatus.Pass, "MAIN/LAUNCHER intent-filter present");
            } else {
                AddResult(section, ResultStatus.Fail,
                    "Missing MAIN/LAUNCHER intent-filter",
                    "App won't appear in launcher");
            }
        }

        // ─── Facebook SDK ───

        private void VerifyFacebookSdk(XmlDocument doc, XmlNamespaceManager ns) {
            const string section = "Facebook SDK";

            var appId = GetMetaDataValue(doc, ns, "com.facebook.sdk.ApplicationId");
            if (appId != null) {
                AddResult(section, ResultStatus.Pass, $"ApplicationId: {appId}");
            } else {
                AddResult(section, ResultStatus.Warn,
                    "com.facebook.sdk.ApplicationId not set",
                    "Facebook SDK won't work without App ID");
            }

            var clientToken = GetMetaDataValue(doc, ns, "com.facebook.sdk.ClientToken");
            if (!string.IsNullOrEmpty(clientToken)) {
                AddResult(section, ResultStatus.Pass, $"ClientToken: {MaskValue(clientToken)}");
            } else {
                AddResult(section, ResultStatus.Warn,
                    "com.facebook.sdk.ClientToken not set",
                    "Required for Facebook SDK 13+");
            }

            var provider = doc.SelectSingleNode(
                "//provider[contains(@android:authorities, 'FacebookContentProvider')]", ns);
            if (provider != null) {
                AddResult(section, ResultStatus.Pass, "FacebookContentProvider registered");
            } else {
                AddResult(section, ResultStatus.Warn,
                    "FacebookContentProvider not found",
                    "Required for Facebook sharing features");
            }
        }

        // ─── Adjust SDK ───

        private void VerifyAdjustSdk(XmlDocument doc, XmlNamespaceManager ns) {
            const string section = "Adjust SDK";

            var receiver = doc.SelectSingleNode(
                "//receiver[@android:name='com.adjust.sdk.AdjustReferrerReceiver']", ns);
            if (receiver != null) {
                AddResult(section, ResultStatus.Pass, "AdjustReferrerReceiver registered");
            } else {
                AddResult(section, ResultStatus.Warn,
                    "AdjustReferrerReceiver not found",
                    "Install referrer attribution may not work on older devices");
            }
        }

        // ─── General ───

        private void VerifyGeneral(XmlDocument doc, XmlNamespaceManager ns) {
            const string section = "General";

            var manifest = doc.DocumentElement;
            var toolsDecl = manifest?.GetAttribute("xmlns:tools");
            if (!string.IsNullOrEmpty(toolsDecl)) {
                AddResult(section, ResultStatus.Pass, "tools namespace declared");
            } else {
                AddResult(section, ResultStatus.Warn,
                    "tools namespace not declared",
                    "Required for tools:node=\"replace\" and tools:remove");
            }

            var app = doc.SelectSingleNode("//application") as XmlElement;
            if (app != null) {
                var debuggable = app.GetAttribute("debuggable", AndroidNs);
                if (debuggable == "true") {
                    AddResult(section, ResultStatus.Warn,
                        "android:debuggable=\"true\" on <application>",
                        "Remove for release builds — security risk");
                } else {
                    AddResult(section, ResultStatus.Pass, "No android:debuggable on <application>");
                }

                var cleartext = app.GetAttribute("usesCleartextTraffic", AndroidNs);
                if (cleartext == "true") {
                    AddResult(section, ResultStatus.Warn,
                        "usesCleartextTraffic=\"true\"",
                        "Allows HTTP traffic — security risk in production");
                }
            }
        }

        // ═══════════════════════════════════════════════
        //  Fix All Issues
        // ═══════════════════════════════════════════════

        private void FixAllIssues() {
            if (!File.Exists(ManifestPath)) {
                CreateMinimalManifest();
            }

            // Backup
            var backupPath = ManifestPath + ".bak";
            File.Copy(ManifestPath, backupPath, true);
            Debug.Log($"[ManifestVerifier] Backup created: {backupPath}");

            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.Load(ManifestPath);

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", AndroidNs);
            ns.AddNamespace("tools", ToolsNs);

            int fixCount = 0;

            // Ensure tools namespace on <manifest>
            fixCount += FixToolsNamespace(doc);

            // Fix privacy meta-data
            fixCount += FixMetaData(doc, ns, "com.facebook.sdk.AutoLogAppEventsEnabled", "false");
            fixCount += FixMetaData(doc, ns, "com.facebook.sdk.AdvertiserIDCollectionEnabled", "false");
            fixCount += FixMetaData(doc, ns, "firebase_analytics_collection_enabled", "false");
            fixCount += FixMetaData(doc, ns, "google_analytics_default_allow_ad_storage", "false");
            fixCount += FixMetaData(doc, ns, "google_analytics_default_allow_analytics_storage", "false");
            fixCount += FixMetaData(doc, ns, "google_analytics_default_allow_ad_user_data", "false");
            fixCount += FixMetaData(doc, ns, "google_analytics_default_allow_ad_personalization_signals", "false");

            // Fix permissions
            fixCount += FixPermission(doc, ns, "android.permission.INTERNET");
            fixCount += FixPermission(doc, ns, "com.google.android.gms.permission.AD_ID");
            fixCount += FixPermission(doc, ns, "com.google.android.finsky.permission.BIND_GET_INSTALL_REFERRER_SERVICE");
            fixCount += FixPermission(doc, ns, "android.permission.ACCESS_NETWORK_STATE");
            fixCount += FixPermission(doc, ns, "android.permission.WAKE_LOCK");

            // Fix launcher activity
            fixCount += FixLauncherActivity(doc, ns);

            // Remove debuggable if present
            fixCount += FixRemoveDebuggable(doc, ns);

            // Save
            doc.Save(ManifestPath);
            AssetDatabase.Refresh();

            Debug.Log($"[ManifestVerifier] Fixed {fixCount} issues. Backup at: {backupPath}");
            EditorUtility.DisplayDialog("Manifest Fixed",
                $"Fixed {fixCount} issues.\nBackup saved to: {backupPath}", "OK");

            // Re-run verification
            RunVerification();
        }

        // ─── Individual Fix Methods ───

        private int FixToolsNamespace(XmlDocument doc) {
            var manifest = doc.DocumentElement;
            if (manifest == null) return 0;

            var existing = manifest.GetAttribute("xmlns:tools");
            if (!string.IsNullOrEmpty(existing)) return 0;

            manifest.SetAttribute("xmlns:tools", ToolsNs);
            Debug.Log("[ManifestVerifier] Added tools namespace.");
            return 1;
        }

        private int FixMetaData(XmlDocument doc, XmlNamespaceManager ns,
            string name, string expectedValue) {

            var node = doc.SelectSingleNode(
                $"//meta-data[@android:name='{name}']", ns) as XmlElement;

            if (node != null) {
                var currentValue = node.GetAttribute("value", AndroidNs);
                if (currentValue == expectedValue) return 0;

                node.SetAttribute("value", AndroidNs, expectedValue);
                Debug.Log($"[ManifestVerifier] Fixed {name}: {currentValue} → {expectedValue}");
                return 1;
            }

            // Add new meta-data to <application>
            var app = doc.SelectSingleNode("//application") as XmlElement;
            if (app == null) return 0;

            var metaData = doc.CreateElement("meta-data");
            metaData.SetAttribute("name", AndroidNs, name);
            metaData.SetAttribute("value", AndroidNs, expectedValue);

            // Insert at beginning of <application> for visibility
            if (app.FirstChild != null) {
                app.InsertBefore(metaData, app.FirstChild);
            } else {
                app.AppendChild(metaData);
            }

            Debug.Log($"[ManifestVerifier] Added {name} = {expectedValue}");
            return 1;
        }

        private int FixPermission(XmlDocument doc, XmlNamespaceManager ns, string permission) {
            var node = doc.SelectSingleNode(
                $"//uses-permission[@android:name='{permission}']", ns);

            if (node != null) return 0;

            var manifest = doc.DocumentElement;
            if (manifest == null) return 0;

            var permNode = doc.CreateElement("uses-permission");
            permNode.SetAttribute("name", AndroidNs, permission);
            manifest.AppendChild(permNode);

            Debug.Log($"[ManifestVerifier] Added permission: {permission}");
            return 1;
        }

        private int FixLauncherActivity(XmlDocument doc, XmlNamespaceManager ns) {
            var activity = doc.SelectSingleNode(
                "//activity[@android:name='com.unity3d.player.UnityPlayerActivity']", ns) as XmlElement;

            if (activity == null) return 0;

            int fixCount = 0;

            // Fix exported
            var exported = activity.GetAttribute("exported", AndroidNs);
            if (exported != "true") {
                activity.SetAttribute("exported", AndroidNs, "true");
                Debug.Log("[ManifestVerifier] Fixed UnityPlayerActivity: exported=true");
                fixCount++;
            }

            // Fix hardwareAccelerated
            var hwAccel = activity.GetAttribute("hardwareAccelerated", AndroidNs);
            if (hwAccel != "true") {
                activity.SetAttribute("hardwareAccelerated", AndroidNs, "true");
                Debug.Log("[ManifestVerifier] Fixed UnityPlayerActivity: hardwareAccelerated=true");
                fixCount++;
            }

            // Fix tools:node="replace"
            var toolsNode = activity.GetAttribute("node", ToolsNs);
            if (toolsNode != "replace") {
                activity.SetAttribute("node", ToolsNs, "replace");
                Debug.Log("[ManifestVerifier] Fixed UnityPlayerActivity: tools:node=replace");
                fixCount++;
            }

            return fixCount;
        }

        private int FixRemoveDebuggable(XmlDocument doc, XmlNamespaceManager ns) {
            var app = doc.SelectSingleNode("//application") as XmlElement;
            if (app == null) return 0;

            var debuggable = app.GetAttribute("debuggable", AndroidNs);
            if (debuggable != "true") return 0;

            app.RemoveAttribute("debuggable", AndroidNs);
            Debug.Log("[ManifestVerifier] Removed android:debuggable from <application>");
            return 1;
        }

        // ─── Create Minimal Manifest ───

        private void CreateMinimalManifest() {
            var dir = Path.GetDirectoryName(ManifestPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(ManifestPath,
@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android""
          xmlns:tools=""http://schemas.android.com/tools"">
  <application>
    <activity
      android:name=""com.unity3d.player.UnityPlayerActivity""
      android:exported=""true""
      android:configChanges=""mcc|mnc|locale|touchscreen|keyboard|keyboardHidden|navigation|orientation|screenLayout|uiMode|screenSize|smallestScreenSize|fontScale|layoutDirection|density""
      android:launchMode=""singleTask""
      android:screenOrientation=""fullUser""
      android:resizeableActivity=""true""
      android:theme=""@android:style/Theme.NoTitleBar.Fullscreen""
      tools:node=""replace"">
      <intent-filter>
        <action android:name=""android.intent.action.MAIN"" />
        <category android:name=""android.intent.category.LAUNCHER"" />
      </intent-filter>
      <meta-data android:name=""unityplayer.UnityActivity"" android:value=""true"" />
    </activity>
  </application>

  <uses-permission android:name=""android.permission.INTERNET"" />
</manifest>");

            Debug.Log($"[ManifestVerifier] Created minimal manifest at: {ManifestPath}");
            AssetDatabase.Refresh();
        }

        // ─── Verify Helpers ───

        private void VerifyMetaData(XmlDocument doc, XmlNamespaceManager ns,
            string section, string name, string expectedValue, string failReason) {

            var value = GetMetaDataValue(doc, ns, name);
            if (value == null) {
                AddResult(section, ResultStatus.Fail,
                    $"<b>{name}</b> — MISSING",
                    $"Add: <meta-data android:name=\"{name}\" android:value=\"{expectedValue}\" />\n{failReason}");
            } else if (value == expectedValue) {
                AddResult(section, ResultStatus.Pass,
                    $"{name} = \"{value}\"");
            } else {
                AddResult(section, ResultStatus.Fail,
                    $"<b>{name}</b> = \"{value}\" (expected \"{expectedValue}\")",
                    failReason);
            }
        }

        private void VerifyPermission(XmlDocument doc, XmlNamespaceManager ns,
            string section, string permission, bool shouldExist, string reason) {

            var node = doc.SelectSingleNode(
                $"//uses-permission[@android:name='{permission}']", ns);

            if (shouldExist) {
                if (node != null) {
                    AddResult(section, ResultStatus.Pass, permission);
                } else {
                    AddResult(section, ResultStatus.Fail,
                        $"<b>{permission}</b> — MISSING", reason);
                }
            }
        }

        private void VerifyPermissionAbsent(XmlDocument doc, XmlNamespaceManager ns,
            string section, string permission, string reason) {

            var node = doc.SelectSingleNode(
                $"//uses-permission[@android:name='{permission}']", ns);

            if (node != null) {
                AddResult(section, ResultStatus.Warn,
                    $"{permission} — present (should be removed?)", reason);
            }
        }

        private string GetMetaDataValue(XmlDocument doc, XmlNamespaceManager ns, string name) {
            var node = doc.SelectSingleNode(
                $"//meta-data[@android:name='{name}']", ns) as XmlElement;
            return node?.GetAttribute("value", AndroidNs);
        }

        private void AddResult(string section, ResultStatus status, string label, string detail = null) {
            _results.Add(new VerifyResult(section, status, label, detail));
            switch (status) {
                case ResultStatus.Pass: _passCount++; break;
                case ResultStatus.Fail: _failCount++; break;
                case ResultStatus.Warn: _warnCount++; break;
            }
        }

        private static string MaskValue(string value) {
            if (string.IsNullOrEmpty(value)) return "(empty)";
            if (value.Length <= 6) return value.Substring(0, 2) + "***";
            return value.Substring(0, 4) + "***" + value.Substring(value.Length - 4);
        }

        // ─── Data ───

        private enum ResultStatus { Pass, Fail, Warn }

        private readonly struct VerifyResult {
            public string Section { get; }
            public ResultStatus Status { get; }
            public string Label { get; }
            public string Detail { get; }

            public VerifyResult(string section, ResultStatus status, string label, string detail) {
                Section = section;
                Status = status;
                Label = label;
                Detail = detail;
            }
        }
    }
}
