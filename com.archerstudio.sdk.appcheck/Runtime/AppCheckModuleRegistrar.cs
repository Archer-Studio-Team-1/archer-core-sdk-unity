using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.AppCheck {

    public static class AppCheckModuleRegistrar {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config =>
                config.EnableAppCheck ? new AppCheckManager() : null);
        }
    }
}
