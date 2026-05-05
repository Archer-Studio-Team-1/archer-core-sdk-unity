using System.IO;
using ArcherStudio.SDK.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Creates a starter SDKLoadingOverlay prefab in Assets/Resources/.
    /// Project owns the visual design — customize freely after creation.
    /// SDK only uses: Canvas (show/hide), SpinnerTransform (auto-rotate).
    /// </summary>
    public static class SDKLoadingOverlayPrefabCreator {

        private const string ResourcesPath = "Assets/Resources";
        private const string PrefabName = "SDKLoadingOverlay";

        [MenuItem("ArcherStudio/SDK/Create Loading Overlay Prefab", false, 30)]
        public static void CreateOrSelect() {
            string prefabPath = $"{ResourcesPath}/{PrefabName}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            EnsureDirectory(ResourcesPath);

            var root = BuildHierarchy();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var created = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);

            Debug.Log($"[SDK] Created Loading Overlay prefab at {prefabPath}. Customize visuals in the prefab.");
        }

        private static GameObject BuildHierarchy() {
            var root = new GameObject(PrefabName);
            var overlay = root.AddComponent<SDKLoadingOverlay>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // Blocker
            var blockerGo = CreateChild("Blocker", root.transform);
            var blockerImg = blockerGo.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.5f);
            blockerImg.raycastTarget = true;
            StretchFull(blockerGo);

            // Spinner placeholder — project replaces with their own visual
            var spinnerGo = CreateChild("Spinner", root.transform);
            var spinnerImg = spinnerGo.AddComponent<Image>();
            spinnerImg.raycastTarget = false;
            spinnerImg.color = Color.white;
            var spinnerRect = spinnerGo.GetComponent<RectTransform>();
            spinnerRect.anchoredPosition = Vector2.zero;
            spinnerRect.sizeDelta = new Vector2(80, 80);

            // Wire references
            var so = new SerializedObject(overlay);
            so.FindProperty("_canvas").objectReferenceValue = canvas;
            so.FindProperty("_spinnerTransform").objectReferenceValue = spinnerRect;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject CreateChild(string name, Transform parent) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(GameObject go) {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureDirectory(string path) {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder)) {
                if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectory(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
