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
}
