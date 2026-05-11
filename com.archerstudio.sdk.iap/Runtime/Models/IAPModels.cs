using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.IAP {

    public enum SubscriptionStatus {
        Unknown,
        Active,
        Cancelled,
        GracePeriod,
        AccountHold,
        Paused,
        Expired
    }

    public readonly struct SubscriptionStateChangedEvent : ISDKEvent {
        public string ProductId { get; }
        public bool WasActive { get; }
        public bool IsActive { get; }
        public SubscriptionStatus Status { get; }

        public SubscriptionStateChangedEvent(string productId, bool wasActive, bool isActive,
            SubscriptionStatus status = SubscriptionStatus.Unknown) {
            ProductId = productId;
            WasActive = wasActive;
            IsActive = isActive;
            Status = status;
        }
    }

    /// <summary>
    /// Server-side subscription status query result.
    /// </summary>
    public readonly struct SubscriptionStatusResult {
        public bool Success { get; }
        public SubscriptionStatus Status { get; }
        public System.DateTime? ExpirationDate { get; }
        public System.DateTime? PurchaseDate { get; }
        public System.DateTime? CancellationDate { get; }
        public bool IsAutoRenewing { get; }
        public bool IsFreeTrial { get; }
        public string ErrorMessage { get; }

        public SubscriptionStatusResult(bool success, SubscriptionStatus status,
            System.DateTime? expirationDate, System.DateTime? purchaseDate,
            System.DateTime? cancellationDate, bool isAutoRenewing, bool isFreeTrial,
            string errorMessage) {
            Success = success;
            Status = status;
            ExpirationDate = expirationDate;
            PurchaseDate = purchaseDate;
            CancellationDate = cancellationDate;
            IsAutoRenewing = isAutoRenewing;
            IsFreeTrial = isFreeTrial;
            ErrorMessage = errorMessage;
        }

        public static SubscriptionStatusResult Failed(string error) =>
            new SubscriptionStatusResult(false, SubscriptionStatus.Unknown,
                null, null, null, false, false, error);
    }

    public enum ProductType {
        Consumable,
        NonConsumable,
        Subscription
    }

    public enum PurchaseFailureReason {
        Unknown,
        UserCancelled,
        PaymentDeclined,
        ProductUnavailable,
        PurchasingUnavailable,
        ExistingPurchasePending,
        DuplicateTransaction,
        SignatureInvalid
    }

    /// <summary>
    /// Immutable product information from the store.
    /// </summary>
    public readonly struct ProductInfo {
        public string ProductId { get; }
        public string LocalizedTitle { get; }
        public string LocalizedDescription { get; }
        public string LocalizedPrice { get; }
        public decimal PriceDecimal { get; }
        public string CurrencyCode { get; }
        public ProductType Type { get; }

        public ProductInfo(string productId, string localizedTitle, string localizedDescription,
            string localizedPrice, decimal priceDecimal, string currencyCode, ProductType type) {
            ProductId = productId;
            LocalizedTitle = localizedTitle;
            LocalizedDescription = localizedDescription;
            LocalizedPrice = localizedPrice;
            PriceDecimal = priceDecimal;
            CurrencyCode = currencyCode;
            Type = type;
        }
    }

    /// <summary>
    /// Immutable purchase result.
    /// </summary>
    public readonly struct PurchaseResult {
        public bool Success { get; }
        public string ProductId { get; }
        public string TransactionId { get; }
        public string Receipt { get; }
        public string ErrorMessage { get; }
        public PurchaseFailureReason FailureReason { get; }

        public PurchaseResult(bool success, string productId, string transactionId,
            string receipt, string errorMessage, PurchaseFailureReason failureReason) {
            Success = success;
            ProductId = productId;
            TransactionId = transactionId;
            Receipt = receipt;
            ErrorMessage = errorMessage;
            FailureReason = failureReason;
        }

        public static PurchaseResult Succeeded(string productId, string transactionId, string receipt) =>
            new PurchaseResult(true, productId, transactionId, receipt, null, default);

        public static PurchaseResult Failed(string productId, string error, PurchaseFailureReason reason) =>
            new PurchaseResult(false, productId, null, null, error, reason);
    }

    /// <summary>
    /// Immutable receipt validation result.
    /// </summary>
    public readonly struct ReceiptValidationResult {
        public bool IsValid { get; }
        public string ProductId { get; }
        public string ErrorMessage { get; }
        public bool IsTestPurchase { get; }

        /// <summary>
        /// True when validation failed due to network/server issues (timeout, 5xx, rate limit)
        /// rather than an invalid receipt. These purchases should be retried later.
        /// </summary>
        public bool IsRetryable { get; }

        public ReceiptValidationResult(bool isValid, string productId, string errorMessage,
            bool isTestPurchase = false, bool isRetryable = false) {
            IsValid = isValid;
            ProductId = productId;
            ErrorMessage = errorMessage;
            IsTestPurchase = isTestPurchase;
            IsRetryable = isRetryable;
        }
    }

    /// <summary>
    /// Immutable snapshot of subscription status for a single product.
    /// Queried via IAPManager.GetSubscriptionInfo(productId).
    /// </summary>
    public readonly struct SubscriptionInfo {
        public string ProductId { get; }

        /// <summary>True when subscription is active and not expired.</summary>
        public bool IsSubscribed { get; }
        public bool IsExpired { get; }
        public bool IsCancelled { get; }
        public bool IsFreeTrial { get; }
        public bool IsIntroductoryPricePeriod { get; }
        public bool IsAutoRenewing { get; }

        public System.DateTime? ExpirationDate { get; }
        public System.DateTime? PurchaseDate { get; }
        public System.DateTime? CancellationDate { get; }
        public System.TimeSpan? RemainingTime { get; }

        /// <summary>ISO 8601 period string, e.g. "P1W" (7 days), "P1M" (1 month).</summary>
        public string SubscriptionPeriod { get; }

        /// <summary>Detailed subscription status from server. Unknown if server data not available.</summary>
        public SubscriptionStatus Status { get; }

        public SubscriptionInfo(
            string productId,
            bool isSubscribed,
            bool isExpired,
            bool isCancelled,
            bool isFreeTrial,
            bool isIntroductoryPricePeriod,
            bool isAutoRenewing,
            System.DateTime? expirationDate,
            System.DateTime? purchaseDate,
            System.DateTime? cancellationDate,
            System.TimeSpan? remainingTime,
            string subscriptionPeriod,
            SubscriptionStatus status = SubscriptionStatus.Unknown) {
            ProductId = productId;
            IsSubscribed = isSubscribed;
            IsExpired = isExpired;
            IsCancelled = isCancelled;
            IsFreeTrial = isFreeTrial;
            IsIntroductoryPricePeriod = isIntroductoryPricePeriod;
            IsAutoRenewing = isAutoRenewing;
            ExpirationDate = expirationDate;
            PurchaseDate = purchaseDate;
            CancellationDate = cancellationDate;
            RemainingTime = remainingTime;
            SubscriptionPeriod = subscriptionPeriod;
            Status = status;
        }
    }
}
