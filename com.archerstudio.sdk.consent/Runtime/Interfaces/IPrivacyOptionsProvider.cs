using System;

namespace ArcherStudio.SDK.Consent {

    /// <summary>
    /// A consent provider that can reopen its form for a player who already answered - what GDPR
    /// expects behind a "Privacy settings" button.
    ///
    /// Separate from <see cref="IConsentProvider"/> so adding it breaks no provider written against the
    /// older interface. <see cref="ConsentManager"/> checks for it at runtime.
    /// </summary>
    public interface IPrivacyOptionsProvider {
        /// <summary>
        /// True when this player must be offered a way to change their answer: a CMP that can reopen
        /// is linked, and the player is in a region that requires one. Meaningful only after
        /// <see cref="IConsentProvider.RequestConsent"/> has completed.
        /// </summary>
        bool IsPrivacyOptionsRequired { get; }

        /// <summary>
        /// Reopens the form. Completes with null on success, or an error message. Does not report the
        /// new status: the caller reads it from <see cref="IConsentProvider.GetCurrentStatus"/>, which
        /// must reflect the new answer by the time this completes.
        /// </summary>
        void ShowPrivacyOptions(Action<string> onComplete);
    }
}
