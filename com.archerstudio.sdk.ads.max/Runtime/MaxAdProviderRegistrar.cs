using UnityEngine;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// Registers the AppLovin MAX provider with AdProviderRegistry before any scene loads,
    /// so AdManager can create it without com.archerstudio.sdk.ads referencing this package.
    /// </summary>
    public static class MaxAdProviderRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            AdProviderRegistry.Register(
                AdMediationPlatform.AppLovinMax, () => new AppLovinMaxProvider());
        }
    }
}
