using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArcherStudio.SDK.Login.Editor {

    [CustomEditor(typeof(LoginConfig))]
    public class LoginConfigEditor : UnityEditor.Editor {

        private const string ResourcesPath = "Assets/Resources";
        private SerializedProperty _androidClientId;

        private void OnEnable() {
            _androidClientId = serializedObject.FindProperty("AndroidClientId");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Android Client ID: lấy từ Google Play Console > Games > Setup > Linked apps > Web application client ID.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Android / Google Play Games Services", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_androidClientId, new GUIContent("Android Client ID"));

            if (string.IsNullOrEmpty(_androidClientId.stringValue)) {
                EditorGUILayout.HelpBox("Android Client ID chưa được cấu hình. Login sẽ thất bại trên Android.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("ArcherStudio/SDK/Login Config", false, 22)]
        public static void CreateOrSelectLoginConfig() {
            string path = $"{ResourcesPath}/LoginConfig.asset";

            var existing = AssetDatabase.LoadAssetAtPath<LoginConfig>(path);
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

            var asset = CreateInstance<LoginConfig>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
