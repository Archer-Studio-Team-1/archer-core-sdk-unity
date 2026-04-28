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
        bool IsInitialized { get; }

        /// <summary>
        /// True after FetchPurchases completes (success or failure).
        /// Subscription state is only valid once this is true.
        /// </summary>
        bool IsPurchasesFetchCompleted { get; }
    }
}
