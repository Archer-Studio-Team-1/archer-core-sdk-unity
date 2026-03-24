using System;

namespace ArcherStudio.SDK.Ads {

    public enum AdFormat {
        Banner,
        Interstitial,
        Rewarded,
        AppOpen,
        Native
    }

    public enum BannerPosition {
        Top,
        Bottom
    }

    public enum AdMediationPlatform {
        AppLovinMax,
        IronSource,
        AdMob
    }

    /// <summary>
    /// Immutable ad revenue data. Bridges to tracking via AdRevenueTracker.
    /// </summary>
    public readonly struct AdRevenueData {
        public string AdPlatform { get; }
        public string AdSource { get; }
        public string AdFormat { get; }
        public string AdUnitName { get; }
        public string Currency { get; }
        public double Value { get; }
        public string Placement { get; }

        public AdRevenueData(string adPlatform, string adSource, string adFormat,
            string adUnitName, string currency, double value, string placement) {
            AdPlatform = adPlatform;
            AdSource = adSource;
            AdFormat = adFormat;
            AdUnitName = adUnitName;
            Currency = currency;
            Value = value;
            Placement = placement;
        }
    }

    /// <summary>
    /// Reward granted to user after watching rewarded ad.
    /// </summary>
    public readonly struct RewardData {
        public string Type { get; }
        public int Amount { get; }

        public RewardData(string type, int amount) {
            Type = type;
            Amount = amount;
        }
    }

    /// <summary>
    /// Result returned after showing an ad.
    /// </summary>
    public readonly struct AdResult {
        public bool Success { get; }
        public bool WasRewarded { get; }
        public RewardData Reward { get; }
        public string Error { get; }
        public string PlacementId { get; }

        public AdResult(bool success, bool wasRewarded, RewardData reward,
            string error, string placementId) {
            Success = success;
            WasRewarded = wasRewarded;
            Reward = reward;
            Error = error;
            PlacementId = placementId;
        }

        public static AdResult Succeeded(string placementId) =>
            new AdResult(true, false, default, null, placementId);

        public static AdResult Rewarded(string placementId, RewardData reward) =>
            new AdResult(true, true, reward, null, placementId);

        public static AdResult Failed(string placementId, string error) =>
            new AdResult(false, false, default, error, placementId);
    }

    /// <summary>
    /// Defines a logical ad placement mapped to platform-specific ad unit IDs.
    /// </summary>
    [Serializable]
    public class AdPlacement {
        public string PlacementId;
        public AdFormat Format;
        public string AndroidUnitId;
        public string IosUnitId;
        public bool AutoLoad = true;

        public string UnitId {
            get {
                #if UNITY_ANDROID
                return AndroidUnitId;
                #elif UNITY_IOS
                return IosUnitId;
                #else
                return AndroidUnitId;
                #endif
            }
        }
    }
}
