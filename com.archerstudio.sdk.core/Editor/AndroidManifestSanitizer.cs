using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Fixes Unity 6 setting android:enabled="false" on UnityPlayerActivity
    /// in the unityLibrary manifest, which prevents the app from launching.
    ///
    /// Unity 6 splits the build into launcher + unityLibrary modules.
    /// It intentionally sets enabled="false" on the activity in unityLibrary,
    /// but fails to set enabled="true" in the launcher manifest, leaving
    /// the activity disabled in the final APK.
    ///
    /// This script runs AFTER Unity generates the Gradle project but BEFORE
    /// Gradle builds, patching enabled="false" → enabled="true".
    /// </summary>
    public class AndroidManifestPostGenerate : IPostGenerateGradleAndroidProject {
        private const string Tag = "ManifestSanitizer";

        public int callbackOrder => 99;

        public void OnPostGenerateGradleAndroidProject(string path) {
            // path = .../unityLibrary
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) {
                Debug.LogWarning($"[{Tag}] Manifest not found: {manifestPath}");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);

            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("android", "http://schemas.android.com/apk/res/android");

            // Find UnityPlayerActivity
            var activity = doc.SelectSingleNode(
                "//activity[@android:name='com.unity3d.player.UnityPlayerActivity']",
                nsManager) as XmlElement;

            if (activity == null) {
                Debug.LogWarning($"[{Tag}] UnityPlayerActivity not found in manifest.");
                return;
            }

            bool modified = false;

            // Fix enabled="false" → "true"
            var enabled = activity.GetAttribute("enabled",
                "http://schemas.android.com/apk/res/android");
            if (enabled == "false") {
                activity.SetAttribute("enabled",
                    "http://schemas.android.com/apk/res/android", "true");
                modified = true;
            }

            // Fix hardwareAccelerated="false" → "true" (needed for WebView, ads SDKs)
            var hwAccel = activity.GetAttribute("hardwareAccelerated",
                "http://schemas.android.com/apk/res/android");
            if (hwAccel == "false") {
                activity.SetAttribute("hardwareAccelerated",
                    "http://schemas.android.com/apk/res/android", "true");
                modified = true;
            }

            if (modified) {
                doc.Save(manifestPath);
                Debug.Log($"[{Tag}] Fixed UnityPlayerActivity: enabled=true, hardwareAccelerated=true");
            }
        }
    }
}
