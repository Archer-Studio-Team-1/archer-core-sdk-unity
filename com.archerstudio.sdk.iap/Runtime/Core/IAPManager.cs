using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using ArcherStudio.SDK.Tracking;
using ArcherStudio.SDK.Tracking.Events;
using UnityEngine;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Central IAP manager. Implements ISDKModule for SDK lifecycle.
    /// Wraps IIAPProvider and bridges purchase events to tracking.
    /// </summary>
    public class IAPManager : ISDKModule {
        private const string Tag = "IAP";

        // ─── ISDKModule ───
        public string ModuleId => "iap";
        public int InitializationPriority => 50;
        public IReadOnlyList<string> Dependencies => new[] { "consent", "tracking" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        // ─── Singleton access ───
        public static IAPManager Instance { get; private set; }

        private IIAPProvider _provider;
        private IReceiptValidator _receiptValidator;
        private IAPConfig _config;

        public event Action<PurchaseResult> OnPurchaseCompleted;

        /// <summary>
        /// True after FetchPurchases has completed (success or failure).
        /// Callers should wait for this before trusting IsSubscribed().
        /// </summary>
        public bool IsSubscriptionStateReady => _provider?.IsPurchasesFetchCompleted ?? false;

        // ─── ISDKModule Lifecycle ───

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;
            Instance = this;

            SDKLogger.Debug(Tag, "IAPManager.InitializeAsync() started.");
            SDKLogger.Debug(Tag, $"  DebugMode={coreConfig.DebugMode}, EnableIAP={coreConfig.EnableIAP}");

            // Step 1: Load config
            _config = Resources.Load<IAPConfig>("IAPConfig");
            if (_config == null) {
                SDKLogger.Warning(Tag,
                    "IAPConfig not found in Resources/IAPConfig. " +
                    "Create one via: Assets > Create > ArcherStudio > SDK > IAP Config, " +
                    "then move to a Resources folder. IAP module will be inactive.");
                State = ModuleState.Ready; // Don't block other modules
                onComplete?.Invoke(true);
                return;
            }

            SDKLogger.Debug(Tag,
                $"  IAPConfig loaded: Enabled={_config.Enabled}, " +
                $"Products={_config.Products?.Count ?? 0}, " +
                $"ReceiptValidation={_config.EnableReceiptValidation}");

            // Step 2: Check if config is enabled
            if (!_config.Enabled) {
                SDKLogger.Info(Tag, "IAPConfig.Enabled=false. IAP module will be inactive.");
                State = ModuleState.Ready;
                onComplete?.Invoke(true);
                return;
            }

            // Step 3: Check products
            if (_config.Products == null || _config.Products.Count == 0) {
                SDKLogger.Warning(Tag, "IAPConfig has no products defined. IAP module will be inactive.");
                State = ModuleState.Ready;
                onComplete?.Invoke(true);
                return;
            }

            // Step 4: Log product details for debugging
            foreach (var product in _config.Products) {
                SDKLogger.Debug(Tag,
                    $"  Product: id={product.ProductId}, type={product.Type}, " +
                    $"storeId={product.StoreSpecificId}");
            }

            // Step 5: Create and initialize provider
            _provider = CreateProvider();
            SDKLogger.Info(Tag,
                $"Initializing IAP provider ({_provider.GetType().Name}) " +
                $"with {_config.Products.Count} products...");

            _provider.Initialize(_config, success => {
                if (success) {
                    SDKLogger.Info(Tag,
                        $"IAPManager initialized successfully. " +
                        $"{_config.Products.Count} products configured.");
                    State = ModuleState.Ready;
                } else {
                    SDKLogger.Error(Tag,
                        "IAP provider failed to initialize. " +
                        "Check logs above for specific error details. Common causes:\n" +
                        "  - Unity Gaming Services not initialized\n" +
                        "  - No internet connection\n" +
                        "  - Invalid product IDs in store dashboard\n" +
                        "  - Unity IAP not configured in Project Settings > Services");
                    State = ModuleState.Failed;
                }
                onComplete?.Invoke(success);
            });
        }

        public void OnConsentChanged(ConsentStatus consent) {
            // IAP doesn't need consent changes typically
        }

        public void Dispose() {
            _provider?.Dispose();
            _provider = null;
            Instance = null;
            State = ModuleState.Disposed;
        }

        // ─── Public API ───

        /// <summary>
        /// Initiate a purchase. Tracks purchase_show and purchase_result events.
        /// </summary>
        public void Purchase(string productId, string source = "", string reason = "",
            Action<PurchaseResult> onComplete = null) {

            if (State != ModuleState.Ready) {
                var result = PurchaseResult.Failed(productId, "IAP not ready.", PurchaseFailureReason.PurchasingUnavailable);
                onComplete?.Invoke(result);
                return;
            }

            var trackingManager = TrackingManager.Instance;

            _provider.Purchase(productId, result => {
                // Track iap_revenue custom event (v2)
                var productInfo = _provider?.GetProduct(productId);
                double revenue = productInfo.HasValue ? (double)productInfo.Value.PriceDecimal : 0;
                int revenueMicro = (int)(revenue * 1_000_000);
                string status = result.Success ? "success" : "fail";
                string failReason = result.Success ? null : result.ErrorMessage;
                string resultCode = result.Success ? null : MapToBillingResponseCode(result.FailureReason);

                trackingManager?.Track(new IapRevenueEvent(
                    productId, revenueMicro, status, failReason, resultCode, reason));

                if (result.Success) {
                    SDKLogger.Info(Tag, $"Purchase succeeded: {productId}");

                    // Validate receipt if configured
                    if (_config.EnableReceiptValidation && _receiptValidator != null) {
                        _receiptValidator.Validate(result.Receipt, productId, validation => {
                            if (!validation.IsValid) {
                                SDKLogger.Warning(Tag,
                                    $"Receipt validation failed for {productId}: {validation.ErrorMessage}");
                            }
                        });
                    }

                    // Track IAP revenue through all providers
                    // Firebase: logs "in_app_purchase" event
                    // Adjust: verifies receipt + tracks revenue internally
                    TrackIAPRevenue(result, source);

                    // Publish SDK event
                    SDKEventBus.Publish(new PurchaseCompletedEvent(result));
                } else {
                    SDKLogger.Warning(Tag,
                        $"Purchase failed: {productId} - {result.ErrorMessage}");
                }

                OnPurchaseCompleted?.Invoke(result);
                onComplete?.Invoke(result);
            });
        }

        /// <summary>
        /// Restore previous purchases (iOS).
        /// </summary>
        public void RestorePurchases(Action<bool> onComplete = null) {
            _provider?.RestorePurchases(onComplete);
        }

        /// <summary>
        /// Get all available products.
        /// </summary>
        public IReadOnlyList<ProductInfo> GetProducts() {
            return _provider?.GetProducts() ?? Array.Empty<ProductInfo>();
        }

        /// <summary>
        /// Get a specific product by ID.
        /// </summary>
        public ProductInfo? GetProduct(string productId) {
            return _provider?.GetProduct(productId);
        }

        /// <summary>
        /// Set a custom receipt validator.
        /// </summary>
        public void SetReceiptValidator(IReceiptValidator validator) {
            _receiptValidator = validator;
        }

        /// <summary>
        /// Get subscription status for a subscription product.
        /// Returns null if product is not a subscription, has no receipt, or IAP not ready.
        /// </summary>
        public SubscriptionInfo? GetSubscriptionInfo(string productId) {
            if (State != ModuleState.Ready) return null;
            return _provider?.GetSubscriptionInfo(productId);
        }

        /// <summary>
        /// Returns true if the subscription is currently active and not expired.
        /// </summary>
        public bool IsSubscribed(string productId) {
            var info = GetSubscriptionInfo(productId);
            return info.HasValue && info.Value.IsSubscribed;
        }

        /// <summary>
        /// Opens the platform's subscription management page so the user can cancel or manage.
        /// </summary>
        public void OpenSubscriptionManagement() {
            #if UNITY_IOS
            Application.OpenURL("https://apps.apple.com/account/subscriptions");
            #elif UNITY_ANDROID
            Application.OpenURL("https://play.google.com/store/account/subscriptions");
            #else
            SDKLogger.Warning(Tag, "OpenSubscriptionManagement: not supported on this platform.");
            #endif
        }

        // ─── IAP Revenue Tracking ───

        /// <summary>
        /// Track IAP revenue through all providers (one call).
        /// Each provider handles its own logic internally:
        /// - Firebase: logs "in_app_purchase" event
        /// - Adjust: verifies receipt + tracks revenue via VerifyAndTrack*Purchase
        /// </summary>
        private void TrackIAPRevenue(PurchaseResult result, string source) {
            var trackingManager = TrackingManager.Instance;
            if (trackingManager == null) return;

            var productInfo = _provider?.GetProduct(result.ProductId);
            double revenue = productInfo.HasValue ? (double)productInfo.Value.PriceDecimal : 0;
            string currency = productInfo.HasValue ? productInfo.Value.CurrencyCode ?? "USD" : "USD";

            if (revenue <= 0) {
                SDKLogger.Warning(Tag,
                    $"IAP revenue is {revenue} for {result.ProductId}. Skipping TrackIAPRevenue.");
                return;
            }

            trackingManager.TrackIAPRevenue(
                result.ProductId, revenue, currency,
                result.TransactionId ?? "", result.Receipt ?? "", source);
        }

        /// <summary>
        /// Map PurchaseFailureReason to error code string for tracking.
        /// Uses BillingResponseCode names where 1:1 match exists (USER_CANCELED, ITEM_UNAVAILABLE, etc).
        /// Uses descriptive names for Unity-specific reasons that have no BillingResponseCode equivalent.
        /// Ref: https://developer.android.com/reference/com/android/billingclient/api/BillingClient.BillingResponseCode
        /// </summary>
        private static string MapToBillingResponseCode(PurchaseFailureReason reason) {
            switch (reason) {
                case PurchaseFailureReason.UserCancelled: return "USER_CANCELED";
                case PurchaseFailureReason.PurchasingUnavailable: return "BILLING_UNAVAILABLE";
                case PurchaseFailureReason.ProductUnavailable: return "ITEM_UNAVAILABLE";
                case PurchaseFailureReason.DuplicateTransaction: return "ITEM_ALREADY_OWNED";
                case PurchaseFailureReason.PaymentDeclined: return "PAYMENT_DECLINED";
                case PurchaseFailureReason.ExistingPurchasePending: return "EXISTING_PURCHASE_PENDING";
                case PurchaseFailureReason.SignatureInvalid: return "SIGNATURE_INVALID";
                default: return "UNKNOWN";
            }
        }

        // ─── Internal ───

        private IIAPProvider CreateProvider() {
            #if HAS_UNITY_IAP
            return new UnityIAPProvider();
            #else
            SDKLogger.Warning(Tag, "HAS_UNITY_IAP not defined. Using stub provider.");
            return new StubIAPProvider();
            #endif
        }
    }

    /// <summary>
    /// SDK event for completed purchases. Other modules can subscribe.
    /// </summary>
    public readonly struct PurchaseCompletedEvent : ISDKEvent {
        public PurchaseResult Result { get; }

        public PurchaseCompletedEvent(PurchaseResult result) { Result = result; }
    }
}
