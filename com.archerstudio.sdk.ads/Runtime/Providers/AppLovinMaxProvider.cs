#if HAS_APPLOVIN_MAX_SDK
using AppLovinMax;
using UnityEngine;
#endif

using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// AppLovin MAX mediation provider. Requires HAS_APPLOVIN_MAX_SDK define.
    /// </summary>
    public class AppLovinMaxProvider : IAdProvider {
        private const string Tag = "MAX";

        public string ProviderId => "applovin_max";
        public event Action<AdRevenueData> OnAdRevenuePaid;

        private AdConfig _config;
        private ConsentStatus _lastConsent = ConsentStatus.Default;

        // Pending callbacks
        private Action<AdResult> _pendingInterstitialCallback;
        private Action<AdResult> _pendingRewardedCallback;
        private Action<AdResult> _pendingAppOpenCallback;
        private string _pendingInterstitialPlacement;
        private string _pendingRewardedPlacement;
        private string _pendingAppOpenPlacement;
        private bool _rewardedUserRewarded;

        public void Initialize(AdConfig config, Action<bool> onComplete) {
            _config = config;

            #if HAS_APPLOVIN_MAX_SDK
            // If MAX was already initialized by MaxConsentProvider (privacy flow),
            // just register ad callbacks — no need to re-init the SDK.
            if (SDKInitCoordinator.IsAdSdkInitializedByConsent) {
                SDKLogger.Info(Tag, "MAX SDK already initialized by consent provider. Registering callbacks.");
                RegisterCallbacks();
                onComplete?.Invoke(true);
                return;
            }
            
            // MUST set consent flags BEFORE InitializeSdk()
            MaxSdk.SetHasUserConsent(_lastConsent.CanShowPersonalizedAds);
            MaxSdk.SetDoNotSell(_lastConsent.IsDoNotSell);
            // NOTE: facebook_limited_data_use is handled automatically by MAX when
            // UMP/TCF is integrated. MAX reads the TC string and applies LDU internally.
            
            // ─── COPPA / Age Restriction ───
            var trackingConfig = Resources.Load<ArcherStudio.SDK.Tracking.TrackingConfig>("TrackingConfig");
            if (trackingConfig != null) {
                // MaxSdk.ALPrivacySettings .SetIsAgeRestrictedUser(trackingConfig.EnableCoppaCompliance);
                SDKLogger.Info(Tag, $"AgeRestricted (COPPA): {trackingConfig.EnableCoppaCompliance}");
            }

            SDKLogger.Info(Tag,
                $"Pre-init consent: HasUserConsent={_lastConsent.CanShowPersonalizedAds}, " +
                $"DoNotSell={_lastConsent.IsDoNotSell}");

            MaxSdk.SetSdkKey(config.SdkKey);

            MaxSdkCallbacks.OnSdkInitializedEvent += sdkConfig => {
                SDKLogger.Info(Tag, "MAX SDK initialized.");
                LogMaxConsentState(sdkConfig);

                if (config.ShowMediationDebugger) {
                    MaxSdk.ShowMediationDebugger();
                }

                RegisterCallbacks();
                onComplete?.Invoke(true);
            };

            MaxSdk.InitializeSdk();
            #else
            SDKLogger.Info(Tag, "Initialized (No SDK).");
            onComplete?.Invoke(true);
            #endif
        }

        public void OnConsentChanged(ConsentStatus consent) {
            _lastConsent = consent;
            #if HAS_APPLOVIN_MAX_SDK
            MaxSdk.SetHasUserConsent(consent.CanShowPersonalizedAds);
            MaxSdk.SetDoNotSell(consent.IsDoNotSell);
            SDKLogger.Debug(Tag,
                $"Consent updated: personalized={consent.CanShowPersonalizedAds}, " +
                $"doNotSell={consent.IsDoNotSell}");
            #endif
        }

        #if HAS_APPLOVIN_MAX_SDK
        private static void LogMaxConsentState(MaxSdkBase.SdkConfiguration sdkConfig) {
            SDKLogger.Info(Tag, "┌─── MAX Consent & Mediation State ───");
            SDKLogger.Info(Tag, $"│ ConsentFlowGeography:  {sdkConfig.ConsentFlowUserGeography}");
            SDKLogger.Info(Tag, $"│ ConsentDialogState:    {sdkConfig.ConsentDialogState}");
            SDKLogger.Info(Tag, $"│ HasUserConsent:        {MaxSdk.HasUserConsent()}");
            SDKLogger.Info(Tag, $"│ IsUserConsentSet:      {MaxSdk.IsUserConsentSet()}");
            SDKLogger.Info(Tag, $"│ IsDoNotSell:           {MaxSdk.IsDoNotSell()}");
            SDKLogger.Info(Tag, $"│ IsDoNotSellSet:        {MaxSdk.IsDoNotSellSet()}");
            SDKLogger.Info(Tag, $"│ CountryCode:           {sdkConfig.CountryCode}");

            // Log available mediation adapters and their versions
            SDKLogger.Info(Tag, "│ ── Mediation Adapters ──");
            foreach (var network in MaxSdk.GetAvailableMediatedNetworks()) {
                SDKLogger.Info(Tag, $"│   {network.Name}: adapter={network.AdapterVersion}, sdk={network.SdkVersion}");
            }

            SDKLogger.Info(Tag, "└──────────────────────────────────");
        }
        #endif

        // ─── Banner ───

        public void ShowBanner(AdPlacement placement, BannerPosition position) {
            SDKLogger.Debug(Tag, $"ShowBanner: {placement.PlacementId} ({position})");
            #if HAS_APPLOVIN_MAX_SDK
            var maxPosition = position == BannerPosition.Top
                ? MaxSdkBase.BannerPosition.TopCenter
                : MaxSdkBase.BannerPosition.BottomCenter;

            MaxSdk.CreateBanner(placement.UnitId, maxPosition);
            MaxSdk.SetBannerBackgroundColor(placement.UnitId, UnityEngine.Color.clear);
            MaxSdk.ShowBanner(placement.UnitId);
            #endif
        }

        public void HideBanner(AdPlacement placement) {
            SDKLogger.Debug(Tag, $"HideBanner: {placement.PlacementId}");
            #if HAS_APPLOVIN_MAX_SDK
            MaxSdk.HideBanner(placement.UnitId);
            #endif
        }

        public void DestroyBanner(AdPlacement placement) {
            SDKLogger.Debug(Tag, $"DestroyBanner: {placement.PlacementId}");
            #if HAS_APPLOVIN_MAX_SDK
            MaxSdk.DestroyBanner(placement.UnitId);
            #endif
        }

        // ─── Interstitial ───

        public bool IsInterstitialReady(AdPlacement placement) {
            #if HAS_APPLOVIN_MAX_SDK
            return MaxSdk.IsInterstitialReady(placement.UnitId);
            #else
            return false;
            #endif
        }

        public void LoadInterstitial(AdPlacement placement) {
            SDKLogger.Debug(Tag, $"LoadInterstitial: {placement.PlacementId}");
            #if HAS_APPLOVIN_MAX_SDK
            MaxSdk.LoadInterstitial(placement.UnitId);
            #endif
        }

        public void ShowInterstitial(AdPlacement placement, Action<AdResult> onComplete) {
            SDKLogger.Info(Tag, $"ShowInterstitial: {placement.PlacementId}");
            #if HAS_APPLOVIN_MAX_SDK
            _pendingInterstitialCallback = onComplete;
            _pendingInterstitialPlacement = placement.PlacementId;
            MaxSdk.ShowInterstitial(placement.UnitId, placement.PlacementId);
            #else
            onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "No SDK."));
            #endif
        }

        // ─── Rewarded ───

        public bool IsRewardedReady(AdPlacement placement) {
            #if HAS_APPLOVIN_MAX_SDK
            return MaxSdk.IsRewardedAdReady(placement.UnitId);
            #else
            return false;
            #endif
        }

        public void LoadRewarded(AdPlacement placement) {
            SDKLogger.Debug(Tag, $"LoadRewarded: {placement.PlacementId}");
            #if HAS_APPLOVIN_MAX_SDK
            MaxSdk.LoadRewardedAd(placement.UnitId);
            #endif
        }

        public void ShowRewarded(AdPlacement placement, string trackPlacement, Action<AdResult> onComplete) {
            SDKLogger.Info(Tag, $"ShowRewarded: {placement}");
            #if HAS_APPLOVIN_MAX_SDK
            _pendingRewardedCallback = onComplete;
            _pendingRewardedPlacement = placement.PlacementId;
            _rewardedUserRewarded = false;
            #if UNITY_ANDROID
            MaxSdk.ShowRewardedAd(placement.AndroidUnitId, trackPlacement);
            #elif UNITY_IOS
            MaxSdk.ShowRewardedAd(placement.IosUnitId, trackPlacement);
            #else
            MaxSdk.ShowRewardedAd(placement.AndroidUnitId, trackPlacement);
            #endif
            
            #else
            onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "No SDK."));
            #endif
        }

        // ─── App Open ───

        public bool IsAppOpenReady(AdPlacement placement) {
            #if HAS_APPLOVIN_MAX_SDK
            return MaxSdk.IsAppOpenAdReady(placement.UnitId);
            #else
            return false;
            #endif
        }

        public void LoadAppOpen(AdPlacement placement) {
            SDKLogger.Debug(Tag, $"LoadAppOpen: {placement.PlacementId}");
            #if HAS_APPLOVIN_MAX_SDK
            MaxSdk.LoadAppOpenAd(placement.UnitId);
            #endif
        }

        public void ShowAppOpen(AdPlacement placement, Action<AdResult> onComplete) {
            SDKLogger.Info(Tag, $"ShowAppOpen: {placement.PlacementId}");
            #if HAS_APPLOVIN_MAX_SDK
            _pendingAppOpenCallback = onComplete;
            _pendingAppOpenPlacement = placement.PlacementId;
            MaxSdk.ShowAppOpenAd(placement.UnitId, placement.PlacementId);
            #else
            onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "No SDK."));
            #endif
        }

        // ─── Callbacks ───

        #if HAS_APPLOVIN_MAX_SDK
        private void RegisterCallbacks() {
            // Interstitial
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (adUnitId, adInfo) => {
                SDKLogger.Debug(Tag, $"Interstitial loaded: {adInfo.NetworkName}");
            };

            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (adUnitId, errorInfo) => {
                SDKLogger.Warning(Tag, $"Interstitial load failed: {errorInfo.Message} (code={errorInfo.Code})");
            };

            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += (adUnitId, adInfo) => {
                SDKLogger.Info(Tag, $"Interstitial displayed: {adInfo.NetworkName}");
            };

            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (adUnitId, adInfo) => {
                SDKLogger.Debug(Tag, "Interstitial hidden.");
                var cb = _pendingInterstitialCallback;
                _pendingInterstitialCallback = null;
                cb?.Invoke(AdResult.Succeeded(_pendingInterstitialPlacement));
                // Auto-reload
                MaxSdk.LoadInterstitial(adUnitId);
            };

            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (adUnitId, errorInfo, adInfo) => {
                SDKLogger.Error(Tag, $"Interstitial display failed: {errorInfo.Message} (code={errorInfo.Code})");
                var cb = _pendingInterstitialCallback;
                _pendingInterstitialCallback = null;
                cb?.Invoke(AdResult.Failed(_pendingInterstitialPlacement, errorInfo.Message));
                MaxSdk.LoadInterstitial(adUnitId);
            };

            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (adUnitId, adInfo) => {
                EmitRevenue(adInfo, "Interstitial");
            };

            // Rewarded
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (adUnitId, adInfo) => {
                SDKLogger.Debug(Tag, $"Rewarded loaded: {adInfo.NetworkName}");
            };

            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (adUnitId, errorInfo) => {
                SDKLogger.Warning(Tag, $"Rewarded load failed: {errorInfo.Message} (code={errorInfo.Code})");
            };

            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += (adUnitId, adInfo) => {
                SDKLogger.Info(Tag, $"Rewarded displayed: {adInfo.NetworkName}");
            };

            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (adUnitId, reward, adInfo) => {
                SDKLogger.Info(Tag, $"Rewarded earned: {reward.Label} x{reward.Amount}");
                _rewardedUserRewarded = true;
            };

            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (adUnitId, adInfo) => {
                SDKLogger.Debug(Tag, $"Rewarded hidden. UserRewarded={_rewardedUserRewarded}");
                var cb = _pendingRewardedCallback;
                _pendingRewardedCallback = null;

                if (_rewardedUserRewarded) {
                    cb?.Invoke(AdResult.Rewarded(_pendingRewardedPlacement,
                        new RewardData("reward", 1)));
                } else {
                    cb?.Invoke(AdResult.Succeeded(_pendingRewardedPlacement));
                }

                MaxSdk.LoadRewardedAd(adUnitId);
            };

            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (adUnitId, errorInfo, adInfo) => {
                SDKLogger.Error(Tag, $"Rewarded display failed: {errorInfo.Message} (code={errorInfo.Code})");
                var cb = _pendingRewardedCallback;
                _pendingRewardedCallback = null;
                cb?.Invoke(AdResult.Failed(_pendingRewardedPlacement, errorInfo.Message));
                MaxSdk.LoadRewardedAd(adUnitId);
            };

            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (adUnitId, adInfo) => {
                EmitRevenue(adInfo, "Rewarded");
            };

            // Banner
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += (adUnitId, adInfo) => {
                EmitRevenue(adInfo, "Banner");
            };

            // App Open
            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += (adUnitId, adInfo) => {
                SDKLogger.Debug(Tag, $"AppOpen loaded: {adInfo.NetworkName}");
            };

            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += (adUnitId, errorInfo) => {
                SDKLogger.Warning(Tag, $"AppOpen load failed: {errorInfo.Message} (code={errorInfo.Code})");
            };

            MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent += (adUnitId, adInfo) => {
                SDKLogger.Info(Tag, $"AppOpen displayed: {adInfo.NetworkName}");
            };

            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += (adUnitId, adInfo) => {
                SDKLogger.Debug(Tag, "AppOpen hidden.");
                var cb = _pendingAppOpenCallback;
                _pendingAppOpenCallback = null;
                cb?.Invoke(AdResult.Succeeded(_pendingAppOpenPlacement));
                MaxSdk.LoadAppOpenAd(adUnitId);
            };

            MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += (adUnitId, errorInfo, adInfo) => {
                SDKLogger.Error(Tag, $"AppOpen display failed: {errorInfo.Message} (code={errorInfo.Code})");
                var cb = _pendingAppOpenCallback;
                _pendingAppOpenCallback = null;
                cb?.Invoke(AdResult.Failed(_pendingAppOpenPlacement, errorInfo.Message));
                MaxSdk.LoadAppOpenAd(adUnitId);
            };

            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += (adUnitId, adInfo) => {
                EmitRevenue(adInfo, "AppOpen");
            };
        }

        private void EmitRevenue(MaxSdkBase.AdInfo adInfo, string format) {
            SDKLogger.Debug(Tag,
                $"Revenue: {format} | {adInfo.NetworkName} | ${adInfo.Revenue:F6} USD");
            var revenueData = new AdRevenueData(
                adPlatform: "applovin_max_sdk", // Standard identifier for Adjust automatic cost matching
                adSource: adInfo.NetworkName,
                adFormat: format,
                adUnitName: adInfo.AdUnitIdentifier,
                currency: "USD",
                value: adInfo.Revenue,
                placement: adInfo.Placement);

            OnAdRevenuePaid?.Invoke(revenueData);
        }
        #endif
    }
}
