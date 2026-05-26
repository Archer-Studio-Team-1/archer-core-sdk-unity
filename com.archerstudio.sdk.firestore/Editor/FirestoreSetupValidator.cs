using UnityEditor;
using UnityEngine;
using ArcherStudio.SDK.Firestore;

namespace ArcherStudio.SDK.Firestore.Editor {

    public static class FirestoreSetupValidator {

        private const string Tag = "Firestore";

        [MenuItem("ArcherStudio/SDK/Firestore/Validate Setup")]
        private static void Validate() {
            var cfg = Resources.Load<FirestoreConfig>("FirestoreConfig");
            if (cfg == null) {
                EditorUtility.DisplayDialog(
                    "Firestore Setup",
                    "FirestoreConfig.asset not found in any Resources/ folder.\n\n" +
                    "Create: right-click in Resources/ → Create → ArcherStudio → SDK → Firestore Config",
                    "OK");
                return;
            }

            var ok = true;
            var messages = new System.Text.StringBuilder();
            messages.AppendLine($"Config asset: {AssetDatabase.GetAssetPath(cfg)}");

            if (string.IsNullOrEmpty(cfg.WebClientId)) {
                messages.AppendLine("⚠ WebClientId is empty. Firebase Auth via Play Games will fall back to anonymous.");
                ok = false;
            } else {
                messages.AppendLine($"✓ WebClientId set ({cfg.WebClientId.Length} chars)");
            }

            if (string.IsNullOrEmpty(cfg.FunctionsRegion)) {
                messages.AppendLine("⚠ FunctionsRegion empty. Defaulting to asia-southeast1.");
            } else {
                messages.AppendLine($"✓ FunctionsRegion = {cfg.FunctionsRegion}");
            }

            messages.AppendLine($"OfflinePersistence = {cfg.EnableOfflinePersistence}");
            messages.AppendLine($"IapCatalogCacheTtlMs = {cfg.IapCatalogCacheTtlMs}");
            messages.AppendLine($"FeatureRegistryCacheTtlMs = {cfg.FeatureRegistryCacheTtlMs}");

            EditorUtility.DisplayDialog(ok ? "Firestore Setup ✓" : "Firestore Setup — Warnings",
                messages.ToString(), "OK");
        }
    }
}
