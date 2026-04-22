using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.CloudSave {

    public static class CloudSaveModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                if (!config.EnableCloudSave) return null;
                return new CloudSaveModule();
            });
        }
    }
}
