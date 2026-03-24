using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking {

    /// <summary>
    /// Auto-registers TrackingManager with SDKModuleFactory.
    /// TrackingManager is a MonoBehaviour singleton — the factory creator
    /// accesses the Instance property which auto-creates the GameObject.
    /// </summary>
    public static class TrackingModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                if (!config.EnableTracking) return null;
                // Accessing Instance auto-creates the singleton GameObject
                return TrackingManager.Instance;
            });
        }
    }
}
