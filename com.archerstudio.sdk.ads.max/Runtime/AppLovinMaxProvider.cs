#if HAS_APPLOVIN_MAX_SDK
using AppLovinMax;
using UnityEngine;
#endif

using System;
using System.Collections.Generic;
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

        // False until someone hands over consent. Then MAX gets no consent flags at all and applies its
        // own regional defaults, instead of an explicit "consented" read from ConsentStatus.Default.
        private bool _consentSupplied;

        // Pending callbacks
        private Action<AdResult> _pendingInterstitialCallback;
        private Action<AdResult> _pendingRewardedCallback;
        private Action<AdResult> _pendingAppOpenCallback;
        private string _pendingInterstitialPlacement;
        private string _pendingRewardedPlacement;
        private string _pendingAppOpenPlacement;
        private bool _rewardedUserRewarded;

        #if HAS_APPLOVIN_MAX_SDK
        // Load retry. MAX does not retry a failed load on its own, and nothing else does
        // either: without this, one failed load at boot (no fill, no network) leaves that
        // format unavailable for the whole session.
        private const int MaxRetryAttempts = 6;
        private const float MaxRetryDelaySeconds = 64f;
        private readonly Dictionary<string, int> _retryAttempts = new Dictionary<string, int>();
        #endif

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
            if (_consentSupplied) {
                MaxSdk.SetHasUserConsent(_lastConsent.CanShowPersonalizedAds);
                MaxSdk.SetDoNotSell(_lastConsent.IsDoNotSell);
            }
            // NOTE: facebook_limited_data_use is handled automatically by MAX when
            // UMP/TCF is integrated. MAX reads the TC string and applies LDU internally.
            
            // COPPA used to be read here from Resources/TrackingConfig, purely to log it - the
            // MaxSdk call that would have applied it was commented out. Reading another module's
            // config meant this package depended on com.archerstudio.sdk.tracking, which defeats
            // the point of shipping mediation on its own. When age restriction is actually needed,
            // it belongs in ConsentStatus, which already reaches this provider before init.

            SDKLogger.Info(Tag, _consentSupplied
                ? $"Pre-init consent: HasUserConsent={_lastConsent.CanShowPersonalizedAds}, " +
                  $"DoNotSell={_lastConsent.IsDoNotSell}"
                : "Pre-init consent: none supplied, MAX applies its own defaults.");

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
            _consentSupplied = true;
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
            SDKLogger.Info(Tag, $"ShowRewarded: {placement.PlacementId}");
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
                ClearLoadRetry(adUnitId);
            };

            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (adUnitId, errorInfo) => {
                SDKLogger.Warning(Tag, $"Interstitial load failed: {errorInfo.Message} (code={errorInfo.Code})");
                ScheduleLoadRetry(adUnitId, "Interstitial", () => MaxSdk.LoadInterstitial(adUnitId));
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
                ClearLoadRetry(adUnitId);
            };

            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (adUnitId, errorInfo) => {
                SDKLogger.Warning(Tag, $"Rewarded load failed: {errorInfo.Message} (code={errorInfo.Code})");
                ScheduleLoadRetry(adUnitId, "Rewarded", () => MaxSdk.LoadRewardedAd(adUnitId));
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
                ClearLoadRetry(adUnitId);
            };

            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += (adUnitId, errorInfo) => {
                SDKLogger.Warning(Tag, $"AppOpen load failed: {errorInfo.Message} (code={errorInfo.Code})");
                ScheduleLoadRetry(adUnitId, "AppOpen", () => MaxSdk.LoadAppOpenAd(adUnitId));
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

        /// <summary>
        /// Schedule another load attempt with exponential backoff (2^n seconds, capped).
        /// Gives up after <see cref="MaxRetryAttempts"/> so a bad ad unit or a long offline
        /// stretch cannot turn into an endless load loop; the next explicit Load call resets it.
        /// </summary>
        private void ScheduleLoadRetry(string adUnitId, string format, Action load) {
            _retryAttempts.TryGetValue(adUnitId, out var attempt);
            attempt++;

            if (attempt > MaxRetryAttempts) {
                SDKLogger.Warning(Tag,
                    $"{format} load failed {MaxRetryAttempts} times for {adUnitId}. " +
                    "Giving up until the next explicit load.");
                return;
            }

            _retryAttempts[adUnitId] = attempt;

            var delay = Math.Min(Math.Pow(2, attempt), MaxRetryDelaySeconds);
            SDKLogger.Info(Tag,
                $"{format} load retry {attempt}/{MaxRetryAttempts} in {delay:F0}s ({adUnitId}).");

            var dispatcher = UnityMainThreadDispatcher.Instance;
            if (dispatcher == null) {
                SDKLogger.Warning(Tag, "UnityMainThreadDispatcher unavailable. Retry skipped.");
                return;
            }

            // EnqueueDelayed starts a coroutine, so it has to run on the main thread. MAX
            // delivers its callbacks there, but hop through the thread-safe queue otherwise
            // rather than trust that — this SDK has already been bitten once by a vendor
            // callback arriving on a worker thread.
            if (UnityMainThreadDispatcher.IsMainThread()) {
                dispatcher.EnqueueDelayed((float)delay, load);
            } else {
                dispatcher.Enqueue(() => dispatcher.EnqueueDelayed((float)delay, load));
            }
        }

        private void ClearLoadRetry(string adUnitId) {
            _retryAttempts.Remove(adUnitId);
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
