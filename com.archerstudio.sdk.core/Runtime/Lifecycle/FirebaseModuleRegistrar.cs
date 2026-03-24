using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Auto-registers FirebaseModule with SDKModuleFactory.
    /// Only registers if at least one Firebase-dependent module is enabled.
    /// </summary>
    public static class FirebaseModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                // Register if any Firebase-dependent module is enabled
                if (!config.EnableTracking)
                    return null;

                return new FirebaseModule();
            });
        }
    }
}
