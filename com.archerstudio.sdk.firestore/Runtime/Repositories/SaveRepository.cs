using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Firestore {

    public sealed class SaveRepository : ISaveRepository {

        private const string Tag = "Firestore";

        private readonly IFirestoreService _service;

        public SaveRepository(IFirestoreService service) {
            _service = service;
        }

        public void SaveFeatureAsync(string featureName,
                                     IReadOnlyDictionary<string, object> data,
                                     int schemaVersion,
                                     Action<FirestoreResult<bool>> onComplete,
                                     int expectedVersion = -1) {
            if (string.IsNullOrEmpty(featureName)) {
                onComplete?.Invoke(FirestoreResult<bool>.Failed(
                    FirestoreErrorCode.InvalidArgument, "featureName required"));
                return;
            }
            if (data == null) {
                onComplete?.Invoke(FirestoreResult<bool>.Failed(
                    FirestoreErrorCode.InvalidArgument, "data required"));
                return;
            }

            var payload = new Dictionary<string, object> {
                { "schemaVersion", (long)schemaVersion },
                { "data", PolymorphicJsonConverter.ToFirestoreDict((IDictionary<string, object>)data) },
                { "updatedBy", "client" },
            };
            // Phase D3 optimistic concurrency: when the caller knows the version
            // it last observed, the next write must be that+1. Rules reject any
            // write that doesn't satisfy the equality, so a client racing against
            // another device gets PermissionDenied and can refresh + retry.
            if (expectedVersion >= 0) {
                payload["version"] = (long)(expectedVersion + 1);
            }

            var path = $"users/{{uid}}/saves/{featureName}";
            _service.SetDocumentAsync(path, payload, onComplete);
        }

        public void LoadFeatureAsync(string featureName,
                                     Action<FirestoreResult<SavedFeatureSnapshot>> onComplete) {
            if (string.IsNullOrEmpty(featureName)) {
                onComplete?.Invoke(FirestoreResult<SavedFeatureSnapshot>.Failed(
                    FirestoreErrorCode.InvalidArgument, "featureName required"));
                return;
            }
            var path = $"users/{{uid}}/saves/{featureName}";
            _service.GetDocumentAsync(path, r => {
                if (!r.Success) {
                    onComplete?.Invoke(FirestoreResult<SavedFeatureSnapshot>.Failed(r.ErrorCode, r.ErrorMessage));
                    return;
                }
                var snap = new SavedFeatureSnapshot {
                    FeatureName = featureName,
                    SchemaVersion = (int)r.Data.TryGet<long>("schemaVersion", 1),
                    Data = PolymorphicJsonConverter.FromFirestoreDict(
                        r.Data.TryGet<IReadOnlyDictionary<string, object>>("data")
                        ?? new Dictionary<string, object>()),
                    UpdatedBy = r.Data.TryGet<string>("updatedBy"),
                    UpdatedAtUnixSec = ExtractTimestamp(r.Data, "updatedAt"),
                    Version = (int)r.Data.TryGet<long>("version", 0),
                };
                onComplete?.Invoke(FirestoreResult<SavedFeatureSnapshot>.Succeeded(snap));
            });
        }

        public void GetFeatureMetadataAsync(string featureName,
                                            Action<FirestoreResult<SavedFeatureMetadata>> onComplete) {
            LoadFeatureAsync(featureName, r => {
                if (!r.Success) {
                    onComplete?.Invoke(FirestoreResult<SavedFeatureMetadata>.Failed(r.ErrorCode, r.ErrorMessage));
                    return;
                }
                onComplete?.Invoke(FirestoreResult<SavedFeatureMetadata>.Succeeded(new SavedFeatureMetadata {
                    FeatureName = r.Data.FeatureName,
                    SchemaVersion = r.Data.SchemaVersion,
                    UpdatedBy = r.Data.UpdatedBy,
                    UpdatedAtUnixSec = r.Data.UpdatedAtUnixSec,
                    Version = r.Data.Version,
                }));
            });
        }

        private static long ExtractTimestamp(IReadOnlyDictionary<string, object> data, string key) {
            if (data == null || !data.TryGetValue(key, out var v) || v == null) return 0;
            // Firestore returns Timestamp as a wrapped struct; reflection extracts seconds.
            var sec = v.GetType().GetProperty("Seconds")?.GetValue(v);
            return sec is long s ? s : 0;
        }
    }
}
