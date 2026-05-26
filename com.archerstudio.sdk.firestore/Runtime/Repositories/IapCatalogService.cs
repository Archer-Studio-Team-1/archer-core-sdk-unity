using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Firestore {

    public sealed class IapCatalogService : IIapCatalogService {

        private const string Tag = "Firestore";
        private readonly IFirestoreService _service;
        private readonly long _cacheTtlMs;

        private IReadOnlyList<IapProduct> _cached;
        private long _expiresAtMs;

        public IapCatalogService(IFirestoreService service, long cacheTtlMs) {
            _service = service;
            _cacheTtlMs = cacheTtlMs;
        }

        public void GetCatalogAsync(Action<FirestoreResult<IReadOnlyList<IapProduct>>> onComplete) {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_cached != null && _expiresAtMs > now) {
                onComplete?.Invoke(FirestoreResult<IReadOnlyList<IapProduct>>.Succeeded(_cached));
                return;
            }
            _service.CallFunctionAsync("getIapProductCatalog", null, r => {
                if (!r.Success) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyList<IapProduct>>.Failed(r.ErrorCode, r.ErrorMessage));
                    return;
                }
                var list = ParseProducts(r.Data);
                _cached = list;
                _expiresAtMs = now + _cacheTtlMs;
                onComplete?.Invoke(FirestoreResult<IReadOnlyList<IapProduct>>.Succeeded(list));
            });
        }

        public void GetProductAsync(string productId, Action<FirestoreResult<IapProduct>> onComplete) {
            GetCatalogAsync(r => {
                if (!r.Success) {
                    onComplete?.Invoke(FirestoreResult<IapProduct>.Failed(r.ErrorCode, r.ErrorMessage));
                    return;
                }
                foreach (var p in r.Data) {
                    if (p.ProductId == productId) {
                        onComplete?.Invoke(FirestoreResult<IapProduct>.Succeeded(p));
                        return;
                    }
                }
                onComplete?.Invoke(FirestoreResult<IapProduct>.Failed(FirestoreErrorCode.NotFound));
            });
        }

        public void InvalidateCache() {
            _cached = null;
            _expiresAtMs = 0;
        }

        private static IReadOnlyList<IapProduct> ParseProducts(IReadOnlyDictionary<string, object> doc) {
            if (!doc.TryGetValue("products", out var raw) || !(raw is IEnumerable<object> arr)) {
                return Array.Empty<IapProduct>();
            }
            var list = new List<IapProduct>();
            foreach (var entry in arr) {
                if (!(entry is IDictionary<string, object> m)) continue;
                list.Add(new IapProduct {
                    ProductId = m.TryGet<string>("productId"),
                    Kind = m.TryGet<string>("kind"),
                    DisplayName = m.TryGet<string>("displayName"),
                    PriceUsdEstimate = m.TryGet<double>("priceUsdEstimate"),
                    IsActive = m.TryGet<bool>("isActive", true),
                    Grants = ParseGrants(m.TryGet<IDictionary<string, object>>("grants")),
                });
            }
            return list;
        }

        private static IapProductGrants ParseGrants(IDictionary<string, object> raw) {
            if (raw == null) return new IapProductGrants();
            return new IapProductGrants {
                Currencies = raw.TryGet<IReadOnlyDictionary<string, string>>("currencies"),
                Entitlements = raw.TryGet<IReadOnlyList<string>>("entitlements"),
                VipTier = raw.TryGet<string>("vipTier"),
                VipDurationDays = (int)raw.TryGet<long>("vipDurationDays"),
            };
        }
    }

}
