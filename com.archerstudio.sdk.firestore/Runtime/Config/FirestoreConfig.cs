using UnityEngine;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Place at Resources/FirestoreConfig.asset. Presence enables the module.
    /// </summary>
    [CreateAssetMenu(fileName = "FirestoreConfig", menuName = "Archer Studio/SDK/Firestore Config")]
    public sealed class FirestoreConfig : ScriptableObject {

        [Tooltip("Web client ID for Firebase Auth via GPGS. Same as CloudSaveConfig.WebClientId.")]
        public string WebClientId;

        [Tooltip("Region for Cloud Functions. Default asia-southeast1.")]
        public string FunctionsRegion = "asia-southeast1";

        [Tooltip("Enable offline persistence on the Firestore client. Recommended true.")]
        public bool EnableOfflinePersistence = true;

        [Tooltip("Listener debounce window in ms. 0 = no debounce.")]
        public int ListenerDebounceMs = 500;

        [Tooltip("Cache TTL for IAP catalog reads in ms. Default 5 min.")]
        public int IapCatalogCacheTtlMs = 5 * 60 * 1000;

        [Tooltip("Cache TTL for feature registry reads in ms. Default 1 h.")]
        public int FeatureRegistryCacheTtlMs = 60 * 60 * 1000;

        [Tooltip("If true, log every Firestore op at Info level. Verbose; disable in PROD.")]
        public bool VerboseLogging;
    }
}
