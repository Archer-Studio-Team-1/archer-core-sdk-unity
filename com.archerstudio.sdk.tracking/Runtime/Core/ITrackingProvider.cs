using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Tracking {

    /// <summary>
    /// Interface for tracking providers (Firebase, Adjust, etc.)
    /// Extended from legacy ITrackingProvider with ProviderId and consent support.
    /// </summary>
    public interface ITrackingProvider {
        string ProviderId { get; }
        void Initialize(Action<bool> onInitialized = null);

        /// <summary>
        /// Track general game events. Do NOT use for ad revenue or IAP revenue.
        /// Ad revenue → <see cref="TrackAdRevenue"/>
        /// IAP revenue → <see cref="TrackIAPRevenue"/>
        /// </summary>
        void TrackEvent(GameTrackingEvent gameEvent);

        /// <summary>
        /// Track ad revenue from ad mediation platforms (AppLovin MAX, IronSource, AdMob, etc.).
        /// - Firebase: logs "ad_impression" event with parameters
        /// - Adjust: uses AdjustAdRevenue API for proper attribution
        /// </summary>
        void TrackAdRevenue(string adPlatform, string adSource, string adFormat,
            string adUnitName, string currency, double value, string placement);

        /// <summary>
        /// Track IAP revenue from successful in-app purchases.
        /// Each provider handles this internally:
        /// - Firebase: logs "in_app_purchase" event with revenue parameters
        /// - Adjust: verifies receipt and tracks revenue via VerifyAndTrack*Purchase
        /// </summary>
        void TrackIAPRevenue(string productId, double revenue, string currency,
            string transactionId, string receipt, string source);

        void SetUserId(string userId);
        void SetUserProperty(string key, string value);
        void SetConsent(ConsentStatus consent);
    }
}
