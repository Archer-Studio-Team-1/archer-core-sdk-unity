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
        /// <param name="expectedVersion">
        /// Phase D3 optimistic concurrency. When &gt;= 0 the SDK writes
        /// <c>version = expectedVersion + 1</c> into the doc; Firestore rules then
        /// enforce that the previous resource's <c>version</c> equals
        /// <paramref name="expectedVersion"/>, rejecting writes from clients that
        /// missed an intervening update. Pass <c>-1</c> (default) to opt out and
        /// keep the legacy last-write-wins behaviour.
        /// </param>
        void SaveFeatureAsync(string featureName,
                              IReadOnlyDictionary<string, object> data,
                              int schemaVersion,
                              Action<FirestoreResult<bool>> onComplete,
                              int expectedVersion = -1);

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
        /// <summary>Monotonic per-write counter (Phase D3). 0 when absent (legacy docs).</summary>
        public int Version { get; set; }
    }

    public sealed class SavedFeatureMetadata {
        public string FeatureName { get; set; }
        public int SchemaVersion { get; set; }
        public string UpdatedBy { get; set; }
        public long UpdatedAtUnixSec { get; set; }
        public int Version { get; set; }
    }
}
