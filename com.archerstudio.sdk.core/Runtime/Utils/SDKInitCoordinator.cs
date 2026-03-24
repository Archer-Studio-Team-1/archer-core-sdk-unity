namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Coordination point for SDK subsystems that share initialization state.
    /// When MAX consent provider initializes MAX SDK during consent phase,
    /// the ad module checks this flag to avoid double-initialization.
    /// </summary>
    public static class SDKInitCoordinator {

        /// <summary>
        /// Set to true when a consent provider has already initialized the ad SDK
        /// (e.g., MAX handles consent + init in one step).
        /// Ad providers check this to skip re-initialization.
        /// </summary>
        public static bool IsAdSdkInitializedByConsent { get; set; }

        /// <summary>
        /// Set to true when consent detected non-GDPR region.
        /// After MAX SDK initializes, AdManager should show CMP Terms/Privacy dialog.
        /// Flow: GoogleUMP detects non-GDPR → returns default consent → SDK init continues →
        ///       AdManager initializes MAX → checks this flag → shows MAX CMP Terms/Privacy.
        /// </summary>
        public static bool NeedsPostInitTermsAndPolicy { get; set; }
    }
}
