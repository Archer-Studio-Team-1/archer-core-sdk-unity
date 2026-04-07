using UnityEngine;

namespace ArcherStudio.SDK.Consent {

    /// <summary>
    /// Utility to read IAB TCF v2.3 consent signals from PlayerPrefs.
    /// UMP writes these values after the user responds to the consent dialog.
    /// Purpose and Vendor IDs are 1-based per IAB spec.
    /// </summary>
    public static class ConsentHelper {

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
            string pConsents = PlayerPrefs.GetString("IABTCF_PurposeConsents", "");
            if (purposeId > 0 && purposeId <= pConsents.Length) {
                return pConsents[purposeId - 1] == '1';
            }
            return false;
        }

        /// <summary>
        /// Check if a specific TCF Vendor has been granted consent.
        /// Index is 1-based per IAB spec.
        /// Key vendors:
        ///   31 = Meta (Facebook)
        ///  311 = AppLovin (Additional Consent)
        ///  755 = Google Advertising Products
        ///   32 = Unity Ads
        ///  702 = Mintegral
        ///   35 = Vungle
        /// </summary>
        public static bool IsVendorGranted(int vendorId) {
            string vConsents = PlayerPrefs.GetString("IABTCF_VendorConsents", "");
            if (vendorId > 0 && vendorId <= vConsents.Length) {
                return vConsents[vendorId - 1] == '1';
            }
            return false;
        }

        /// <summary>
        /// Get the raw IAB TCF TC String.
        /// Can be passed to SDKs that support direct TCF ingestion (e.g., Adjust).
        /// </summary>
        public static string GetTcString() {
            return PlayerPrefs.GetString("IABTCF_tcString", "");
        }

        /// <summary>
        /// Check if the GDPR applies according to the CMP.
        /// 0 = no, 1 = yes.
        /// </summary>
        public static bool IsGdprApplies() {
            return PlayerPrefs.GetInt("IABTCF_gdprApplies", 0) == 1;
        }
    }
}
