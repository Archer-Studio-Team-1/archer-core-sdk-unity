using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Base class for per-module configuration ScriptableObjects.
    /// Each SDK module (ads, tracking, IAP, etc.) extends this.
    /// </summary>
    public abstract class ModuleConfigBase : ScriptableObject {
        [Tooltip("Enable or disable this module.")]
        public bool Enabled = true;
    }
}
