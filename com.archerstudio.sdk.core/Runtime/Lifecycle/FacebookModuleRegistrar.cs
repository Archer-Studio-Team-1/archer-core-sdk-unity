using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Auto-registers FacebookModule with SDKModuleFactory.
    /// Only registers when Facebook SDK is detected (HAS_FACEBOOK_SDK defined).
    /// </summary>
    public static class FacebookModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
#if HAS_FACEBOOK_SDK
            SDKModuleFactory.RegisterCreator(config => new FacebookModule());
#endif
        }
    }
}
