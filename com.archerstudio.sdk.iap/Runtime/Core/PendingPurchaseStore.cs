using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Persists purchases that were charged by the store but failed server validation
    /// (network errors, server outage, rate limiting). On next app launch, IAPManager
    /// retries validation and grants rewards for any that succeed.
    ///
    /// Storage: PlayerPrefs JSON. Purchases older than MaxAgeDays are pruned automatically.
    /// </summary>
    internal class PendingPurchaseStore {
        private const string Tag = "IAP.PendingStore";
        private const string PrefsKey = "archerstudio_iap_pending_purchases";
        private const int MaxAgeDays = 7;

        private PendingPurchaseList _cache;

        internal PendingPurchaseStore() {
            _cache = Load();
            Prune();
        }

        internal List<PendingPurchase> GetAll() {
            return new List<PendingPurchase>(_cache.items);
        }

        internal void Add(string productId, string receipt, string transactionId, string source) {
            // Avoid duplicates by transactionId
            for (int i = _cache.items.Count - 1; i >= 0; i--) {
                if (_cache.items[i].transactionId == transactionId) {
                    SDKLogger.Debug(Tag, $"Duplicate pending purchase skipped: {transactionId}");
                    return;
                }
            }

            _cache.items.Add(new PendingPurchase {
                productId = productId,
                receipt = receipt,
                transactionId = transactionId,
                source = source,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                retryCount = 0,
            });

            Save();
            SDKLogger.Info(Tag, $"Saved pending purchase: {productId} (txn: {transactionId})");
        }

        internal void Remove(string transactionId) {
            for (int i = _cache.items.Count - 1; i >= 0; i--) {
                if (_cache.items[i].transactionId == transactionId) {
                    _cache.items.RemoveAt(i);
                    Save();
                    SDKLogger.Info(Tag, $"Removed pending purchase: {transactionId}");
                    return;
                }
            }
        }

        internal void IncrementRetry(string transactionId) {
            for (int i = 0; i < _cache.items.Count; i++) {
                if (_cache.items[i].transactionId == transactionId) {
                    var item = _cache.items[i];
                    item.retryCount++;
                    _cache.items[i] = item;
                    Save();
                    return;
                }
            }
        }

        internal int Count => _cache.items.Count;

        private void Prune() {
            var cutoff = DateTime.UtcNow.AddDays(-MaxAgeDays);
            int removed = 0;

            for (int i = _cache.items.Count - 1; i >= 0; i--) {
                if (DateTime.TryParse(_cache.items[i].timestampUtc, out var ts) && ts < cutoff) {
                    SDKLogger.Warning(Tag,
                        $"Pruned expired pending purchase: {_cache.items[i].productId} " +
                        $"(txn: {_cache.items[i].transactionId}, age: {(DateTime.UtcNow - ts).Days}d)");
                    _cache.items.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0) Save();
        }

        private PendingPurchaseList Load() {
            var json = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) {
                return new PendingPurchaseList { items = new List<PendingPurchase>() };
            }

            try {
                var list = JsonUtility.FromJson<PendingPurchaseList>(json);
                if (list.items == null) list.items = new List<PendingPurchase>();
                return list;
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Failed to load pending purchases: {e.Message}");
                return new PendingPurchaseList { items = new List<PendingPurchase>() };
            }
        }

        private void Save() {
            var json = JsonUtility.ToJson(_cache);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
        }

        // JsonUtility requires wrapper class for List serialization
        [Serializable]
        private class PendingPurchaseList {
            public List<PendingPurchase> items;
        }

        [Serializable]
        internal struct PendingPurchase {
            public string productId;
            public string receipt;
            public string transactionId;
            public string source;
            public string timestampUtc;
            public int retryCount;
        }
    }
}
