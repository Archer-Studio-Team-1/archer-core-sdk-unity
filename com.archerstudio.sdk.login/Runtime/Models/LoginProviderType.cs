namespace ArcherStudio.SDK.Login {

    /// <summary>
    /// Identifier for an auth backend exposed by <see cref="ILoginProvider"/>.
    /// Phase 6 v2 wires Firebase Auth as the hub: each value here maps to one
    /// Firebase credential type so the Firestore module can decide whether the
    /// signed-in user counts as "authenticated with provider" (cloud-save eligible)
    /// or merely anonymous.
    ///
    /// Add new values when a new provider lands. Do not reorder existing values
    /// — the enum is referenced from serialized events and persisted analytics.
    /// </summary>
    public enum LoginProviderType {
        /// <summary>No provider active (StubLoginProvider, guest mode, or pre-init).</summary>
        None = 0,

        /// <summary>Google Play Games Services. Android only.</summary>
        GooglePlayGames = 1,

        /// <summary>Generic Google account sign-in (Google Identity Services / OAuth).</summary>
        GoogleAccount = 2,

        /// <summary>Facebook Login (Facebook Unity SDK).</summary>
        Facebook = 3,

        /// <summary>Sign in with Apple. iOS first; deferred for Phase 6 v2.</summary>
        AppleSignIn = 4,
    }
}
