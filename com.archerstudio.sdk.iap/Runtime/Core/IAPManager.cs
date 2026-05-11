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
        private bool _serverValidationEnabled;
        private PendingPurchaseStore _pendingStore;

        // Cached server-side subscription details (productId → result)
        private readonly Dictionary<string, SubscriptionStatusResult> _subscriptionDetails
            = new Dictionary<string, SubscriptionStatusResult>();

        // Cached purchase tokens for subscription queries
        private readonly Dictionary<string, (string purchaseToken, string transactionId)> _subscriptionTokens
            = new Dictionary<string, (string, string)>();

        public event Action<PurchaseResult> OnPurchaseCompleted;

        /// <summary>
        /// Fired when a subscription's active state changes (expired, renewed, cancelled, restored).
        /// Parameters: productId, isNowActive.
        /// </summary>
        public event Action<string, bool> OnSubscriptionStateChanged;

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
            _provider.OnSubscriptionStateChanged += OnProviderSubscriptionStateChanged;
            _provider.OnSubscriptionOrderObserved += OnProviderSubscriptionOrderObserved;
            SDKLogger.Info(Tag,
                $"Initializing IAP provider ({_provider.GetType().Name}) " +
                $"with {_config.Products.Count} products...");

            // Step 6: Auto-setup ServerReceiptValidator
            // Requires both IAPConfig.EnableReceiptValidation AND environment security toggle
            var securityConfig = coreConfig.GetActiveSecurityConfig();
            _serverValidationEnabled = _config.EnableReceiptValidation
                && securityConfig.EnableIAPServerValidation
                && !string.IsNullOrEmpty(_config.ValidationServerUrl);

            if (_serverValidationEnabled && _receiptValidator == null) {
                _receiptValidator = new ServerReceiptValidator(
                    _config.ValidationServerUrl, _config.ValidationApiKey);
                SDKLogger.Info(Tag, "ServerReceiptValidator auto-configured.");
            } else if (!_serverValidationEnabled) {
                SDKLogger.Info(Tag, "Server receipt validation disabled for this environment.");
            }

            _pendingStore = new PendingPurchaseStore();

            _provider.Initialize(_config, success => {
                if (success) {
                    SDKLogger.Info(Tag,
                        $"IAPManager initialized successfully. " +
                        $"{_config.Products.Count} products configured.");
                    State = ModuleState.Ready;

                    if (_serverValidationEnabled && _pendingStore.Count > 0) {
                        RetryPendingPurchases();
                    }
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
            if (_provider != null) {
                _provider.OnSubscriptionStateChanged -= OnProviderSubscriptionStateChanged;
                _provider.OnSubscriptionOrderObserved -= OnProviderSubscriptionOrderObserved;
                _provider.Dispose();
                _provider = null;
            }
            Instance = null;
            State = ModuleState.Disposed;
        }

        private void OnProviderSubscriptionOrderObserved(string productId, string transactionId, string receipt) {
            // Cache the token so QuerySubscriptionStatus can call the server even for
            // subscriptions restored from a previous session (where the new-purchase
            // CompletePurchaseSuccess path never runs).
            string purchaseToken = null;
            try {
                #if UNITY_ANDROID
                if (!string.IsNullOrEmpty(receipt)) {
                    var outer = JsonUtility.FromJson<GooglePlayReceiptWrapper>(receipt);
                    if (outer != null && !string.IsNullOrEmpty(outer.Payload)) {
                        var gp = JsonUtility.FromJson<GooglePlayPayloadWrapper>(outer.Payload);
                        if (gp != null && !string.IsNullOrEmpty(gp.json)) {
                            var data = JsonUtility.FromJson<GooglePlayPurchaseDataWrapper>(gp.json);
                            purchaseToken = data?.purchaseToken;
                        }
                    }
                }
                #endif
            } catch (Exception e) {
                SDKLogger.Warning(Tag, $"Failed to parse receipt for {productId}: {e.Message}");
            }
            CacheSubscriptionToken(productId, purchaseToken, transactionId);
        }

        private void OnProviderSubscriptionStateChanged(string productId, bool isActive) {
            SDKLogger.Info(Tag, $"Subscription state changed: {productId} → {(isActive ? "active" : "inactive")}");
            OnSubscriptionStateChanged?.Invoke(productId, isActive);
            SDKEventBus.Publish(new SubscriptionStateChangedEvent(productId, !isActive, isActive));
        }

        /// <summary>
        /// Refresh server-side status for all currently-known subscription products.
        /// Call this after IsSubscriptionStateReady becomes true to avoid trusting a stale
        /// store cache that still contains expired orders. Safe to call repeatedly.
        /// </summary>
        public void RefreshAllSubscriptionStatuses() {
            if (_receiptValidator == null || !_serverValidationEnabled) return;
            if (_subscriptionTokens.Count == 0) return;

            foreach (var entry in _subscriptionTokens) {
                var pid = entry.Key;
                QuerySubscriptionStatus(pid, result => {
                    if (result.Status == SubscriptionStatus.Unknown) return;
                    // If the server flipped the active state vs what the store cache reports,
                    // notify listeners so they can react (e.g. revoke benefits).
                    var info = GetSubscriptionInfo(pid);
                    if (info.HasValue) {
                        SDKLogger.Info(Tag,
                            $"Refreshed {pid}: status={result.Status}, expires={result.ExpirationDate}, " +
                            $"isSubscribed={info.Value.IsSubscribed}");
                        OnSubscriptionStateChanged?.Invoke(pid, info.Value.IsSubscribed);
                    }
                });
            }
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

            _provider.Purchase(productId, result => {
                if (result.Success) {
                    SDKLogger.Info(Tag, $"Purchase succeeded: {productId}");

                    // Validate receipt server-side if enabled for this environment
                    if (_serverValidationEnabled && _receiptValidator != null) {
                        _receiptValidator.Validate(result.Receipt, productId, validation => {
                            if (validation.IsValid) {
                                CompletePurchaseSuccess(result, source, validation.IsTestPurchase);
                                OnPurchaseCompleted?.Invoke(result);
                                onComplete?.Invoke(result);
                            } else if (validation.IsRetryable) {
                                // Server unreachable — user already charged. Save for retry.
                                _pendingStore.Add(productId, result.Receipt,
                                    result.TransactionId, source);
                                SDKLogger.Warning(Tag,
                                    $"Validation failed (retryable) for {productId}: {validation.ErrorMessage}. " +
                                    "Purchase saved for retry on next launch.");
                                CompletePurchaseSuccess(result, source, skipTracking: true);
                                OnPurchaseCompleted?.Invoke(result);
                                onComplete?.Invoke(result);
                            } else {
                                SDKLogger.Warning(Tag,
                                    $"Receipt validation REJECTED for {productId}: {validation.ErrorMessage}. " +
                                    "Purchase will NOT be granted.");
                                var rejected = PurchaseResult.Failed(
                                    productId,
                                    $"Server validation failed: {validation.ErrorMessage}",
                                    PurchaseFailureReason.SignatureInvalid);
                                OnPurchaseCompleted?.Invoke(rejected);
                                onComplete?.Invoke(rejected);
                            }
                        });
                        return; // Wait for async validation callback
                    }

                    // No server validation — grant immediately, skip tracking (fail-safe)
                    CompletePurchaseSuccess(result, source, skipTracking: true);
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
        /// When server-side data is cached (via QuerySubscriptionStatus) the server is the
        /// source of truth — its ExpirationDate / Status override the local store cache,
        /// which can stale-report a subscription as still active after it has expired or
        /// been refunded. Falls back to the store cache only when no server data exists.
        /// Returns null if product is not a subscription or IAP not ready.
        /// </summary>
        public SubscriptionInfo? GetSubscriptionInfo(string productId) {
            if (State != ModuleState.Ready) return null;
            var storeInfo = _provider?.GetSubscriptionInfo(productId);
            if (!storeInfo.HasValue) return null;

            // Normalize: caches are keyed by the provider's canonical definition.id, but
            // callers often pass the platform store id (e.g. "com.archer.idk.vip7").
            var canonicalId = _provider?.ResolveProductId(productId) ?? productId;

            // Server data present and not a failed query (Status != Unknown means we got an
            // authoritative answer — even if response.valid was false for expired/cancelled).
            if (_subscriptionDetails.TryGetValue(canonicalId, out var serverData)
                && serverData.Status != SubscriptionStatus.Unknown) {

                // Authoritative active check: prefer ExpirationDate vs now (handles
                // "cancelled but still inside paid period"); fall back to Status enum.
                bool isActive;
                if (serverData.ExpirationDate.HasValue) {
                    isActive = serverData.ExpirationDate.Value > DateTime.UtcNow;
                } else {
                    isActive = serverData.Status == SubscriptionStatus.Active
                            || serverData.Status == SubscriptionStatus.GracePeriod;
                }

                return new SubscriptionInfo(
                    storeInfo.Value.ProductId,
                    isSubscribed: isActive,
                    isExpired: !isActive,
                    isCancelled: serverData.Status == SubscriptionStatus.Cancelled,
                    isFreeTrial: serverData.IsFreeTrial,
                    isIntroductoryPricePeriod: storeInfo.Value.IsIntroductoryPricePeriod,
                    isAutoRenewing: serverData.IsAutoRenewing,
                    expirationDate: serverData.ExpirationDate,
                    purchaseDate: serverData.PurchaseDate,
                    cancellationDate: serverData.CancellationDate,
                    remainingTime: serverData.ExpirationDate.HasValue
                        ? TimeSpan.FromTicks(Math.Max(0,
                            (serverData.ExpirationDate.Value - DateTime.UtcNow).Ticks))
                        : (TimeSpan?)null,
                    subscriptionPeriod: storeInfo.Value.SubscriptionPeriod,
                    status: serverData.Status);
            }

            return storeInfo;
        }

        /// <summary>
        /// Returns true if the subscription is currently active and not expired.
        /// </summary>
        public bool IsSubscribed(string productId) {
            var info = GetSubscriptionInfo(productId);
            return info.HasValue && info.Value.IsSubscribed;
        }

        /// <summary>
        /// Query the server for detailed subscription status (expiry, grace period, etc.).
        /// Results are cached and merged into subsequent GetSubscriptionInfo() calls.
        /// </summary>
        public void QuerySubscriptionStatus(string productId, Action<SubscriptionStatusResult> onComplete = null) {
            if (_receiptValidator == null || !_serverValidationEnabled) {
                SDKLogger.Warning(Tag, "QuerySubscriptionStatus: server validation not enabled.");
                onComplete?.Invoke(SubscriptionStatusResult.Failed("Server validation not enabled."));
                return;
            }

            var canonicalId = _provider?.ResolveProductId(productId) ?? productId;
            if (!_subscriptionTokens.TryGetValue(canonicalId, out var tokens) ||
                (string.IsNullOrEmpty(tokens.purchaseToken) && string.IsNullOrEmpty(tokens.transactionId))) {
                SDKLogger.Warning(Tag,
                    $"QuerySubscriptionStatus: no purchase token cached for {productId} (canonical={canonicalId}).");
                onComplete?.Invoke(SubscriptionStatusResult.Failed("No purchase token available."));
                return;
            }

            _receiptValidator.QuerySubscriptionStatus(
                tokens.purchaseToken, tokens.transactionId, canonicalId, result => {
                if (result.Status != SubscriptionStatus.Unknown) {
                    _subscriptionDetails[canonicalId] = result;
                    SDKLogger.Info(Tag,
                        $"Subscription status for {productId} (canonical={canonicalId}): " +
                        $"valid={result.Success}, status={result.Status}, " +
                        $"expires={result.ExpirationDate}, autoRenew={result.IsAutoRenewing}");
                } else {
                    SDKLogger.Warning(Tag,
                        $"Subscription status query failed for {productId}: {result.ErrorMessage}");
                }
                onComplete?.Invoke(result);
            });
        }

        /// <summary>
        /// Cache a purchase token/transactionId for a subscription product.
        /// Used internally for subsequent QuerySubscriptionStatus calls.
        /// </summary>
        public void CacheSubscriptionToken(string productId, string purchaseToken, string transactionId) {
            if (string.IsNullOrEmpty(productId)) return;
            var canonicalId = _provider?.ResolveProductId(productId) ?? productId;
            _subscriptionTokens[canonicalId] = (purchaseToken, transactionId);
        }

        /// <summary>
        /// Fetch the latest subscription product info from the store.
        /// Should be called to refresh subscription status.
        /// </summary>
        /// <param name="onComplete"></param>
        public void FetchSubscriptionProduct(Action<bool> onComplete = null)
        {
            _provider?.FetchSubscriptionProduct(onComplete);
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

        // ─── Pending Purchase Recovery ───

        private void RetryPendingPurchases() {
            var pending = _pendingStore.GetAll();
            SDKLogger.Info(Tag, $"Retrying {pending.Count} pending purchase(s)...");

            foreach (var p in pending) {
                SDKLogger.Info(Tag, $"Retrying pending: {p.productId} (txn: {p.transactionId}, attempt #{p.retryCount + 1})");

                _receiptValidator.Validate(p.receipt, p.productId, validation => {
                    if (validation.IsValid) {
                        _pendingStore.Remove(p.transactionId);
                        var recovered = PurchaseResult.Succeeded(p.productId, p.transactionId, p.receipt);
                        CompletePurchaseSuccess(recovered, p.source, validation.IsTestPurchase);
                        SDKLogger.Info(Tag, $"Pending purchase recovered: {p.productId}");
                        OnPurchaseCompleted?.Invoke(recovered);
                    } else if (validation.IsRetryable) {
                        _pendingStore.IncrementRetry(p.transactionId);
                        SDKLogger.Warning(Tag,
                            $"Pending retry still failing for {p.productId}: {validation.ErrorMessage}. " +
                            "Will retry next launch.");
                    } else {
                        // Server says receipt is genuinely invalid — remove from pending
                        _pendingStore.Remove(p.transactionId);
                        SDKLogger.Warning(Tag,
                            $"Pending purchase permanently rejected: {p.productId} — {validation.ErrorMessage}");
                    }
                });
            }
        }

        // ─── Internal: Purchase completion ───

        /// <summary>
        /// Finalize a successful purchase: track revenue and publish event.
        /// Called after server validation passes (or immediately if validation is disabled).
        /// </summary>
        private void CompletePurchaseSuccess(PurchaseResult result, string source,
            bool isTestPurchase = false, bool skipTracking = false) {

            // Cache purchase token for subscription status queries
            var productInfo = _provider?.GetProduct(result.ProductId);
            if (productInfo.HasValue && productInfo.Value.Type == ProductType.Subscription) {
                CacheSubscriptionTokenFromReceipt(result);
            }

            #if PRODUCTION
            if (!isTestPurchase && !skipTracking) {
                TrackIAPRevenueEvent(result, source);
                TrackIAPRevenue(result, source);
            } else {
                SDKLogger.Info(Tag, $"Revenue tracking skipped for {result.ProductId}" +
                    (isTestPurchase ? " (test purchase)" : " (no server validation)"));
            }
            #endif

            SDKEventBus.Publish(new PurchaseCompletedEvent(result));
        }

        private void CacheSubscriptionTokenFromReceipt(PurchaseResult result) {
            if (string.IsNullOrEmpty(result.Receipt)) return;
            try {
                string purchaseToken = null;
                string transactionId = result.TransactionId;

                #if UNITY_ANDROID
                var outerReceipt = JsonUtility.FromJson<GooglePlayReceiptWrapper>(result.Receipt);
                if (outerReceipt != null && !string.IsNullOrEmpty(outerReceipt.Payload)) {
                    var gpPayload = JsonUtility.FromJson<GooglePlayPayloadWrapper>(outerReceipt.Payload);
                    if (gpPayload != null && !string.IsNullOrEmpty(gpPayload.json)) {
                        var purchaseData = JsonUtility.FromJson<GooglePlayPurchaseDataWrapper>(gpPayload.json);
                        purchaseToken = purchaseData?.purchaseToken;
                    }
                }
                #endif

                CacheSubscriptionToken(result.ProductId, purchaseToken, transactionId);
            } catch (Exception e) {
                SDKLogger.Warning(Tag, $"Failed to cache subscription token: {e.Message}");
            }
        }

        [Serializable] private class GooglePlayReceiptWrapper { public string Payload; }
        [Serializable] private class GooglePlayPayloadWrapper { public string json; }
        [Serializable] private class GooglePlayPurchaseDataWrapper { public string purchaseToken; }

        // ─── IAP Revenue Tracking ───

        private void TrackIAPRevenueEvent(PurchaseResult result, string source) {
            var trackingManager = TrackingManager.Instance;
            if (trackingManager == null) return;

            var productInfo = _provider?.GetProduct(result.ProductId);
            double revenue = productInfo.HasValue ? (double)productInfo.Value.PriceDecimal : 0;
            int revenueMicro = (int)(revenue * 1_000_000);
            string currency = productInfo.HasValue ? productInfo.Value.CurrencyCode ?? "USD" : "USD";

            trackingManager.Track(new IapRevenueEvent(
                result.ProductId, revenueMicro, currency, revenueMicro,
                "success", null, null, source));
        }

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
