using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking.Editor {

    /// <summary>
    /// Editor window to manage optional Adjust SDK plugin dependencies.
    /// Generates AdjustTrackingDependencies.xml for EDM4U resolution.
    ///
    /// Menu: ArcherStudio > SDK > Adjust Dependencies
    /// </summary>
    public class AdjustDependencyManagerWindow : EditorWindow {

        // ─── Prefs Keys ───
        private const string PrefsPrefix = "ArcherSDK_AdjustDep_";

        // ─── Dependency File ───
        private static readonly string XmlDir =
            "Packages/com.archerstudio.sdk.tracking/Editor";
        private static readonly string XmlPath =
            XmlDir + "/AdjustTrackingDependencies.xml";

        // ─── Maven Repositories ───
        private const string RepoMavenCentral = "https://repo1.maven.org/maven2/";
        private const string RepoHuawei = "https://developer.huawei.com/repo/";

        // ─── Default Versions ───
        private const string DefaultAdjustPluginVersion = "5.5.1";
        private const string DefaultHuaweiHmsVersion = "3.4.62.300";
        private const string DefaultGoogleOdmSdkVersion = "3.0.0";
        private const string DefaultAdjustOdmPodVersion = "5.5.3";
        private const string DefaultOdmMinTarget = "12.0";

        // ─── Toggle State ───
        private bool _enableMetaReferrer;
        private bool _enableOaid;
        private bool _enableHuaweiHms;
        private bool _enableGoogleLvl;
        private bool _enableGoogleOdm;

        // ─── Versions ───
        private string _metaReferrerVersion;
        private string _oaidVersion;
        private string _huaweiHmsVersion;
        private string _googleLvlVersion;
        private string _adjustOdmPodVersion;
        private string _googleOdmSdkVersion;
        private string _odmMinTarget;

        // ─── UI ───
        private Vector2 _scrollPos;
        private bool _showAdvanced;
        private bool _showValidation;
        private bool _isDirty;

        [MenuItem("ArcherStudio/SDK/Adjust Dependencies", false, 30)]
        public static void ShowWindow() {
            var window = GetWindow<AdjustDependencyManagerWindow>("Adjust Dependencies");
            window.minSize = new Vector2(480, 520);
            window.Show();
        }

        private void OnEnable() {
            LoadPrefs();
            DetectCurrentState();
        }

        private void OnGUI() {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            EditorGUILayout.Space(8);
            DrawAndroidSection();
            EditorGUILayout.Space(8);
            DrawIOSSection();
            EditorGUILayout.Space(8);
            DrawVersionSection();
            EditorGUILayout.Space(8);
            DrawValidationSection();
            EditorGUILayout.Space(12);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        // ══════════════════════════════════════════════════════════════
        //  HEADER
        // ══════════════════════════════════════════════════════════════

        private void DrawHeader() {
            EditorGUILayout.LabelField("Adjust SDK — Optional Dependencies",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Toggle optional Adjust plugins below, then click Apply to " +
                "regenerate the EDM4U dependency file.\n" +
                "After applying, run Assets > External Dependency Manager > " +
                "Android Resolver > Force Resolve.",
                MessageType.Info);
        }

        // ══════════════════════════════════════════════════════════════
        //  ANDROID
        // ══════════════════════════════════════════════════════════════

        private void DrawAndroidSection() {
            EditorGUILayout.LabelField("Android Plugins", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope()) {
                
                // Signature Library Info (SDK v5 specific)
                EditorGUILayout.HelpBox("Adjust Signature Library is BUILT-IN to SDK v5. " +
                    "No separate dependency or initialization is required.", MessageType.Info);
                EditorGUILayout.Space(4);

                // Meta Install Referrer
                DrawToggle(ref _enableMetaReferrer,
                    "Meta Install Referrer",
                    "Reads Meta Install Referrer for Adjust attribution.\n" +
                    "Requires TrackingConfig.MetaAppId to be set.");

                // OAID
                DrawToggle(ref _enableOaid,
                    "OAID Plugin (Chinese devices)",
                    "Reads OAID on devices without Google Play Services.\n" +
                    "For Huawei, Xiaomi, OPPO, etc.\n" +
                    "Requires TrackingConfig.EnableOaid = true.");

                // Huawei HMS (only relevant if OAID enabled)
                using (new EditorGUI.DisabledScope(!_enableOaid)) {
                    DrawToggle(ref _enableHuaweiHms,
                        "  + Huawei HMS Ads Identifier",
                        "Adds HMS Core for OAID on Huawei devices.\n" +
                        "Only needed when targeting Huawei AppGallery.");
                }

                // Google LVL
                DrawToggle(ref _enableGoogleLvl,
                    "Google Play License Verification (LVL)",
                    "Verifies app installed from Google Play (anti-sideload).\n" +
                    "Zero config — works automatically when added.");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  iOS
        // ══════════════════════════════════════════════════════════════

        private void DrawIOSSection() {
            EditorGUILayout.LabelField("iOS Plugins", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope()) {
                DrawToggle(ref _enableGoogleOdm,
                    "Google On-device Conversion (ODM)",
                    "Attribution for ads on Google iOS apps → app install.\n" +
                    "Note: Firebase Analytics 11.14.0+ already bundles this.\n" +
                    "Only add if NOT using Firebase Analytics.");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  VERSIONS (Advanced)
        // ══════════════════════════════════════════════════════════════

        private void DrawVersionSection() {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced,
                "Version Overrides", true, EditorStyles.foldoutHeader);

            if (!_showAdvanced) return;

            using (new EditorGUI.IndentLevelScope()) {
                EditorGUILayout.HelpBox(
                    "Override dependency versions if you need to pin a specific " +
                    "release. Leave defaults unless you have a reason to change.",
                    MessageType.None);

                _metaReferrerVersion = VersionField("Meta Referrer",
                    _metaReferrerVersion, DefaultAdjustPluginVersion);
                _oaidVersion = VersionField("OAID Plugin",
                    _oaidVersion, DefaultAdjustPluginVersion);
                _huaweiHmsVersion = VersionField("Huawei HMS",
                    _huaweiHmsVersion, DefaultHuaweiHmsVersion);
                _googleLvlVersion = VersionField("Google LVL",
                    _googleLvlVersion, DefaultAdjustPluginVersion);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("iOS", EditorStyles.miniLabel);
                _adjustOdmPodVersion = VersionField("Adjust ODM Pod",
                    _adjustOdmPodVersion, DefaultAdjustOdmPodVersion);
                _googleOdmSdkVersion = VersionField("GoogleAdsOnDeviceConversion",
                    _googleOdmSdkVersion, DefaultGoogleOdmSdkVersion);
                _odmMinTarget = VersionField("Min iOS Target",
                    _odmMinTarget, DefaultOdmMinTarget);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  VALIDATION — Check resolved libraries in project
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Library descriptors for build validation.
        /// Each entry maps a toggle to its expected resolved file patterns.
        /// </summary>
        private static readonly LibraryCheck[] LibraryChecks = {
            new LibraryCheck(
                "Adjust OAID Plugin",
                "adjust-android-oaid",
                new[] { "adjust-android-oaid" },
                "com.adjust.sdk:adjust-android-oaid"),
            new LibraryCheck(
                "Huawei HMS Ads Identifier",
                "huawei-hms",
                new[] { "ads-identifier", "hms" },
                "com.huawei.hms:ads-identifier"),
            new LibraryCheck(
                "Meta Install Referrer",
                "meta-referrer",
                new[] { "adjust-android-meta-referrer" },
                "com.adjust.sdk:adjust-android-meta-referrer"),
            new LibraryCheck(
                "Google Play LVL",
                "google-lvl",
                new[] { "adjust-android-google-lvl" },
                "com.adjust.sdk:adjust-android-google-lvl"),
        };

        private struct LibraryCheck {
            public readonly string DisplayName;
            public readonly string Key;
            public readonly string[] FilePatterns;
            public readonly string MavenSpec;

            public LibraryCheck(string displayName, string key,
                string[] filePatterns, string mavenSpec) {
                DisplayName = displayName;
                Key = key;
                FilePatterns = filePatterns;
                MavenSpec = mavenSpec;
            }
        }

        private void DrawValidationSection() {
            _showValidation = EditorGUILayout.Foldout(_showValidation,
                "Build Validation", true, EditorStyles.foldoutHeader);

            if (!_showValidation) return;

            EditorGUILayout.HelpBox(
                "Scans Assets/Plugins/Android/ for resolved libraries.\n" +
                "Run EDM4U Force Resolve first, then check here.",
                MessageType.None);

            using (new EditorGUI.IndentLevelScope()) {
                foreach (var check in LibraryChecks) {
                    bool isEnabled = IsLibraryToggleEnabled(check.Key);
                    var result = ScanForLibrary(check.FilePatterns);

                    EditorGUILayout.BeginHorizontal();

                    // Status icon
                    string icon;
                    string statusText;
                    if (result.Found) {
                        icon = "●";
                        statusText = $"FOUND: {result.FileName}";
                    } else if (!isEnabled) {
                        icon = "○";
                        statusText = "Not enabled";
                    } else {
                        icon = "✖";
                        statusText = "MISSING — Run Force Resolve";
                    }

                    // Color
                    var prevColor = GUI.contentColor;
                    if (result.Found) {
                        GUI.contentColor = new Color(0.3f, 0.85f, 0.3f);
                    } else if (isEnabled) {
                        GUI.contentColor = new Color(0.9f, 0.3f, 0.3f);
                    } else {
                        GUI.contentColor = new Color(0.6f, 0.6f, 0.6f);
                    }

                    EditorGUILayout.LabelField($"{icon} {check.DisplayName}",
                        GUILayout.Width(240));
                    EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);

                    GUI.contentColor = prevColor;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Refresh Scan", GUILayout.Width(120))) {
                    Repaint();
                }
            }
        }

        private bool IsLibraryToggleEnabled(string key) {
            switch (key) {
                case "adjust-android-oaid": return _enableOaid;
                case "huawei-hms": return _enableHuaweiHms && _enableOaid;
                case "meta-referrer": return _enableMetaReferrer;
                case "google-lvl": return _enableGoogleLvl;
                default: return false;
            }
        }

        private struct ScanResult {
            public bool Found;
            public string FileName;
        }

        /// <summary>
        /// Scan Assets/Plugins/Android/ for AAR/JAR files matching any of the patterns.
        /// EDM4U resolves Maven dependencies into this folder.
        /// </summary>
        private static ScanResult ScanForLibrary(string[] patterns) {
            var result = new ScanResult { Found = false, FileName = null };

            string[] searchDirs = {
                "Assets/Plugins/Android",
                "Assets/Plugins/Android/libs",
            };

            foreach (var dir in searchDirs) {
                if (!Directory.Exists(dir)) continue;

                var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                foreach (var file in files) {
                    string fileName = Path.GetFileName(file).ToLowerInvariant();

                    // Skip .meta files
                    if (fileName.EndsWith(".meta")) continue;

                    // Only check AAR and JAR
                    if (!fileName.EndsWith(".aar") && !fileName.EndsWith(".jar")) continue;

                    foreach (var pattern in patterns) {
                        if (fileName.Contains(pattern.ToLowerInvariant())) {
                            result.Found = true;
                            result.FileName = Path.GetFileName(file);
                            return result;
                        }
                    }
                }
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════
        //  ACTIONS
        // ══════════════════════════════════════════════════════════════

        private void DrawActions() {
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
            if (GUILayout.Button("Apply & Generate XML", GUILayout.Height(32))) {
                GenerateXml();
                SavePrefs();
                _isDirty = false;
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Reset to Defaults", GUILayout.Height(32),
                    GUILayout.Width(140))) {
                if (EditorUtility.DisplayDialog("Reset",
                        "Reset all toggles to defaults (all disabled)?", "Reset", "Cancel")) {
                    ResetDefaults();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Open Dependencies.xml", GUILayout.Height(24))) {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(XmlPath);
                if (asset != null) {
                    AssetDatabase.OpenAsset(asset);
                } else {
                    EditorUtility.DisplayDialog("Not Found",
                        $"File not found at:\n{XmlPath}\n\nClick 'Apply' to generate it.",
                        "OK");
                }
            }

            if (GUILayout.Button("Force Resolve (EDM4U)", GUILayout.Height(24))) {
                TriggerEdm4uResolve();
            }

            EditorGUILayout.EndHorizontal();

            // Status
            EditorGUILayout.Space(4);
            bool xmlExists = File.Exists(XmlPath);
            string status = xmlExists
                ? $"XML: {XmlPath}"
                : "XML not generated yet. Click Apply.";
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
        }

        // ══════════════════════════════════════════════════════════════
        //  XML GENERATION
        // ══════════════════════════════════════════════════════════════

        private void GenerateXml() {
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<!--");
            sb.AppendLine("  EDM4U Dependencies for Adjust plugins in ArcherStudio SDK Tracking.");
            sb.AppendLine("  Auto-generated by AdjustDependencyManagerWindow.");
            sb.AppendLine("  Do not edit manually — use ArcherStudio > SDK > Adjust Dependencies.");
            sb.AppendLine("-->");
            sb.AppendLine("<dependencies>");
            sb.AppendLine();
            sb.AppendLine("  <androidPackages>");

            // Meta Install Referrer
            if (_enableMetaReferrer) {
                sb.AppendLine();
                sb.AppendLine("    <!-- Meta Install Referrer Plugin -->");
                AppendAndroidPackage(sb,
                    $"com.adjust.sdk:adjust-android-meta-referrer:{_metaReferrerVersion}",
                    RepoMavenCentral);
            }

            // OAID
            if (_enableOaid) {
                sb.AppendLine();
                sb.AppendLine("    <!-- OAID Plugin (Chinese devices) -->");
                AppendAndroidPackage(sb,
                    $"com.adjust.sdk:adjust-android-oaid:{_oaidVersion}",
                    RepoMavenCentral);

                // Huawei HMS
                if (_enableHuaweiHms) {
                    sb.AppendLine();
                    sb.AppendLine("    <!-- Huawei HMS Ads Identifier (for OAID on Huawei) -->");
                    AppendAndroidPackage(sb,
                        $"com.huawei.hms:ads-identifier:{_huaweiHmsVersion}",
                        RepoHuawei);
                }
            }

            // Google LVL
            if (_enableGoogleLvl) {
                sb.AppendLine();
                sb.AppendLine("    <!-- Google Play License Verification Library -->");
                AppendAndroidPackage(sb,
                    $"com.adjust.sdk:adjust-android-google-lvl:{_googleLvlVersion}",
                    RepoMavenCentral);
            }

            sb.AppendLine();
            sb.AppendLine("  </androidPackages>");
            sb.AppendLine();
            sb.AppendLine("  <iosPods>");

            // Google ODM
            if (_enableGoogleOdm) {
                sb.AppendLine();
                sb.AppendLine("    <!-- Google On-device Conversion Measurement -->");
                sb.AppendLine(
                    $"    <iosPod name=\"Adjust/AdjustGoogleOdm\" " +
                    $"version=\"{_adjustOdmPodVersion}\" " +
                    $"minTargetSdk=\"{_odmMinTarget}\"/>");
                sb.AppendLine(
                    $"    <iosPod name=\"GoogleAdsOnDeviceConversion\" " +
                    $"version=\"{_googleOdmSdkVersion}\" " +
                    $"minTargetSdk=\"{_odmMinTarget}\"/>");
            }

            sb.AppendLine();
            sb.AppendLine("  </iosPods>");
            sb.AppendLine();
            sb.AppendLine("</dependencies>");

            // Ensure directory exists
            if (!Directory.Exists(XmlDir)) {
                Directory.CreateDirectory(XmlDir);
            }

            File.WriteAllText(XmlPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();

            int count = CountEnabled();
            Debug.Log($"[ArcherSDK] Adjust dependencies generated: {count} plugin(s) enabled → {XmlPath}");
            EditorUtility.DisplayDialog("Adjust Dependencies",
                $"Generated {XmlPath}\n\n" +
                $"{count} plugin(s) enabled.\n\n" +
                "Run 'Assets > External Dependency Manager > Android Resolver > Force Resolve' " +
                "to download the dependencies.",
                "OK");
        }

        private static void AppendAndroidPackage(StringBuilder sb, string spec, string repo) {
            sb.AppendLine($"    <androidPackage spec=\"{spec}\">");
            sb.AppendLine("      <repositories>");
            sb.AppendLine($"        <repository>{repo}</repository>");
            sb.AppendLine("      </repositories>");
            sb.AppendLine("    </androidPackage>");
        }

        // ══════════════════════════════════════════════════════════════
        //  DETECT CURRENT STATE FROM XML
        // ══════════════════════════════════════════════════════════════

        private void DetectCurrentState() {
            if (!File.Exists(XmlPath)) return;

            string xml = File.ReadAllText(XmlPath);

            // Detect which dependencies are currently uncommented (active)
            _enableMetaReferrer = IsPackageActive(xml, "adjust-android-meta-referrer");
            _enableOaid = IsPackageActive(xml, "adjust-android-oaid");
            _enableHuaweiHms = IsPackageActive(xml, "com.huawei.hms:ads-identifier");
            _enableGoogleLvl = IsPackageActive(xml, "adjust-android-google-lvl");
            _enableGoogleOdm = IsPackageActive(xml, "AdjustGoogleOdm");
        }

        private static bool IsPackageActive(string xml, string specFragment) {
            int idx = 0;
            while (true) {
                idx = xml.IndexOf(specFragment, idx, System.StringComparison.Ordinal);
                if (idx < 0) return false;

                bool insideComment = false;
                int searchBack = idx;
                while (searchBack >= 0) {
                    int commentEnd = xml.LastIndexOf("-->", searchBack, System.StringComparison.Ordinal);
                    int commentStart = xml.LastIndexOf("<!--", searchBack, System.StringComparison.Ordinal);
                    if (commentStart < 0) break;
                    if (commentEnd >= 0 && commentEnd > commentStart) break;
                    insideComment = true;
                    break;
                }
                if (!insideComment) return true;
                idx += specFragment.Length;
            }
        }

        private static void TriggerEdm4uResolve() {
            var resolverType = System.Type.GetType("GooglePlayServices.PlayServicesResolver, Google.JarResolver.Editor") 
                ?? System.Type.GetType("Google.AndroidResolverInternal, Google.AndroidResolver.Editor");

            if (resolverType != null) {
                var method = resolverType.GetMethod("MenuForceResolve", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null) { method.Invoke(null, null); return; }
            }

            EditorUtility.DisplayDialog("EDM4U", "Could not find External Dependency Manager resolver.", "OK");
        }

        private void SavePrefs() {
            EditorPrefs.SetBool(PrefsPrefix + "MetaReferrer", _enableMetaReferrer);
            EditorPrefs.SetBool(PrefsPrefix + "Oaid", _enableOaid);
            EditorPrefs.SetBool(PrefsPrefix + "HuaweiHms", _enableHuaweiHms);
            EditorPrefs.SetBool(PrefsPrefix + "GoogleLvl", _enableGoogleLvl);
            EditorPrefs.SetBool(PrefsPrefix + "GoogleOdm", _enableGoogleOdm);

            EditorPrefs.SetString(PrefsPrefix + "MetaReferrerVer", _metaReferrerVersion);
            EditorPrefs.SetString(PrefsPrefix + "OaidVer", _oaidVersion);
            EditorPrefs.SetString(PrefsPrefix + "HuaweiHmsVer", _huaweiHmsVersion);
            EditorPrefs.SetString(PrefsPrefix + "GoogleLvlVer", _googleLvlVersion);
            EditorPrefs.SetString(PrefsPrefix + "AdjustOdmPodVer", _adjustOdmPodVersion);
            EditorPrefs.SetString(PrefsPrefix + "GoogleOdmSdkVer", _googleOdmSdkVersion);
            EditorPrefs.SetString(PrefsPrefix + "OdmMinTarget", _odmMinTarget);
        }

        private void LoadPrefs() {
            _enableMetaReferrer = EditorPrefs.GetBool(PrefsPrefix + "MetaReferrer", false);
            _enableOaid = EditorPrefs.GetBool(PrefsPrefix + "Oaid", false);
            _enableHuaweiHms = EditorPrefs.GetBool(PrefsPrefix + "HuaweiHms", false);
            _enableGoogleLvl = EditorPrefs.GetBool(PrefsPrefix + "GoogleLvl", false);
            _enableGoogleOdm = EditorPrefs.GetBool(PrefsPrefix + "GoogleOdm", false);

            _metaReferrerVersion = EditorPrefs.GetString(PrefsPrefix + "MetaReferrerVer", DefaultAdjustPluginVersion);
            _oaidVersion = EditorPrefs.GetString(PrefsPrefix + "OaidVer", DefaultAdjustPluginVersion);
            _huaweiHmsVersion = EditorPrefs.GetString(PrefsPrefix + "HuaweiHmsVer", DefaultHuaweiHmsVersion);
            _googleLvlVersion = EditorPrefs.GetString(PrefsPrefix + "GoogleLvlVer", DefaultAdjustPluginVersion);
            _adjustOdmPodVersion = EditorPrefs.GetString(PrefsPrefix + "AdjustOdmPodVer", DefaultAdjustOdmPodVersion);
            _googleOdmSdkVersion = EditorPrefs.GetString(PrefsPrefix + "GoogleOdmSdkVer", DefaultGoogleOdmSdkVersion);
            _odmMinTarget = EditorPrefs.GetString(PrefsPrefix + "OdmMinTarget", DefaultOdmMinTarget);
        }

        private void ResetDefaults() {
            _enableMetaReferrer = _enableOaid = _enableHuaweiHms = _enableGoogleLvl = _enableGoogleOdm = false;
            _metaReferrerVersion = _oaidVersion = _googleLvlVersion = DefaultAdjustPluginVersion;
            _huaweiHmsVersion = DefaultHuaweiHmsVersion;
            _adjustOdmPodVersion = DefaultAdjustOdmPodVersion;
            _googleOdmSdkVersion = DefaultGoogleOdmSdkVersion;
            _odmMinTarget = DefaultOdmMinTarget;
            SavePrefs(); Repaint();
        }

        private void DrawToggle(ref bool value, string label, string tooltip) {
            EditorGUILayout.BeginHorizontal();
            bool prev = value;
            value = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), value);
            if (prev != value) _isDirty = true;
            EditorGUILayout.EndHorizontal();
        }

        private static string VersionField(string label, string current, string defaultVal) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(200));
            string result = EditorGUILayout.TextField(current);
            if (GUILayout.Button("↺", GUILayout.Width(24))) { result = defaultVal; GUI.FocusControl(null); }
            EditorGUILayout.EndHorizontal();
            return result;
        }

        private int CountEnabled() {
            int count = 0;
            if (_enableMetaReferrer) count++;
            if (_enableOaid) count++;
            if (_enableHuaweiHms && _enableOaid) count++;
            if (_enableGoogleLvl) count++;
            if (_enableGoogleOdm) count++;
            return count;
        }
    }
}
