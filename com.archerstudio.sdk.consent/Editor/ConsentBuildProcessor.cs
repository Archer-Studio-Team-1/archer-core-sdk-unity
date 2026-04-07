using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace ArcherStudio.SDK.Consent.Editor {

    /// <summary>
    /// Build processor that adds Google Analytics consent default keys to iOS Info.plist.
    /// Sets all consent defaults to false (denied) so no data is collected before user consent.
    /// Android defaults are set directly in AndroidManifest.xml.
    /// </summary>
    public class ConsentBuildProcessor : IPostprocessBuildWithReport {
        public int callbackOrder => 50;

        public void OnPostprocessBuild(BuildReport report) {
#if UNITY_IOS
            ConfigureIosPlist(report.summary.outputPath);
#endif
        }

#if UNITY_IOS
        private static void ConfigureIosPlist(string buildPath) {
            string plistPath = System.IO.Path.Combine(buildPath, "Info.plist");
            if (!System.IO.File.Exists(plistPath)) {
                Debug.LogWarning("[Consent] Info.plist not found at: " + plistPath);
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            // Set Google Analytics consent defaults to false (denied before user consent)
            SetBoolIfMissing(root, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_STORAGE", false);
            SetBoolIfMissing(root, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_ANALYTICS_STORAGE", false);
            SetBoolIfMissing(root, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA", false);
            SetBoolIfMissing(root, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS", false);

            // iOS ATT: NSUserTrackingUsageDescription (required for ATT prompt)
            SetStringIfMissing(root, "NSUserTrackingUsageDescription",
                "This app uses your data to provide personalized ads and improve your experience.");

            plist.WriteToFile(plistPath);
            Debug.Log("[Consent] iOS Info.plist: consent defaults + ATT description configured");
        }

        private static void SetBoolIfMissing(PlistElementDict root, string key, bool value) {
            if (root[key] == null) {
                root.SetBoolean(key, value);
            }
        }

        private static void SetStringIfMissing(PlistElementDict root, string key, string value) {
            if (root[key] == null) {
                root.SetString(key, value);
            }
        }
#endif
    }
}
