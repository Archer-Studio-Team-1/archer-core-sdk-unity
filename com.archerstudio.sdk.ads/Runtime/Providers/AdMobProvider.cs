using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// Google AdMob direct provider stub (without mediation).
    /// Requires HAS_ADMOB_SDK define and com.google.ads.mobile package.
    /// TODO: Implement when standalone AdMob integration is needed.
    /// </summary>
    public class AdMobProvider : IAdProvider {
        public string ProviderId => "admob";
        public event Action<AdRevenueData> OnAdRevenuePaid;

        public void Initialize(AdConfig config, Action<bool> onComplete) {
            SDKLogger.Warning("AdMob", "AdMob provider not yet implemented.");
            onComplete?.Invoke(false);
        }

        public void OnConsentChanged(ConsentStatus consent) { }

        public void ShowBanner(AdPlacement placement, BannerPosition position) { }
        public void HideBanner(AdPlacement placement) { }
        public void DestroyBanner(AdPlacement placement) { }

        public bool IsInterstitialReady(AdPlacement placement) => false;
        public void LoadInterstitial(AdPlacement placement) { }
        public void ShowInterstitial(AdPlacement placement, Action<AdResult> onComplete) {
            onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "Not implemented."));
        }

        public bool IsRewardedReady(AdPlacement placement) => false;
        public void LoadRewarded(AdPlacement placement) { }
        public void ShowRewarded(AdPlacement placement, string trackPlacement, Action<AdResult> onComplete) {
            onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "Not implemented."));
        }

        public bool IsAppOpenReady(AdPlacement placement) => false;
        public void LoadAppOpen(AdPlacement placement) { }
        public void ShowAppOpen(AdPlacement placement, Action<AdResult> onComplete) {
            onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "Not implemented."));
        }
    }
}
