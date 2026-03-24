using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.IAP {

    [CreateAssetMenu(fileName = "IAPConfig", menuName = "ArcherStudio/SDK/IAP Config")]
    public class IAPConfig : ModuleConfigBase {

        [Header("Products")]
        public List<IAPProductDefinition> Products = new List<IAPProductDefinition>();

        [Header("Receipt Validation")]
        [Tooltip("Validate receipts server-side.")]
        public bool EnableReceiptValidation = false;

        [Tooltip("Server URL for receipt validation (if enabled).")]
        public string ValidationServerUrl;
    }

    [Serializable]
    public class IAPProductDefinition {
        public string ProductId;
        public ProductType Type;
        public string GooglePlayStoreId;
        public string AppleAppStoreId;

        public string StoreSpecificId {
            get {
                #if UNITY_ANDROID
                return string.IsNullOrEmpty(GooglePlayStoreId) ? ProductId : GooglePlayStoreId;
                #elif UNITY_IOS
                return string.IsNullOrEmpty(AppleAppStoreId) ? ProductId : AppleAppStoreId;
                #else
                return ProductId;
                #endif
            }
        }
    }
}
