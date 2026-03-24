using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Push {

    /// <summary>
    /// Configuration for the Push Notifications module.
    /// Create via: Assets > Create > ArcherStudio > SDK > Push Config.
    /// Place in a Resources folder.
    /// </summary>
    [CreateAssetMenu(fileName = "PushConfig", menuName = "ArcherStudio/SDK/Push Config")]
    public class PushConfig : ModuleConfigBase {
        [Tooltip("Auto-request notification permission on init.")]
        public bool AutoRequestPermission = false;

        [Tooltip("Default topics to subscribe on init.")]
        public string[] DefaultTopics = new string[0];
    }
}
