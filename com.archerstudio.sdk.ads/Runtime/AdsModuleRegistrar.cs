using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// Auto-registers AdManager with SDKModuleFactory.
    /// Runs before any scene loads, enabling SDKBootstrap auto-discovery.
    /// </summary>
    public static class AdsModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                if (!config.EnableAds) return null;
                return new AdManager();
            });
        }
    }
}
