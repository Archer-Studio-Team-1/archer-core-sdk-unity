#if HAS_UNITY_IAP
using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;
using UnityEngine.Purchasing;
#if HAS_UNITY_SERVICES
using Unity.Services.Core;
#endif

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Unity IAP v5.0.4 provider using StoreController + event-based API.
    ///
    /// Flow: Connect → FetchProducts → OnProductsFetched → FetchPurchases → ready.
    ///       PurchaseProduct → OnPurchasePending → ConfirmPurchase → OnPurchaseConfirmed.
    ///
    /// v5 API key types:
    ///   - OnPurchaseConfirmed → Action&lt;Order&gt;
    ///   - OnPurchasesFetched  → Action&lt;Orders&gt; (contains PendingOrders, ConfirmedOrders, DeferredOrders)
    ///   - OnPurchasesFetchFailed → Action&lt;PurchasesFetchFailureDescription&gt;
    ///   - OnStoreDisconnected → Action&lt;StoreConnectionFailureDescription&gt;
    ///   - ProcessPendingOrdersOnPurchasesFetched defaults true (auto-dispatches to OnPurchasePending)
    ///
    /// Includes:
    ///   - Unity Gaming Services pre-init check
    ///   - Store connection before product fetch
    ///   - FetchPurchases for pending/restored purchases
    ///   - FetchProductsWithNoRetries + manual retry (max 2 retries)
    ///   - Generation-based stale callback protection
    ///   - Deferred purchase handling (Google Play parental approval)
    ///   - Store disconnection recovery with IsRetryable check
    /// </summary>
    public class UnityIAPProvider : IIAPProvider {
        private const string Tag = "IAP-Unity";
        private const float DefaultFetchTimeoutSeconds = 30f;
        private const int MaxRetryCount = 2;
        private static readonly float[] RetryDelays = { 2f, 5f };

        private StoreController _controller;
        private Action<PurchaseResult> _purchaseCallback;
        private bool _initialized;
        private bool _connected;
        private bool _fetchCompleted; // True when products fetched or all retries exhausted
        private bool _disposed;

        private IAPConfig _config;
        private int _retryCount;
        private int _fetchGeneration; // Tracks which fetch attempt is "current"
        private float _fetchTimeoutSeconds;

        // IAP v5: active subscription product IDs, refreshed from FetchPurchases results
        private readonly HashSet<string> _activeSubscriptions = new HashSet<string>();

        public bool IsInitialized => _initialized;

        // ─── IIAPProvider ───

        public void Initialize(IAPConfig config, Action<bool> onComplete) {
            _config = config;
            _retryCount = 0;
            _fetchGeneration = 0;
            _fetchCompleted = false;
            _connected = false;
            _disposed = false;
            _fetchTimeoutSeconds = DefaultFetchTimeoutSeconds;

            SDKLogger.Debug(Tag, "Starting Unity IAP v5 initialization...");

            // Step 1: Check Unity Gaming Services
            if (!CheckUnityServicesReady()) {
                onComplete?.Invoke(false);
                return;
            }

            // Step 2: Create StoreController
            if (!CreateStoreController()) {
                onComplete?.Invoke(false);
                return;
            }

            // Init is successful once StoreController is created.
            // Connect + FetchProducts runs in background — not required for init.
            _initialized = true;
            SDKLogger.Info(Tag, "Unity IAP service initialized. Connecting to store...");
            onComplete?.Invoke(true);

            // Step 3: Connect to store, then fetch products
            ConnectAndFetch();
        }

        /// <summary>
        /// Connect to the store, then begin fetching products.
        /// IAP v5 requires Connect() before FetchProducts().
        /// </summary>
        private async void ConnectAndFetch() {
            if (_disposed) return;

            try {
                SDKLogger.Debug(Tag, "Connecting to store...");
                await _controller.Connect();
                _connected = true;
                SDKLogger.Info(Tag, "Store connected successfully.");

                // Now fetch products
                BeginFetchAttempt();
            } catch (Exception e) {
                SDKLogger.Error(Tag,
                    $"Store connection failed: {e.Message}. " +
                    "IAP purchases will not be available. " +
                    "Will retry on next app launch.");
                // Don't hang — mark fetch as completed so no one waits
                _fetchCompleted = true;
            }
        }

        public void Purchase(string productId, Action<PurchaseResult> onComplete) {
            if (!_initialized) {
                onComplete?.Invoke(PurchaseResult.Failed(productId, "Store not initialized.",
                    PurchaseFailureReason.PurchasingUnavailable));
                return;
            }

            if (_purchaseCallback != null) {
                onComplete?.Invoke(PurchaseResult.Failed(productId,
                    "Another purchase is already in progress.",
                    PurchaseFailureReason.ExistingPurchasePending));
                return;
            }

            // Validate product availability before purchasing
            var product = _controller.GetProductById(productId);
            if (product == null || !product.availableToPurchase) {
                onComplete?.Invoke(PurchaseResult.Failed(productId, "Product not available.",
                    PurchaseFailureReason.ProductUnavailable));
                return;
            }

            _purchaseCallback = onComplete;

            // IAP v5: Use string overload convenience method
            _controller.PurchaseProduct(productId);
        }

        public void RestorePurchases(Action<bool> onComplete) {
            if (!_initialized) {
                onComplete?.Invoke(false);
                return;
            }

            // IAP v5.0.2+: RestoreTransactions also triggers OnPurchasesFetched
            // for restored purchases, which are auto-processed via
            // ProcessPendingOrdersOnPurchasesFetched (default true).
            _controller.RestoreTransactions((success, error) => {
                if (!success) {
                    SDKLogger.Warning(Tag, $"Restore failed: {error}");
                }
                onComplete?.Invoke(success);
            });
        }

        public IReadOnlyList<ProductInfo> GetProducts() {
            if (!_initialized) return Array.Empty<ProductInfo>();

            var result = new List<ProductInfo>();
            foreach (var product in _controller.GetProducts()) {
                if (product.availableToPurchase) {
                    result.Add(MapProduct(product));
                }
            }
            return result;
        }

        public ProductInfo? GetProduct(string productId) {
            if (!_initialized) return null;

            var product = _controller.GetProductById(productId);
            if (product == null || !product.availableToPurchase) return null;
            return MapProduct(product);
        }

        /// <summary>
        /// IAP v5: SubscriptionManager was removed in v5.2.1. Subscription state is now
        /// derived exclusively from the orders cache populated by FetchPurchases.
        /// A product in _activeSubscriptions means the store currently has an active order
        /// for it (PendingOrder or ConfirmedOrder from the last FetchPurchases call).
        /// Detailed fields (ExpirationDate, etc.) require server-side receipt validation.
        /// </summary>
        public SubscriptionInfo? GetSubscriptionInfo(string productId) {
            if (!_initialized || _controller == null) return null;

            var product = _controller.GetProductById(productId);
            if (product == null || product.definition.type != UnityEngine.Purchasing.ProductType.Subscription)
                return null;

            if (!product.availableToPurchase) return null;

            bool isActive = _activeSubscriptions.Contains(productId);
            return new SubscriptionInfo(
                productId,
                isSubscribed: isActive,
                isExpired: !isActive,
                isCancelled: false,
                isFreeTrial: false,
                isIntroductoryPricePeriod: false,
                isAutoRenewing: isActive,
                expirationDate: null,
                purchaseDate: null,
                cancellationDate: null,
                remainingTime: null,
                subscriptionPeriod: null);
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;

            // Unsubscribe events to prevent leaks
            if (_controller != null) {
                _controller.OnProductsFetched -= OnProductsFetched;
                _controller.OnProductsFetchFailed -= OnProductsFetchFailed;
                _controller.OnPurchasePending -= OnPurchasePending;
                _controller.OnPurchaseConfirmed -= OnPurchaseConfirmed;
                _controller.OnPurchaseFailed -= OnPurchaseFailed;
                _controller.OnPurchaseDeferred -= OnPurchaseDeferred;
                _controller.OnPurchasesFetched -= OnPurchasesFetched;
                _controller.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
                _controller.OnStoreDisconnected -= OnStoreDisconnected;
            }

            IAPCoroutineRunner.CancelAll();
            _controller = null;
        }

        // ─── Init Steps ───

        private bool CheckUnityServicesReady() {
            #if HAS_UNITY_SERVICES
            if (UnityServices.State != ServicesInitializationState.Initialized) {
                SDKLogger.Error(Tag,
                    "Unity Gaming Services (UGS) is not initialized. " +
                    "Call UnityServices.InitializeAsync() before SDK initialization. " +
                    $"Current UGS state: {UnityServices.State}");
                return false;
            }
            SDKLogger.Debug(Tag, "Unity Gaming Services: OK (initialized).");
            #else
            SDKLogger.Debug(Tag,
                "Unity Services package not detected. Skipping UGS check. " +
                "If IAP fails, ensure com.unity.services.core is installed.");
            #endif
            return true;
        }

        private bool CreateStoreController() {
            try {
                SDKLogger.Debug(Tag, "Creating StoreController...");
                _controller = UnityIAPServices.StoreController();
                SDKLogger.Debug(Tag, "StoreController created successfully.");
            } catch (Exception e) {
                SDKLogger.Error(Tag,
                    $"Failed to create StoreController: {e.Message}\n" +
                    $"Ensure Unity IAP is properly configured in Project Settings > Services.\n" +
                    $"Exception type: {e.GetType().Name}");
                return false;
            }

            // Subscribe to v5 events
            _controller.OnProductsFetched += OnProductsFetched;
            _controller.OnProductsFetchFailed += OnProductsFetchFailed;
            _controller.OnPurchasePending += OnPurchasePending;
            _controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _controller.OnPurchaseFailed += OnPurchaseFailed;
            _controller.OnPurchaseDeferred += OnPurchaseDeferred;
            _controller.OnPurchasesFetched += OnPurchasesFetched;
            _controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            _controller.OnStoreDisconnected += OnStoreDisconnected;

            return true;
        }

        // ─── Background Product Fetch (with retry) ───

        /// <summary>
        /// Start a new fetch attempt. Increments generation so stale callbacks are ignored.
        /// Uses FetchProductsWithNoRetries so we control retry logic ourselves.
        /// </summary>
        private void BeginFetchAttempt() {
            if (_fetchCompleted || _disposed) return;

            if (_controller == null) {
                SDKLogger.Error(Tag, "StoreController is null. Cannot fetch products.");
                _fetchCompleted = true;
                return;
            }

            _fetchGeneration++;
            var generation = _fetchGeneration;

            var definitions = BuildProductDefinitions(_config);

            SDKLogger.Info(Tag,
                $"Fetching {definitions.Count} products " +
                $"(attempt {_retryCount + 1}/{MaxRetryCount + 1}, " +
                $"timeout: {_fetchTimeoutSeconds}s, gen: {generation})...");

            LogProductDefinitions(definitions);

            // IAP v5: Use FetchProductsWithNoRetries for manual retry control.
            // FetchProducts(defs) uses built-in retry which would conflict with ours.
            _controller.FetchProductsWithNoRetries(definitions);

            // Start timeout for this generation
            IAPCoroutineRunner.DelayedCall(_fetchTimeoutSeconds, () => {
                OnFetchTimeout(generation);
            });
        }

        private void OnFetchTimeout(int generation) {
            if (_fetchCompleted || _disposed || generation != _fetchGeneration) return;

            SDKLogger.Warning(Tag,
                $"FetchProducts timed out after {_fetchTimeoutSeconds}s " +
                $"(attempt {_retryCount + 1}/{MaxRetryCount + 1}, gen: {generation}).");

            RetryOrFail();
        }

        /// <summary>
        /// Single retry decision point. Called from both OnProductsFetchFailed and timeout.
        /// </summary>
        private void RetryOrFail() {
            if (_fetchCompleted || _disposed) return;

            if (_retryCount < MaxRetryCount) {
                _retryCount++;
                var delayIndex = Mathf.Min(_retryCount - 1, RetryDelays.Length - 1);
                var delay = RetryDelays[delayIndex];

                SDKLogger.Info(Tag,
                    $"Retrying FetchProducts (attempt {_retryCount + 1}/{MaxRetryCount + 1}) " +
                    $"after {delay}s delay...");

                IAPCoroutineRunner.DelayedCall(delay, () => {
                    if (_fetchCompleted || _disposed) return;
                    BeginFetchAttempt();
                });
            } else {
                SDKLogger.Warning(Tag,
                    $"FetchProducts failed after {MaxRetryCount + 1} attempts. " +
                    "Products will not be available until next app restart. " +
                    "Possible causes:\n" +
                    "  - No internet connection\n" +
                    "  - Store service unavailable\n" +
                    "  - Invalid product IDs\n" +
                    "  - Unity IAP not configured in store dashboard");
                _fetchCompleted = true;
            }
        }

        private List<ProductDefinition> BuildProductDefinitions(IAPConfig config) {
            var definitions = new List<ProductDefinition>();
            foreach (var product in config.Products) {
                var type = MapProductType(product.Type);
                var storeSpecificId = product.StoreSpecificId;
                definitions.Add(new ProductDefinition(product.ProductId, storeSpecificId, type));
            }
            return definitions;
        }

        private void LogProductDefinitions(List<ProductDefinition> definitions) {
            foreach (var def in definitions) {
                SDKLogger.Verbose(Tag,
                    $"  Product: id={def.id}, storeSpecificId={def.storeSpecificId}, type={def.type}");
            }
        }

        // ─── Event Handlers ───

        /// <summary>
        /// IAP v5: Called when FetchProducts succeeds. Parameter is List&lt;Product&gt;.
        /// After products are loaded, fetch existing purchases.
        /// </summary>
        private void OnProductsFetched(List<UnityEngine.Purchasing.Product> products) {
            if (_fetchCompleted || _disposed) return;
            _fetchCompleted = true;

            IAPCoroutineRunner.CancelAll(); // Stop pending timeouts

            SDKLogger.Info(Tag, $"Products fetched successfully. {products.Count} products available.");
            foreach (var product in products) {
                var meta = product.metadata;
                SDKLogger.Verbose(Tag,
                    $"  Fetched: {product.definition.id} " +
                    $"| available={product.availableToPurchase} " +
                    $"| price={meta.localizedPriceString} " +
                    $"| currency={meta.isoCurrencyCode}");
            }

            // IAP v5: Fetch existing purchases (pending/restored) after products are loaded.
            // ProcessPendingOrdersOnPurchasesFetched defaults to true, so pending orders
            // are automatically dispatched to OnPurchasePending — no manual processing needed.
            SDKLogger.Debug(Tag, "Fetching existing purchases...");
            _controller.FetchPurchases();
        }

        /// <summary>
        /// IAP v5.0.4: Called when FetchPurchases succeeds.
        /// Parameter is Orders (contains PendingOrders, ConfirmedOrders, DeferredOrders).
        ///
        /// NOTE: With ProcessPendingOrdersOnPurchasesFetched=true (default), pending orders
        /// are automatically dispatched to OnPurchasePending. We only log here to avoid
        /// double-processing.
        /// </summary>
        private void OnPurchasesFetched(Orders orders) {
            if (_disposed) return;

            var pendingCount = orders.PendingOrders?.Count ?? 0;
            var confirmedCount = orders.ConfirmedOrders?.Count ?? 0;
            var deferredCount = orders.DeferredOrders?.Count ?? 0;

            SDKLogger.Info(Tag,
                $"Purchases fetched: {pendingCount} pending, " +
                $"{confirmedCount} confirmed, {deferredCount} deferred.");

            // IAP v5: refresh subscription cache from current order state
            RefreshSubscriptionCache(orders.PendingOrders, orders.ConfirmedOrders);

            // Log pending orders (auto-processed by ProcessPendingOrdersOnPurchasesFetched)
            if (orders.PendingOrders != null) {
                foreach (var order in orders.PendingOrders) {
                    var items = order.CartOrdered.Items();
                    if (items == null || items.Count == 0) continue;

                    var productId = items[0].Product.definition.id;
                    SDKLogger.Info(Tag,
                        $"  Pending: {productId} (txn: {order.Info.TransactionID})");
                }
            }

            // Log confirmed orders
            if (orders.ConfirmedOrders != null) {
                foreach (var order in orders.ConfirmedOrders) {
                    var items = order.CartOrdered.Items();
                    if (items == null || items.Count == 0) continue;

                    var productId = items[0].Product.definition.id;
                    SDKLogger.Verbose(Tag,
                        $"  Confirmed: {productId} (txn: {order.Info.TransactionID})");
                }
            }

            // Log deferred orders
            if (orders.DeferredOrders != null) {
                foreach (var order in orders.DeferredOrders) {
                    var items = order.CartOrdered.Items();
                    if (items == null || items.Count == 0) continue;

                    var productId = items[0].Product.definition.id;
                    SDKLogger.Verbose(Tag,
                        $"  Deferred: {productId}");
                }
            }
        }

        /// <summary>
        /// IAP v5.0.4: Called when FetchPurchases fails.
        /// Parameter is PurchasesFetchFailureDescription (has FailureReason + Message).
        /// </summary>
        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure) {
            if (_disposed) return;
            SDKLogger.Warning(Tag,
                $"Fetch purchases failed: {failure.FailureReason} - {failure.Message}. " +
                "Pending purchases may not be processed until next attempt.");
        }

        /// <summary>
        /// IAP v5: Called when FetchProducts fails. Triggers retry logic.
        /// </summary>
        private void OnProductsFetchFailed(ProductFetchFailed failure) {
            if (_fetchCompleted || _disposed) return;

            SDKLogger.Warning(Tag,
                $"Product fetch failed: {failure.FailureReason}. " +
                $"Attempt {_retryCount + 1}/{MaxRetryCount + 1}.");

            // Increment generation to invalidate the pending timeout for this attempt.
            _fetchGeneration++;

            RetryOrFail();
        }

        /// <summary>
        /// IAP v5: Called for new purchases and auto-processed restored purchases.
        /// Receipt and TransactionID are available on PendingOrder (not on confirmed).
        /// </summary>
        private void OnPurchasePending(PendingOrder pendingOrder) {
            var cartItems = pendingOrder.CartOrdered.Items();
            if (cartItems == null || cartItems.Count == 0) return;

            var product = cartItems[0].Product;
            var productId = product.definition.id;
            var info = pendingOrder.Info;

            SDKLogger.Info(Tag, $"Purchase pending: {productId} (txn: {info.TransactionID})");

            // Track new subscription purchase immediately (before next FetchPurchases)
            if (product.definition.type == UnityEngine.Purchasing.ProductType.Subscription) {
                _activeSubscriptions.Add(productId);
            }

            // Build result while receipt is still available (v5: receipt is only on PendingOrder)
            var result = PurchaseResult.Succeeded(productId, info.TransactionID, info.Receipt);

            _purchaseCallback?.Invoke(result);
            _purchaseCallback = null;

            // Confirm the pending order to finalize the transaction
            _controller.ConfirmPurchase(pendingOrder);
        }

        /// <summary>
        /// IAP v5: Called when a purchase fails. FailedOrder inherits from Order.
        /// </summary>
        private void OnPurchaseFailed(FailedOrder failedOrder) {
            var cartItems = failedOrder.CartOrdered.Items();
            var productId = cartItems != null && cartItems.Count > 0
                ? cartItems[0].Product.definition.id
                : "unknown";

            var reason = MapFailureReason(failedOrder.FailureReason);
            var message = failedOrder.Details ?? failedOrder.FailureReason.ToString();

            SDKLogger.Warning(Tag, $"Purchase failed: {productId} - {reason}: {message}");

            var result = PurchaseResult.Failed(productId, message, reason);

            _purchaseCallback?.Invoke(result);
            _purchaseCallback = null;
        }

        /// <summary>
        /// IAP v5.0.4: Called when a purchase is confirmed.
        /// Event type is Action&lt;Order&gt; (not ConfirmedOrder).
        /// NOTE: Receipt and TransactionID may be empty for confirmed consumables.
        /// </summary>
        private void OnPurchaseConfirmed(Order confirmedOrder) {
            if (_disposed) return;

            var items = confirmedOrder.CartOrdered.Items();
            if (items == null || items.Count == 0) return;

            var productId = items[0].Product.definition.id;
            SDKLogger.Info(Tag,
                $"Purchase confirmed: {productId} (txn: {confirmedOrder.Info.TransactionID})");
        }

        /// <summary>
        /// IAP v5: Called when a purchase is deferred (e.g., parental approval on Google Play).
        /// </summary>
        private void OnPurchaseDeferred(DeferredOrder deferredOrder) {
            if (_disposed) return;

            var items = deferredOrder.CartOrdered.Items();
            if (items == null || items.Count == 0) return;

            var productId = items[0].Product.definition.id;
            SDKLogger.Info(Tag,
                $"Purchase deferred (pending approval): {productId}. " +
                "This purchase requires additional action from the user (e.g., parental approval).");

            // Don't call _purchaseCallback — purchase is not yet complete.
            // It will come through OnPurchasePending when approved.
        }

        /// <summary>
        /// IAP v5.0.4: Called when the store disconnects.
        /// Parameter is StoreConnectionFailureDescription (has Message + IsRetryable).
        /// </summary>
        private void OnStoreDisconnected(StoreConnectionFailureDescription failure) {
            if (_disposed) return;

            _connected = false;
            SDKLogger.Warning(Tag,
                $"Store disconnected: {failure.Message ?? "(unknown)"}. " +
                $"Retryable: {failure.IsRetryable}");

            // Only attempt reconnection if the failure is retryable
            if (failure.IsRetryable) {
                SDKLogger.Info(Tag, "Attempting to reconnect...");
                ConnectAndFetch();
            } else {
                SDKLogger.Warning(Tag,
                    "Store disconnection is not retryable. " +
                    "IAP will be unavailable until app restart.");
            }
        }

        // ─── Subscription Cache ───

        /// <summary>
        /// Rebuilds _activeSubscriptions from the latest FetchPurchases result.
        /// Pending orders (not yet confirmed) and confirmed orders both indicate an active subscription.
        /// </summary>
        private void RefreshSubscriptionCache(
            IReadOnlyList<PendingOrder> pendingOrders,
            IReadOnlyList<Order> confirmedOrders) {
            _activeSubscriptions.Clear();

            if (pendingOrders != null) {
                foreach (var order in pendingOrders) {
                    TryAddSubscription(order.CartOrdered.Items());
                }
            }

            if (confirmedOrders != null) {
                foreach (var order in confirmedOrders) {
                    TryAddSubscription(order.CartOrdered.Items());
                }
            }

            if (_activeSubscriptions.Count > 0) {
                SDKLogger.Info(Tag,
                    $"Active subscriptions: [{string.Join(", ", _activeSubscriptions)}]");
            }
        }

        private void TryAddSubscription(IReadOnlyList<CartItem> items) {
            if (items == null || items.Count == 0) return;
            var product = items[0].Product;
            if (product.definition.type == UnityEngine.Purchasing.ProductType.Subscription) {
                _activeSubscriptions.Add(product.definition.id);
            }
        }

        // ─── Mapping ───

        private static UnityEngine.Purchasing.ProductType MapProductType(IAP.ProductType type) {
            switch (type) {
                case IAP.ProductType.Consumable:    return UnityEngine.Purchasing.ProductType.Consumable;
                case IAP.ProductType.NonConsumable:  return UnityEngine.Purchasing.ProductType.NonConsumable;
                case IAP.ProductType.Subscription:   return UnityEngine.Purchasing.ProductType.Subscription;
                default:                             return UnityEngine.Purchasing.ProductType.Consumable;
            }
        }

        private static ProductInfo MapProduct(UnityEngine.Purchasing.Product product) {
            var meta = product.metadata;
            return new ProductInfo(
                productId: product.definition.id,
                localizedTitle: meta.localizedTitle,
                localizedDescription: meta.localizedDescription,
                localizedPrice: meta.localizedPriceString,
                priceDecimal: meta.localizedPrice,
                currencyCode: meta.isoCurrencyCode,
                type: MapProductTypeReverse(product.definition.type)
            );
        }

        private static IAP.ProductType MapProductTypeReverse(UnityEngine.Purchasing.ProductType type) {
            switch (type) {
                case UnityEngine.Purchasing.ProductType.Consumable:    return IAP.ProductType.Consumable;
                case UnityEngine.Purchasing.ProductType.NonConsumable:  return IAP.ProductType.NonConsumable;
                case UnityEngine.Purchasing.ProductType.Subscription:   return IAP.ProductType.Subscription;
                default:                                                return IAP.ProductType.Consumable;
            }
        }

        private static PurchaseFailureReason MapFailureReason(UnityEngine.Purchasing.PurchaseFailureReason reason) {
            switch (reason) {
                case UnityEngine.Purchasing.PurchaseFailureReason.UserCancelled:          return PurchaseFailureReason.UserCancelled;
                case UnityEngine.Purchasing.PurchaseFailureReason.PaymentDeclined:         return PurchaseFailureReason.PaymentDeclined;
                case UnityEngine.Purchasing.PurchaseFailureReason.ProductUnavailable:      return PurchaseFailureReason.ProductUnavailable;
                case UnityEngine.Purchasing.PurchaseFailureReason.PurchasingUnavailable:   return PurchaseFailureReason.PurchasingUnavailable;
                case UnityEngine.Purchasing.PurchaseFailureReason.ExistingPurchasePending: return PurchaseFailureReason.ExistingPurchasePending;
                case UnityEngine.Purchasing.PurchaseFailureReason.DuplicateTransaction:    return PurchaseFailureReason.DuplicateTransaction;
                case UnityEngine.Purchasing.PurchaseFailureReason.SignatureInvalid:         return PurchaseFailureReason.SignatureInvalid;
                default:                                                                   return PurchaseFailureReason.Unknown;
            }
        }
    }
}
#endif
