#if HAS_FIREBASE_SDK
using Firebase;
using Firebase.Analytics;
#endif
using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking {

    /// <summary>
    /// Firebase Analytics tracking provider.
    /// Requires FirebaseModule to be initialized first (dependency enforced by TrackingManager).
    ///
    /// Consent Mode v2 — 4 consent types per Google docs:
    ///   AnalyticsStorage  → CanCollectAnalytics     (analytics cookie/storage)
    ///   AdStorage         → CanShowPersonalizedAds  (ad cookie/storage)
    ///   AdUserData        → CanTrackAttribution     (advertiser ID for attribution)
    ///   AdPersonalization → CanShowPersonalizedAds  (personalized ads)
    /// </summary>
    public class FirebaseTrackingProvider : ITrackingProvider {
        public string ProviderId => "firebase";

        private bool _isReady;
        private Core.ConsentStatus _currentConsent = Core.ConsentStatus.Default;

        private readonly Dictionary<string, object> _cachedParams = new Dictionary<string, object>(20);

        public void Initialize(Action<bool> onInitialized = null) {
            #if HAS_FIREBASE_SDK
            if (!FirebaseInitializer.IsAvailable) {
                SDKLogger.Error("Firebase", "Analytics init skipped — Firebase not available.");
                onInitialized?.Invoke(false);
                return;
            }

            try {
                ApplyFirebaseConsent(_currentConsent);
            } catch (Exception e) {
                SDKLogger.Error("Firebase", $"Analytics setup error: {e.Message}");
            }

            _isReady = true;
            SDKLogger.Info("Firebase", "Firebase Analytics ready.");
            onInitialized?.Invoke(true);
            #else
            _isReady = true;
            SDKLogger.Info("Firebase", "Firebase (No SDK). Stub provider active.");
            onInitialized?.Invoke(true);
            #endif
        }

        #if HAS_FIREBASE_SDK
        /// <summary>
        /// Apply consent to Firebase Analytics using Consent Mode v2.
        /// Maps 1:1 to Google consent types per TCF Purpose mapping:
        ///   AnalyticsStorage  ← CanCollectAnalytics
        ///   AdStorage         ← CanStoreAdData        (TCF Purpose 1)
        ///   AdUserData        ← CanTrackAttribution   (TCF Purpose 1, 7)
        ///   AdPersonalization ← CanShowPersonalizedAds (TCF Purpose 3, 4)
        /// </summary>
        private static void ApplyFirebaseConsent(Core.ConsentStatus consent) {
            var analyticsStorage = consent.CanCollectAnalytics
                ? Firebase.Analytics.ConsentStatus.Granted
                : Firebase.Analytics.ConsentStatus.Denied;
            var adStorage = consent.CanStoreAdData
                ? Firebase.Analytics.ConsentStatus.Granted
                : Firebase.Analytics.ConsentStatus.Denied;
            var adUserData = consent.CanTrackAttribution
                ? Firebase.Analytics.ConsentStatus.Granted
                : Firebase.Analytics.ConsentStatus.Denied;
            var adPersonalization = consent.CanShowPersonalizedAds
                ? Firebase.Analytics.ConsentStatus.Granted
                : Firebase.Analytics.ConsentStatus.Denied;

            FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, Firebase.Analytics.ConsentStatus> {
                { ConsentType.AnalyticsStorage, analyticsStorage },
                { ConsentType.AdStorage, adStorage },
                { ConsentType.AdUserData, adUserData },
                { ConsentType.AdPersonalization, adPersonalization },
            });

            FirebaseAnalytics.SetAnalyticsCollectionEnabled(consent.CanCollectAnalytics);

            SDKLogger.Info("Firebase",
                $"  Consent applied: AnalyticsStorage={analyticsStorage}, AdStorage={adStorage}, " +
                $"AdUserData={adUserData}, AdPersonalization={adPersonalization}, " +
                $"Collection={consent.CanCollectAnalytics}");
        }
        #endif

        public void TrackEvent(GameTrackingEvent gameEvent) {
            #if HAS_FIREBASE_SDK
            if (!_isReady) return;

            _cachedParams.Clear();
            gameEvent.FillParams(_cachedParams);

            int count = _cachedParams.Count;
            if (count == 0) {
                FirebaseAnalytics.LogEvent(gameEvent.EventName);
                return;
            }

            var parameters = new Parameter[count];
            int index = 0;

            foreach (var kvp in _cachedParams) {
                if (kvp.Value is string s) parameters[index] = new Parameter(kvp.Key, s);
                else if (kvp.Value is long l) parameters[index] = new Parameter(kvp.Key, l);
                else if (kvp.Value is int i) parameters[index] = new Parameter(kvp.Key, i);
                else if (kvp.Value is double d) parameters[index] = new Parameter(kvp.Key, d);
                else if (kvp.Value is float f) parameters[index] = new Parameter(kvp.Key, f);
                else parameters[index] = new Parameter(kvp.Key, kvp.Value.ToString());
                index++;
            }

            FirebaseAnalytics.LogEvent(gameEvent.EventName, parameters);
            #endif
        }

        public void TrackAdRevenue(string adPlatform, string adSource, string adFormat,
            string adUnitName, string currency, double value, string placement) {
            #if HAS_FIREBASE_SDK
            if (!_isReady) return;

            var parameters = new Parameter[] {
                new Parameter("ad_platform", adPlatform ?? ""),
                new Parameter("ad_source", adSource ?? ""),
                new Parameter("ad_format", adFormat ?? ""),
                new Parameter("ad_unit_name", adUnitName ?? ""),
                new Parameter("currency", currency ?? "USD"),
                new Parameter("value", value),
                new Parameter("placement", placement ?? ""),
            };

            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAdImpression, parameters);
            SDKLogger.Info("Firebase", $"AdRevenue: {adPlatform}/{adSource} {currency} {value:F6} [{adFormat}]");
            #endif
        }

        public void TrackIAPRevenue(string productId, double revenue, string currency,
            string transactionId, string receipt, string source) {
            #if HAS_FIREBASE_SDK
            if (!_isReady) return;

            string storeName = Application.platform == RuntimePlatform.Android ? "GooglePlay" : "Other";
            Parameter[] purchaseParameters = {
                new Parameter(FirebaseAnalytics.ParameterTransactionID, transactionId),
                new Parameter(FirebaseAnalytics.ParameterItemID, productId),
                new Parameter(FirebaseAnalytics.ParameterValue, revenue),
                new Parameter(FirebaseAnalytics.ParameterCurrency, currency),
                new Parameter(FirebaseAnalytics.ParameterAffiliation, storeName)
            };

            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventPurchase, purchaseParameters);
            SDKLogger.Info("Firebase", $"IAPRevenue: {productId} {currency} {revenue:F2} txn={transactionId}");
            #endif
        }

        public void SetUserId(string userId) {
            #if HAS_FIREBASE_SDK
            if (_isReady) FirebaseAnalytics.SetUserId(userId);
            #endif
        }

        public void SetUserProperty(string key, string value) {
            #if HAS_FIREBASE_SDK
            if (_isReady) FirebaseAnalytics.SetUserProperty(key, value);
            #endif
        }

        public void SetConsent(Core.ConsentStatus consent) {
            #if HAS_FIREBASE_SDK
            _currentConsent = consent;

            if (_isReady) {
                try {
                    ApplyFirebaseConsent(consent);
                } catch (Exception e) {
                    SDKLogger.Error("Firebase", $"SetConsent failed: {e.Message}");
                }
            }
            #endif
        }
    }
}
