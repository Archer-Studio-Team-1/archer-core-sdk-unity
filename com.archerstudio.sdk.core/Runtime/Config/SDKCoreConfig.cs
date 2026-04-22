using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Master configuration for the entire SDK.
    /// Create via: Assets > Create > ArcherStudio > SDK > Core Config.
    /// Place in a Resources folder or assign directly to SDKInitializer.
    /// </summary>
    [CreateAssetMenu(fileName = "SDKCoreConfig", menuName = "ArcherStudio/SDK/Core Config")]
    public class SDKCoreConfig : ScriptableObject {

        [Header("General")]
        [Tooltip("Application identifier used across SDK modules.")]
        public string AppId;

        [Tooltip("Enable verbose SDK logging.")]
        public bool DebugMode;

        [Tooltip("Minimum log level for SDK output.")]
        public LogLevel MinLogLevel = LogLevel.Info;

        [Header("Module Toggles")]
        public bool EnableConsent = true;
        public bool EnableLogin = false;
        public bool EnableTracking = true;
        public bool EnableAnalytics = true;
        public bool EnableAds = true;
        public bool EnableIAP = true;
        public bool EnableRemoteConfig = true;
        public bool EnablePush = false;
        public bool EnableDeepLink = false;
        public bool EnableTestLab = false;
        public bool EnableCloudSave = false;
    }
}
