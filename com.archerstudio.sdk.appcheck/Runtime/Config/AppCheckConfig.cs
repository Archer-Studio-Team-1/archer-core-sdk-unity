using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.AppCheck {

    [CreateAssetMenu(fileName = "AppCheckConfig", menuName = "ArcherStudio/SDK/App Check Config")]
    public class AppCheckConfig : ModuleConfigBase {

        [Header("App Check Settings")]
        [Tooltip("Use Debug provider in Editor for testing.")]
        public bool UseDebugProviderInEditor = true;
    }
}
