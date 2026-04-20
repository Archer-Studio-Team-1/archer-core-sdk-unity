namespace ArcherStudio.SDK.IAP {

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

        public ReceiptValidationResult(bool isValid, string productId, string errorMessage) {
            IsValid = isValid;
            ProductId = productId;
            ErrorMessage = errorMessage;
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
            string subscriptionPeriod) {
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
        }
    }
}
