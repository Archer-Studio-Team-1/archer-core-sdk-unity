using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Core.Editor {
    /// <summary>
    /// Quản lý việc hoán đổi môi trường Firebase (google-services.json, GoogleService-Info.plist)
    /// dựa trên SDKCoreConfig và Scripting Define Symbols.
    /// Ưu tiên: Nếu có symbol PRODUCTION -> Buộc dùng cấu hình Production.
    /// </summary>
    public class SDKFirebaseSwitcher : IPreprocessBuildWithReport {
        public const string ConfigSourceDir = "Assets/FirebaseConfigs";
        public const string AndroidConfigName = "google-services.json";
        public const string IosConfigName = "GoogleService-Info.plist";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            SyncWithBuildSettings(report.summary.platformGroup);
        }

        /// <summary>
        /// Đồng bộ cấu hình dựa trên Scripting Define Symbols hiện tại.
        /// Được gọi tự động khi Build hoặc khi Refresh Editor.
        /// </summary>
        public static void SyncWithBuildSettings(BuildTargetGroup group = BuildTargetGroup.Unknown) {
            if (group == BuildTargetGroup.Unknown) {
                group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            }

            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            var symbolList = symbols.Split(';').Select(s => s.Trim()).ToList();

            SDKEnvironment targetEnv;
            
            // 1. Ưu tiên cao nhất: Nếu có symbol PRODUCTION -> Force Production
            if (symbolList.Contains("PRODUCTION")) {
                targetEnv = SDKEnvironment.Production;
            } 
            // 2. Ưu tiên 2: Nếu có symbol DEV -> Force Development
            else if (symbolList.Contains("DEV")) {
                targetEnv = SDKEnvironment.Development;
            }
            // 3. Fallback: Dùng giá trị trong SDKCoreConfig
            else {
                var coreConfig = Resources.Load<SDKCoreConfig>("SDKCoreConfig");
                targetEnv = coreConfig != null ? coreConfig.Environment : SDKEnvironment.Development;
            }

            ApplyEnvironment(targetEnv, group);
        }

        /// <summary>
        /// API để chuyển môi trường từ Setup Wizard hoặc Menu.
        /// </summary>
        public static void SwitchEnvironment(SDKEnvironment env) {
            ApplyEnvironment(env, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            AssetDatabase.Refresh();
        }

        private static void ApplyEnvironment(SDKEnvironment env, BuildTargetGroup group) {
            // Cập nhật Asset config nếu có
            var coreConfig = Resources.Load<SDKCoreConfig>("SDKCoreConfig");
            if (coreConfig != null && coreConfig.Environment != env) {
                coreConfig.Environment = env;
                EditorUtility.SetDirty(coreConfig);
                AssetDatabase.SaveAssets();
            }

            string folder = (env == SDKEnvironment.Production) ? "PROD" : "DEV";
            string symbol = (env == SDKEnvironment.Production) ? "PRODUCTION" : "DEV";

            // 1. Copy file vật lý
            if (CopyConfigFiles(folder)) {
                Debug.Log($"[ArcherSDK] Syncing Firebase config for {env} (Folder: {folder})");
            }

            // 2. Đảm bảo Symbols đồng bộ
            UpdateSymbols(group, symbol);
        }

        private static bool CopyConfigFiles(string folderName) {
            string sourceAndroid = Path.Combine(ConfigSourceDir, folderName, AndroidConfigName);
            string targetAndroid = Path.Combine("Assets", AndroidConfigName);
            bool success = false;

            if (File.Exists(sourceAndroid)) {
                File.Copy(sourceAndroid, targetAndroid, true);
                success = true;
            }

            string sourceIos = Path.Combine(ConfigSourceDir, folderName, IosConfigName);
            string targetIos = Path.Combine("Assets", IosConfigName);
            if (File.Exists(sourceIos)) {
                File.Copy(sourceIos, targetIos, true);
                success = true;
            }

            return success;
        }

        private static void UpdateSymbols(BuildTargetGroup group, string activeSymbol) {
            if (group == BuildTargetGroup.Unknown) return;
            
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            var list = symbols.Split(';').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            // Dọn dẹp tất cả symbol liên quan đến env cũ
            list.Remove("PRODUCTION");
            list.Remove("DEV");
            list.Remove("FIREBASE_PROD");
            list.Remove("FIREBASE_DEV");

            // Thêm symbol mới
            if (!list.Contains(activeSymbol)) {
                list.Add(activeSymbol);
            }

            string newSymbols = string.Join(";", list);
            if (symbols != newSymbols) {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newSymbols);
                Debug.Log($"[ArcherSDK] Updated Scripting Symbols for {group}: {activeSymbol}");
            }
        }
    }
}
