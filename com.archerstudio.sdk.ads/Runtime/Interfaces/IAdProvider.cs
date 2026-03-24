using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// Interface for ad mediation providers (AppLovin MAX, IronSource, AdMob).
    /// </summary>
    public interface IAdProvider {
        string ProviderId { get; }
        void Initialize(AdConfig config, Action<bool> onComplete);
        void OnConsentChanged(ConsentStatus consent);

        // ─── Banner ───
        void ShowBanner(AdPlacement placement, BannerPosition position);
        void HideBanner(AdPlacement placement);
        void DestroyBanner(AdPlacement placement);

        // ─── Interstitial ───
        bool IsInterstitialReady(AdPlacement placement);
        void LoadInterstitial(AdPlacement placement);
        void ShowInterstitial(AdPlacement placement, Action<AdResult> onComplete);

        // ─── Rewarded ───
        bool IsRewardedReady(AdPlacement placement);
        void LoadRewarded(AdPlacement placement);
        void ShowRewarded(AdPlacement placement, string trackPlacement, Action<AdResult> onComplete);

        // ─── App Open ───
        bool IsAppOpenReady(AdPlacement placement);
        void LoadAppOpen(AdPlacement placement);
        void ShowAppOpen(AdPlacement placement, Action<AdResult> onComplete);

        // ─── Revenue ───
        event Action<AdRevenueData> OnAdRevenuePaid;
    }
}
