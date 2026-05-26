using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Mirror of firebase-functions/packages/firestore-core/src/lib/featureRegistry.ts.
    /// Reads _config/save_features_registry once per session (cached per FirestoreConfig.FeatureRegistryCacheTtlMs).
    /// Used by MigrationRunner + Phase-5 CloudSyncBridge to know which features exist
    /// and what their cloud paths + tiers are.
    /// </summary>
    public sealed class FeatureRegistry {

        private const string Tag = "Firestore";
        private const string RegistryDocPath = "_config/save_features_registry";

        private readonly IFirestoreService _service;
        private readonly long _cacheTtlMs;
        private IReadOnlyList<SaveFeatureMeta> _cached;
        private long _expiresAtMs;

        public FeatureRegistry(IFirestoreService service, long cacheTtlMs) {
            _service = service;
            _cacheTtlMs = cacheTtlMs;
        }

        public void GetFeaturesAsync(Action<FirestoreResult<IReadOnlyList<SaveFeatureMeta>>> onComplete) {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_cached != null && _expiresAtMs > now) {
                onComplete?.Invoke(FirestoreResult<IReadOnlyList<SaveFeatureMeta>>.Succeeded(_cached));
                return;
            }
            _service.GetDocumentAsync(RegistryDocPath, result => {
                if (!result.Success || result.Data == null) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyList<SaveFeatureMeta>>.Failed(
                        result.ErrorCode, result.ErrorMessage));
                    return;
                }
                var list = Parse(result.Data);
                _cached = list;
                _expiresAtMs = now + _cacheTtlMs;
                if (list.Count == 0) {
                    SDKLogger.Warning(Tag, "Feature registry empty. Seed _config/save_features_registry.");
                }
                onComplete?.Invoke(FirestoreResult<IReadOnlyList<SaveFeatureMeta>>.Succeeded(list));
            });
        }

        public void InvalidateCache() {
            _cached = null;
            _expiresAtMs = 0;
        }

        private static IReadOnlyList<SaveFeatureMeta> Parse(IReadOnlyDictionary<string, object> doc) {
            if (!doc.TryGetValue("features", out var raw) || !(raw is IEnumerable<object> arr)) {
                return Array.Empty<SaveFeatureMeta>();
            }
            var result = new List<SaveFeatureMeta>();
            foreach (var entry in arr) {
                if (!(entry is IDictionary<string, object> m)) continue;
                var meta = new SaveFeatureMeta {
                    Name = m.TryGet<string>("name"),
                    Tier = m.TryGet<string>("tier"),
                    SchemaVersion = (int)(m.TryGet<long>("schemaVersion", 1)),
                    CloudDoc = m.TryGet<string>("cloudDoc"),
                    CloudCollection = m.TryGet<string>("cloudCollection"),
                    Description = m.TryGet<string>("description"),
                };
                result.Add(meta);
            }
            return result;
        }
    }

    internal static class DictExtensions {
        public static T TryGet<T>(this IDictionary<string, object> dict, string key, T fallback = default) {
            if (dict != null && dict.TryGetValue(key, out var v) && v is T t) return t;
            return fallback;
        }
    }
}
