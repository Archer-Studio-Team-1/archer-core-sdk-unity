using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Configuration for the SDK Bootstrap process.
    /// Controls initialization behavior and module auto-discovery.
    /// Place in a Resources folder as "SDKBootstrapConfig".
    /// </summary>
    [CreateAssetMenu(fileName = "SDKBootstrapConfig", menuName = "ArcherStudio/SDK/Bootstrap Config")]
    public class SDKBootstrapConfig : ScriptableObject {

        [Header("Behavior")]
        [Tooltip("Automatically discover and register ISDKModule instances in the scene.")]
        public bool AutoDiscoverModules = true;

        [Tooltip("Continue to next step even if some modules fail to initialize.")]
        public bool ContinueOnModuleFailure = true;

        [Tooltip("Maximum time (seconds) to wait for SDK initialization before timing out.")]
        public float MaxInitTimeout = 15f;
    }
}
