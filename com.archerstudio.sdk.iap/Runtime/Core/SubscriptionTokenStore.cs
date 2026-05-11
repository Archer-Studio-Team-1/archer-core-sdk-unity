using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Persists subscription purchase tokens (Google) and transaction IDs (Apple) to
    /// PlayerPrefs so they survive app restarts.
    ///
    /// Why this is necessary: Unity IAP v5 only exposes the receipt on PendingOrder.
    /// Once an order is confirmed, subsequent FetchPurchases calls return it as a
    /// confirmed Order with an empty Receipt field. That means tokens captured at
    /// first-purchase time would be lost on next app launch, and QuerySubscriptionStatus
    /// could not call the server to verify if the subscription is still active —
    /// the very scenario where the local store cache can stale-report expired subs
    /// as still active.
    ///
    /// Storage: PlayerPrefs JSON. Entries older than MaxAgeDays are pruned.
    /// </summary>
    internal class SubscriptionTokenStore {
        private const string Tag = "IAP.TokenStore";
        private const string PrefsKey = "archerstudio_iap_subscription_tokens";
        private const int MaxAgeDays = 90;

        private SubscriptionTokenList _cache;

        internal SubscriptionTokenStore() {
            _cache = Load();
            Prune();
        }

        internal int Count => _cache.items.Count;

        internal bool TryGet(string productId, out string purchaseToken, out string transactionId) {
            for (int i = 0; i < _cache.items.Count; i++) {
                if (_cache.items[i].productId == productId) {
                    purchaseToken = _cache.items[i].purchaseToken;
                    transactionId = _cache.items[i].transactionId;
                    return !string.IsNullOrEmpty(purchaseToken) || !string.IsNullOrEmpty(transactionId);
                }
            }
            purchaseToken = null;
            transactionId = null;
            return false;
        }

        internal void Set(string productId, string purchaseToken, string transactionId) {
            if (string.IsNullOrEmpty(productId)) return;

            for (int i = 0; i < _cache.items.Count; i++) {
                if (_cache.items[i].productId == productId) {
                    // Update existing entry; preserve any token we already had if new is empty
                    if (!string.IsNullOrEmpty(purchaseToken)) _cache.items[i].purchaseToken = purchaseToken;
                    if (!string.IsNullOrEmpty(transactionId)) _cache.items[i].transactionId = transactionId;
                    _cache.items[i].updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    Save();
                    return;
                }
            }

            _cache.items.Add(new SubscriptionTokenEntry {
                productId = productId,
                purchaseToken = purchaseToken ?? "",
                transactionId = transactionId ?? "",
                updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
            Save();
        }

        internal void Remove(string productId) {
            for (int i = _cache.items.Count - 1; i >= 0; i--) {
                if (_cache.items[i].productId == productId) {
                    _cache.items.RemoveAt(i);
                    Save();
                    return;
                }
            }
        }

        internal IReadOnlyList<SubscriptionTokenEntry> GetAll() => _cache.items;

        private void Prune() {
            long cutoff = DateTimeOffset.UtcNow.AddDays(-MaxAgeDays).ToUnixTimeSeconds();
            int removed = 0;
            for (int i = _cache.items.Count - 1; i >= 0; i--) {
                if (_cache.items[i].updatedAt < cutoff) {
                    _cache.items.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0) {
                SDKLogger.Info(Tag, $"Pruned {removed} stale subscription token entries.");
                Save();
            }
        }

        private static SubscriptionTokenList Load() {
            try {
                var json = PlayerPrefs.GetString(PrefsKey, null);
                if (string.IsNullOrEmpty(json)) return new SubscriptionTokenList();
                var list = JsonUtility.FromJson<SubscriptionTokenList>(json);
                return list ?? new SubscriptionTokenList();
            } catch (Exception e) {
                SDKLogger.Warning(Tag, $"Failed to load token store: {e.Message}");
                return new SubscriptionTokenList();
            }
        }

        private void Save() {
            try {
                var json = JsonUtility.ToJson(_cache);
                PlayerPrefs.SetString(PrefsKey, json);
                PlayerPrefs.Save();
            } catch (Exception e) {
                SDKLogger.Warning(Tag, $"Failed to save token store: {e.Message}");
            }
        }

        [Serializable]
        internal class SubscriptionTokenList {
            public List<SubscriptionTokenEntry> items = new List<SubscriptionTokenEntry>();
        }

        [Serializable]
        internal class SubscriptionTokenEntry {
            public string productId;
            public string purchaseToken;
            public string transactionId;
            public long updatedAt;
        }
    }
}
