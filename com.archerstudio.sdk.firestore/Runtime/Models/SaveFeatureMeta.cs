using System.Collections.Generic;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Mirrors firebase-functions/packages/firestore-core/src/lib/featureRegistry.ts.
    /// Read from Firestore _config/save_features_registry by FeatureRegistry.
    /// </summary>
    public sealed class SaveFeatureMeta {
        public string Name { get; set; }
        public string Tier { get; set; }                  // "T0"|"T1"|"T2"
        public int SchemaVersion { get; set; }
        public string CloudDoc { get; set; }              // "users/{uid}/saves/<name>"
        public string CloudCollection { get; set; }       // For features stored as subcollection
        public string Description { get; set; }
        public IReadOnlyDictionary<string, int> FieldCaps { get; set; }
        public IReadOnlyDictionary<string, FeatureFieldRange> FieldRanges { get; set; }
    }

    public sealed class FeatureFieldRange {
        public long Min { get; set; }
        public long Max { get; set; }
    }
}
