using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Auto-registers IAPManager with SDKModuleFactory.
    /// Runs before any scene loads, enabling SDKBootstrap auto-discovery.
    /// </summary>
    public static class IAPModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                if (!config.EnableIAP) return null;
                return new IAPManager();
            });
        }
    }
}
