using System;
using ArcherStudio.SDK.Core;
using UnityEngine;
using ConsentStatus = ArcherStudio.SDK.Core.ConsentStatus;
#if HAS_GOOGLE_UMP
using GoogleMobileAds.Ump.Api;
using GoogleConsentStatus = GoogleMobileAds.Ump.Api.ConsentStatus;
#endif

namespace ArcherStudio.SDK.Consent {

    /// <summary>
    /// Google User Messaging Platform consent provider.
    /// Requires HAS_GOOGLE_UMP scripting define and Google Mobile Ads SDK.
    /// </summary>
    public class GoogleUmpProvider : IConsentProvider {
        private const string Tag = "GoogleUMP";
        private const float UmpTimeoutSeconds = 10f;

        private readonly ConsentConfig _config;
        private bool _isConsentRequired;
        private bool _callbackInvoked;
        private bool _isFormShowing;
        private int _retryCount;
        private const int MaxRetries = 1;

        /// <summary>
        /// Always true — UMP needs to call ConsentInformation.Update() (network call)
        /// before it can determine if consent is actually required.
        /// The actual requirement is resolved inside RequestConsent().
        /// </summary>
        public bool IsConsentRequired => true;

        public GoogleUmpProvider(ConsentConfig config) {
            _config = config;
        }

        public void RequestConsent(Action<ConsentStatus> onComplete) {
            _callbackInvoked = false;
            _isFormShowing = false;
            _retryCount = 0;

            InternalRequestConsent(onComplete);
        }

        private void InternalRequestConsent(Action<ConsentStatus> onComplete) {
            #if HAS_GOOGLE_UMP
            // ─── 1. Check Internet Reachability Early ───
            if (Application.internetReachability == NetworkReachability.NotReachable) {
                SDKLogger.Warning(Tag, "No internet connection. Using default consent (temporary).");
                // Invoke callback but don't mark as a permanent success in ConsentManager
                onComplete?.Invoke(ConsentStatus.Default);
                return;
            }

            SDKLogger.Info(Tag, $"Starting UMP consent request (attempt {_retryCount + 1})...");
            // ... rest of the method

            // Timeout protection — only for the network update part
            StartTimeout(onComplete);

            var requestParameters = new ConsentRequestParameters();

            if (_config != null && _config.TestGeography != DebugGeography.Disabled) {
                var debugSettings = new ConsentDebugSettings {
                    DebugGeography = _config.TestGeography == DebugGeography.EEA
                        ? GoogleMobileAds.Ump.Api.DebugGeography.EEA
                        : GoogleMobileAds.Ump.Api.DebugGeography.Other
                };
                requestParameters.ConsentDebugSettings = debugSettings;
                SDKLogger.Debug(Tag, $"Test geography: {_config.TestGeography}");
            }

            SDKLogger.Debug(Tag, "Calling ConsentInformation.Update()...");
            ConsentInformation.Update(requestParameters, (FormError updateError) => {
                if (_callbackInvoked) return; // Already handled by timeout

                if (updateError != null) {
                    SDKLogger.Warning(Tag, $"Consent update failed: {updateError.Message}");
                    
                    if (_retryCount < MaxRetries) {
                        _retryCount++;
                        SDKLogger.Info(Tag, $"Retrying UMP update in 2s...");
                        UnityMainThreadDispatcher.Instance.EnqueueDelayed(2f, () => InternalRequestConsent(onComplete));
                    } else {
                        SDKLogger.Error(Tag, "UMP update failed after retries. Using default consent.");
                        InvokeOnce(onComplete, ConsentStatus.Default);
                    }
                    return;
                }

                _isConsentRequired =
                    ConsentInformation.ConsentStatus != GoogleConsentStatus.NotRequired;

                if (_isConsentRequired) {
                    if (ConsentInformation.IsConsentFormAvailable()) {
                        _isFormShowing = true; // Mark that UI is about to show
                        SDKLogger.Info(Tag, "Showing Google UMP consent form...");

                        ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) => {
                            _isFormShowing = false;
                            if (showError != null) {
                                SDKLogger.Error(Tag, $"Consent form error: {showError.Message}");
                            }
                            InvokeOnce(onComplete, BuildStatus());
                        });
                    } else {
                        SDKLogger.Warning(Tag, "Consent form not available. Using current status.");
                        InvokeOnce(onComplete, BuildStatus());
                    }
                } else {
                    SDKLogger.Info(Tag, "Non-GDPR region detected.");
                    SDKInitCoordinator.NeedsPostInitTermsAndPolicy = true;
                    InvokeOnce(onComplete, BuildNonGdprStatus());
                }
            });
            #else
            SDKLogger.Warning(Tag, "HAS_GOOGLE_UMP not defined. Returning default consent.");
            onComplete?.Invoke(ConsentStatus.Default);
            #endif
        }

        /// <summary>
        /// Prevent double-invoke of the callback (timeout + normal completion race).
        /// </summary>
        private void InvokeOnce(Action<ConsentStatus> onComplete, ConsentStatus status) {
            if (_callbackInvoked) return;
            _callbackInvoked = true;
            onComplete?.Invoke(status);
        }

        /// <summary>
        /// Start a timeout coroutine for the network update part.
        /// If the form is showing, the timeout is ignored to give user time to read.
        /// </summary>
        private void StartTimeout(Action<ConsentStatus> onComplete) {
            if (UnityMainThreadDispatcher.Instance == null) return;

            UnityMainThreadDispatcher.Instance.EnqueueDelayed(UmpTimeoutSeconds, () => {
                // If form is showing or callback already invoked, don't timeout
                if (_callbackInvoked || _isFormShowing) return;

                SDKLogger.Warning(Tag, $"UMP update timed out after {UmpTimeoutSeconds}s.");
                
                if (_retryCount < MaxRetries) {
                    // Let the Update callback handle retry if it eventually arrives, 
                    // or just let this timeout trigger the fallback if it's really stuck.
                    _retryCount++;
                    SDKLogger.Info(Tag, "Timeout retry...");
                    InternalRequestConsent(onComplete);
                } else {
                    InvokeOnce(onComplete, ConsentStatus.Default);
                }
            });
        }

        public ConsentStatus GetCurrentStatus() {
            #if HAS_GOOGLE_UMP
            return BuildStatus();
            #else
            return ConsentStatus.Default;
            #endif
        }

        public void ResetConsent() {
            #if HAS_GOOGLE_UMP
            ConsentInformation.Reset();
            #endif
        }

        private ConsentStatus BuildNonGdprStatus() {
            return new ConsentStatus(
                canShowPersonalizedAds: true,
                canCollectAnalytics: true,
                canTrackAttribution: true,
                isEeaUser: false,
                hasAttConsent: true, // ATT handled separately by ConsentManager
                source: ConsentSource.GoogleUMP);
        }

        #if HAS_GOOGLE_UMP
        /// <summary>
        /// Build ConsentStatus from raw IABTCF_PurposeConsents string.
        ///
        /// Previous approach read UMP_CoMo* internal keys, but these may not be
        /// written yet when the UMP callback fires (timing issue on Android).
        ///
        /// Now reads IABTCF_PurposeConsents directly (guaranteed written per IAB spec)
        /// and computes consent mode per Google's official mapping:
        ///   ad_storage        = P1
        ///   ad_user_data      = P1 && P7
        ///   ad_personalization = P3 && P4
        ///   analytics_storage = P1 && P7
        /// </summary>
        private ConsentStatus BuildStatus() {
            bool isEea = _isConsentRequired;

            // Read raw TCF purpose bits from SharedPreferences/NSUserDefaults
            bool p1 = ConsentHelper.IsPurposeGranted(1);
            bool p3 = ConsentHelper.IsPurposeGranted(3);
            bool p4 = ConsentHelper.IsPurposeGranted(4);
            bool p7 = ConsentHelper.IsPurposeGranted(7);

            // Compute consent mode per Google TCF mapping docs
            bool adStorage = p1;                    // P1
            bool adUserData = p1 && p7;             // P1 && P7
            bool adPersonalization = p3 && p4;      // P3 && P4
            bool analyticsStorage = p1 && p7;       // P1 && P7

            SDKLogger.Info(Tag, "┌─── UMP Consent (from TCF Purposes) ───");
            SDKLogger.Info(Tag, $"│ P1  (storage/access):    {p1}");
            SDKLogger.Info(Tag, $"│ P3  (ads profile):       {p3}");
            SDKLogger.Info(Tag, $"│ P4  (select ads):        {p4}");
            SDKLogger.Info(Tag, $"│ P7  (ad measurement):    {p7}");
            SDKLogger.Info(Tag, $"│ → ad_storage:            {adStorage} (P1)");
            SDKLogger.Info(Tag, $"│ → ad_user_data:          {adUserData} (P1&&P7)");
            SDKLogger.Info(Tag, $"│ → ad_personalization:    {adPersonalization} (P3&&P4)");
            SDKLogger.Info(Tag, $"│ → analytics_storage:     {analyticsStorage} (P1&&P7)");
            SDKLogger.Info(Tag, $"│ CanRequestAds:           {ConsentInformation.CanRequestAds()}");
            SDKLogger.Info(Tag, "└────────────────────────────────────────");

            return new ConsentStatus(
                canShowPersonalizedAds: adPersonalization,
                canCollectAnalytics: analyticsStorage,
                canTrackAttribution: adUserData,
                isEeaUser: isEea,
                hasAttConsent: true,
                source: ConsentSource.GoogleUMP,
                canStoreAdData: adStorage);
        }
        #endif
    }
}
