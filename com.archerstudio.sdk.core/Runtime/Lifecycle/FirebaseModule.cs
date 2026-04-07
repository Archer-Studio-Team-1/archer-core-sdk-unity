using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Shared Firebase initialization module.
    /// Runs CheckAndFixDependenciesAsync once, before any module that depends on Firebase.
    /// Priority 10 = after consent (0), before tracking/crashreporting (20).
    /// Implements Consent Mode v2 by calling FirebaseAnalytics.setConsent() via native bridge.
    /// </summary>
    public class FirebaseModule : ISDKModule {
        private const string Tag = "Firebase";

        public string ModuleId => "firebase";
        public int InitializationPriority => 10;
        public IReadOnlyList<string> Dependencies => new[] { "consent" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;

            SDKLogger.Info(Tag, "┌─── Firebase Module ───");
            SDKLogger.Info(Tag, "│ Initializing shared Firebase dependencies...");
            SDKLogger.Info(Tag, "└───────────────────────");

            FirebaseInitializer.EnsureInitialized(available => {
                if (available) {
                    State = ModuleState.Ready;
                    SDKLogger.Info(Tag, "Firebase Module ready — all providers can proceed.");
                } else {
                    State = ModuleState.Failed;
                    SDKLogger.Error(Tag, "Firebase Module failed — providers will use stubs.");
                }

                // Always complete — don't block SDK init even if Firebase fails
                onComplete?.Invoke(true);
            });
        }

        public void OnConsentChanged(ConsentStatus consent) {
            ApplyFirebaseConsentMode(consent);
        }

        /// <summary>
        /// Call Firebase Analytics setConsent() via native bridge.
        /// Firebase Unity SDK does not expose setConsent() directly.
        /// Maps ConsentStatus fields to Consent Mode v2 types:
        ///   CanStoreAdData        -> AD_STORAGE
        ///   CanCollectAnalytics   -> ANALYTICS_STORAGE
        ///   CanTrackAttribution   -> AD_USER_DATA
        ///   CanShowPersonalizedAds -> AD_PERSONALIZATION
        /// </summary>
        private static void ApplyFirebaseConsentMode(ConsentStatus consent) {
#if HAS_FIREBASE_APP && UNITY_ANDROID && !UNITY_EDITOR
            ApplyFirebaseConsentAndroid(consent);
#elif HAS_FIREBASE_APP && UNITY_IOS && !UNITY_EDITOR
            ApplyFirebaseConsentIos(consent);
#else
            SDKLogger.Debug(Tag,
                $"Firebase consent mode skipped (editor/standalone). " +
                $"Values: adStorage={consent.CanStoreAdData}, analytics={consent.CanCollectAnalytics}, " +
                $"adUserData={consent.CanTrackAttribution}, adPersonalization={consent.CanShowPersonalizedAds}");
#endif
        }

#if HAS_FIREBASE_APP && UNITY_ANDROID && !UNITY_EDITOR
        private static void ApplyFirebaseConsentAndroid(ConsentStatus consent) {
            try {
                using var analyticsClass = new AndroidJavaClass("com.google.firebase.analytics.FirebaseAnalytics");
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var analytics = analyticsClass.CallStatic<AndroidJavaObject>("getInstance", activity);

                using var consentTypeClass = new AndroidJavaClass("com.google.firebase.analytics.FirebaseAnalytics$ConsentType");
                using var consentStatusClass = new AndroidJavaClass("com.google.firebase.analytics.FirebaseAnalytics$ConsentStatus");

                var adStorage = consentTypeClass.GetStatic<AndroidJavaObject>("AD_STORAGE");
                var analyticsStorage = consentTypeClass.GetStatic<AndroidJavaObject>("ANALYTICS_STORAGE");
                var adUserData = consentTypeClass.GetStatic<AndroidJavaObject>("AD_USER_DATA");
                var adPersonalization = consentTypeClass.GetStatic<AndroidJavaObject>("AD_PERSONALIZATION");

                var granted = consentStatusClass.GetStatic<AndroidJavaObject>("GRANTED");
                var denied = consentStatusClass.GetStatic<AndroidJavaObject>("DENIED");

                // Build EnumMap<ConsentType, ConsentStatus>
                using var enumMapClass = new AndroidJavaClass("java.util.EnumMap");
                using var consentMap = new AndroidJavaObject("java.util.EnumMap", consentTypeClass);

                consentMap.Call<AndroidJavaObject>("put", adStorage, consent.CanStoreAdData ? granted : denied);
                consentMap.Call<AndroidJavaObject>("put", analyticsStorage, consent.CanCollectAnalytics ? granted : denied);
                consentMap.Call<AndroidJavaObject>("put", adUserData, consent.CanTrackAttribution ? granted : denied);
                consentMap.Call<AndroidJavaObject>("put", adPersonalization, consent.CanShowPersonalizedAds ? granted : denied);

                analytics.Call("setConsent", consentMap);

                SDKLogger.Info(Tag,
                    $"Firebase consent mode set: " +
                    $"AD_STORAGE={Granted(consent.CanStoreAdData)}, " +
                    $"ANALYTICS_STORAGE={Granted(consent.CanCollectAnalytics)}, " +
                    $"AD_USER_DATA={Granted(consent.CanTrackAttribution)}, " +
                    $"AD_PERSONALIZATION={Granted(consent.CanShowPersonalizedAds)}");
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Failed to set Firebase consent mode (Android): {e.Message}");
            }
        }
#endif

#if HAS_FIREBASE_APP && UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void ArcherSDK_SetFirebaseConsent(
            bool adStorage, bool analyticsStorage, bool adUserData, bool adPersonalization);

        private static void ApplyFirebaseConsentIos(ConsentStatus consent) {
            try {
                ArcherSDK_SetFirebaseConsent(
                    consent.CanStoreAdData,
                    consent.CanCollectAnalytics,
                    consent.CanTrackAttribution,
                    consent.CanShowPersonalizedAds);

                SDKLogger.Info(Tag,
                    $"Firebase consent mode set: " +
                    $"AD_STORAGE={Granted(consent.CanStoreAdData)}, " +
                    $"ANALYTICS_STORAGE={Granted(consent.CanCollectAnalytics)}, " +
                    $"AD_USER_DATA={Granted(consent.CanTrackAttribution)}, " +
                    $"AD_PERSONALIZATION={Granted(consent.CanShowPersonalizedAds)}");
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Failed to set Firebase consent mode (iOS): {e.Message}");
            }
        }
#endif

        private static string Granted(bool value) => value ? "granted" : "denied";

        public void Dispose() {
            State = ModuleState.Disposed;
        }
    }
}
