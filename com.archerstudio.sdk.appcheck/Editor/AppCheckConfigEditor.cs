using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArcherStudio.SDK.AppCheck.Editor {

    [CustomEditor(typeof(AppCheckConfig))]
    public class AppCheckConfigEditor : UnityEditor.Editor {

        private const string ResourcesPath = "Assets/Resources";

        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "App Check behavior by build type:\n\n" +
                "PRODUCTION build:\n" +
                "  Play Integrity (Android) / DeviceCheck (iOS)\n" +
                "  Real attestation — blocks modified APKs\n\n" +
                "Dev build (non-PRODUCTION):\n" +
                "  UseDebugProviderInDev=true → Firebase Debug Provider\n" +
                "  UseDebugProviderInDev=false → Disabled (stub)\n\n" +
                "Editor:\n" +
                "  Always disabled — IAP works without attestation",
                MessageType.Info);

            EditorGUILayout.Space(4);
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            #if HAS_FIREBASE_APP_CHECK
            EditorGUILayout.HelpBox("Firebase App Check SDK detected.", MessageType.None);
            #else
            EditorGUILayout.HelpBox(
                "Firebase App Check SDK NOT detected.\n" +
                "Install com.google.firebase.app-check package.\n" +
                "Stub provider will be used until then.",
                MessageType.Warning);
            #endif

            #if PRODUCTION
            EditorGUILayout.HelpBox("PRODUCTION symbol defined. Real attestation will be used.", MessageType.None);
            #endif

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("ArcherStudio/SDK/App Check Config", false, 25)]
        public static void CreateOrSelectConfig() {
            string path = $"{ResourcesPath}/AppCheckConfig.asset";

            var existing = AssetDatabase.LoadAssetAtPath<AppCheckConfig>(path);
            if (existing != null) {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesPath)) {
                string parent = Path.GetDirectoryName(ResourcesPath)?.Replace('\\', '/') ?? "Assets";
                string folder = Path.GetFileName(ResourcesPath);
                AssetDatabase.CreateFolder(parent, folder);
            }

            var asset = CreateInstance<AppCheckConfig>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
