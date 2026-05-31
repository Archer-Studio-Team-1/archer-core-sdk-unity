using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Firestore {

    public sealed class SaveRepository : ISaveRepository {

        private const string Tag = "Firestore";

        /// <summary>
        /// Marker value the provider replaces with a Firestore server timestamp
        /// (FieldValue.ServerTimestamp) at write time. Lets this transport-agnostic
        /// repository request a server timestamp without referencing Firebase types.
        /// Scoped to the save payload only, so docs with strict per-field rules
        /// (e.g. session heartbeat) are unaffected.
        /// </summary>
        public const string ServerTimestampSentinel = "__sdk_server_timestamp__";

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
                // Server-stamped last-save time. The provider swaps this sentinel for
                // FieldValue.ServerTimestamp; readers (conflict resolver) surface it as
                // "last saved on cloud". saves/{feature} rules don't whitelist keys, so
                // the extra field is accepted.
                { "updatedAt", ServerTimestampSentinel },
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
            switch (v) {
                case long l:  return NormalizeEpochToSeconds(l);
                case int i:   return i;
                case double d: return NormalizeEpochToSeconds((long)d);
                case float f:  return NormalizeEpochToSeconds((long)f);
                case string str: return long.TryParse(str, out var p) ? NormalizeEpochToSeconds(p) : 0;
                case DateTime dt: return new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();
                case DateTimeOffset dto: return dto.ToUnixTimeSeconds();
            }
            // Firebase.Firestore.Timestamp is a wrapped struct that exposes
            // ToDateTimeOffset()/ToDateTime() — NOT a public Seconds property. The old
            // "Seconds" reflection therefore always returned 0, so every reader of
            // UpdatedAtUnixSec saw an epoch of 0 ("unknown save time"). Prefer the real
            // API via reflection (keeps this repository transport-agnostic), falling
            // back to a Seconds property only if a future SDK adds one.
            var t = v.GetType();
            try {
                var toDto = t.GetMethod("ToDateTimeOffset", Type.EmptyTypes);
                if (toDto != null && toDto.Invoke(v, null) is DateTimeOffset off)
                    return off.ToUnixTimeSeconds();
                var toDt = t.GetMethod("ToDateTime", Type.EmptyTypes);
                if (toDt != null && toDt.Invoke(v, null) is DateTime dtv)
                    return new DateTimeOffset(dtv.ToUniversalTime()).ToUnixTimeSeconds();
            } catch (Exception e) {
                SDKLogger.Warning(Tag, $"ExtractTimestamp: Timestamp reflection threw: {e.Message}");
            }
            var secProp = t.GetProperty("Seconds");
            if (secProp != null && secProp.GetValue(v) is long sec) return NormalizeEpochToSeconds(sec);
            return 0;
        }

        /// <summary>Treat values that look like milliseconds (≥ 1e12) as ms and fold to seconds.</summary>
        private static long NormalizeEpochToSeconds(long value) =>
            value >= 1_000_000_000_000L ? value / 1000L : value;
    }
}
