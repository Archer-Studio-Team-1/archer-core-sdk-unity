using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Consent {

    /// <summary>
    /// Auto-registers ConsentManager with SDKModuleFactory.
    /// Runs before any scene loads, enabling SDKBootstrap auto-discovery.
    /// </summary>
    public static class ConsentModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                if (!config.EnableConsent) return null;
                return new ConsentManager();
            });
        }
    }
}
