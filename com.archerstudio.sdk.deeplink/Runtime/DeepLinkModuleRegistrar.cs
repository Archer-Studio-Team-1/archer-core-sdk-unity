using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.DeepLink {

    /// <summary>
    /// Auto-registers DeepLinkManager with SDKModuleFactory.
    /// Runs before any scene loads, enabling SDKBootstrap auto-discovery.
    /// </summary>
    public static class DeepLinkModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                if (!config.EnableDeepLink) return null;
                return new DeepLinkManager();
            });
        }
    }
}
