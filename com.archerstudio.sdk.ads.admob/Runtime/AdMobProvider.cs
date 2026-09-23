#if HAS_ADMOB_SDK
using GoogleMobileAds.Api;
using UnityEngine;
#endif

using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// Google AdMob provider. Requires HAS_ADMOB_SDK (com.google.ads.mobile).
    ///
    /// Works the same whether AdMob serves its own demand or mediates other networks: mediation is
    /// configured in the AdMob console and through adapter packages, not here.
    /// </summary>
    public class AdMobProvider : IAdProvider {
        private const string Tag = "AdMob";

        public string ProviderId => "admob";

        #pragma warning disable 67 // Raised only in the HAS_ADMOB_SDK build; without the plugin nothing earns.
        public event Action<AdRevenueData> OnAdRevenuePaid;
        #pragma warning restore 67

        #if HAS_ADMOB_SDK
        // Load retry. AdMob does not retry a failed load either, and one failure at boot would
        // otherwise leave a format unavailable for the whole session.
        private const int MaxRetryAttempts = 6;
        private const float MaxRetryDelaySeconds = 64f;

        private readonly Dictionary<string, InterstitialAd> _interstitials =
            new Dictionary<string, InterstitialAd>();
        private readonly Dictionary<string, RewardedAd> _rewarded =
            new Dictionary<string, RewardedAd>();
        private readonly Dictionary<string, AppOpenAd> _appOpen =
            new Dictionary<string, AppOpenAd>();
        private readonly Dictionary<string, BannerView> _banners =
            new Dictionary<string, BannerView>();
        private readonly Dictionary<string, int> _retryAttempts = new Dictionary<string, int>();

        private ConsentStatus _lastConsent = ConsentStatus.Default;
        private bool _initialized;

        // ─── Lifecycle ───

        public void Initialize(AdConfig config, Action<bool> onComplete) {
            SDKLogger.Info(Tag, "Initializing Google Mobile Ads...");

            // Android pauses Unity behind a full-screen ad; make iOS behave the same so game code has
            // one behaviour to reason about.
            MobileAds.SetiOSAppPauseOnBackground(true);

            MobileAds.Initialize(status => {
                if (status == null) {
                    SDKLogger.Error(Tag, "Google Mobile Ads failed to initialize.");
                    onComplete?.Invoke(false);
                    return;
                }

                _initialized = true;
                LogAdapters(status);
                onComplete?.Invoke(true);
            });
        }

        public void OnConsentChanged(ConsentStatus consent) {
            _lastConsent = consent;

            // AdMob has no setter for this: personalization is decided per request, so the next
            // AdRequest carries it. Consent that changes mid-session therefore applies to the next ad
            // loaded, not to one already in hand.
            SDKLogger.Debug(Tag,
                $"Consent updated: personalized={consent.CanShowPersonalizedAds}, " +
                $"doNotSell={consent.IsDoNotSell}");
        }

        // ─── Banner ───

        public void ShowBanner(AdPlacement placement, BannerPosition position) {
            var unitId = placement.UnitId;
            if (string.IsNullOrEmpty(unitId)) return;

            if (!_banners.TryGetValue(unitId, out var banner) || banner == null) {
                banner = new BannerView(unitId, AdSize.Banner,
                    position == BannerPosition.Top ? AdPosition.Top : AdPosition.Bottom);

                banner.OnAdPaid += value => EmitRevenue(value, "Banner", unitId, placement.PlacementId);
                banner.OnBannerAdLoadFailed += error =>
                    SDKLogger.Warning(Tag, $"Banner load failed: {error}");

                _banners[unitId] = banner;
                banner.LoadAd(BuildRequest());
            }

            banner.Show();
        }

        public void HideBanner(AdPlacement placement) {
            if (_banners.TryGetValue(placement.UnitId ?? string.Empty, out var banner)) {
                banner?.Hide();
            }
        }

        public void DestroyBanner(AdPlacement placement) {
            var unitId = placement.UnitId ?? string.Empty;
            if (!_banners.TryGetValue(unitId, out var banner)) return;

            banner?.Destroy();
            _banners.Remove(unitId);
        }

        // ─── Interstitial ───

        public bool IsInterstitialReady(AdPlacement placement) {
            return _interstitials.TryGetValue(placement.UnitId ?? string.Empty, out var ad)
                   && ad != null && ad.CanShowAd();
        }

        public void LoadInterstitial(AdPlacement placement) {
            var unitId = placement.UnitId;
            if (!CanLoad(unitId)) return;

            InterstitialAd.Load(unitId, BuildRequest(), (ad, error) => {
                if (error != null || ad == null) {
                    SDKLogger.Warning(Tag, $"Interstitial load failed: {error}");
                    ScheduleLoadRetry(unitId, "Interstitial", () => LoadInterstitial(placement));
                    return;
                }

                ClearLoadRetry(unitId);
                _interstitials[unitId] = ad;
                ad.OnAdPaid += value =>
                    EmitRevenue(value, "Interstitial", unitId, placement.PlacementId);

                SDKLogger.Debug(Tag, $"Interstitial loaded: {unitId}");
            });
        }

        public void ShowInterstitial(AdPlacement placement, Action<AdResult> onComplete) {
            var unitId = placement.UnitId ?? string.Empty;

            if (!_interstitials.TryGetValue(unitId, out var ad) || ad == null || !ad.CanShowAd()) {
                onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "No interstitial ready."));
                LoadInterstitial(placement);
                return;
            }

            _interstitials.Remove(unitId);
            var settled = false;

            ad.OnAdFullScreenContentClosed += () => {
                if (settled) return;
                settled = true;

                ad.Destroy();
                onComplete?.Invoke(AdResult.Succeeded(placement.PlacementId));
                LoadInterstitial(placement);
            };

            ad.OnAdFullScreenContentFailed += error => {
                if (settled) return;
                settled = true;

                SDKLogger.Error(Tag, $"Interstitial display failed: {error}");
                ad.Destroy();
                onComplete?.Invoke(AdResult.Failed(placement.PlacementId, error?.GetMessage()));
                LoadInterstitial(placement);
            };

            ad.Show();
        }

        // ─── Rewarded ───

        public bool IsRewardedReady(AdPlacement placement) {
            return _rewarded.TryGetValue(placement.UnitId ?? string.Empty, out var ad)
                   && ad != null && ad.CanShowAd();
        }

        public void LoadRewarded(AdPlacement placement) {
            var unitId = placement.UnitId;
            if (!CanLoad(unitId)) return;

            RewardedAd.Load(unitId, BuildRequest(), (ad, error) => {
                if (error != null || ad == null) {
                    SDKLogger.Warning(Tag, $"Rewarded load failed: {error}");
                    ScheduleLoadRetry(unitId, "Rewarded", () => LoadRewarded(placement));
                    return;
                }

                ClearLoadRetry(unitId);
                _rewarded[unitId] = ad;
                ad.OnAdPaid += value => EmitRevenue(value, "Rewarded", unitId, placement.PlacementId);

                SDKLogger.Debug(Tag, $"Rewarded loaded: {unitId}");
            });
        }

        public void ShowRewarded(AdPlacement placement, string trackPlacement,
            Action<AdResult> onComplete) {

            var unitId = placement.UnitId ?? string.Empty;

            if (!_rewarded.TryGetValue(unitId, out var ad) || ad == null || !ad.CanShowAd()) {
                onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "No rewarded ad ready."));
                LoadRewarded(placement);
                return;
            }

            _rewarded.Remove(unitId);
            var settled = false;
            RewardData? earned = null;

            ad.OnAdFullScreenContentClosed += () => {
                if (settled) return;
                settled = true;

                ad.Destroy();

                // The reward callback fires before the ad closes; a player who backed out early never
                // triggers it, and that difference is the whole point of the rewarded format.
                onComplete?.Invoke(earned.HasValue
                    ? AdResult.Rewarded(placement.PlacementId, earned.Value)
                    : AdResult.Succeeded(placement.PlacementId));

                LoadRewarded(placement);
            };

            ad.OnAdFullScreenContentFailed += error => {
                if (settled) return;
                settled = true;

                SDKLogger.Error(Tag, $"Rewarded display failed: {error}");
                ad.Destroy();
                onComplete?.Invoke(AdResult.Failed(placement.PlacementId, error?.GetMessage()));
                LoadRewarded(placement);
            };

            ad.Show(reward => {
                earned = new RewardData(reward.Type, (int)reward.Amount);
                SDKLogger.Info(Tag, $"Rewarded earned: {reward.Type} x{reward.Amount}");
            });
        }

        // ─── App Open ───

        public bool IsAppOpenReady(AdPlacement placement) {
            return _appOpen.TryGetValue(placement.UnitId ?? string.Empty, out var ad)
                   && ad != null && ad.CanShowAd();
        }

        public void LoadAppOpen(AdPlacement placement) {
            var unitId = placement.UnitId;
            if (!CanLoad(unitId)) return;

            AppOpenAd.Load(unitId, BuildRequest(), (ad, error) => {
                if (error != null || ad == null) {
                    SDKLogger.Warning(Tag, $"AppOpen load failed: {error}");
                    ScheduleLoadRetry(unitId, "AppOpen", () => LoadAppOpen(placement));
                    return;
                }

                ClearLoadRetry(unitId);
                _appOpen[unitId] = ad;
                ad.OnAdPaid += value => EmitRevenue(value, "AppOpen", unitId, placement.PlacementId);

                SDKLogger.Debug(Tag, $"AppOpen loaded: {unitId}");
            });
        }

        public void ShowAppOpen(AdPlacement placement, Action<AdResult> onComplete) {
            var unitId = placement.UnitId ?? string.Empty;

            if (!_appOpen.TryGetValue(unitId, out var ad) || ad == null || !ad.CanShowAd()) {
                onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "No app open ad ready."));
                LoadAppOpen(placement);
                return;
            }

            _appOpen.Remove(unitId);
            var settled = false;

            ad.OnAdFullScreenContentClosed += () => {
                if (settled) return;
                settled = true;

                ad.Destroy();
                onComplete?.Invoke(AdResult.Succeeded(placement.PlacementId));
                LoadAppOpen(placement);
            };

            ad.OnAdFullScreenContentFailed += error => {
                if (settled) return;
                settled = true;

                SDKLogger.Error(Tag, $"AppOpen display failed: {error}");
                ad.Destroy();
                onComplete?.Invoke(AdResult.Failed(placement.PlacementId, error?.GetMessage()));
                LoadAppOpen(placement);
            };

            ad.Show();
        }

        // ─── Internals ───

        private bool CanLoad(string unitId) {
            if (!_initialized) {
                SDKLogger.Warning(Tag, "Load requested before initialization. Ignored.");
                return false;
            }

            if (string.IsNullOrEmpty(unitId)) {
                SDKLogger.Warning(Tag, "Placement has no ad unit for this platform. Ignored.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// One request per load, carrying the consent in force right now. AdMob decides
        /// personalization per request rather than through a global setter, so this is the only place
        /// consent reaches the network.
        /// </summary>
        private AdRequest BuildRequest() {
            var request = new AdRequest();

            if (!_lastConsent.CanShowPersonalizedAds) {
                // The npa extra is how the Unity plugin asks for non-personalized ads. A project that
                // runs UMP can leave consent to it, and this stays consistent with that decision.
                request.Extras.Add("npa", "1");
            }

            return request;
        }

        private void EmitRevenue(AdValue adValue, string format, string unitId, string placementId) {
            if (adValue == null) return;

            // AdValue.Value is in micros of the currency; AdRevenueData wants whole units.
            var value = adValue.Value / 1_000_000d;

            SDKLogger.Debug(Tag, $"Revenue: {format} | {adValue.CurrencyCode} {value:F6}");

            OnAdRevenuePaid?.Invoke(new AdRevenueData(
                adPlatform: "admob", // Identifier attribution tools match AdMob spend against.
                adSource: "admob",   // With mediation, the adapter name would come from ResponseInfo.
                adFormat: format,
                adUnitName: unitId,
                currency: adValue.CurrencyCode,
                value: value,
                placement: placementId));
        }

        private void ScheduleLoadRetry(string unitId, string format, Action load) {
            _retryAttempts.TryGetValue(unitId, out var attempt);
            attempt++;

            if (attempt > MaxRetryAttempts) {
                SDKLogger.Warning(Tag,
                    $"{format} load failed {MaxRetryAttempts} times for {unitId}. " +
                    "Giving up until the next explicit load.");
                return;
            }

            _retryAttempts[unitId] = attempt;

            var delay = Math.Min(Math.Pow(2, attempt), MaxRetryDelaySeconds);
            SDKLogger.Info(Tag,
                $"{format} load retry {attempt}/{MaxRetryAttempts} in {delay:F0}s ({unitId}).");

            var dispatcher = UnityMainThreadDispatcher.Instance;
            if (dispatcher == null) {
                SDKLogger.Warning(Tag, "UnityMainThreadDispatcher unavailable. Retry skipped.");
                return;
            }

            // EnqueueDelayed starts a coroutine, so it has to run on the main thread.
            if (UnityMainThreadDispatcher.IsMainThread()) {
                dispatcher.EnqueueDelayed((float)delay, load);
            } else {
                dispatcher.Enqueue(() => dispatcher.EnqueueDelayed((float)delay, load));
            }
        }

        private void ClearLoadRetry(string unitId) {
            _retryAttempts.Remove(unitId);
        }

        private static void LogAdapters(InitializationStatus status) {
            var adapters = status.getAdapterStatusMap();
            if (adapters == null) return;

            SDKLogger.Info(Tag, "┌─── AdMob Adapters ───");
            foreach (var entry in adapters) {
                SDKLogger.Info(Tag, $"│   {entry.Key}: {entry.Value.InitializationState}");
            }
            SDKLogger.Info(Tag, "└──────────────────────");
        }
        #else
        public void Initialize(AdConfig config, Action<bool> onComplete) {
            SDKLogger.Warning(Tag,
                "com.google.ads.mobile is not installed. Install it to serve AdMob ads.");
            onComplete?.Invoke(false);
        }

        public void OnConsentChanged(ConsentStatus consent) { }

        public void ShowBanner(AdPlacement placement, BannerPosition position) { }
        public void HideBanner(AdPlacement placement) { }
        public void DestroyBanner(AdPlacement placement) { }

        public bool IsInterstitialReady(AdPlacement placement) => false;
        public void LoadInterstitial(AdPlacement placement) { }
        public void ShowInterstitial(AdPlacement placement, Action<AdResult> onComplete) {
            onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "No SDK."));
        }

        public bool IsRewardedReady(AdPlacement placement) => false;
        public void LoadRewarded(AdPlacement placement) { }
        public void ShowRewarded(AdPlacement placement, string trackPlacement,
            Action<AdResult> onComplete) {
            onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "No SDK."));
        }

        public bool IsAppOpenReady(AdPlacement placement) => false;
        public void LoadAppOpen(AdPlacement placement) { }
        public void ShowAppOpen(AdPlacement placement, Action<AdResult> onComplete) {
            onComplete?.Invoke(AdResult.Failed(placement.PlacementId, "No SDK."));
        }
        #endif
    }
}
