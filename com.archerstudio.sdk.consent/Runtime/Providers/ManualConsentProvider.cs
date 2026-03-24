using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Consent {

    /// <summary>
    /// Manual/default consent provider.
    /// Returns all-granted consent immediately.
    /// Use for games with custom consent UI or regions without consent requirements.
    /// </summary>
    public class ManualConsentProvider : IConsentProvider {
        public bool IsConsentRequired => false;

        public void RequestConsent(Action<ConsentStatus> onComplete) {
            onComplete?.Invoke(ConsentStatus.Default);
        }

        public ConsentStatus GetCurrentStatus() {
            return ConsentStatus.Default;
        }

        public void ResetConsent() {
            // Nothing to reset for manual provider.
        }
    }
}
