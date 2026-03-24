#if HAS_ADJUST_SDK
using AdjustSdk;
#endif

using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking {

    [CreateAssetMenu(fileName = "TrackingConfig", menuName = "ArcherStudio/SDK/Tracking Config")]
    public class TrackingConfig : ModuleConfigBase {

        [Header("Adjust — Core")]
        [Tooltip("Adjust App Token from your Adjust dashboard.")]
        public string AdjustAppToken = "";

        [Tooltip("Use sandbox environment in debug/development builds.")]
        public bool UseSandboxInDebug = true;

#if HAS_ADJUST_SDK
        [Tooltip("Adjust log level. Use Verbose for debugging, Suppress for production.")]
        public AdjustLogLevel AdjustLogLevel = AdjustLogLevel.Info;
#endif

        [Header("Adjust — Event Tokens")]
        [Tooltip("Adjust event token for IAP purchase revenue tracking.")]
        public string AdjustTokenPurchase = "";

        [Header("Adjust — Privacy")]
        [Tooltip("Enable COPPA compliance for children-targeted apps. Cannot be undone at runtime.")]
        public bool EnableCoppaCompliance;

        [Header("Adjust — Behavior")]
        [Tooltip("Continue sending data when app is in background.")]
        public bool EnableSendInBackground;

        [Tooltip("External device ID for cross-platform attribution.")]
        public string ExternalDeviceId;

        [Tooltip("Preinstalled tracker token.")]
        public string DefaultTracker;

        [Tooltip("Enable LinkMe for deferred deep link matching on iOS.")]
        public bool EnableLinkMe;

        [Tooltip("Allow Adjust to open deferred deep links automatically.")]
        public bool EnableDeferredDeepLinkOpening = true;

        [Header("Adjust — Store Info")]
        [Tooltip("Store name for Adjust attribution (e.g., GooglePlay, AppStore, HuaweiAppGallery, " +
                 "SamsungGalaxyStore, AmazonAppStore, XiaomiGetApps, etc.). Leave empty to skip.")]
        public string StoreName;

        [Tooltip("App ID in the specified store. Leave empty if not applicable.")]
        public string StoreAppId;

        [Header("Adjust — Plugins")]
        [Tooltip("Meta (Facebook) App ID for Meta Install Referrer plugin. " +
                 "Leave empty to disable. Requires: com.adjust.sdk:adjust-android-meta-referrer")]
        public string MetaAppId;

        [Tooltip("Enable OAID reading on Chinese Android devices (Huawei, Xiaomi, OPPO, etc.). " +
                 "Requires: com.adjust.sdk:adjust-android-oaid + optional com.huawei.hms:ads-identifier")]
        public bool EnableOaid;

        [Header("Adjust — Global Parameters")]
        [Tooltip("Global callback parameters sent with every Adjust event.")]
        public List<StringPair> GlobalCallbackParams = new List<StringPair>();

        [Tooltip("Global partner parameters sent with every Adjust event.")]
        public List<StringPair> GlobalPartnerParams = new List<StringPair>();

        [Header("Providers")]
        [Tooltip("Which tracking providers to enable.")]
        public List<TrackingProviderType> EnabledProviders = new List<TrackingProviderType> {
            TrackingProviderType.Firebase,
            TrackingProviderType.Adjust
        };

        [Header("Debug")]
        [Tooltip("Log event tracking details in dev builds.")]
        public bool VerboseLogging = true;
    }

    public enum TrackingProviderType {
        Firebase,
        Adjust
    }

    /// <summary>
    /// Serializable key-value pair for Inspector display.
    /// </summary>
    [Serializable]
    public struct StringPair {
        public string Key;
        public string Value;

        public StringPair(string key, string value) {
            Key = key;
            Value = value;
        }
    }
}
