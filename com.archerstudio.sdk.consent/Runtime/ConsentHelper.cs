using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Consent {

    /// <summary>
    /// Utility to read IAB TCF v2.3 consent signals.
    /// UMP writes these values after the user responds to the consent dialog.
    /// Purpose and Vendor IDs are 1-based per IAB spec.
    ///
    /// IMPORTANT: On Android, UMP writes to the default SharedPreferences,
    /// NOT Unity's PlayerPrefs (which uses a separate file). This class reads
    /// from the correct source per platform:
    ///   Android → SharedPreferences via JNI
    ///   iOS     → NSUserDefaults (same as PlayerPrefs)
    ///   Editor  → PlayerPrefs (for testing)
    /// </summary>
    public static class ConsentHelper {
        private const string Tag = "ConsentHelper";

        /// <summary>
        /// Check if a specific TCF Purpose has been granted consent.
        /// Index is 1-based per IAB spec.
        /// Key purposes:
        ///   1 = Store/access info (ad_storage, ad_user_data)
        ///   3 = Create personalized ads profile (ad_personalization)
        ///   4 = Select personalized ads (ad_personalization)
        ///   7 = Measure ad performance (ad_user_data)
        ///   9 = Market research
        ///  10 = Product development
        /// </summary>
        public static bool IsPurposeGranted(int purposeId) {
            string pConsents = ReadTcfString("IABTCF_PurposeConsents");
            if (purposeId > 0 && purposeId <= pConsents.Length) {
                return pConsents[purposeId - 1] == '1';
            }
            return false;
        }

        /// <summary>
        /// Check if a specific TCF Vendor has been granted consent.
        /// Index is 1-based per IAB spec.
        /// Only works for vendors in the Global Vendor List (GVL).
        /// For Additional Consent vendors (e.g. AppLovin ID 311), use IsAdditionalConsentVendorGranted().
        /// Key TCF vendors:
        ///   31 = Meta (Facebook)
        ///  755 = Google Advertising Products
        ///   32 = Unity Ads
        ///  702 = Mintegral
        ///   35 = Vungle
        /// </summary>
        public static bool IsVendorGranted(int vendorId) {
            string vConsents = ReadTcfString("IABTCF_VendorConsents");
            if (vendorId > 0 && vendorId <= vConsents.Length) {
                return vConsents[vendorId - 1] == '1';
            }
            return false;
        }

        /// <summary>
        /// Check if a vendor in Google's Additional Consent (AC) list has been granted consent.
        /// AC vendors are NOT in the TCF Global Vendor List, stored separately in IABTCF_AddtlConsent.
        /// Format: "2~311.755.1234" (version~dot-separated vendor IDs)
        /// Key AC vendors:
        ///  311 = AppLovin
        /// </summary>
        public static bool IsAdditionalConsentVendorGranted(int vendorId) {
            string acString = ReadTcfString("IABTCF_AddtlConsent");
            if (string.IsNullOrEmpty(acString)) return false;

            // Format: "2~311.755.1234" → split on '~', take second part, split on '.'
            int tildeIndex = acString.IndexOf('~');
            if (tildeIndex < 0 || tildeIndex >= acString.Length - 1) return false;

            string vendorsPart = acString.Substring(tildeIndex + 1);
            string vendorStr = vendorId.ToString();

            foreach (string id in vendorsPart.Split('.')) {
                if (id == vendorStr) return true;
            }
            return false;
        }

        /// <summary>
        /// Get the raw IAB TCF TC String.
        /// Can be passed to SDKs that support direct TCF ingestion (e.g., Adjust).
        /// </summary>
        public static string GetTcString() {
            return ReadTcfString("IABTCF_tcString");
        }

        /// <summary>
        /// Get the Additional Consent (AC) string.
        /// Contains consent for non-TCF vendors (Google's AC spec).
        /// </summary>
        public static string GetAdditionalConsentString() {
            return ReadTcfString("IABTCF_AddtlConsent");
        }

        /// <summary>
        /// Check if the GDPR applies according to the CMP.
        /// 0 = no, 1 = yes.
        /// </summary>
        public static bool IsGdprApplies() {
            return ReadTcfInt("IABTCF_gdprApplies", 0) == 1;
        }

        /// <summary>
        /// Get raw IABTCF_PurposeConsents binary string for debug logging.
        /// </summary>
        public static string ReadPurposeConsentsRaw() {
            return ReadTcfString("IABTCF_PurposeConsents");
        }

        /// <summary>
        /// Read a string value from the TCF storage.
        /// Android: default SharedPreferences (where UMP writes).
        /// iOS/Editor: NSUserDefaults / PlayerPrefs.
        /// </summary>
        private static string ReadTcfString(string key) {
#if UNITY_ANDROID && !UNITY_EDITOR
            return ReadAndroidSharedPrefsString(key, "");
#else
            return PlayerPrefs.GetString(key, "");
#endif
        }

        /// <summary>
        /// Read an int value from the TCF storage.
        /// </summary>
        private static int ReadTcfInt(string key, int defaultValue) {
#if UNITY_ANDROID && !UNITY_EDITOR
            return ReadAndroidSharedPrefsInt(key, defaultValue);
#else
            return PlayerPrefs.GetInt(key, defaultValue);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject GetDefaultSharedPreferences() {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            string prefsName = activity.Call<string>("getPackageName") + "_preferences";
            return activity.Call<AndroidJavaObject>("getSharedPreferences", prefsName, 0);
        }

        private static string ReadAndroidSharedPrefsString(string key, string defaultValue) {
            try {
                using var prefs = GetDefaultSharedPreferences();
                return prefs.Call<string>("getString", key, defaultValue);
            } catch (System.Exception e) {
                SDKLogger.Warning(Tag, $"Failed to read string '{key}': {e.Message}");
                return defaultValue;
            }
        }

        private static int ReadAndroidSharedPrefsInt(string key, int defaultValue) {
            try {
                using var prefs = GetDefaultSharedPreferences();
                return prefs.Call<int>("getInt", key, defaultValue);
            } catch (System.Exception e) {
                SDKLogger.Warning(Tag, $"Failed to read int '{key}': {e.Message}");
                return defaultValue;
            }
        }
#endif
    }
}
