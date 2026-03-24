using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.RemoteConfig {

    [CreateAssetMenu(fileName = "RemoteConfigConfig", menuName = "ArcherStudio/SDK/Remote Config")]
    public class RemoteConfigConfig : ModuleConfigBase {

        [Header("Fetch Settings")]
        [Tooltip("Minimum fetch interval in seconds.")]
        public long MinimumFetchIntervalSeconds = 3600;

        [Tooltip("Auto-fetch on initialization.")]
        public bool AutoFetchOnInit = true;

        [Header("Feature Flags")]
        [Tooltip("Enable local feature flag fallbacks.")]
        public bool EnableLocalDefaults = true;
    }
}
