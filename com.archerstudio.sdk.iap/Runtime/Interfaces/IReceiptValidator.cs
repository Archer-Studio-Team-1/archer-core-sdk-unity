using System;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Interface for server-side or local receipt validation.
    /// </summary>
    public interface IReceiptValidator {
        void Validate(string receipt, string productId, Action<ReceiptValidationResult> onComplete);

        /// <summary>
        /// Query the server for current subscription status.
        /// Returns detailed state (active, cancelled, grace period, etc.) and expiration date.
        /// </summary>
        void QuerySubscriptionStatus(string purchaseToken, string transactionId,
            string productId, Action<SubscriptionStatusResult> onComplete);
    }
}
