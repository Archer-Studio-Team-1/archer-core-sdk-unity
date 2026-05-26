using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Per-feature save read/write. Wraps users/{uid}/saves/{feature} doc operations.
    /// Used by the IDK CloudSyncBridge (Phase 5) to mirror local saves to the cloud.
    /// </summary>
    public interface ISaveRepository {

        /// <summary>
        /// Write a feature's data. The dictionary becomes the doc body; the SDK adds
        /// schemaVersion, updatedAt (server timestamp), updatedBy fields automatically.
        /// </summary>
        void SaveFeatureAsync(string featureName,
                              IReadOnlyDictionary<string, object> data,
                              int schemaVersion,
                              Action<FirestoreResult<bool>> onComplete);

        /// <summary>
        /// Read a feature's data. Returns NotFound if the doc has never been written.
        /// </summary>
        void LoadFeatureAsync(string featureName,
                              Action<FirestoreResult<SavedFeatureSnapshot>> onComplete);

        /// <summary>
        /// Read a feature's schemaVersion + updatedAt without pulling the full data
        /// payload. Used by migration heartbeat checks.
        /// </summary>
        void GetFeatureMetadataAsync(string featureName,
                                     Action<FirestoreResult<SavedFeatureMetadata>> onComplete);
    }

    public sealed class SavedFeatureSnapshot {
        public string FeatureName { get; set; }
        public int SchemaVersion { get; set; }
        public IDictionary<string, object> Data { get; set; }
        public string UpdatedBy { get; set; }
        public long UpdatedAtUnixSec { get; set; }
    }

    public sealed class SavedFeatureMetadata {
        public string FeatureName { get; set; }
        public int SchemaVersion { get; set; }
        public string UpdatedBy { get; set; }
        public long UpdatedAtUnixSec { get; set; }
    }
}
