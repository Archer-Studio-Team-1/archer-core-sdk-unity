using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Firestore {

    public sealed class BackupUploader : IBackupUploader {

        private readonly IFirestoreService _service;

        public BackupUploader(IFirestoreService service) {
            _service = service;
        }

        public void UploadAsync(string featureName,
                                int schemaVersion,
                                string normalizedJson,
                                string checksumSha256,
                                Action<FirestoreResult<BackupReceipt>> onComplete) {
            if (string.IsNullOrEmpty(featureName) || string.IsNullOrEmpty(normalizedJson)) {
                onComplete?.Invoke(FirestoreResult<BackupReceipt>.Failed(
                    FirestoreErrorCode.InvalidArgument, "featureName + normalizedJson required"));
                return;
            }

            var payload = new Dictionary<string, object> {
                { "featureName", featureName },
                { "schemaVersion", (long)schemaVersion },
                { "jsonContent", normalizedJson },
            };
            if (!string.IsNullOrEmpty(checksumSha256)) payload["checksum"] = checksumSha256;

            _service.CallFunctionAsync("uploadMigrationBackup", payload, r => {
                if (!r.Success) {
                    onComplete?.Invoke(FirestoreResult<BackupReceipt>.Failed(r.ErrorCode, r.ErrorMessage));
                    return;
                }
                onComplete?.Invoke(FirestoreResult<BackupReceipt>.Succeeded(new BackupReceipt {
                    StoragePath = r.Data.TryGet<string>("storagePath"),
                    Bytes = r.Data.TryGet<long>("bytes"),
                    UploadedAtUnixMs = r.Data.TryGet<long>("uploadedAtMs"),
                }));
            });
        }
    }
}
