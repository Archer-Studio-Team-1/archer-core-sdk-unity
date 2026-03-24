using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Push {

    /// <summary>
    /// Auto-registers PushManager with SDKModuleFactory.
    /// Runs before any scene loads, enabling SDKBootstrap auto-discovery.
    /// </summary>
    public static class PushModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                if (!config.EnablePush) return null;
                return new PushManager();
            });
        }
    }
}
