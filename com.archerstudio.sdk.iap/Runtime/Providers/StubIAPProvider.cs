using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Stub IAP provider for editor testing or when Unity IAP is not installed.
    /// </summary>
    public class StubIAPProvider : IIAPProvider {
        public bool IsInitialized { get; private set; }

        public void Initialize(IAPConfig config, Action<bool> onComplete) {
            IsInitialized = true;
            SDKLogger.Info("IAP-Stub", "Stub IAP provider initialized.");
            onComplete?.Invoke(true);
        }

        public void Purchase(string productId, Action<PurchaseResult> onComplete) {
            #if UNITY_EDITOR
            // Simulate success in editor
            SDKLogger.Info("IAP-Stub", $"Simulated purchase: {productId}");
            onComplete?.Invoke(PurchaseResult.Succeeded(productId, "stub_txn_" + productId, "stub_receipt"));
            #else
            onComplete?.Invoke(PurchaseResult.Failed(productId, "No IAP SDK.", PurchaseFailureReason.ProductUnavailable));
            #endif
        }

        public void RestorePurchases(Action<bool> onComplete) {
            onComplete?.Invoke(true);
        }

        public IReadOnlyList<ProductInfo> GetProducts() => Array.Empty<ProductInfo>();

        public ProductInfo? GetProduct(string productId) => null;

        public SubscriptionInfo? GetSubscriptionInfo(string productId) => null;

        public void Dispose() {
            IsInitialized = false;
        }
    }
}
