using System.Text;
using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Dumps the full SDK state: all module init status, consent values,
    /// and per-vendor consent details. Call DumpAll() from a debug command.
    /// Output goes to both SDKLogger and Debug.Log for logcat/console visibility.
    /// </summary>
    public static class SDKDebugDumper {
        private const string Tag = "SDKDebug";

        /// <summary>
        /// Dump everything: SDK init state, all modules, consent, and per-vendor details.
        /// </summary>
        public static void DumpAll() {
            var sb = new StringBuilder(2048);

            sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              SDK FULL DEBUG DUMP                             ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════╣");

            DumpCoreState(sb);
            DumpModuleStates(sb);
            DumpConsentState(sb);
            DumpFirebaseConsentMode(sb);
            DumpFacebookState(sb);
            DumpMaxState(sb);
            DumpAdjustState(sb);
            DumpTcfRawData(sb);
            DumpBuildInfo(sb);

            sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");

            string output = sb.ToString();

            // Print each line via SDKLogger for consistent formatting
            foreach (string line in output.Split('\n')) {
                if (!string.IsNullOrWhiteSpace(line)) {
                    SDKLogger.Info(Tag, line.TrimEnd());
                }
            }

            // Also print as single Debug.Log block for easy copy from logcat
            Debug.Log($"[SDK:DebugDump]\n{output}");
        }

        // ─── Core State ───

        private static void DumpCoreState(StringBuilder sb) {
            sb.AppendLine("║ ── SDK Core ──");

            var initializer = SDKInitializer.Instance;
            if (initializer == null) {
                sb.AppendLine("║   SDKInitializer: NOT FOUND");
                return;
            }

            sb.AppendLine($"║   IsInitialized:     {initializer.IsInitialized}");

            var config = initializer.Config;
            if (config != null) {
                sb.AppendLine($"║   AppId:             {config.AppId ?? "(not set)"}");
                sb.AppendLine($"║   DebugMode:         {config.DebugMode}");
                sb.AppendLine($"║   MinLogLevel:       {config.MinLogLevel}");
                sb.AppendLine($"║   EnableConsent:     {config.EnableConsent}");
                sb.AppendLine($"║   EnableTracking:    {config.EnableTracking}");
                sb.AppendLine($"║   EnableAds:         {config.EnableAds}");
                sb.AppendLine($"║   EnableIAP:         {config.EnableIAP}");
            }

            sb.AppendLine($"║   Build:             {GetBuildEnvironment()}");
        }

        private static string GetBuildEnvironment() {
            #if PRODUCTION
            return "PRODUCTION";
            #elif STAGING
            return "STAGING";
            #elif DEV
            return "DEV";
            #else
            return $"DEFAULT (isDebugBuild={Debug.isDebugBuild})";
            #endif
        }

        // ─── Module States ───

        private static void DumpModuleStates(StringBuilder sb) {
            sb.AppendLine("║ ── Module States ──");

            var initializer = SDKInitializer.Instance;
            if (initializer == null) return;

            // Access modules through public API
            string[] moduleIds = { "consent", "firebase", "tracking", "facebook", "ads", "iap", "remoteconfig", "push", "deeplink" };
            foreach (string id in moduleIds) {
                var module = initializer.GetModule(id);
                if (module != null) {
                    string stateIcon = module.State switch {
                        ModuleState.Ready => "✓",
                        ModuleState.Failed => "✗",
                        ModuleState.Initializing => "…",
                        ModuleState.NotInitialized => "○",
                        ModuleState.Disposed => "×",
                        _ => "?"
                    };
                    sb.AppendLine($"║   [{stateIcon}] {id,-15} Priority={module.InitializationPriority,-3} State={module.State}");
                }
            }
        }

        // ─── Consent State ───

        private static void DumpConsentState(StringBuilder sb) {
            sb.AppendLine("║ ── Consent Status ──");

            var initializer = SDKInitializer.Instance;
            var consentModule = initializer?.GetModule("consent");
            if (consentModule == null) {
                sb.AppendLine("║   ConsentManager: NOT REGISTERED");
                return;
            }

            // Use reflection to get CurrentStatus since ConsentManager is in a different assembly
            var statusProp = consentModule.GetType().GetProperty("CurrentStatus");
            if (statusProp == null) {
                sb.AppendLine("║   ConsentManager: cannot read CurrentStatus");
                return;
            }

            var status = (ConsentStatus)statusProp.GetValue(consentModule);
            sb.AppendLine($"║   Source:                  {status.Source}");
            sb.AppendLine($"║   CanShowPersonalizedAds:  {status.CanShowPersonalizedAds}");
            sb.AppendLine($"║   CanCollectAnalytics:     {status.CanCollectAnalytics}");
            sb.AppendLine($"║   CanTrackAttribution:     {status.CanTrackAttribution}");
            sb.AppendLine($"║   CanStoreAdData:          {status.CanStoreAdData}");
            sb.AppendLine($"║   IsEeaUser:               {status.IsEeaUser}");
            sb.AppendLine($"║   HasAttConsent:            {status.HasAttConsent}");
            sb.AppendLine($"║   IsDoNotSell:              {status.IsDoNotSell}");
        }

        // ─── Firebase Consent Mode v2 ───

        private static void DumpFirebaseConsentMode(StringBuilder sb) {
            sb.AppendLine("║ ── Firebase Consent Mode v2 ──");
            #if HAS_FIREBASE_APP
            sb.AppendLine("║   Firebase SDK:      AVAILABLE");
            // Consent mode values are set via native bridge, can't read back directly
            // Show what SHOULD be set based on ConsentStatus
            var initializer = SDKInitializer.Instance;
            var consentModule = initializer?.GetModule("consent");
            if (consentModule != null) {
                var statusProp = consentModule.GetType().GetProperty("CurrentStatus");
                if (statusProp != null) {
                    var s = (ConsentStatus)statusProp.GetValue(consentModule);
                    sb.AppendLine($"║   AD_STORAGE:        {Gd(s.CanStoreAdData)}");
                    sb.AppendLine($"║   ANALYTICS_STORAGE: {Gd(s.CanCollectAnalytics)}");
                    sb.AppendLine($"║   AD_USER_DATA:      {Gd(s.CanTrackAttribution)}");
                    sb.AppendLine($"║   AD_PERSONALIZATION:{Gd(s.CanShowPersonalizedAds)}");
                }
            }
            #else
            sb.AppendLine("║   Firebase SDK:      NOT AVAILABLE");
            #endif
        }

        // ─── Facebook SDK ───

        private static void DumpFacebookState(StringBuilder sb) {
            sb.AppendLine("║ ── Facebook SDK ──");

            System.Type fbType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
                fbType = asm.GetType("Facebook.Unity.FB");
                if (fbType != null) break;
            }

            if (fbType == null) {
                sb.AppendLine("║   Facebook SDK:      NOT AVAILABLE");
                return;
            }

            sb.AppendLine($"║   FB.IsInitialized:  {GetStaticProp(fbType, "IsInitialized")}");
            sb.AppendLine($"║   FB.IsLoggedIn:     {GetStaticProp(fbType, "IsLoggedIn")}");
            sb.AppendLine($"║   FB.AppId:          {GetStaticProp(fbType, "AppId") ?? "(null)"}");
        }

        // ─── AppLovin MAX ───

        private static void DumpMaxState(StringBuilder sb) {
            sb.AppendLine("║ ── AppLovin MAX ──");

            // MaxSdk is in a separate assembly (com.applovin.mediation.ads), use reflection
            var maxType = System.Type.GetType("MaxSdk, MaxSdk.Scripts")
                       ?? System.Type.GetType("MaxSdk");
            if (maxType == null) {
                // Fallback: search all assemblies
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
                    maxType = asm.GetType("MaxSdk");
                    if (maxType != null) break;
                }
            }

            if (maxType == null) {
                sb.AppendLine("║   MAX SDK:           NOT AVAILABLE");
                return;
            }

            var versionProp = maxType.GetProperty("Version", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            sb.AppendLine($"║   SDK Version:        {versionProp?.GetValue(null) ?? "?"}");

            sb.AppendLine($"║   IsInitialized:      {CallStaticBool(maxType, "IsInitialized")}");
            sb.AppendLine($"║   HasUserConsent:     {CallStaticBool(maxType, "HasUserConsent")}");
            sb.AppendLine($"║   IsUserConsentSet:   {CallStaticBool(maxType, "IsUserConsentSet")}");
            sb.AppendLine($"║   IsDoNotSell:        {CallStaticBool(maxType, "IsDoNotSell")}");
            sb.AppendLine($"║   IsDoNotSellSet:     {CallStaticBool(maxType, "IsDoNotSellSet")}");

            // Mediation adapters
            var getNetworks = maxType.GetMethod("GetAvailableMediatedNetworks", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (getNetworks != null) {
                var networks = getNetworks.Invoke(null, null) as System.Collections.IEnumerable;
                if (networks != null) {
                    sb.AppendLine("║   ── Mediation Adapters ──");
                    foreach (var network in networks) {
                        var nType = network.GetType();
                        string name = nType.GetProperty("Name")?.GetValue(network)?.ToString() ?? "?";
                        string adapterVer = nType.GetProperty("AdapterVersion")?.GetValue(network)?.ToString() ?? "?";
                        string sdkVer = nType.GetProperty("SdkVersion")?.GetValue(network)?.ToString() ?? "?";
                        sb.AppendLine($"║     {name,-20} adapter={adapterVer} sdk={sdkVer}");
                    }
                }
            }
        }

        // ─── Adjust ───

        private static void DumpAdjustState(StringBuilder sb) {
            sb.AppendLine("║ ── Adjust ──");

            System.Type adjustType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) {
                adjustType = asm.GetType("AdjustSdk.Adjust");
                if (adjustType != null) break;
            }

            if (adjustType == null) {
                sb.AppendLine("║   Adjust SDK:        NOT AVAILABLE");
                return;
            }

            sb.AppendLine("║   SDK Available:     YES");

            // Consent mapping from current status
            var initializer = SDKInitializer.Instance;
            var consentModule = initializer?.GetModule("consent");
            if (consentModule != null) {
                var statusProp = consentModule.GetType().GetProperty("CurrentStatus");
                if (statusProp != null) {
                    var s = (ConsentStatus)statusProp.GetValue(consentModule);
                    sb.AppendLine($"║   google_dma eea:              {(s.IsEeaUser ? "1" : "0")}");
                    sb.AppendLine($"║   google_dma ad_personalization:{(s.CanShowPersonalizedAds ? "1" : "0")}");
                    sb.AppendLine($"║   google_dma ad_user_data:     {(s.CanTrackAttribution ? "1" : "0")}");
                    sb.AppendLine($"║   google_dma ad_storage:       {(s.CanStoreAdData ? "1" : "0")}");
                    sb.AppendLine($"║   google_dma npa:              {(s.CanShowPersonalizedAds ? "0" : "1")}");
                    sb.AppendLine($"║   facebook ldu_country:        {(s.IsDoNotSell ? "1" : "0")}");
                    sb.AppendLine($"║   facebook ldu_state:          {(s.IsDoNotSell ? "1000" : "0")}");
                    sb.AppendLine($"║   MeasurementConsent:          {s.CanCollectAnalytics}");
                }
            }

            sb.AppendLine($"║   Environment:       {GetBuildEnvironment()}");
        }

        // ─── Raw TCF Data ───

        private static void DumpTcfRawData(StringBuilder sb) {
            sb.AppendLine("║ ── Raw TCF Data ──");

            // Use reflection to call ConsentHelper (different assembly)
            var helperType = System.Type.GetType("ArcherStudio.SDK.Consent.ConsentHelper, ArcherStudio.SDK.Consent");
            if (helperType == null) {
                sb.AppendLine("║   ConsentHelper:     NOT AVAILABLE");
                return;
            }

            string tcString = (string)helperType.GetMethod("GetTcString")?.Invoke(null, null) ?? "";
            bool gdprApplies = (bool)(helperType.GetMethod("IsGdprApplies")?.Invoke(null, null) ?? false);
            string purposeRaw = (string)helperType.GetMethod("ReadPurposeConsentsRaw")?.Invoke(null, null) ?? "";
            string acString = (string)helperType.GetMethod("GetAdditionalConsentString")?.Invoke(null, null) ?? "";

            sb.AppendLine($"║   IABTCF_gdprApplies:      {gdprApplies}");
            sb.AppendLine($"║   IABTCF_tcString:         {(string.IsNullOrEmpty(tcString) ? "(empty)" : tcString.Substring(0, System.Math.Min(tcString.Length, 50)) + "...")}");
            sb.AppendLine($"║   IABTCF_PurposeConsents:  {(string.IsNullOrEmpty(purposeRaw) ? "(empty)" : purposeRaw)}");

            // Purpose breakdown
            var purposeMethod = helperType.GetMethod("IsPurposeGranted");
            if (purposeMethod != null) {
                int[] purposes = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                string[] names = { "Store/access", "Basic ads", "Ads profile", "Select ads", "Content profile",
                                   "Select content", "Ad measure", "Content measure", "Market research", "Dev/improve" };
                for (int i = 0; i < purposes.Length; i++) {
                    bool granted = (bool)purposeMethod.Invoke(null, new object[] { purposes[i] });
                    sb.AppendLine($"║     Purpose {purposes[i],-2} ({names[i],-16}): {(granted ? "GRANTED" : "DENIED")}");
                }
            }

            // Key vendors
            sb.AppendLine("║   ── Key Vendor Consent ──");
            var vendorMethod = helperType.GetMethod("IsVendorGranted");
            if (vendorMethod != null) {
                var vendors = new[] {
                    (31, "Meta/Facebook"), (32, "Unity Ads"), (35, "Vungle"),
                    (702, "Mintegral"), (755, "Google Ads")
                };
                foreach (var (id, name) in vendors) {
                    bool granted = (bool)vendorMethod.Invoke(null, new object[] { id });
                    sb.AppendLine($"║     Vendor {id,-4} ({name,-15}): {(granted ? "GRANTED" : "DENIED")}");
                }
            }

            // Additional Consent
            sb.AppendLine($"║   IABTCF_AddtlConsent:     {(string.IsNullOrEmpty(acString) ? "(empty)" : acString)}");
            var acMethod = helperType.GetMethod("IsAdditionalConsentVendorGranted");
            if (acMethod != null) {
                bool appLovin = (bool)acMethod.Invoke(null, new object[] { 311 });
                sb.AppendLine($"║     AC Vendor 311 (AppLovin):    {(appLovin ? "GRANTED" : "DENIED")}");
            }
        }

        // ─── Build Info ───

        private static void DumpBuildInfo(StringBuilder sb) {
            sb.AppendLine("║ ── Build Info ──");
            sb.AppendLine($"║   Platform:         {Application.platform}");
            sb.AppendLine($"║   Unity:            {Application.unityVersion}");
            sb.AppendLine($"║   Bundle:           {Application.identifier}");
            sb.AppendLine($"║   Version:          {Application.version}");
            sb.AppendLine($"║   isDebugBuild:     {Debug.isDebugBuild}");
            sb.AppendLine($"║   SystemLanguage:   {Application.systemLanguage}");
            sb.AppendLine($"║   Environment:      {GetBuildEnvironment()}");
        }

        // ─── Reflection Helpers ───

        private static string CallStaticBool(System.Type type, string methodName) {
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null) return "?";
            try { return method.Invoke(null, null)?.ToString() ?? "?"; }
            catch { return "error"; }
        }

        private static object GetStaticProp(System.Type type, string propName) {
            var prop = type.GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop == null) return "?";
            try { return prop.GetValue(null); }
            catch { return "error"; }
        }

        private static string Gd(bool granted) => granted ? "GRANTED" : "DENIED";
    }
}
