#if HAS_ADJUST_SDK
using AdjustSdk;
#endif

using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking {

    /// <summary>
    /// Full Adjust SDK v5 tracking provider.
    /// Supports: events, revenue, ad revenue, subscriptions, attribution,
    /// push token, GDPR, third-party sharing, measurement consent,
    /// COPPA, offline mode, global parameters, deep link processing, LinkMe.
    /// </summary>
    public class AdjustTrackingProvider : ITrackingProvider {
        public string ProviderId => "adjust";

        private bool _isInited;
        private bool _consentApplied;
        private bool _sdkStarted;
        private Core.ConsentStatus _pendingMeasurementConsent;
        private readonly TrackingConfig _config;
        private readonly Core.ConsentStatus _initialConsent;

        // Optimization: Reuse dictionary
        private readonly Dictionary<string, object> _cachedParams = new Dictionary<string, object>(20);

        public AdjustTrackingProvider(TrackingConfig config, Core.ConsentStatus initialConsent) {
            _config = config;
            _initialConsent = initialConsent;
        }

        public void Initialize(Action<bool> onInitialized = null) {
            #if HAS_ADJUST_SDK
            // PRODUCTION symbol forces production environment regardless of other flags.
            // Without PRODUCTION: sandbox when UseSandboxInDebug=true OR Debug.isDebugBuild=true.
            #if PRODUCTION
            var environment = AdjustEnvironment.Production;
            string envReason = "Production (PRODUCTION symbol defined)";
            #else
            var environment = _config.UseSandboxInDebug || Debug.isDebugBuild
                ? AdjustEnvironment.Sandbox
                : AdjustEnvironment.Production;

            string envReason = _config.UseSandboxInDebug || Debug.isDebugBuild
                ? $"Sandbox (UseSandboxInDebug={_config.UseSandboxInDebug}, isDebugBuild={Debug.isDebugBuild})"
                : "Production (no debug flags)";
            #endif

            SDKLogger.Info("Adjust", "═══════════════════════════════════════");
            SDKLogger.Info("Adjust", "  Adjust SDK Initialization");
            SDKLogger.Info("Adjust", "═══════════════════════════════════════");
            SDKLogger.Info("Adjust", $"  App Token:    {MaskToken(_config.AdjustAppToken)}");
            SDKLogger.Info("Adjust", $"  Environment:  {envReason}");
            SDKLogger.Info("Adjust", $"  Platform:     {Application.platform}");
            SDKLogger.Info("Adjust", $"  DebugBuild:   {Debug.isDebugBuild}");

            var adjustConfig = new AdjustConfig(_config.AdjustAppToken, environment) {
                SessionSuccessDelegate = OnSessionSuccess,
                SessionFailureDelegate = OnSessionFailure,
                EventSuccessDelegate = OnEventSuccess,
                EventFailureDelegate = OnEventFailure,
                AttributionChangedDelegate = OnAttributionChanged
            };

            // ─── Log Level ───
            #if PRODUCTION
            adjustConfig.LogLevel = AdjustLogLevel.Suppress;
            SDKLogger.Debug("Adjust", "  Log Level:    Suppress (PRODUCTION)");
            #else
            if (_config.AdjustLogLevel != AdjustLogLevel.Info) {
                adjustConfig.LogLevel = _config.AdjustLogLevel;
            }
            SDKLogger.Debug("Adjust", $"  Log Level:    {_config.AdjustLogLevel}");
            #endif

            // ─── COPPA Compliance ───
            if (_config.EnableCoppaCompliance) {
                adjustConfig.IsCoppaComplianceEnabled = true;
            }
            SDKLogger.Debug("Adjust", $"  COPPA:        {_config.EnableCoppaCompliance}");

            // ─── Background Sending ───
            if (_config.EnableSendInBackground) {
                adjustConfig.IsSendingInBackgroundEnabled = true;
            }
            SDKLogger.Debug("Adjust", $"  SendInBg:     {_config.EnableSendInBackground}");

            // ─── External Device ID ───
            if (!string.IsNullOrEmpty(_config.ExternalDeviceId)) {
                adjustConfig.ExternalDeviceId = _config.ExternalDeviceId;
                SDKLogger.Debug("Adjust", $"  ExtDeviceId:  {_config.ExternalDeviceId}");
            }

            // ─── Default Tracker ───
            if (!string.IsNullOrEmpty(_config.DefaultTracker)) {
                adjustConfig.DefaultTracker = _config.DefaultTracker;
                SDKLogger.Debug("Adjust", $"  DefTracker:   {_config.DefaultTracker}");
            }

            // ─── LinkMe ───
            if (_config.EnableLinkMe) {
                adjustConfig.IsLinkMeEnabled = true;
            }
            SDKLogger.Debug("Adjust", $"  LinkMe:       {_config.EnableLinkMe}");

            // ─── Deferred Deep Link ───
            if (_config.EnableDeferredDeepLinkOpening) {
                adjustConfig.IsDeferredDeeplinkOpeningEnabled = true;
            }
            SDKLogger.Debug("Adjust", $"  DeferredDL:   {_config.EnableDeferredDeepLinkOpening}");

            // ─── Store Info ───
            string storeName = _config.StoreName;
            string storeAppId = _config.StoreAppId;

            #if UNITY_IOS
            // On iOS, store is always appstore. Auto-fill if empty.
            if (string.IsNullOrEmpty(storeName)) storeName = "appstore";
            #elif UNITY_ANDROID
            // On Android, default to google if not specified.
            if (string.IsNullOrEmpty(storeName)) storeName = "google";
            #endif

            if (!string.IsNullOrEmpty(storeName)) {
                var storeInfo = new AdjustStoreInfo(storeName);
                if (!string.IsNullOrEmpty(storeAppId)) {
                    storeInfo.StoreAppId = storeAppId;
                }
                adjustConfig.StoreInfo = storeInfo;
                SDKLogger.Info("Adjust", $"  Store Name:   {storeName}");
                SDKLogger.Info("Adjust", $"  Store AppId:  {storeAppId ?? "(not set)"}");
            }

            // ─── Meta Install Referrer ───
            if (!string.IsNullOrEmpty(_config.MetaAppId)) {
                adjustConfig.FbAppId = _config.MetaAppId;
                SDKLogger.Debug("Adjust", $"  Meta AppId:   {_config.MetaAppId}");
            }

            // ─── OAID Plugin (Android only) ───
            #if UNITY_ANDROID
            if (_config.EnableOaid) {
                SDKLogger.Debug("Adjust", "  OAID:         Enabling...");
                AdjustOaidPlugin.ReadOaid();
                SDKLogger.Debug("Adjust", $"  OAID:         ReadOaid() called (initialized={AdjustOaidPlugin.IsInitialized})");
            } else {
                SDKLogger.Debug("Adjust", "  OAID:         Disabled");
            }
            #endif

            // ─── URL Strategy ───
            // adjustConfig.UrlStrategy can be set if needed for data residency

            // ─── Verify config before init ───
            SDKLogger.Info("Adjust", "───────────────────────────────────────");
            if (adjustConfig.StoreInfo != null) {
                SDKLogger.Info("Adjust",
                    $"  [Verify] AdjustConfig.StoreInfo → " +
                    $"StoreName={adjustConfig.StoreInfo.StoreName ?? "(null)"}, " +
                    $"StoreAppId={adjustConfig.StoreInfo.StoreAppId ?? "(null)"}");
            }

            // ─── CRITICAL: Set consent/DMA BEFORE InitSdk ───
            // Adjust sends install event immediately on InitSdk().
            // TrackThirdPartySharing must be called first so DMA params
            // are included in the install request.
            ApplyConsentBeforeInit(_initialConsent);

            SDKLogger.Info("Adjust", "  Calling Adjust.InitSdk()...");
            try {
                Adjust.InitSdk(adjustConfig);
            } catch (Exception e) {
                SDKLogger.Error("Adjust", $"  Adjust.InitSdk() failed: {e.Message}");
            }

            SDKLogger.Info("Adjust", "═══════════════════════════════════════");

            // Always complete — don't wait for session callbacks and don't block on errors.
            _isInited = true;
            onInitialized?.Invoke(true);

            #else
            _isInited = true;
            onInitialized?.Invoke(true);
            SDKLogger.Info("Adjust", "Initialized (No SDK).");
            #endif
        }

        #if HAS_ADJUST_SDK

        // ─── Session Callbacks ───

        private void OnSessionSuccess(AdjustSessionSuccess data) {
            SDKLogger.Info("Adjust", $"Session success — timestamp={data.Timestamp}");

            // SDK is now fully started — safe to call TrackMeasurementConsent
            if (!_sdkStarted) {
                _sdkStarted = true;
                Adjust.TrackMeasurementConsent(_pendingMeasurementConsent.CanCollectAnalytics);
                SDKLogger.Info("Adjust",
                    $"  MeasurementConsent={_pendingMeasurementConsent.CanCollectAnalytics} (sent on session start)");
            }

            Adjust.GetAdid(id => {
                TrackingManager.Instance?.UpdateUserProfile(p => { p.AdjustId = id ?? "Null"; });
                SDKLogger.Info("Adjust", $"  ADID: {id ?? "(null)"}");
            });

            Adjust.GetSdkVersion(ver => {
                SDKLogger.Info("Adjust", $"  SDK Version: {ver ?? "(null)"}");
            });

            #if UNITY_ANDROID
            Adjust.GetGoogleAdId(gaid => {
                SDKLogger.Info("Adjust", $"  Google AdId: {gaid ?? "(null)"}");
            });
            #endif

            #if UNITY_IOS
            Adjust.GetIdfa(idfa => {
                SDKLogger.Info("Adjust", $"  IDFA: {idfa ?? "(null)"}");
            });
            Adjust.GetIdfv(idfv => {
                SDKLogger.Info("Adjust", $"  IDFV: {idfv ?? "(null)"}");
            });
            #endif

            Adjust.GetAttribution(OnAttributionChanged);
        }

        private void OnSessionFailure(AdjustSessionFailure data) {
            SDKLogger.Error("Adjust",
                $"Session failed — msg={data.Message}, willRetry={data.WillRetry}, " +
                $"timestamp={data.Timestamp}");
        }

        // ─── Event Callbacks ───

        private void OnEventSuccess(AdjustEventSuccess data) {
            SDKLogger.Debug("Adjust",
                $"Event success: token={data.EventToken}, callbackId={data.CallbackId}");
        }

        private void OnEventFailure(AdjustEventFailure data) {
            SDKLogger.Warning("Adjust",
                $"Event failed: token={data.EventToken}, msg={data.Message}, willRetry={data.WillRetry}");
        }

        // ─── Attribution ───

        private void OnAttributionChanged(AdjustAttribution attribution) {
            SDKLogger.Info("Adjust",
                $"Attribution: network={attribution.Network}, " +
                $"campaign={attribution.Campaign}, " +
                $"adgroup={attribution.Adgroup}, " +
                $"creative={attribution.Creative}");
        }

        #endif

        // ─── Event Tracking ───

        /// <summary>
        /// Track a custom Adjust event. Only for custom events with a token.
        /// Do NOT use for purchase (use VerifyAndTrack*Purchase) or
        /// ad revenue (use TrackAdRevenue).
        /// </summary>
        public void TrackEvent(GameTrackingEvent gameEvent) {
            /*#if HAS_ADJUST_SDK
            if (!_isInited) {
                SDKLogger.Warning("Adjust", $"TrackEvent '{gameEvent.EventName}' skipped — not ready.");
                return;
            }

            string token = gameEvent.AdjustToken;
            if (string.IsNullOrEmpty(token)) return;

            SDKLogger.Verbose("Adjust", $"TrackEvent: {gameEvent.EventName} (token={token})");

            var adjustEvent = new AdjustEvent(token);

            // Deduplication
            if (!string.IsNullOrEmpty(gameEvent.DeduplicationId)) {
                adjustEvent.DeduplicationId = gameEvent.DeduplicationId;
            }

            // Callback ID
            if (!string.IsNullOrEmpty(gameEvent.CallbackId)) {
                adjustEvent.CallbackId = gameEvent.CallbackId;
            }

            // Partner + Callback parameters from event params
            _cachedParams.Clear();
            gameEvent.FillParams(_cachedParams);

            foreach (var kvp in _cachedParams) {
                string val = kvp.Value?.ToString() ?? "";
                adjustEvent.AddPartnerParameter(kvp.Key, val);
                adjustEvent.AddCallbackParameter(kvp.Key, val);
            }

            Adjust.TrackEvent(adjustEvent);
            #endif*/
        }

        // ─── IAP Revenue (ITrackingProvider) ───

        /// <summary>
        /// Verify and track IAP revenue with Adjust.
        /// Auto-detects platform:
        /// - iOS: VerifyAndTrackAppStorePurchase with transactionId
        /// - Android: VerifyAndTrackPlayStorePurchase with purchaseToken extracted from receipt
        /// </summary>
        public void TrackIAPRevenue(string productId, double revenue, string currency,
            string transactionId, string receipt, string source) {
            #if HAS_ADJUST_SDK
            if (!_isInited) {
                SDKLogger.Warning("Adjust", $"TrackIAPRevenue '{productId}' skipped — not ready.");
                return;
            }

            if (revenue <= 0) {
                SDKLogger.Warning("Adjust",
                    $"TrackIAPRevenue skipped — revenue={revenue} for {productId}.");
                return;
            }

            if (string.IsNullOrEmpty(_config.AdjustTokenPurchase)) {
                SDKLogger.Warning("Adjust",
                    "TrackIAPRevenue skipped — AdjustTokenPurchase not set in TrackingConfig.");
                return;
            }

            SDKLogger.Info("Adjust", "┌─── TrackIAPRevenue ───");
            SDKLogger.Info("Adjust", $"│ API:         VerifyAndTrack*Purchase");
            SDKLogger.Info("Adjust", $"│ Token:       {MaskToken(_config.AdjustTokenPurchase)}");
            SDKLogger.Info("Adjust", $"│ Product:     {productId}");
            SDKLogger.Info("Adjust", $"│ Revenue:     {revenue:F2} {currency}");
            SDKLogger.Info("Adjust", $"│ Transaction: {transactionId}");
            SDKLogger.Info("Adjust", $"│ Source:      {source}");

            #if UNITY_IOS
            SDKLogger.Info("Adjust", $"│ Platform:    iOS (AppStore)");
            SDKLogger.Info("Adjust", "└───────────────────────");
            VerifyAndTrackAppStorePurchase(
                _config.AdjustTokenPurchase,
                revenue, currency, productId, transactionId ?? "");
            #elif UNITY_ANDROID
            string purchaseToken = ExtractGooglePurchaseToken(receipt);
            SDKLogger.Info("Adjust", $"│ Platform:    Android (PlayStore)");
            SDKLogger.Info("Adjust", $"│ PurchaseToken: {(string.IsNullOrEmpty(purchaseToken) ? "(empty)" : purchaseToken.Substring(0, System.Math.Min(8, purchaseToken.Length)) + "...")}");
            SDKLogger.Info("Adjust", "└───────────────────────");
            VerifyAndTrackPlayStorePurchase(
                _config.AdjustTokenPurchase,
                revenue, currency, productId, purchaseToken);
            #endif
            #endif
        }

        private static string ExtractGooglePurchaseToken(string receipt) {
            if (string.IsNullOrEmpty(receipt)) return "";
            try {
                var wrapper = JsonUtility.FromJson<ReceiptWrapper>(receipt);
                if (string.IsNullOrEmpty(wrapper?.Payload)) return "";
                var payload = JsonUtility.FromJson<GooglePayload>(wrapper.Payload);
                if (string.IsNullOrEmpty(payload?.json)) return "";
                var data = JsonUtility.FromJson<GooglePurchaseData>(payload.json);
                return data?.purchaseToken ?? "";
            } catch (Exception e) {
                SDKLogger.Warning("Adjust", $"Failed to extract purchaseToken: {e.Message}");
                return "";
            }
        }

        [Serializable] private class ReceiptWrapper { public string Payload; }
        [Serializable] private class GooglePayload { public string json; }
        [Serializable] private class GooglePurchaseData { public string purchaseToken; }

        // ─── Purchase Verification ───

        /// <summary>
        /// Verify and track an App Store (iOS) purchase with Adjust.
        /// Combines verification + revenue event recording in one call.
        /// See: https://dev.adjust.com/en/sdk/unity/features/purchase-verification
        /// </summary>
        public void VerifyAndTrackAppStorePurchase(
            string eventToken, double revenue, string currency,
            string productId, string transactionId,
            Action<string, int, string> onResult = null) {
            #if HAS_ADJUST_SDK && UNITY_IOS
            if (!_isInited) {
                SDKLogger.Warning("Adjust", "VerifyAndTrackAppStorePurchase skipped — not ready.");
                onResult?.Invoke("not_ready", -1, "Adjust not initialized");
                return;
            }

            var adjustEvent = new AdjustEvent(eventToken);
            adjustEvent.SetRevenue(revenue, currency);
            adjustEvent.ProductId = productId;
            adjustEvent.TransactionId = transactionId;

            SDKLogger.Info("Adjust",
                $"Verifying iOS purchase: product={productId}, txn={transactionId}, " +
                $"revenue={revenue} {currency}");

            Adjust.VerifyAndTrackAppStorePurchase(adjustEvent, result => {
                SDKLogger.Info("Adjust",
                    $"iOS purchase verification: status={result.VerificationStatus}, " +
                    $"code={result.Code}, msg={result.Message}");
                onResult?.Invoke(result.VerificationStatus, result.Code, result.Message);
            });
            #else
            onResult?.Invoke("platform_not_supported", -1, "iOS only");
            #endif
        }

        /// <summary>
        /// Verify and track a Play Store (Android) purchase with Adjust.
        /// Combines verification + revenue event recording in one call.
        /// See: https://dev.adjust.com/en/sdk/unity/features/purchase-verification
        /// </summary>
        private void VerifyAndTrackPlayStorePurchase(
            string eventToken, double revenue, string currency,
            string productId, string purchaseToken,
            Action<string, int, string> onResult = null) {
            #if HAS_ADJUST_SDK && UNITY_ANDROID
            if (!_isInited) {
                SDKLogger.Warning("Adjust", "VerifyAndTrackPlayStorePurchase skipped — not ready.");
                onResult?.Invoke("not_ready", -1, "Adjust not initialized");
                return;
            }

            var adjustEvent = new AdjustEvent(eventToken);
            adjustEvent.SetRevenue(revenue, currency);
            adjustEvent.ProductId = productId;
            adjustEvent.PurchaseToken = purchaseToken;

            SDKLogger.Info("Adjust",
                $"Verifying Android purchase: product={productId}, " +
                $"revenue={revenue} {currency}");

            Adjust.VerifyAndTrackPlayStorePurchase(adjustEvent, result => {
                SDKLogger.Info("Adjust",
                    $"Android purchase verification: status={result.VerificationStatus}, " +
                    $"code={result.Code}, msg={result.Message}");
                onResult?.Invoke(result.VerificationStatus, result.Code, result.Message);
            });
            #else
            onResult?.Invoke("platform_not_supported", -1, "Android only");
            #endif
        }

        /// <summary>
        /// Verify an App Store purchase without recording a revenue event.
        /// Use this when you only need verification status.
        /// </summary>
        private void VerifyAppStorePurchase(
            string productId, string transactionId,
            Action<string, int, string> onResult = null) {
            #if HAS_ADJUST_SDK && UNITY_IOS
            if (!_isInited) {
                onResult?.Invoke("not_ready", -1, "Adjust not initialized");
                return;
            }

            var purchase = new AdjustAppStorePurchase(productId, transactionId);

            SDKLogger.Debug("Adjust", $"Verifying iOS purchase (no event): product={productId}");

            Adjust.VerifyAppStorePurchase(purchase, result => {
                SDKLogger.Info("Adjust",
                    $"iOS verify result: status={result.VerificationStatus}, " +
                    $"code={result.Code}, msg={result.Message}");
                onResult?.Invoke(result.VerificationStatus, result.Code, result.Message);
            });
            #else
            onResult?.Invoke("platform_not_supported", -1, "iOS only");
            #endif
        }

        /// <summary>
        /// Verify a Play Store purchase without recording a revenue event.
        /// Use this when you only need verification status.
        /// </summary>
        public void VerifyPlayStorePurchase(
            string productId, string purchaseToken,
            Action<string, int, string> onResult = null) {
            #if HAS_ADJUST_SDK && UNITY_ANDROID
            if (!_isInited) {
                onResult?.Invoke("not_ready", -1, "Adjust not initialized");
                return;
            }

            var purchase = new AdjustPlayStorePurchase(productId, purchaseToken);

            SDKLogger.Debug("Adjust", $"Verifying Android purchase (no event): product={productId}");

            Adjust.VerifyPlayStorePurchase(purchase, result => {
                SDKLogger.Info("Adjust",
                    $"Android verify result: status={result.VerificationStatus}, " +
                    $"code={result.Code}, msg={result.Message}");
                onResult?.Invoke(result.VerificationStatus, result.Code, result.Message);
            });
            #else
            onResult?.Invoke("platform_not_supported", -1, "Android only");
            #endif
        }

        // ─── Ad Revenue (ITrackingProvider) ───

        public void TrackAdRevenue(string adPlatform, string adSource, string adFormat,
            string adUnitName, string currency, double value, string placement) {
            #if HAS_ADJUST_SDK
            if (!_isInited) return;

            var adRevenue = new AdjustAdRevenue(adPlatform);
            adRevenue.SetRevenue(value, currency);
            adRevenue.AdImpressionsCount = 1;

            if (!string.IsNullOrEmpty(adSource)) {
                adRevenue.AdRevenueNetwork = adSource;
            }
            if (!string.IsNullOrEmpty(adUnitName)) {
                adRevenue.AdRevenueUnit = adUnitName;
            }
            if (!string.IsNullOrEmpty(placement)) {
                adRevenue.AdRevenuePlacement = placement;
            }

            Adjust.TrackAdRevenue(adRevenue);
            SDKLogger.Info("Adjust", "┌─── TrackAdRevenue ───");
            SDKLogger.Info("Adjust", $"│ API:         AdjustAdRevenue");
            SDKLogger.Info("Adjust", $"│ Platform:    {adPlatform}");
            SDKLogger.Info("Adjust", $"│ Network:     {adSource}");
            SDKLogger.Info("Adjust", $"│ Format:      {adFormat}");
            SDKLogger.Info("Adjust", $"│ Unit:        {adUnitName}");
            SDKLogger.Info("Adjust", $"│ Revenue:     {value:F6} {currency}");
            SDKLogger.Info("Adjust", $"│ Placement:   {placement}");
            SDKLogger.Info("Adjust", "└──────────────────────");
            #endif
        }

        // ─── Subscription Tracking ───

        /// <summary>
        /// Track App Store subscription for Adjust attribution.
        /// </summary>
        public void TrackAppStoreSubscription(string price, string currency,
            string transactionId, string receipt) {
            #if HAS_ADJUST_SDK && UNITY_IOS
            if (!_isInited) return;

            var subscription = new AdjustAppStoreSubscription(price, currency, transactionId);

            if (!string.IsNullOrEmpty(receipt)) {
                // iOS receipt is set via transactionDate and salesRegion if needed
            }

            Adjust.TrackAppStoreSubscription(subscription);
            SDKLogger.Debug("Adjust", $"iOS subscription tracked: {transactionId}");
            #endif
        }

        /// <summary>
        /// Track Play Store subscription for Adjust attribution.
        /// </summary>
        public void TrackPlayStoreSubscription(string price, string currency,
            string sku, string orderId, string signature, string purchaseToken) {
            #if HAS_ADJUST_SDK && UNITY_ANDROID
            if (!_isInited) return;

            var subscription = new AdjustPlayStoreSubscription(
                price, currency, sku, orderId, signature, purchaseToken);

            Adjust.TrackPlayStoreSubscription(subscription);
            SDKLogger.Debug("Adjust", $"Android subscription tracked: {orderId}");
            #endif
        }

        // ─── Push Token ───

        /// <summary>
        /// Set push notification token for Adjust uninstall tracking.
        /// </summary>
        public void SetPushToken(string token) {
            #if HAS_ADJUST_SDK
            if (string.IsNullOrEmpty(token)) return;
            Adjust.SetPushToken(token);
            SDKLogger.Debug("Adjust", "Push token set.");
            #endif
        }

        // ─── Privacy & Consent ───

        /// <summary>
        /// Apply consent/DMA params BEFORE Adjust.InitSdk() so install event includes DMA.
        /// </summary>
        /// <summary>
        /// Apply consent BEFORE Adjust.InitSdk().
        /// Per Adjust docs: TrackThirdPartySharing and TrackMeasurementConsent
        /// MUST be called before InitSdk to be included in the install event.
        /// See: https://dev.adjust.com/en/sdk/android/features/privacy/
        /// </summary>
        /// <summary>
        /// Build and send TPS with Google DMA + Facebook LDU params.
        /// Used both pre-init and on consent changes.
        /// </summary>
        private static void SendThirdPartySharing(ConsentStatus consent) {
            #if HAS_ADJUST_SDK
            var tps = new AdjustThirdPartySharing(consent.CanTrackAttribution);

            // ─── Google DMA (Digital Markets Act) ───
            // All fields required for full DMA compliance on Adjust dashboard.
            string eea = consent.IsEeaUser ? "1" : "0";
            string adPersonalization = consent.CanShowPersonalizedAds ? "1" : "0";
            string adUserData = consent.CanTrackAttribution ? "1" : "0";
            string adStorage = consent.CanCollectAnalytics ? "1" : "0";
            string npa = consent.CanShowPersonalizedAds ? "0" : "1";

            tps.AddGranularOption("google_dma", "eea", eea);
            tps.AddGranularOption("google_dma", "ad_personalization", adPersonalization);
            tps.AddGranularOption("google_dma", "ad_user_data", adUserData);
            tps.AddGranularOption("google_dma", "ad_storage", adStorage);
            tps.AddGranularOption("google_dma", "npa", npa);

            // ─── Facebook LDU (CCPA) ───
            string fbCountry = consent.IsDoNotSell ? "1" : "0";
            string fbState = consent.IsDoNotSell ? "1000" : "0";
            tps.AddGranularOption("facebook", "data_processing_options_country", fbCountry);
            tps.AddGranularOption("facebook", "data_processing_options_state", fbState);

            Adjust.TrackThirdPartySharing(tps);

            SDKLogger.Info("Adjust",
                $"  TPS google_dma: eea={eea}, ad_personalization={adPersonalization}, " +
                $"ad_user_data={adUserData}, ad_storage={adStorage}, npa={npa}");
            SDKLogger.Info("Adjust",
                $"  TPS facebook: data_processing_options_country={fbCountry}, " +
                $"data_processing_options_state={fbState}");
            #endif
        }

        private void ApplyConsentBeforeInit(ConsentStatus consent) {
            #if HAS_ADJUST_SDK
            SendThirdPartySharing(consent);
            // NOTE: TrackMeasurementConsent requires SDK to be started,
            // called in SetConsent() after InitSdk.
            #endif
        }

        public void SetConsent(ConsentStatus consent) {
            #if HAS_ADJUST_SDK
            // Store consent for MeasurementConsent (sent on session start)
            _pendingMeasurementConsent = consent;

            if (!_consentApplied) {
                // First call after init: TPS already sent pre-init → skip duplicate.
                // MeasurementConsent will be sent in OnSessionSuccess when SDK is started.
                _consentApplied = true;
                SDKLogger.Info("Adjust",
                    $"Post-init consent stored. MeasurementConsent deferred to session start.");
                return;
            }

            // Subsequent calls: consent changed at runtime → re-send TPS + Measurement
            SDKLogger.Info("Adjust",
                $"Consent update: ads={consent.CanShowPersonalizedAds}, " +
                $"attribution={consent.CanTrackAttribution}, eea={consent.IsEeaUser}");
            SendThirdPartySharing(consent);

            if (_sdkStarted) {
                Adjust.TrackMeasurementConsent(consent.CanCollectAnalytics);
            }
            #endif
        }

        /// <summary>
        /// GDPR right to be forgotten. Permanently disables Adjust tracking.
        /// WARNING: This is irreversible. The user must reinstall the app.
        /// </summary>
        public void GdprForgetMe() {
            #if HAS_ADJUST_SDK
            Adjust.GdprForgetMe();
            SDKLogger.Info("Adjust", "GDPR Forget Me sent. Tracking permanently disabled.");
            #endif
        }

        /// <summary>
        /// Enable or disable measurement consent (data sharing with Adjust).
        /// </summary>
        public void TrackMeasurementConsent(bool enabled) {
            #if HAS_ADJUST_SDK
            Adjust.TrackMeasurementConsent(enabled);
            SDKLogger.Debug("Adjust", $"Measurement consent: {enabled}");
            #endif
        }

        // ─── Offline / Online Mode ───

        /// <summary>
        /// Switch to offline mode. Events are queued and sent when back online.
        /// </summary>
        public void SwitchToOfflineMode() {
            #if HAS_ADJUST_SDK
            Adjust.SwitchToOfflineMode();
            SDKLogger.Debug("Adjust", "Switched to offline mode.");
            #endif
        }

        /// <summary>
        /// Switch back to online mode. Queued events will be sent.
        /// </summary>
        public void SwitchBackToOnlineMode() {
            #if HAS_ADJUST_SDK
            Adjust.SwitchBackToOnlineMode();
            SDKLogger.Debug("Adjust", "Switched back to online mode.");
            #endif
        }

        // ─── SDK Control ───

        /// <summary>
        /// Disable Adjust SDK (e.g., user opt-out). Persists across sessions.
        /// </summary>
        public void Disable() {
            #if HAS_ADJUST_SDK
            Adjust.Disable();
            SDKLogger.Info("Adjust", "SDK disabled.");
            #endif
        }

        /// <summary>
        /// Re-enable Adjust SDK after it was disabled.
        /// </summary>
        public void Enable() {
            #if HAS_ADJUST_SDK
            Adjust.Enable();
            SDKLogger.Info("Adjust", "SDK enabled.");
            #endif
        }

        /// <summary>
        /// Check if Adjust SDK is currently enabled.
        /// </summary>
        public void IsEnabled(Action<bool> callback) {
            #if HAS_ADJUST_SDK
            Adjust.IsEnabled(callback);
            #else
            callback?.Invoke(false);
            #endif
        }

        // ─── Deep Link Processing ───

        /// <summary>
        /// Process a deep link URL for Adjust reattribution.
        /// Call this when your app receives a deep link.
        /// </summary>
        public void ProcessDeeplink(string url) {
            #if HAS_ADJUST_SDK
            if (string.IsNullOrEmpty(url)) return;
            var deeplink = new AdjustDeeplink(url);
            Adjust.ProcessDeeplink(deeplink);
            SDKLogger.Debug("Adjust", $"Deep link processed: {url}");
            #endif
        }

        /// <summary>
        /// Process a deep link and get the resolved URL via callback.
        /// </summary>
        public void ProcessAndResolveDeeplink(string url, Action<string> callback) {
            #if HAS_ADJUST_SDK
            if (string.IsNullOrEmpty(url)) {
                callback?.Invoke(null);
                return;
            }
            var deeplink = new AdjustDeeplink(url);
            Adjust.ProcessAndResolveDeeplink(deeplink, resolvedUrl => {
                SDKLogger.Debug("Adjust", $"Resolved deep link: {resolvedUrl}");
                callback?.Invoke(resolvedUrl);
            });
            #else
            callback?.Invoke(url);
            #endif
        }

        // ─── Global Parameters ───

        /// <summary>
        /// Add a global callback parameter sent with every event.
        /// </summary>
        public void AddGlobalCallbackParameter(string key, string value) {
            #if HAS_ADJUST_SDK
            Adjust.AddGlobalCallbackParameter(key, value);
            #endif
        }

        /// <summary>
        /// Remove a global callback parameter.
        /// </summary>
        public void RemoveGlobalCallbackParameter(string key) {
            #if HAS_ADJUST_SDK
            Adjust.RemoveGlobalCallbackParameter(key);
            #endif
        }

        /// <summary>
        /// Remove all global callback parameters.
        /// </summary>
        public void RemoveGlobalCallbackParameters() {
            #if HAS_ADJUST_SDK
            Adjust.RemoveGlobalCallbackParameters();
            #endif
        }

        /// <summary>
        /// Add a global partner parameter sent with every event.
        /// </summary>
        public void AddGlobalPartnerParameter(string key, string value) {
            #if HAS_ADJUST_SDK
            Adjust.AddGlobalPartnerParameter(key, value);
            #endif
        }

        /// <summary>
        /// Remove a global partner parameter.
        /// </summary>
        public void RemoveGlobalPartnerParameter(string key) {
            #if HAS_ADJUST_SDK
            Adjust.RemoveGlobalPartnerParameter(key);
            #endif
        }

        /// <summary>
        /// Remove all global partner parameters.
        /// </summary>
        public void RemoveGlobalPartnerParameters() {
            #if HAS_ADJUST_SDK
            Adjust.RemoveGlobalPartnerParameters();
            #endif
        }

        // ─── Device Info ───

        /// <summary>
        /// Get Adjust device identifier (ADID).
        /// </summary>
        public void GetAdid(Action<string> callback) {
            #if HAS_ADJUST_SDK
            Adjust.GetAdid(callback);
            #else
            callback?.Invoke(null);
            #endif
        }

        /// <summary>
        /// Get current attribution data.
        /// </summary>
        public void GetAttribution(Action<object> callback) {
            #if HAS_ADJUST_SDK
            Adjust.GetAttribution(attr => callback?.Invoke(attr));
            #else
            callback?.Invoke(null);
            #endif
        }

        /// <summary>
        /// Get IDFA (iOS only).
        /// </summary>
        public void GetIdfa(Action<string> callback) {
            #if HAS_ADJUST_SDK && UNITY_IOS
            Adjust.GetIdfa(callback);
            #else
            callback?.Invoke(null);
            #endif
        }

        /// <summary>
        /// Get IDFV (iOS only).
        /// </summary>
        public void GetIdfv(Action<string> callback) {
            #if HAS_ADJUST_SDK && UNITY_IOS
            Adjust.GetIdfv(callback);
            #else
            callback?.Invoke(null);
            #endif
        }

        /// <summary>
        /// Get Google Ads ID (Android only).
        /// </summary>
        public void GetGoogleAdId(Action<string> callback) {
            #if HAS_ADJUST_SDK && UNITY_ANDROID
            Adjust.GetGoogleAdId(callback);
            #else
            callback?.Invoke(null);
            #endif
        }

        /// <summary>
        /// Get Amazon Advertising ID (Fire OS only).
        /// </summary>
        public void GetAmazonAdId(Action<string> callback) {
            #if HAS_ADJUST_SDK && UNITY_ANDROID
            Adjust.GetAmazonAdId(callback);
            #else
            callback?.Invoke(null);
            #endif
        }

        /// <summary>
        /// Get Adjust SDK version.
        /// </summary>
        public void GetSdkVersion(Action<string> callback) {
            #if HAS_ADJUST_SDK
            Adjust.GetSdkVersion(callback);
            #else
            callback?.Invoke("no-sdk");
            #endif
        }

        // ─── Last Deep Link ───

        /// <summary>
        /// Get the last deep link that opened the app.
        /// </summary>
        public void GetLastDeeplink(Action<string> callback) {
            #if HAS_ADJUST_SDK
            Adjust.GetLastDeeplink(callback);
            #else
            callback?.Invoke(null);
            #endif
        }

        // ─── ITrackingProvider (unused for Adjust) ───

        public void SetUserId(string userId) {
            // Adjust identifies users via ADID, not custom user IDs.
            // Use global partner parameter if needed.
            #if HAS_ADJUST_SDK
            if (!string.IsNullOrEmpty(userId)) {
                Adjust.AddGlobalPartnerParameter("user_id", userId);
            }
            #endif
        }

        public void SetUserProperty(string key, string value) {
            // Map user properties as global partner parameters for campaign segmenting
            #if HAS_ADJUST_SDK
            if (!string.IsNullOrEmpty(key)) {
                string partnerKey = key;

                Adjust.AddGlobalPartnerParameter(partnerKey, value ?? "");
                SDKLogger.Verbose("Adjust", $"Partner Param Sync: {partnerKey}={value}");
            }
            #endif
        }

        // ─── Helpers ───

        /// <summary>
        /// Mask token for safe logging (show first 4 chars only).
        /// </summary>
        private static string MaskToken(string token) {
            if (string.IsNullOrEmpty(token)) return "(empty)";
            if (token.Length <= 4) return token;
            return token.Substring(0, 4) + "****";
        }
    }
}