using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Login {

    public static class LoginModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            // Registrar gates on EnableLogin=false (returns null = no module created).
            // LoginModule.InitializeAsync also checks EnableLogin as a secondary guard.
            SDKModuleFactory.RegisterCreator(config => {
                if (!config.EnableLogin) return null;
                return new LoginModule();
            });
        }
    }
}
