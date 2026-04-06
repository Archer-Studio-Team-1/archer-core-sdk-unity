using ArcherStudio.SDK.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace ArcherStudio.SDK.TestLab.Editor {

    /// <summary>
    /// Build processor that automatically configures Android manifest and iOS plist
    /// for Firebase Test Lab Game Loop support.
    /// </summary>
    public class TestLabBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report) {
            var coreConfig = Resources.Load<SDKCoreConfig>("SDKCoreConfig");
            if (coreConfig != null && !coreConfig.EnableTestLab) return;

            var config = Resources.Load<TestLabConfig>("TestLabConfig");
            if (config == null || !config.Enabled) return;

#if UNITY_ANDROID
            ConfigureAndroidManifest(config);
#endif
        }

        public void OnPostprocessBuild(BuildReport report) {
            var coreConfig = Resources.Load<SDKCoreConfig>("SDKCoreConfig");
            if (coreConfig != null && !coreConfig.EnableTestLab) return;

            var config = Resources.Load<TestLabConfig>("TestLabConfig");
            if (config == null || !config.Enabled) return;

#if UNITY_IOS
            ConfigureIosPlist(report.summary.outputPath, config);
#endif
        }

#if UNITY_ANDROID
        private static void ConfigureAndroidManifest(TestLabConfig config) {
            const string manifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

            if (!System.IO.File.Exists(manifestPath)) {
                Debug.LogWarning("[TestLab] AndroidManifest.xml not found at expected path. " +
                    "Add the Game Loop intent-filter manually or create AndroidManifest.xml at " + manifestPath);
                return;
            }

            string content = System.IO.File.ReadAllText(manifestPath);
            if (content.Contains("com.google.intent.action.TEST_LOOP")) {
                Debug.Log("[TestLab] AndroidManifest already contains Game Loop intent-filter");
                return;
            }

            // Insert intent-filter before closing </activity> tag
            const string intentFilter =
                "\n            <!-- Firebase Test Lab Game Loop -->\n" +
                "            <intent-filter>\n" +
                "                <action android:name=\"com.google.intent.action.TEST_LOOP\" />\n" +
                "                <category android:name=\"android.intent.category.DEFAULT\" />\n" +
                "                <data android:mimeType=\"application/javascript\" />\n" +
                "            </intent-filter>\n";

            // Add scenario metadata if we have scenarios
            string metadata = "";
            if (config.Scenarios.Count > 0) {
                metadata = $"            <meta-data android:name=\"com.google.test.loops\" android:value=\"{config.Scenarios.Count}\" />\n";
            }

            int insertIndex = content.IndexOf("</activity>", System.StringComparison.Ordinal);
            if (insertIndex < 0) {
                Debug.LogWarning("[TestLab] Could not find </activity> in AndroidManifest.xml. Add intent-filter manually.");
                return;
            }

            content = content.Insert(insertIndex, intentFilter + metadata);
            System.IO.File.WriteAllText(manifestPath, content);
            Debug.Log("[TestLab] Added Game Loop intent-filter to AndroidManifest.xml");
            AssetDatabase.Refresh();
        }
#endif

#if UNITY_IOS
        private static void ConfigureIosPlist(string buildPath, TestLabConfig config) {
            string plistPath = System.IO.Path.Combine(buildPath, "Info.plist");
            if (!System.IO.File.Exists(plistPath)) {
                Debug.LogWarning("[TestLab] Info.plist not found at: " + plistPath);
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            // Add URL scheme: firebase-game-loop
            var urlTypes = root["CFBundleURLTypes"]?.AsArray() ?? root.CreateArray("CFBundleURLTypes");
            bool hasScheme = false;
            foreach (var urlType in urlTypes.values) {
                var dict = urlType.AsDict();
                var schemes = dict?["CFBundleURLSchemes"]?.AsArray();
                if (schemes == null) continue;
                foreach (var scheme in schemes.values) {
                    if (scheme.AsString() == "firebase-game-loop") {
                        hasScheme = true;
                        break;
                    }
                }
                if (hasScheme) break;
            }

            if (!hasScheme) {
                var newUrlType = urlTypes.AddDict();
                newUrlType.SetString("CFBundleURLName", "com.google.firebase.testlab");
                var schemes = newUrlType.CreateArray("CFBundleURLSchemes");
                schemes.AddString("firebase-game-loop");
                Debug.Log("[TestLab] Added firebase-game-loop URL scheme to Info.plist");
            }

            // Add scenario count
            if (config.Scenarios.Count > 0) {
                root.SetInteger("FTLGameLoopScenarios", config.Scenarios.Count);
            }

            plist.WriteToFile(plistPath);
        }
#endif
    }
}
