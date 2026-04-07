using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Consent {

    /// <summary>
    /// Central consent management module.
    /// Initializes first (priority=0), then broadcasts ConsentChangedEvent to all modules.
    /// </summary>
    public class ConsentManager : ISDKModule {
        private const string Tag = "Consent";
        private const string ConsentPrefsKey = "archersdk_consent_status";
        private const string ConsentGrantedKey = "archersdk_consent_granted";
        private const string ConsentEeaKey = "archersdk_consent_eea";
        private const string ConsentDnsKey = "archersdk_consent_donotsell";

        public string ModuleId => "consent";
        public int InitializationPriority => 0;
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        public ConsentStatus CurrentStatus { get; private set; } = ConsentStatus.Default;

        private IConsentProvider _provider;
        private ConsentConfig _config;

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;

            // Try to find ConsentConfig - look for it in Resources
            _config = Resources.Load<ConsentConfig>("ConsentConfig");

            // Load cached consent first
            if (HasCachedConsent()) {
                CurrentStatus = LoadCachedConsent();
                SDKLogger.Info(Tag, $"Loaded cached consent: {CurrentStatus}");
            }

            LogConsentConfig();

            // Create provider based on config
            _provider = CreateProvider();
            SDKLogger.Info(Tag,
                $"Provider: {_provider?.GetType().Name ?? "(null)"}, " +
                $"IsConsentRequired: {_provider?.IsConsentRequired ?? false}");

            if (_provider == null || !_provider.IsConsentRequired) {
                SDKLogger.Info(Tag, "Consent not required. Using default (all granted).");
                CurrentStatus = ConsentStatus.Default;
                BroadcastConsent();
                State = ModuleState.Ready;
                onComplete?.Invoke(true);
                return;
            }

            // Request consent — provider handles GDPR/non-GDPR routing.
            // CRITICAL: Provider callback may fire on Android UI thread (Google UMP).
            // Marshal to Unity main thread for PlayerPrefs, State, EventBus safety.
            _provider.RequestConsent(status => {
                // Marshal to main thread — provider callback may fire on Android UI thread.
                RunOnMainThread(() => {
                    CurrentStatus = status;

                    if (status.Source != ConsentSource.Default) {
                        SaveConsent(status);
                        SDKLogger.Debug(Tag, "Permanent consent saved to cache.");
                    } else {
                        SDKLogger.Info(Tag, "Temporary consent used (not cached). Will re-request next launch.");
                    }

                    bool maxHandlesAtt = _config != null &&
                        _config.ProviderType == ConsentProviderType.AppLovinMax;

                    #if UNITY_IOS
                    if (_config != null && _config.RequestATT && !maxHandlesAtt) {
                        RequestATTWithDelay(_config.AttDelay, () => {
                            BroadcastConsent();
                            State = ModuleState.Ready;
                            onComplete?.Invoke(true);
                        });
                        return;
                    }
                    #endif

                    BroadcastConsent();
                    State = ModuleState.Ready;
                    onComplete?.Invoke(true);
                });
            });
        }

        public void OnConsentChanged(ConsentStatus consent) {
            // This module is the source of consent - nothing to do here.
        }

        /// <summary>
        /// Manually set consent (for games with custom consent UI).
        /// </summary>
        public void SetConsent(bool granted, bool isEeaUser) {
            SDKLogger.Info(Tag, $"SetConsent: granted={granted}, isEeaUser={isEeaUser}");
            CurrentStatus = ConsentStatus.FromLegacy(granted, isEeaUser);
            SaveConsent(CurrentStatus);
            BroadcastConsent();
        }

        /// <summary>
        /// Reset consent and show dialog again.
        /// </summary>
        public void ResetConsent() {
            SDKLogger.Info(Tag, "ResetConsent: clearing cached consent.");
            PlayerPrefs.DeleteKey(ConsentPrefsKey);
            PlayerPrefs.DeleteKey(ConsentGrantedKey);
            PlayerPrefs.DeleteKey(ConsentEeaKey);
            PlayerPrefs.DeleteKey(ConsentDnsKey);
            _provider?.ResetConsent();
        }

        public void Dispose() {
            State = ModuleState.Disposed;
        }

        /// <summary>
        /// Show CMP dialog for existing users (e.g., from a "Privacy Settings" button).
        /// Only supported when using AppLovin MAX consent provider with a CMP integrated.
        /// </summary>
        public void ShowCmpForExistingUser(Action<string> onComplete) {
            if (_provider is MaxConsentProvider maxProvider) {
                maxProvider.ShowCmpForExistingUser(onComplete);
            } else {
                SDKLogger.Warning(Tag, "ShowCmpForExistingUser is only supported with AppLovinMax provider.");
                onComplete?.Invoke("CMP not supported for current provider.");
            }
        }

        private void LogConsentConfig() {
            if (_config == null) {
                SDKLogger.Debug(Tag, "ConsentConfig: null (using default Manual provider)");
                return;
            }

            SDKLogger.Info(Tag, "┌─── Consent Config ───");
            SDKLogger.Info(Tag, $"│ Enabled:            {_config.Enabled}");
            SDKLogger.Info(Tag, $"│ ProviderType:       {_config.ProviderType}");
            if (_config.ProviderType == ConsentProviderType.AppLovinMax) {
                SDKLogger.Info(Tag, $"│ MAX SDK Key:        {MaskKey(_config.MaxSdkKey)}");
                SDKLogger.Info(Tag, $"│ ShowDebugger:       {_config.MaxShowMediationDebugger}");
            }
            SDKLogger.Info(Tag, $"│ RequestATT:         {_config.RequestATT}");
            SDKLogger.Info(Tag, $"│ ATT Delay:          {_config.AttDelay}s");
            SDKLogger.Info(Tag, $"│ ForceShowInEditor:  {_config.ForceShowInEditor}");
            SDKLogger.Info(Tag, $"│ TestGeography:      {_config.TestGeography}");
            SDKLogger.Info(Tag, "└──────────────────────");
        }

        private static string MaskKey(string key) {
            if (string.IsNullOrEmpty(key)) return "(not set)";
            if (key.Length <= 8) return key.Substring(0, 2) + "***";
            return key.Substring(0, 4) + "***" + key.Substring(key.Length - 4);
        }

        private IConsentProvider CreateProvider() {
            var providerType = _config != null
                ? _config.ProviderType
                : ConsentProviderType.Manual;

            switch (providerType) {
                case ConsentProviderType.GoogleUMP:
                    #if HAS_GOOGLE_UMP
                    return new GoogleUmpProvider(_config);
                    #else
                    SDKLogger.Warning(Tag,
                        "GoogleUMP selected but HAS_GOOGLE_UMP not defined. Falling back to Manual.");
                    return new ManualConsentProvider();
                    #endif
                case ConsentProviderType.AppLovinMax:
                    #if HAS_APPLOVIN_MAX_SDK
                    return new MaxConsentProvider(_config);
                    #else
                    SDKLogger.Warning(Tag,
                        "AppLovinMax selected but HAS_APPLOVIN_MAX_SDK not defined. Falling back to Manual.");
                    return new ManualConsentProvider();
                    #endif
                case ConsentProviderType.Manual:
                default:
                    return new ManualConsentProvider();
            }
        }

        private static void RunOnMainThread(Action action) {
            var dispatcher = UnityMainThreadDispatcher.Instance;
            if (dispatcher != null) {
                dispatcher.Enqueue(action);
            } else {
                action?.Invoke();
            }
        }

        private void BroadcastConsent() {
            SDKLogger.Info(Tag, $"Broadcasting consent: {CurrentStatus}");

            // Dump full debug info for device verification
            DumpConsentDebugInfo(CurrentStatus);

            // Apply consent to Facebook SDK (manifest defaults are false)
            ApplyFacebookConsent(CurrentStatus);

            SDKEventBus.Publish(new ConsentChangedEvent(CurrentStatus));
        }

        /// <summary>
        /// Dump all consent signals in a single block for easy device verification.
        /// Covers: ConsentStatus, TCF strings, UMP consent mode, Firebase mapping, Facebook state, Adjust DMA.
        /// Filter logcat/console by "[SDK:Consent]" to see this block.
        /// </summary>
        private static void DumpConsentDebugInfo(ConsentStatus status) {
            SDKLogger.Info(Tag, "╔══════════════════════════════════════════════════════════╗");
            SDKLogger.Info(Tag, "║           CONSENT DEBUG DUMP (device verification)       ║");
            SDKLogger.Info(Tag, "╠══════════════════════════════════════════════════════════╣");

            // 1. ConsentStatus fields
            SDKLogger.Info(Tag, "║ ── ConsentStatus ──");
            SDKLogger.Info(Tag, $"║   Source:              {status.Source}");
            SDKLogger.Info(Tag, $"║   CanShowPersonalizedAds: {status.CanShowPersonalizedAds}");
            SDKLogger.Info(Tag, $"║   CanCollectAnalytics:    {status.CanCollectAnalytics}");
            SDKLogger.Info(Tag, $"║   CanTrackAttribution:    {status.CanTrackAttribution}");
            SDKLogger.Info(Tag, $"║   CanStoreAdData:         {status.CanStoreAdData}");
            SDKLogger.Info(Tag, $"║   IsEeaUser:              {status.IsEeaUser}");
            SDKLogger.Info(Tag, $"║   HasAttConsent:           {status.HasAttConsent}");
            SDKLogger.Info(Tag, $"║   IsDoNotSell:             {status.IsDoNotSell}");

            // 2. Firebase Consent Mode v2 mapping
            SDKLogger.Info(Tag, "║ ── Firebase Consent Mode v2 (will be set) ──");
            SDKLogger.Info(Tag, $"║   AD_STORAGE:          {Gd(status.CanStoreAdData)}");
            SDKLogger.Info(Tag, $"║   ANALYTICS_STORAGE:   {Gd(status.CanCollectAnalytics)}");
            SDKLogger.Info(Tag, $"║   AD_USER_DATA:        {Gd(status.CanTrackAttribution)}");
            SDKLogger.Info(Tag, $"║   AD_PERSONALIZATION:  {Gd(status.CanShowPersonalizedAds)}");

            // 3. Facebook SDK mapping
            SDKLogger.Info(Tag, "║ ── Facebook SDK (will be set) ──");
            SDKLogger.Info(Tag, $"║   AutoLogAppEvents:      {status.CanCollectAnalytics}");
            SDKLogger.Info(Tag, $"║   AdvertiserIDCollection: {status.CanTrackAttribution}");
            SDKLogger.Info(Tag, $"║   LDU (DataProcessing):   {(status.IsDoNotSell ? "LDU enabled" : "disabled")}");
            #if UNITY_IOS
            SDKLogger.Info(Tag, $"║   AdvertiserTracking:     {status.HasAttConsent}");
            #endif

            // 4. MAX SDK mapping
            SDKLogger.Info(Tag, "║ ── AppLovin MAX (will be set) ──");
            SDKLogger.Info(Tag, $"║   HasUserConsent:         {status.CanShowPersonalizedAds}");
            SDKLogger.Info(Tag, $"║   DoNotSell:              {status.IsDoNotSell}");
            SDKLogger.Info(Tag, $"║   facebook_limited_data_use: {(status.CanShowPersonalizedAds ? "false" : "true")}");

            // 5. Adjust DMA mapping
            SDKLogger.Info(Tag, "║ ── Adjust DMA (will be set) ──");
            SDKLogger.Info(Tag, $"║   eea:                 {(status.IsEeaUser ? "1" : "0")}");
            SDKLogger.Info(Tag, $"║   ad_personalization:  {(status.CanShowPersonalizedAds ? "1" : "0")}");
            SDKLogger.Info(Tag, $"║   ad_user_data:        {(status.CanTrackAttribution ? "1" : "0")}");
            SDKLogger.Info(Tag, $"║   ad_storage:          {(status.CanStoreAdData ? "1" : "0")}");
            SDKLogger.Info(Tag, $"║   npa:                 {(status.CanShowPersonalizedAds ? "0" : "1")}");
            SDKLogger.Info(Tag, $"║   MeasurementConsent:  {status.CanCollectAnalytics}");

            // 6. Raw TCF data (read from correct storage per platform)
            SDKLogger.Info(Tag, "║ ── Raw TCF Data ──");
            string tcString = ConsentHelper.GetTcString();
            SDKLogger.Info(Tag, $"║   IABTCF_tcString:       {(string.IsNullOrEmpty(tcString) ? "(empty)" : tcString.Substring(0, System.Math.Min(tcString.Length, 40)) + "...")}");
            SDKLogger.Info(Tag, $"║   IABTCF_gdprApplies:    {ConsentHelper.IsGdprApplies()}");

            string purposeConsents = ConsentHelper.ReadPurposeConsentsRaw();
            SDKLogger.Info(Tag, $"║   IABTCF_PurposeConsents: {(string.IsNullOrEmpty(purposeConsents) ? "(empty)" : purposeConsents)}");
            SDKLogger.Info(Tag, $"║     Purpose 1 (storage):        {ConsentHelper.IsPurposeGranted(1)}");
            SDKLogger.Info(Tag, $"║     Purpose 3 (ads profile):    {ConsentHelper.IsPurposeGranted(3)}");
            SDKLogger.Info(Tag, $"║     Purpose 4 (select ads):     {ConsentHelper.IsPurposeGranted(4)}");
            SDKLogger.Info(Tag, $"║     Purpose 7 (ad measurement): {ConsentHelper.IsPurposeGranted(7)}");
            SDKLogger.Info(Tag, $"║     Purpose 9 (market research):{ConsentHelper.IsPurposeGranted(9)}");
            SDKLogger.Info(Tag, $"║     Purpose 10 (dev/improve):   {ConsentHelper.IsPurposeGranted(10)}");

            SDKLogger.Info(Tag, "║ ── Key Vendor Consent ──");
            SDKLogger.Info(Tag, $"║     Vendor 31  (Meta/Facebook):  {ConsentHelper.IsVendorGranted(31)}");
            SDKLogger.Info(Tag, $"║     Vendor 32  (Unity Ads):      {ConsentHelper.IsVendorGranted(32)}");
            SDKLogger.Info(Tag, $"║     Vendor 35  (Vungle):         {ConsentHelper.IsVendorGranted(35)}");
            SDKLogger.Info(Tag, $"║     Vendor 702 (Mintegral):      {ConsentHelper.IsVendorGranted(702)}");
            SDKLogger.Info(Tag, $"║     Vendor 755 (Google Ads):     {ConsentHelper.IsVendorGranted(755)}");

            string acString = ConsentHelper.GetAdditionalConsentString();
            SDKLogger.Info(Tag, $"║   IABTCF_AddtlConsent:   {(string.IsNullOrEmpty(acString) ? "(empty)" : acString)}");
            SDKLogger.Info(Tag, $"║     AC Vendor 311 (AppLovin):    {ConsentHelper.IsAdditionalConsentVendorGranted(311)}");

            SDKLogger.Info(Tag, "╚══════════════════════════════════════════════════════════╝");
        }

        private static string Gd(bool granted) => granted ? "GRANTED" : "DENIED";

        /// <summary>
        /// Enable/disable Facebook data collection based on consent.
        /// Manifest defaults AutoLogAppEventsEnabled=false and AdvertiserIDCollectionEnabled=false.
        /// This method enables them programmatically when user grants consent.
        /// </summary>
        /// <summary>
        /// Pending Facebook consent to apply when FB.Init() is called later.
        /// Null means no pending consent.
        /// </summary>
        private static ConsentStatus? _pendingFacebookConsent;

        /// <summary>
        /// Call this from game code after FB.Init() completes to apply pending consent.
        /// Example: FB.Init(() => ConsentManager.ApplyPendingFacebookConsent());
        /// </summary>
        public static void ApplyPendingFacebookConsent() {
            #if HAS_FACEBOOK_SDK
            if (!_pendingFacebookConsent.HasValue) return;
            var status = _pendingFacebookConsent.Value;
            _pendingFacebookConsent = null;
            ApplyFacebookConsentInternal(status);
            #endif
        }

        private static void ApplyFacebookConsent(ConsentStatus status) {
            #if HAS_FACEBOOK_SDK
            if (!Facebook.Unity.FB.IsInitialized) {
                // FB not initialized yet — store consent and register callback.
                // FacebookModule will call OnFacebookInitialized after FB.Init().
                SDKLogger.Debug(Tag,
                    "Facebook SDK not initialized. Consent stored as pending.");
                _pendingFacebookConsent = status;

                // Register once to apply when FacebookModule finishes FB.Init()
                FacebookModule.OnFacebookInitialized -= ApplyPendingFacebookConsent;
                FacebookModule.OnFacebookInitialized += ApplyPendingFacebookConsent;
                return;
            }

            ApplyFacebookConsentInternal(status);
            #endif
        }

        #if HAS_FACEBOOK_SDK
        private static void ApplyFacebookConsentInternal(ConsentStatus status) {
            try {
                Facebook.Unity.FB.Mobile.SetAutoLogAppEventsEnabled(status.CanCollectAnalytics);
                Facebook.Unity.FB.Mobile.SetAdvertiserIDCollectionEnabled(status.CanTrackAttribution);

                // CCPA: Limited Data Use (LDU) — restricts Facebook data processing
                Facebook.Unity.FB.Mobile.SetDataProcessingOptions(
                    new string[] { status.IsDoNotSell ? "LDU" : "" }, 0, 0);

                #if UNITY_IOS
                Facebook.Unity.FB.Mobile.SetAdvertiserTrackingEnabled(status.HasAttConsent);
                #endif

                SDKLogger.Info(Tag,
                    $"Facebook consent applied — AutoLog={status.CanCollectAnalytics}, " +
                    $"AdIDCollection={status.CanTrackAttribution}, " +
                    $"LDU={status.IsDoNotSell}");
            } catch (System.Exception e) {
                SDKLogger.Warning(Tag, $"Facebook consent apply failed: {e.Message}");
            }
        }
        #endif

        private bool HasCachedConsent() {
            return PlayerPrefs.HasKey(ConsentPrefsKey);
        }

        private ConsentStatus LoadCachedConsent() {
            bool granted = PlayerPrefs.GetInt(ConsentGrantedKey, 1) == 1;
            bool eea = PlayerPrefs.GetInt(ConsentEeaKey, 0) == 1;
            bool dns = PlayerPrefs.GetInt(ConsentDnsKey, 0) == 1;
            return new ConsentStatus(
                canShowPersonalizedAds: granted,
                canCollectAnalytics: granted,
                canTrackAttribution: granted,
                isEeaUser: eea,
                hasAttConsent: granted,
                source: ConsentSource.Manual,
                isDoNotSell: dns);
        }

        private void SaveConsent(ConsentStatus status) {
            PlayerPrefs.SetInt(ConsentPrefsKey, 1);
            PlayerPrefs.SetInt(ConsentGrantedKey, status.CanShowPersonalizedAds ? 1 : 0);
            PlayerPrefs.SetInt(ConsentEeaKey, status.IsEeaUser ? 1 : 0);
            PlayerPrefs.SetInt(ConsentDnsKey, status.IsDoNotSell ? 1 : 0);
            PlayerPrefs.Save();
        }

        #if UNITY_IOS
        private void RequestATTWithDelay(float delay, Action onComplete) {
            SDKLogger.Info(Tag, $"Requesting iOS ATT (delay={delay}s)...");

            var dispatcher = UnityMainThreadDispatcher.Instance;
            if (dispatcher == null) {
                SDKLogger.Warning(Tag,
                    "UnityMainThreadDispatcher not available. Skipping ATT request.");
                onComplete?.Invoke();
                return;
            }

            bool attCompleted = false;

            // Timeout — if ATT callback never fires (e.g., simulator, old iOS)
            dispatcher.EnqueueDelayed(delay + 30f, () => {
                if (attCompleted) return;
                attCompleted = true;
                SDKLogger.Warning(Tag, "ATT request timed out. Using current consent.");
                BroadcastConsent();
                onComplete?.Invoke();
            });

            dispatcher.EnqueueDelayed(delay, () => {
                try {
                    Unity.Advertisement.IosSupport.ATTrackingStatusBinding
                        .RequestAuthorizationTrackingWithCompletionHandler(status => {
                            if (attCompleted) return;
                            attCompleted = true;

                            bool attGranted = status ==
                                Unity.Advertisement.IosSupport.ATTrackingStatusBinding
                                    .AuthorizationTrackingStatus.AUTHORIZED;

                            SDKLogger.Info(Tag, $"ATT result: {status}, granted={attGranted}");

                            CurrentStatus = new ConsentStatus(
                                canShowPersonalizedAds: CurrentStatus.CanShowPersonalizedAds,
                                canCollectAnalytics: CurrentStatus.CanCollectAnalytics,
                                canTrackAttribution: attGranted,
                                isEeaUser: CurrentStatus.IsEeaUser,
                                hasAttConsent: attGranted,
                                source: ConsentSource.ATT,
                                isDoNotSell: CurrentStatus.IsDoNotSell);

                            SaveConsent(CurrentStatus);
                            BroadcastConsent();
                            onComplete?.Invoke();
                        });
                } catch (System.Exception e) {
                    if (attCompleted) return;
                    attCompleted = true;
                    SDKLogger.Warning(Tag, $"ATT request failed: {e.Message}. Skipping.");
                    BroadcastConsent();
                    onComplete?.Invoke();
                }
            });
        }
        #endif
    }
}
