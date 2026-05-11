using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Interface for IAP store providers (Unity IAP, custom).
    /// Implements IDisposable to ensure event subscriptions and resources are cleaned up.
    /// </summary>
    public interface IIAPProvider : IDisposable {
        void Initialize(IAPConfig config, Action<bool> onComplete);
        void Purchase(string productId, Action<PurchaseResult> onComplete);
        void RestorePurchases(Action<bool> onComplete);
        IReadOnlyList<ProductInfo> GetProducts();
        ProductInfo? GetProduct(string productId);
        SubscriptionInfo? GetSubscriptionInfo(string productId);
        void FetchSubscriptionProduct(Action<bool> onComplete);
        bool IsInitialized { get; }

        /// <summary>
        /// True after FetchPurchases completes (success or failure).
        /// Subscription state is only valid once this is true.
        /// </summary>
        bool IsPurchasesFetchCompleted { get; }

        /// <summary>
        /// Fired when a subscription's active state changes after a FetchPurchases refresh.
        /// Parameters: productId, isNowActive.
        /// </summary>
        event Action<string, bool> OnSubscriptionStateChanged;

        /// <summary>
        /// Fired whenever a subscription order is observed (new purchase, restore, or
        /// FetchPurchases). Carries the raw receipt so the manager can extract purchase
        /// tokens for server-side status queries. Receipt may be empty for confirmed orders
        /// on some platforms — caller should handle that.
        /// Parameters: productId, transactionId, receipt.
        /// </summary>
        event Action<string, string, string> OnSubscriptionOrderObserved;
    }
}
