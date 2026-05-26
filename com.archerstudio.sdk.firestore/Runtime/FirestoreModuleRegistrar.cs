using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Firestore {

    public static class FirestoreModuleRegistrar {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator(config => {
                // No EnableFirestore flag in core yet — gate via FirestoreConfig presence.
                var firestoreCfg = Resources.Load<FirestoreConfig>("FirestoreConfig");
                if (firestoreCfg == null) return null;
                return new FirestoreModule();
            });
        }
    }
}
