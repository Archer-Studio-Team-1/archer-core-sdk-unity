using System;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Uploads pre-write snapshots to Cloud Storage as immutable archives. Used by
    /// the IDK MigrationRunner during the BackingUp state.
    /// </summary>
    public interface IBackupUploader {
        void UploadAsync(string featureName,
                         int schemaVersion,
                         string normalizedJson,
                         string checksumSha256,
                         Action<FirestoreResult<BackupReceipt>> onComplete);
    }

    public sealed class BackupReceipt {
        public string StoragePath { get; set; }
        public long Bytes { get; set; }
        public long UploadedAtUnixMs { get; set; }
    }
}
