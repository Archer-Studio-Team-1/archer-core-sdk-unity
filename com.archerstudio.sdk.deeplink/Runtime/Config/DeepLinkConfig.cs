using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.DeepLink {

    /// <summary>
    /// Configuration for the Deep Link module.
    /// Create via: Assets > Create > ArcherStudio > SDK > Deep Link Config.
    /// Place in a Resources folder.
    /// </summary>
    [CreateAssetMenu(fileName = "DeepLinkConfig", menuName = "ArcherStudio/SDK/Deep Link Config")]
    public class DeepLinkConfig : ModuleConfigBase {
        [Header("URI Schemes")]
        [Tooltip("Custom URI scheme (e.g., 'myapp').")]
        public string UriScheme;

        [Header("Firebase Dynamic Links")]
        [Tooltip("Firebase Dynamic Links domain.")]
        public string DynamicLinksDomain;

        [Header("Deferred Deep Links")]
        [Tooltip("Enable deferred deep link handling.")]
        public bool EnableDeferredDeepLinks = true;
    }
}
