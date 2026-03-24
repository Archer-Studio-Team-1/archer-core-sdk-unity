using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.RemoteConfig {

    /// <summary>
    /// Auto-registers RemoteConfigManager with SDKModuleFactory.
    /// Runs before any scene loads, enabling SDKBootstrap auto-discovery.
    /// </summary>
    public static class RemoteConfigModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                if (!config.EnableRemoteConfig) return null;
                return new RemoteConfigManager();
            });
        }
    }
}
