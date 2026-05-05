using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.AppCheck {

    [CreateAssetMenu(fileName = "AppCheckConfig", menuName = "ArcherStudio/SDK/App Check Config")]
    public class AppCheckConfig : ModuleConfigBase {

        [Header("App Check Settings")]
        [Tooltip("Use Firebase Debug Provider on development builds (non-PRODUCTION). " +
                 "Allows testing App Check flow without Play Integrity / DeviceCheck.")]
        public bool UseDebugProviderInDev = false;
    }
}
