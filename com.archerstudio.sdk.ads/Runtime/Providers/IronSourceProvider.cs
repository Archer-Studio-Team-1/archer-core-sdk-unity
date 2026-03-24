using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// IronSource / Unity LevelPlay mediation provider stub.
    /// Requires HAS_IRONSOURCE_SDK define and com.unity.services.levelplay package.
    /// TODO: Implement when IronSource integration is needed.
    /// </summary>
    public class IronSourceProvider : IAdProvider {
        public string ProviderId => "ironsource";
        public event Action<AdRevenueData> OnAdRevenuePaid;

        public void Initialize(AdConfig config, Action<bool> onComplete) {
            SDKLogger.Warning("IronSource", "IronSource provider not yet implemented.");
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
