using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// Central ad manager. Implements ISDKModule for SDK lifecycle.
    /// Manages placements, frequency capping, and delegates to the active IAdProvider.
    /// </summary>
    public class AdManager : ISDKModule {
        private const string Tag = "Ads";

        // ─── ISDKModule ───
        public string ModuleId => "ads";
        public int InitializationPriority => 50;
        public IReadOnlyList<string> Dependencies => new[] { "consent", "tracking" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        // ─── Singleton access ───
        public static AdManager Instance { get; private set; }

        // ─── Internal ───
        private IAdProvider _provider;
        private AdConfig _config;
        private FrequencyCapper _frequencyCapper;
        private AdRevenueTracker _revenueTracker;
        private readonly Dictionary<string, AdPlacement> _placements =
            new Dictionary<string, AdPlacement>();

        public event Action<AdRevenueData> OnAdRevenuePaid;

        // ─── ISDKModule Lifecycle ───

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;
            Instance = this;

            _config = Resources.Load<AdConfig>("AdConfig");
            if (_config == null) {
                SDKLogger.Error(Tag, "AdConfig not found in Resources. Cannot initialize.");
                State = ModuleState.Failed;
                onComplete?.Invoke(false);
                return;
            }

            // Build placement lookup
            _placements.Clear();
            foreach (var placement in _config.Placements) {
                _placements[placement.PlacementId] = placement;
            }

            LogAdConfig();

            _frequencyCapper = new FrequencyCapper(_config);
            _revenueTracker = new AdRevenueTracker();

            // Create provider
            _provider = CreateProvider();
            if (_provider == null) {
                SDKLogger.Error(Tag, "Failed to create ad provider.");
                State = ModuleState.Failed;
                onComplete?.Invoke(false);
                return;
            }

            // Wire revenue callback
            _provider.OnAdRevenuePaid += OnProviderRevenuePaid;

            // Subscribe to future consent changes
            SDKEventBus.Subscribe<ConsentChangedEvent>(OnConsentEvent);

            // Pass current consent to provider BEFORE init
            // (ConsentChangedEvent was already broadcast in batch 1, before AdManager)
            var consentModule = SDKInitializer.Instance?.GetModule("consent");
            if (consentModule is ArcherStudio.SDK.Consent.ConsentManager cm) {
                _provider.OnConsentChanged(cm.CurrentStatus);
                SDKLogger.Debug(Tag, $"Pre-init consent: {cm.CurrentStatus}");
            }

            // Initialize provider — wrapped in try-catch to prevent stuck
            try {
                _provider.Initialize(_config, success => {
                    if (success) {
                        SDKLogger.Info(Tag,
                            $"AdManager initialized with {_provider.ProviderId}. " +
                            $"{_placements.Count} placements configured.");

                        // Auto-load placements
                        AutoLoadPlacements();

                        // Non-GDPR: show Terms/Privacy after MAX SDK is ready.
                        // BLOCKS — waits for user to accept before completing init.
                        ShowPostInitTermsIfNeeded(() => {
                            State = ModuleState.Ready;
                            onComplete?.Invoke(true);
                        });
                    } else {
                        SDKLogger.Error(Tag, $"Ad provider '{_provider.ProviderId}' failed to init.");
                        State = ModuleState.Failed;
                        onComplete?.Invoke(false);
                    }
                });
            } catch (System.Exception e) {
                SDKLogger.Error(Tag, $"Ad provider init threw exception: {e.Message}");
                State = ModuleState.Failed;
                onComplete?.Invoke(false);
            }
        }

        public void OnConsentChanged(ConsentStatus consent) {
            _provider?.OnConsentChanged(consent);
        }

        /// <summary>
        /// For non-GDPR users: flag is set so game can show Terms/Privacy UI.
        /// Does NOT block SDK init — all SDKs must init regardless of region.
        /// Game reads SDKInitCoordinator.NeedsPostInitTermsAndPolicy after boot.
        /// </summary>
        private void ShowPostInitTermsIfNeeded(Action onDone) {
            if (SDKInitCoordinator.NeedsPostInitTermsAndPolicy) {
                SDKLogger.Info(Tag,
                    "Non-GDPR: NeedsPostInitTermsAndPolicy=true. " +
                    "Game should show Terms/Privacy UI after SDK init.");
            }

            // Never block — all SDKs must init for non-GDPR regions
            onDone?.Invoke();
        }

        public void Dispose() {
            if (_provider != null) {
                _provider.OnAdRevenuePaid -= OnProviderRevenuePaid;
            }
            SDKEventBus.Unsubscribe<ConsentChangedEvent>(OnConsentEvent);
            Instance = null;
            State = ModuleState.Disposed;
        }

        // ─── Public API ───

        /// <summary>
        /// Show a banner.
        /// </summary>
        /// <param name="adsId">Ad placement ID defined in AdConfig.</param>
        public void ShowBanner(string adsId, BannerPosition position = BannerPosition.Bottom) {
            if (!TryGetPlacement(adsId, out var placement)) return;
            _provider?.ShowBanner(placement, position);
        }

        public void HideBanner(string adsId) {
            if (!TryGetPlacement(adsId, out var placement)) return;
            _provider?.HideBanner(placement);
        }

        public void DestroyBanner(string adsId) {
            if (!TryGetPlacement(adsId, out var placement)) return;
            _provider?.DestroyBanner(placement);
        }

        /// <summary>
        /// Check if an interstitial is loaded and ready to show.
        /// </summary>
        /// <param name="adsId">Ad placement ID defined in AdConfig (maps to ad unit ID).</param>
        public bool IsInterstitialReady(string adsId) {
            if (!TryGetPlacement(adsId, out var placement)) return false;
            return _provider?.IsInterstitialReady(placement) ?? false;
        }

        /// <summary>
        /// Show interstitial. Respects frequency capping.
        /// </summary>
        /// <param name="adsId">Ad placement ID defined in AdConfig (maps to ad unit ID).</param>
        /// <param name="placementId">Custom placement string for tracking (e.g., "level_complete", "shop"). Logged instead of adsId.</param>
        public void ShowInterstitial(string adsId, string placementId = null,
            Action<AdResult> onComplete = null) {

            string trackingPlacement = placementId ?? adsId;

            if (!TryGetPlacement(adsId, out var placement)) {
                onComplete?.Invoke(AdResult.Failed(trackingPlacement, "Placement not found."));
                return;
            }

            var capResult = _frequencyCapper.CanShow(adsId, AdFormat.Interstitial);
            if (!capResult.IsAllowed) {
                SDKLogger.Debug(Tag, $"Interstitial capped: {capResult.Reason}");
                onComplete?.Invoke(AdResult.Failed(trackingPlacement, capResult.Reason));
                return;
            }

            _provider?.ShowInterstitial(placement, result => {
                if (result.Success) {
                    _frequencyCapper.RecordShow(adsId, AdFormat.Interstitial);
                }
                // Return result with tracking placementId
                onComplete?.Invoke(new AdResult(result.Success, result.WasRewarded,
                    result.Reward, result.Error, trackingPlacement));
            });
        }

        /// <summary>
        /// Check if a rewarded ad is loaded and ready to show.
        /// </summary>
        /// <param name="adsId">Ad placement ID defined in AdConfig (maps to ad unit ID).</param>
        public bool IsRewardedReady(string adsId) {
            if (!TryGetPlacement(adsId, out var placement)) return false;
            return _provider?.IsRewardedReady(placement) ?? false;
        }

        /// <summary>
        /// Show rewarded ad. Respects frequency capping.
        /// </summary>
        /// <param name="adsId">Ad placement ID defined in AdConfig (maps to ad unit ID).</param>
        /// <param name="placementId">Custom placement string for tracking (e.g., "shop_gems", "revive"). Logged instead of adsId.</param>
        /// <param name="onComplete"></param>
        /// <param name="onRewarded"></param>
        /// <param name="onFailed"></param>
        /// <param name="onClosed"></param>
        public void ShowRewarded(string adsId, string placementId = null,
            Action<AdResult> onComplete = null,
            Action<RewardData> onRewarded = null,
            Action<string> onFailed = null,
            Action onClosed = null) {

            string trackingPlacement = placementId ?? adsId;

            if (!TryGetPlacement(adsId, out var placement)) {
                var result = AdResult.Failed(trackingPlacement, "Placement not found.");
                onFailed?.Invoke(result.Error);
                onComplete?.Invoke(result);
                return;
            }

            var capResult = _frequencyCapper.CanShow(adsId, AdFormat.Rewarded);
            if (!capResult.IsAllowed) {
                SDKLogger.Debug(Tag, $"Rewarded capped: {capResult.Reason}");
                var result = AdResult.Failed(trackingPlacement, capResult.Reason);
                onFailed?.Invoke(result.Error);
                onComplete?.Invoke(result);
                return;
            }

            _provider?.ShowRewarded(placement, trackingPlacement, result => {
                // Build result with tracking placementId
                var trackingResult = new AdResult(result.Success, result.WasRewarded,
                    result.Reward, result.Error, trackingPlacement);

                if (result.Success) {
                    _frequencyCapper.RecordShow(adsId, AdFormat.Rewarded);
                    if (result.WasRewarded) {
                        onRewarded?.Invoke(result.Reward);
                    }
                    onClosed?.Invoke();
                } else {
                    onFailed?.Invoke(result.Error);
                }
                onComplete?.Invoke(trackingResult);
            });
        }

        /// <summary>
        /// Show app open ad.
        /// </summary>
        public void ShowAppOpen(string placementId = null, Action<AdResult> onComplete = null) {
            if (!_config.EnableAppOpenAd) {
                onComplete?.Invoke(AdResult.Failed(placementId ?? "", "App open ads disabled."));
                return;
            }

            // Find first app open placement if none specified
            AdPlacement placement = null;
            if (placementId != null) {
                TryGetPlacement(placementId, out placement);
            } else {
                foreach (var p in _config.Placements) {
                    if (p.Format == AdFormat.AppOpen) { placement = p; break; }
                }
            }

            if (placement == null) {
                onComplete?.Invoke(AdResult.Failed(placementId ?? "", "No app open placement."));
                return;
            }

            _provider?.ShowAppOpen(placement, onComplete);
        }

        /// <summary>
        /// Manually load an ad.
        /// </summary>
        /// <param name="adsId">Ad placement ID defined in AdConfig.</param>
        public void LoadAd(string adsId) {
            if (!TryGetPlacement(adsId, out var placement)) return;

            switch (placement.Format) {
                case AdFormat.Interstitial:
                    _provider?.LoadInterstitial(placement);
                    break;
                case AdFormat.Rewarded:
                    _provider?.LoadRewarded(placement);
                    break;
                case AdFormat.AppOpen:
                    _provider?.LoadAppOpen(placement);
                    break;
            }
        }

        // ─── Internal ───

        private void LogAdConfig() {
            SDKLogger.Info(Tag, "┌─── Ads Config ───");
            SDKLogger.Info(Tag, $"│ Enabled:            {_config.Enabled}");
            SDKLogger.Info(Tag, $"│ MediationPlatform:  {_config.MediationPlatform}");
            SDKLogger.Info(Tag, $"│ SDK Key:            {MaskKey(_config.SdkKey)}");
            SDKLogger.Info(Tag, $"│ Placements:         {_config.Placements.Count}");
            foreach (var p in _config.Placements) {
                SDKLogger.Info(Tag,
                    $"│   [{p.Format}] {p.PlacementId} → " +
                    $"android={MaskUnitId(p.AndroidUnitId)}, ios={MaskUnitId(p.IosUnitId)}, " +
                    $"autoLoad={p.AutoLoad}");
            }
            SDKLogger.Info(Tag, $"│ InterstitialCooldown: {_config.InterstitialCooldownSeconds}s");
            SDKLogger.Info(Tag, $"│ MaxInterstitials:   {_config.MaxInterstitialsPerSession}/session");
            SDKLogger.Info(Tag, $"│ MaxRewarded:        {_config.MaxRewardedPerSession}/session");
            SDKLogger.Info(Tag, $"│ AppOpenAd:          {_config.EnableAppOpenAd}");
            SDKLogger.Info(Tag, $"│ ShowDebugger:       {_config.ShowMediationDebugger}");
            SDKLogger.Info(Tag, "└───────────────────");
        }

        private static string MaskKey(string key) {
            if (string.IsNullOrEmpty(key)) return "(not set)";
            if (key.Length <= 8) return key.Substring(0, 2) + "***";
            return key.Substring(0, 4) + "***" + key.Substring(key.Length - 4);
        }

        private static string MaskUnitId(string unitId) {
            if (string.IsNullOrEmpty(unitId)) return "(not set)";
            // Ad unit IDs are not secret, but mask middle for readability
            if (unitId.Length <= 12) return unitId;
            return unitId.Substring(0, 6) + "..." + unitId.Substring(unitId.Length - 6);
        }

        private IAdProvider CreateProvider() {
            var platform = _config.MediationPlatform;
            switch (platform) {
                case AdMediationPlatform.AppLovinMax:
                    return new AppLovinMaxProvider();
                case AdMediationPlatform.IronSource:
                    return new IronSourceProvider();
                case AdMediationPlatform.AdMob:
                    return new AdMobProvider();
                default:
                    SDKLogger.Error(Tag, $"Unknown mediation platform: {platform}");
                    return null;
            }
        }

        private bool TryGetPlacement(string placementId, out AdPlacement placement) {
            if (_placements.TryGetValue(placementId, out placement)) return true;
            SDKLogger.Warning(Tag, $"Placement '{placementId}' not found in config.");
            return false;
        }

        private void AutoLoadPlacements() {
            foreach (var placement in _config.Placements) {
                if (!placement.AutoLoad) continue;
                switch (placement.Format) {
                    case AdFormat.Interstitial:
                        _provider?.LoadInterstitial(placement);
                        break;
                    case AdFormat.Rewarded:
                        _provider?.LoadRewarded(placement);
                        break;
                    case AdFormat.AppOpen:
                        _provider?.LoadAppOpen(placement);
                        break;
                }
            }
        }

        private void OnProviderRevenuePaid(AdRevenueData data) {
            _revenueTracker.OnRevenuePaid(data);
            OnAdRevenuePaid?.Invoke(data);
        }

        private void OnConsentEvent(ConsentChangedEvent e) {
            OnConsentChanged(e.Status);
        }
    }
}
