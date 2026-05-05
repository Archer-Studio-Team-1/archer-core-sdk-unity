using System;
using UnityEngine;

namespace ArcherStudio.SDK.Core {

    public enum SDKEnvironment {
        Development,
        Production
    }

    /// <summary>
    /// Per-environment security settings for App Check and IAP validation.
    /// </summary>
    [Serializable]
    public class SDKSecurityConfig {
        [Tooltip("Enable App Check / Play Integrity attestation in this environment.")]
        public bool EnableAppCheck = false;

        [Tooltip("Enable server-side IAP receipt validation in this environment. " +
                 "When disabled, purchases are granted immediately without server call.")]
        public bool EnableIAPServerValidation = false;
    }

    /// <summary>
    /// Master configuration for the entire SDK.
    /// Create via: Assets > Create > ArcherStudio > SDK > Core Config.
    /// Place in a Resources folder or assign directly to SDKInitializer.
    /// </summary>
    [CreateAssetMenu(fileName = "SDKCoreConfig", menuName = "ArcherStudio/SDK/Core Config")]
    public class SDKCoreConfig : ScriptableObject {

        [Header("General")]
        [Tooltip("Target environment (affects Firebase config and tracking).")]
        public SDKEnvironment Environment = SDKEnvironment.Development;

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

        [Header("Security — Editor")]
        public SDKSecurityConfig Editor = new SDKSecurityConfig {
            EnableAppCheck = false,
            EnableIAPServerValidation = false,
        };

        [Header("Security — Development Build")]
        public SDKSecurityConfig Dev = new SDKSecurityConfig {
            EnableAppCheck = false,
            EnableIAPServerValidation = false,
        };

        [Header("Security — Production Build")]
        public SDKSecurityConfig Production = new SDKSecurityConfig {
            EnableAppCheck = true,
            EnableIAPServerValidation = true,
        };

        [Header("Loading Overlay")]
        [Tooltip("Show a full-screen loading overlay during SDK async operations (e.g. server receipt validation).")]
        public bool ShowLoadingOverlay = true;

        [Tooltip("Auto-dismiss timeout in seconds. 0 = no timeout (manual dismiss only).")]
        public float LoadingOverlayTimeout = 15f;

        /// <summary>
        /// Returns the security config for the current runtime environment.
        /// Editor → Editor config, PRODUCTION symbol → Production config, otherwise → Dev config.
        /// </summary>
        public SDKSecurityConfig GetActiveSecurityConfig() {
            #if UNITY_EDITOR
            return Editor;
            #elif PRODUCTION
            return Production;
            #else
            return Dev;
            #endif
        }

        // Backward compat — modules that read EnableAppCheck directly
        public bool EnableAppCheck => GetActiveSecurityConfig().EnableAppCheck;
    }
}
