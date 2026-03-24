using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.RemoteConfig {

    /// <summary>
    /// Convenience wrapper for reading feature flags from Remote Config.
    /// </summary>
    public static class FeatureFlagService {

        /// <summary>
        /// Check if a feature flag is enabled.
        /// </summary>
        public static bool IsEnabled(string flagKey, bool defaultValue = false) {
            var manager = RemoteConfigManager.Instance;
            if (manager == null || manager.State != ModuleState.Ready) {
                SDKLogger.Warning("FeatureFlags",
                    $"RemoteConfig not ready, returning default for '{flagKey}'");
                return defaultValue;
            }
            return manager.GetBool(flagKey, defaultValue);
        }

        /// <summary>
        /// Get a feature flag variant string (for A/B testing).
        /// </summary>
        public static string GetVariant(string flagKey, string defaultValue = "") {
            var manager = RemoteConfigManager.Instance;
            if (manager == null || manager.State != ModuleState.Ready) {
                return defaultValue;
            }
            return manager.GetString(flagKey, defaultValue);
        }
    }
}
