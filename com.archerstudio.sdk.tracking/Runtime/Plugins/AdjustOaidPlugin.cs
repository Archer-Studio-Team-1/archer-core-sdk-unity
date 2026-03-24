#if HAS_ADJUST_SDK && UNITY_ANDROID
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking {

    /// <summary>
    /// Unity wrapper for Adjust OAID plugin (Android only).
    /// Reads Open Anonymous Device Identifier (OAID) on Chinese Android devices
    /// where Google Play Services is unavailable (Huawei, Xiaomi, OPPO, etc.).
    ///
    /// Requires Maven dependency: com.adjust.sdk:adjust-android-oaid:5.5.1
    /// Optional: com.huawei.hms:ads-identifier:3.4.62.300 (for Huawei devices)
    /// Optional: MSA SDK AAR + supplierconfig.json (for non-Huawei Chinese devices)
    ///
    /// MUST be called BEFORE Adjust.InitSdk().
    /// </summary>
    public static class AdjustOaidPlugin {
        private const string Tag = "Adjust:OAID";
        private const string JavaClass = "com.adjust.sdk.oaid.AdjustOaid";

        private static bool _initialized;

        /// <summary>
        /// Enable OAID reading. Must be called before Adjust.InitSdk().
        /// Uses MSA SDK (if present) or HMS Core SDK (Huawei) to read OAID.
        /// </summary>
        public static void ReadOaid() {
            if (_initialized) return;

            // JNI calls only work on actual Android devices, not in Editor
            if (Application.platform != RuntimePlatform.Android) {
                SDKLogger.Debug(Tag, "OAID skipped — not running on Android device.");
                return;
            }

            try {
                using (var activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = activityClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var appContext = activity.Call<AndroidJavaObject>("getApplicationContext"))
                using (var oaidClass = new AndroidJavaClass(JavaClass)) {
                    oaidClass.CallStatic("readOaid", appContext);
                    _initialized = true;
                    SDKLogger.Info(Tag, "OAID reading enabled.");
                }
            } catch (System.Exception ex) {
                SDKLogger.Warning(Tag,
                    $"Failed to enable OAID reading. " +
                    $"Ensure 'com.adjust.sdk:adjust-android-oaid' is in Dependencies.xml. " +
                    $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check at runtime which OAID-related libraries are available on the classpath.
        /// Logs presence of: AdjustOaid plugin, Huawei HMS Ads Identifier, MSA SDK.
        /// Safe to call from any thread; no-op in Editor.
        /// </summary>
        public static void DiagnoseDependencies() {
            if (Application.platform != RuntimePlatform.Android) {
                SDKLogger.Info(Tag, "Dependency diagnostics only available on Android device.");
                return;
            }

            SDKLogger.Info(Tag, "── OAID Dependency Diagnostics ──");

            // 1. Adjust OAID plugin
            CheckJavaClass("com.adjust.sdk.oaid.AdjustOaid",
                "adjust-android-oaid", "com.adjust.sdk:adjust-android-oaid");

            // 2. Huawei HMS Ads Identifier
            CheckJavaClass("com.huawei.hms.ads.identifier.AdvertisingIdClient",
                "Huawei HMS Ads Identifier", "com.huawei.hms:ads-identifier");

            // 3. MSA SDK (OAID for non-Huawei Chinese devices)
            CheckJavaClass("com.bun.miitmdid.core.MdidSdkHelper",
                "MSA SDK (OAID)", "MSA AAR + supplierconfig.json");

            SDKLogger.Info(Tag, "── End Diagnostics ──");
        }

        private static void CheckJavaClass(string className, string friendlyName, string dependency) {
            try {
                using (new AndroidJavaClass(className)) {
                    // If constructor succeeds without exception, the class exists
                    SDKLogger.Info(Tag, $"  [OK] {friendlyName} — class '{className}' found.");
                }
            } catch (System.Exception) {
                SDKLogger.Warning(Tag,
                    $"  [MISSING] {friendlyName} — class '{className}' not found. " +
                    $"Add dependency: {dependency}");
            }
        }

        /// <summary>
        /// Read the device OAID value directly.
        /// Tries HMS AdvertisingIdClient (on background thread) then MSA SDK (async callback).
        /// Returns null if neither SDK is available.
        /// NOTE: This is a direct device read, independent of Adjust's internal OAID handling.
        /// </summary>
        public static void GetOaid(System.Action<string> callback) {
            if (Application.platform != RuntimePlatform.Android) {
                SDKLogger.Debug(Tag, "GetOaid skipped — not running on Android device.");
                callback?.Invoke(null);
                return;
            }

            // Run on background thread — HMS getAdvertisingIdInfo() is a blocking network call.
            // JNI calls from non-Unity threads require AttachCurrentThread.
            var thread = new System.Threading.Thread(() => {
                AndroidJNI.AttachCurrentThread();
                try {
                    // Try 1: Huawei HMS AdvertisingIdClient
                    string oaid = TryGetOaidFromHms();
                    if (!string.IsNullOrEmpty(oaid)) {
                        SDKLogger.Info(Tag, $"OAID (HMS): {oaid}");
                        UnityMainThreadDispatcher.Instance.Enqueue(() => callback?.Invoke(oaid));
                        return;
                    }

                    // Try 2: MSA SDK (async callback with wait)
                    TryGetOaidFromMsa(msaOaid => {
                        if (!string.IsNullOrEmpty(msaOaid)) {
                            SDKLogger.Info(Tag, $"OAID (MSA): {msaOaid}");
                            UnityMainThreadDispatcher.Instance.Enqueue(() => callback?.Invoke(msaOaid));
                        } else {
                            SDKLogger.Warning(Tag, "OAID not available — neither HMS nor MSA SDK found.");
                            UnityMainThreadDispatcher.Instance.Enqueue(() => callback?.Invoke(null));
                        }
                    });
                } finally {
                    AndroidJNI.DetachCurrentThread();
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Check OAID initialization status.
        /// </summary>
        public static bool IsInitialized => _initialized;

        private static string TryGetOaidFromHms() {
            try {
                using (var activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = activityClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var appContext = activity.Call<AndroidJavaObject>("getApplicationContext"))
                using (var adIdClient = new AndroidJavaClass(
                    "com.huawei.hms.ads.identifier.AdvertisingIdClient"))
                using (var adIdInfo = adIdClient.CallStatic<AndroidJavaObject>(
                    "getAdvertisingIdInfo", appContext)) {
                    if (adIdInfo == null) {
                        SDKLogger.Debug(Tag, "HMS getAdvertisingIdInfo returned null.");
                        return null;
                    }
                    string oaid = adIdInfo.Call<string>("getId");
                    bool isLimitTracking = adIdInfo.Call<bool>("isLimitAdTrackingEnabled");
                    SDKLogger.Debug(Tag, $"HMS raw OAID: {oaid}, limitTracking: {isLimitTracking}");
                    return oaid;
                }
            } catch (System.Exception ex) {
                SDKLogger.Debug(Tag, $"HMS AdvertisingIdClient failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static void TryGetOaidFromMsa(System.Action<string> callback) {
            try {
                using (var activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = activityClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var helper = new AndroidJavaClass("com.bun.miitmdid.core.MdidSdkHelper")) {
                    var listener = new OaidListenerProxy(callback);
                    int code = helper.CallStatic<int>("InitSdk", activity, true, listener);
                    if (code != 0) {
                        // InitSdk failed — SDK not available
                        SDKLogger.Debug(Tag, $"MSA InitSdk returned error code: {code}");
                        callback?.Invoke(null);
                    }
                    // code == 0: callback will be invoked by MSA SDK asynchronously
                }
            } catch (System.Exception) {
                // MSA SDK not available
                callback?.Invoke(null);
            }
        }

        /// <summary>
        /// Proxy for MSA SDK's IIdentifierListener callback.
        /// </summary>
        private class OaidListenerProxy : AndroidJavaProxy {
            private readonly System.Action<string> _callback;
            private bool _invoked;

            public OaidListenerProxy(System.Action<string> callback)
                : base("com.bun.miitmdid.interfaces.IIdentifierListener") {
                _callback = callback;
            }

            // Called by MSA SDK when OAID is available
            void OnSupport(bool isSupport, AndroidJavaObject idSupplier) {
                if (_invoked) return;
                _invoked = true;

                string oaid = null;
                if (isSupport && idSupplier != null) {
                    try {
                        oaid = idSupplier.Call<string>("getOAID");
                        SDKLogger.Debug(Tag, $"MSA callback OAID: {oaid}");
                    } catch (System.Exception ex) {
                        SDKLogger.Warning(Tag, $"MSA getOAID failed: {ex.Message}");
                    }
                }
                _callback?.Invoke(oaid);
            }
        }

        /// <summary>
        /// Disable OAID reading. Must be called before Adjust.InitSdk().
        /// </summary>
        public static void DoNotReadOaid() {
            if (Application.platform != RuntimePlatform.Android) {
                SDKLogger.Debug(Tag, "OAID skip — not running on Android device.");
                return;
            }

            try {
                using (var oaidClass = new AndroidJavaClass(JavaClass)) {
                    oaidClass.CallStatic("doNotReadOaid");
                    SDKLogger.Info(Tag, "OAID reading disabled.");
                }
            } catch (System.Exception ex) {
                SDKLogger.Warning(Tag, $"Failed to disable OAID: {ex.Message}");
            }
        }
    }
}
#endif
