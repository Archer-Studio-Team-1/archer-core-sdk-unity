#if HAS_APPLOVIN_MAX_SDK
using AppLovinMax;
#endif

using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Consent {

    /// <summary>
    /// AppLovin MAX consent provider.
    /// MAX SDK handles the full privacy flow during initialization:
    ///   1. Regional compliance check (GDPR detection)
    ///   2. Google UMP dialog (if GDPR region + CMP configured)
    ///   3. iOS ATT prompt (if applicable)
    ///   4. OnSdkInitializedEvent fires when complete
    ///
    /// Configure the privacy flow in Unity: AppLovin > Integration Manager.
    /// Requires HAS_APPLOVIN_MAX_SDK define.
    /// </summary>
    public class MaxConsentProvider : IConsentProvider {
        private const string Tag = "Consent-MAX";

        private readonly ConsentConfig _config;
        private ConsentStatus _status = ConsentStatus.Default;

        public bool IsConsentRequired => true;

        public MaxConsentProvider(ConsentConfig config) {
            _config = config;
        }

        public ConsentStatus GetCurrentStatus() => _status;

        private bool _callbackInvoked;
        private const float MaxInitTimeoutSeconds = 15f;

        public void RequestConsent(Action<ConsentStatus> onComplete) {
            _callbackInvoked = false;

            #if HAS_APPLOVIN_MAX_SDK
            if (string.IsNullOrEmpty(_config.MaxSdkKey)) {
                SDKLogger.Error(Tag,
                    "MaxSdkKey is empty in ConsentConfig. Cannot initialize MAX consent flow.");
                onComplete?.Invoke(ConsentStatus.Default);
                return;
            }

            SDKLogger.Info(Tag, "Initializing MAX SDK with privacy flow...");

            // Timeout — if MAX SDK never fires OnSdkInitializedEvent
            UnityMainThreadDispatcher.Instance?.EnqueueDelayed(MaxInitTimeoutSeconds, () => {
                if (_callbackInvoked) return;
                _callbackInvoked = true;
                SDKLogger.Warning(Tag,
                    $"MAX SDK init timed out after {MaxInitTimeoutSeconds}s. Using default consent.");
                onComplete?.Invoke(ConsentStatus.Default);
            });

            try {
                MaxSdk.SetSdkKey(_config.MaxSdkKey);

                MaxSdkCallbacks.OnSdkInitializedEvent += sdkConfig => {
                    if (_callbackInvoked) return;
                    _callbackInvoked = true;
                    OnMaxInitialized(sdkConfig, onComplete);
                };

                MaxSdk.InitializeSdk();
            } catch (System.Exception e) {
                if (_callbackInvoked) return;
                _callbackInvoked = true;
                SDKLogger.Error(Tag, $"MAX SDK init failed: {e.Message}");
                onComplete?.Invoke(ConsentStatus.Default);
            }
            #else
            SDKLogger.Warning(Tag,
                "AppLovinMax selected but HAS_APPLOVIN_MAX_SDK not defined. Using default consent.");
            onComplete?.Invoke(ConsentStatus.Default);
            #endif
        }

        public void ResetConsent() {
            SDKLogger.Info(Tag, "ResetConsent: clearing MAX consent state.");
            _status = ConsentStatus.Default;
        }

        /// <summary>
        /// Show CMP dialog for existing users (e.g., from a "Privacy Settings" button).
        /// Only works when a supported CMP (Google UMP, etc.) is integrated.
        /// </summary>
        public void ShowCmpForExistingUser(Action<string> onComplete) {
            #if HAS_APPLOVIN_MAX_SDK
            var cmpService = MaxSdk.CmpService;
            if (cmpService == null || !cmpService.HasSupportedCmp) {
                SDKLogger.Warning(Tag, "No supported CMP detected.");
                onComplete?.Invoke("No supported CMP detected.");
                return;
            }

            SDKLogger.Info(Tag, "Showing CMP for existing user...");
            cmpService.ShowCmpForExistingUser(error => {
                if (error == null) {
                    SDKLogger.Info(Tag, "CMP flow completed successfully.");
                    onComplete?.Invoke(null);
                } else {
                    SDKLogger.Warning(Tag,
                        $"CMP flow error: {error.Message} (code={error.Code})");
                    onComplete?.Invoke(error.Message);
                }
            });
            #else
            onComplete?.Invoke("MAX SDK not available.");
            #endif
        }

        #if HAS_APPLOVIN_MAX_SDK
        private void OnMaxInitialized(
            MaxSdkBase.SdkConfiguration sdkConfig,
            Action<ConsentStatus> onComplete) {

            // Mark that MAX SDK is already initialized (so AdManager won't re-init)
            SDKInitCoordinator.IsAdSdkInitializedByConsent = true;

            // ─── Read all consent signals from MAX ───
            var geography = sdkConfig.ConsentFlowUserGeography;
            bool isGdpr = geography == MaxSdkBase.ConsentFlowUserGeography.Gdpr;

            bool hasUserConsent = MaxSdk.HasUserConsent() || !isGdpr;
            bool doNotSell = MaxSdk.IsDoNotSellSet() && MaxSdk.IsDoNotSell();

            // iOS ATT — MAX handles ATT automatically when privacy flow is enabled
            #if UNITY_IOS
            var attStatus = sdkConfig.AppTrackingStatus;
            bool hasAttConsent = attStatus == MaxSdkBase.AppTrackingStatus.Authorized;
            #else
            bool hasAttConsent = true; // Android doesn't have ATT
            #endif

            // CMP availability
            bool hasCmp = MaxSdk.CmpService != null && MaxSdk.CmpService.HasSupportedCmp;

            bool canPersonalize = hasUserConsent && !doNotSell && hasAttConsent;

            _status = new ConsentStatus(
                canShowPersonalizedAds: canPersonalize,
                canCollectAnalytics: hasUserConsent,
                canTrackAttribution: hasUserConsent && hasAttConsent,
                isEeaUser: isGdpr,
                hasAttConsent: hasAttConsent,
                source: ConsentSource.AppLovinMax,
                isDoNotSell: doNotSell);

            // ─── Detailed logging ───
            SDKLogger.Info(Tag, "┌─── MAX Privacy Flow Complete ───");
            SDKLogger.Info(Tag, $"│ Geography:       {geography}");
            SDKLogger.Info(Tag, $"│ IsGDPR:          {isGdpr}");
            SDKLogger.Info(Tag, $"│ HasUserConsent:   {MaxSdk.HasUserConsent()}");
            SDKLogger.Info(Tag, $"│ DoNotSell:        {doNotSell} (set={MaxSdk.IsDoNotSellSet()})");
            #if UNITY_IOS
            SDKLogger.Info(Tag, $"│ ATT Status:       {attStatus}");
            SDKLogger.Info(Tag, $"│ HasAttConsent:     {hasAttConsent}");
            #endif
            SDKLogger.Info(Tag, $"│ HasCMP:           {hasCmp}");
            SDKLogger.Info(Tag, $"│ CanPersonalize:   {canPersonalize}");
            SDKLogger.Info(Tag, $"│ Result:           {_status}");
            SDKLogger.Info(Tag, "└─────────────────────────────────");

            if (_config.MaxShowMediationDebugger) {
                MaxSdk.ShowMediationDebugger();
            }

            onComplete?.Invoke(_status);
        }
        #endif
    }
}
