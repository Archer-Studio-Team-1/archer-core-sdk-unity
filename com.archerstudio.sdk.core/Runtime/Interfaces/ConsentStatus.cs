namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Immutable value object representing user consent state.
    /// Maps 1:1 to Google Consent Mode v2 types:
    ///   CanCollectAnalytics   → ANALYTICS_STORAGE
    ///   CanStoreAdData        → AD_STORAGE        (TCF Purpose 1)
    ///   CanTrackAttribution   → AD_USER_DATA      (TCF Purpose 1, 7)
    ///   CanShowPersonalizedAds → AD_PERSONALIZATION (TCF Purpose 3, 4)
    /// </summary>
    public readonly struct ConsentStatus {
        /// <summary>Consent for personalized ads (TCF Purpose 3, 4 → ad_personalization).</summary>
        public bool CanShowPersonalizedAds { get; }

        /// <summary>Consent for analytics data collection (→ analytics_storage).</summary>
        public bool CanCollectAnalytics { get; }

        /// <summary>Consent for advertiser ID / attribution (TCF Purpose 1, 7 → ad_user_data).</summary>
        public bool CanTrackAttribution { get; }

        /// <summary>Consent for ad cookie/data storage (TCF Purpose 1 → ad_storage).</summary>
        public bool CanStoreAdData { get; }

        public bool IsEeaUser { get; }
        public bool HasAttConsent { get; }

        /// <summary>
        /// CCPA Do Not Sell flag. When true, user has opted out of data sale.
        /// Used for Facebook LDU (Limited Data Use) and mediation SDK DoNotSell.
        /// </summary>
        public bool IsDoNotSell { get; }

        public ConsentSource Source { get; }

        public ConsentStatus(
            bool canShowPersonalizedAds,
            bool canCollectAnalytics,
            bool canTrackAttribution,
            bool isEeaUser,
            bool hasAttConsent,
            ConsentSource source,
            bool isDoNotSell = false,
            bool canStoreAdData = true) {
            CanShowPersonalizedAds = canShowPersonalizedAds;
            CanCollectAnalytics = canCollectAnalytics;
            CanTrackAttribution = canTrackAttribution;
            CanStoreAdData = canStoreAdData;
            IsEeaUser = isEeaUser;
            HasAttConsent = hasAttConsent;
            IsDoNotSell = isDoNotSell;
            Source = source;
        }

        /// <summary>
        /// Default consent: all granted, non-EEA user, not do-not-sell.
        /// Used before consent dialog is shown.
        /// </summary>
        public static ConsentStatus Default => new ConsentStatus(
            canShowPersonalizedAds: true,
            canCollectAnalytics: true,
            canTrackAttribution: true,
            isEeaUser: false,
            hasAttConsent: true,
            source: ConsentSource.Default,
            isDoNotSell: false,
            canStoreAdData: true);

        /// <summary>
        /// Bridge to legacy SetConsent(bool granted, bool isEeaUser) API.
        /// </summary>
        public static ConsentStatus FromLegacy(bool granted, bool isEeaUser) {
            return new ConsentStatus(
                canShowPersonalizedAds: granted,
                canCollectAnalytics: granted,
                canTrackAttribution: granted,
                isEeaUser: isEeaUser,
                hasAttConsent: granted,
                source: ConsentSource.Manual,
                isDoNotSell: !granted,
                canStoreAdData: granted);
        }

        public override string ToString() {
            return $"[Consent ads={CanShowPersonalizedAds} analytics={CanCollectAnalytics} " +
                   $"attribution={CanTrackAttribution} adStorage={CanStoreAdData} " +
                   $"eea={IsEeaUser} att={HasAttConsent} doNotSell={IsDoNotSell} source={Source}]";
        }
    }

    public enum ConsentSource {
        Default,
        GoogleUMP,
        Manual,
        ATT,
        AppLovinMax
    }
}
