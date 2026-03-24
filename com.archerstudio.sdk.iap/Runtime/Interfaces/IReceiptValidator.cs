using System;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Interface for server-side or local receipt validation.
    /// </summary>
    public interface IReceiptValidator {
        void Validate(string receipt, string productId, Action<ReceiptValidationResult> onComplete);
    }
}
