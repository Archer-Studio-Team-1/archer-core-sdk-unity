using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Consent {

    /// <summary>
    /// Interface for consent dialog providers (Google UMP, custom UI, etc.)
    /// </summary>
    public interface IConsentProvider {
        /// <summary>
        /// Request user consent. Shows dialog if needed.
        /// Callback returns the resulting ConsentStatus.
        /// </summary>
        void RequestConsent(Action<ConsentStatus> onComplete);

        /// <summary>
        /// Get current cached consent status without showing a dialog.
        /// </summary>
        ConsentStatus GetCurrentStatus();

        /// <summary>
        /// Whether this region/user requires a consent dialog.
        /// </summary>
        bool IsConsentRequired { get; }

        /// <summary>
        /// Reset consent (for testing or when user requests it from settings).
        /// </summary>
        void ResetConsent();
    }
}
