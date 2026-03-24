using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking {

    /// <summary>
    /// Central tracking manager. Implements ISDKModule for lifecycle integration.
    /// Manages multiple tracking providers and a persistent UserProfile.
    /// </summary>
    public class TrackingManager : SingletonMonoDontDestroy<TrackingManager>, ISDKModule {
        private const string Tag = "Tracking";

        // ─── ISDKModule ───
        public string ModuleId => "tracking";
        public int InitializationPriority => 20;
        public IReadOnlyList<string> Dependencies => new[] { "consent", "firebase" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        // ─── Internal State ───
        private readonly List<ITrackingProvider> _providers = new List<ITrackingProvider>();
        private UserProfile _currentUserProfile;
        private bool _isDirty;
        private TrackingConfig _config;
        private ConsentStatus _currentConsent = ConsentStatus.Default;

        // Optimization: Reusable dictionary to avoid allocations per event
        private static readonly Dictionary<string, object> SharedParams =
            new Dictionary<string, object>(32);

        public UserProfile CurrentUserProfile => _currentUserProfile;

        // ─── ISDKModule Lifecycle ───

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;

            _config = Resources.Load<TrackingConfig>("TrackingConfig");

            // Cache persistent path for thread-safe access in UserProfile
            UserProfile.PersistentDataPath = Application.persistentDataPath;
            _currentUserProfile = UserProfile.Load();

            LogTrackingConfig();

            // Read current consent from ConsentManager BEFORE provider init.
            // ConsentChangedEvent was broadcast in batch 1, before TrackingManager
            // subscribes — so we must read it directly.
            var consentModule = SDKInitializer.Instance?.GetModule("consent");
            if (consentModule is ArcherStudio.SDK.Consent.ConsentManager cm) {
                _currentConsent = cm.CurrentStatus;
                SDKLogger.Info(Tag, $"Pre-init consent: {_currentConsent}");
            }

            InitializeProviders(() => {
                if (_currentUserProfile != null) {
                    IdentifyUser(_currentUserProfile);
                }

                // Subscribe to future consent changes
                SDKEventBus.Subscribe<ConsentChangedEvent>(OnConsentEvent);

                State = ModuleState.Ready;
                SDKLogger.Info(Tag, "TrackingManager initialized.");
                onComplete?.Invoke(true);
            });
        }

        public void OnConsentChanged(ConsentStatus consent) {
            _currentConsent = consent;
            foreach (var provider in _providers) {
                provider.SetConsent(consent);
            }
        }

        public void Dispose() {
            SDKEventBus.Unsubscribe<ConsentChangedEvent>(OnConsentEvent);
            State = ModuleState.Disposed;
        }

        // ─── Provider Management ───

        private void InitializeProviders(Action onAllReady) {
            var enabledTypes = _config != null
                ? _config.EnabledProviders
                : new List<TrackingProviderType> {
                    TrackingProviderType.Firebase,
                    TrackingProviderType.Adjust
                };

            int remaining = enabledTypes.Count;
            if (remaining == 0) {
                onAllReady?.Invoke();
                return;
            }

            foreach (var providerType in enabledTypes) {
                var provider = CreateProvider(providerType);
                if (provider == null) {
                    remaining--;
                    if (remaining <= 0) onAllReady?.Invoke();
                    continue;
                }

                _providers.Add(provider);
                try {
                    provider.Initialize(success => {
                        RunOnMainThread(() => {
                            if (success) {
                                SDKLogger.Info(Tag, $"Provider '{provider.ProviderId}' initialized.");
                                provider.SetConsent(_currentConsent);

                                if (provider.ProviderId == "firebase") {
                                    CalculateRetentionDays();
                                }
                            } else {
                                SDKLogger.Error(Tag,
                                    $"Provider '{provider.ProviderId}' failed to initialize.");
                            }

                            remaining--;
                            if (remaining <= 0) onAllReady?.Invoke();
                        });
                    });
                } catch (Exception e) {
                    SDKLogger.Error(Tag,
                        $"Provider '{provider.ProviderId}' threw during Initialize: {e.Message}");
                    remaining--;
                    if (remaining <= 0) onAllReady?.Invoke();
                }
            }
        }

        private void LogTrackingConfig() {
            if (_config == null) {
                SDKLogger.Debug(Tag, "TrackingConfig: null (using default providers)");
                return;
            }

            SDKLogger.Info(Tag, "┌─── Tracking Config ───");
            SDKLogger.Info(Tag, $"│ Enabled:            {_config.Enabled}");
            SDKLogger.Info(Tag, $"│ Adjust App Token:   {MaskToken(_config.AdjustAppToken)}");
            SDKLogger.Info(Tag, $"│ UseSandboxInDebug:  {_config.UseSandboxInDebug}");
            SDKLogger.Info(Tag, $"│ Meta App ID:        {MaskId(_config.MetaAppId)}");
            SDKLogger.Info(Tag, $"│ External Device ID: {MaskId(_config.ExternalDeviceId)}");
            SDKLogger.Info(Tag, $"│ Default Tracker:    {MaskId(_config.DefaultTracker)}");
            SDKLogger.Info(Tag, $"│ Store Name:         {(string.IsNullOrEmpty(_config.StoreName) ? "(not set)" : _config.StoreName)}");
            SDKLogger.Info(Tag, $"│ Store App ID:       {MaskId(_config.StoreAppId)}");
            SDKLogger.Info(Tag, $"│ COPPA Compliance:   {_config.EnableCoppaCompliance}");
            SDKLogger.Info(Tag, $"│ SendInBackground:   {_config.EnableSendInBackground}");
            SDKLogger.Info(Tag, $"│ LinkMe:             {_config.EnableLinkMe}");
            SDKLogger.Info(Tag, $"│ OAID:               {_config.EnableOaid}");
            SDKLogger.Info(Tag, $"│ DeferredDeepLink:   {_config.EnableDeferredDeepLinkOpening}");
            SDKLogger.Info(Tag, $"│ VerboseLogging:     {_config.VerboseLogging}");

            string providers = string.Join(", ", _config.EnabledProviders);
            SDKLogger.Info(Tag, $"│ Providers:          [{providers}]");

            if (_config.GlobalCallbackParams.Count > 0) {
                SDKLogger.Info(Tag, $"│ GlobalCallbackParams: {_config.GlobalCallbackParams.Count}");
                foreach (var p in _config.GlobalCallbackParams) {
                    SDKLogger.Verbose(Tag, $"│   {p.Key} = {p.Value}");
                }
            }
            if (_config.GlobalPartnerParams.Count > 0) {
                SDKLogger.Info(Tag, $"│ GlobalPartnerParams:  {_config.GlobalPartnerParams.Count}");
                foreach (var p in _config.GlobalPartnerParams) {
                    SDKLogger.Verbose(Tag, $"│   {p.Key} = {p.Value}");
                }
            }

            SDKLogger.Info(Tag, "└────────────────────────");
        }

        private static string MaskToken(string token) {
            if (string.IsNullOrEmpty(token)) return "(not set)";
            if (token.Length <= 4) return "***";
            return token.Substring(0, 4) + "***";
        }

        private static string MaskId(string id) {
            if (string.IsNullOrEmpty(id)) return "(not set)";
            if (id.Length <= 6) return id.Substring(0, 2) + "***";
            return id.Substring(0, 4) + "***" + id.Substring(id.Length - 4);
        }

        private ITrackingProvider CreateProvider(TrackingProviderType type) {
            switch (type) {
                case TrackingProviderType.Firebase:
                    return new FirebaseTrackingProvider();
                case TrackingProviderType.Adjust:
                    if (_config == null) {
                        SDKLogger.Error(Tag, "TrackingConfig is null. Cannot create AdjustTrackingProvider.");
                        return null;
                    }
                    return new AdjustTrackingProvider(_config, _currentConsent);
                default:
                    SDKLogger.Warning(Tag, $"Unknown provider type: {type}");
                    return null;
            }
        }

        /// <summary>
        /// Register a custom tracking provider at runtime.
        /// </summary>
        public void RegisterProvider(ITrackingProvider provider) {
            if (provider == null) return;
            _providers.Add(provider);
            provider.SetConsent(_currentConsent);
            SDKLogger.Info(Tag, $"Registered custom provider: {provider.ProviderId}");
        }

        // ─── Event Tracking ───

        public void Track(GameTrackingEvent e) {
            SharedParams.Clear();
            e.FillParams(SharedParams);

            bool verbose = _config == null || _config.VerboseLogging;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verbose) {
                string jsonParams = JsonConvert.SerializeObject(SharedParams, Formatting.None);
                Debug.Log($"<color=cyan>[Tracking] Event: {e.EventName} | Params: {jsonParams}</color>");
            }
            #endif

            foreach (var provider in _providers) {
                provider.TrackEvent(e);
            }
        }

        // ─── Ad Revenue Tracking ───

        /// <summary>
        /// Track ad revenue through all providers.
        /// Firebase: logs "ad_impression" event. Adjust: uses AdjustAdRevenue API.
        /// </summary>
        public void TrackAdRevenue(string adPlatform, string adSource, string adFormat,
            string adUnitName, string currency, double value, string placement) {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool verbose = _config == null || _config.VerboseLogging;
            if (verbose) {
                Debug.Log($"<color=cyan>[Tracking] AdRevenue: {adPlatform}/{adSource} " +
                          $"{currency} {value:F6} [{adFormat}]</color>");
            }
            #endif

            foreach (var provider in _providers) {
                provider.TrackAdRevenue(adPlatform, adSource, adFormat,
                    adUnitName, currency, value, placement);
            }
        }

        // ─── IAP Revenue Tracking ───

        /// <summary>
        /// Track IAP revenue through all providers.
        /// Firebase: logs "in_app_purchase" event with revenue.
        /// Adjust: verifies receipt and tracks revenue via VerifyAndTrack*Purchase.
        /// </summary>
        public void TrackIAPRevenue(string productId, double revenue, string currency,
            string transactionId, string receipt, string source) {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool verbose = _config != null ? _config.VerboseLogging : true;
            if (verbose) {
                Debug.Log($"<color=cyan>[Tracking] IAPRevenue: {productId} " +
                          $"{currency} {revenue:F2} txn={transactionId}</color>");
            }
            #endif

            foreach (var provider in _providers) {
                provider.TrackIAPRevenue(productId, revenue, currency,
                    transactionId, receipt, source);
            }
        }

        /// <summary>
        /// Get the Adjust tracking provider instance for advanced operations.
        /// Returns null if Adjust is not enabled.
        /// </summary>
        public AdjustTrackingProvider GetAdjustProvider() {
            foreach (var provider in _providers) {
                if (provider is AdjustTrackingProvider adjustProvider) {
                    return adjustProvider;
                }
            }
            return null;
        }

        // ─── User Profile ───

        public void IdentifyUser(UserProfile profile) {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            string jsonProfile = JsonConvert.SerializeObject(profile, Formatting.None);
            Debug.Log($"<color=green>[Tracking] Identify User: {profile.UserId} | Profile: {jsonProfile}</color>");
            #endif

            if (_currentUserProfile != null) {
                _currentUserProfile.OnPropertyChanged -= OnUserProfilePropertyChanged;
            }

            _currentUserProfile = profile;
            _currentUserProfile.OnPropertyChanged += OnUserProfilePropertyChanged;

            var allProperties = profile.GetAllProperties();
            foreach (var provider in _providers) {
                provider.SetUserId(profile.UserId);
                foreach (var prop in allProperties) {
                    provider.SetUserProperty(prop.Key, prop.Value);
                }
            }
        }

        public void UpdateUserProfile(Action<UserProfile> updateAction) {
            if (_currentUserProfile == null) {
                SDKLogger.Warning(Tag,
                    "UpdateUserProfile called but profile is null. Creating new default profile.");
                _currentUserProfile = new UserProfile();
                IdentifyUser(_currentUserProfile);
            }

            updateAction?.Invoke(_currentUserProfile);
        }

        public void SetUserProperty(string key, string value) {
            if (_currentUserProfile == null) {
                _currentUserProfile = new UserProfile();
                IdentifyUser(_currentUserProfile);
            }

            _currentUserProfile.SetProperty(key, value);
        }

        // ─── Internal ───

        private static void RunOnMainThread(Action action) {
            var dispatcher = UnityMainThreadDispatcher.Instance;
            if (dispatcher != null) {
                dispatcher.Enqueue(action);
            } else {
                action?.Invoke();
            }
        }

        private void OnConsentEvent(ConsentChangedEvent e) {
            OnConsentChanged(e.Status);
        }

        private void OnUserProfilePropertyChanged(string key, string value) {
            _isDirty = true;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"<color=green>[Tracking] Auto-Sync Property: {key} = {value}</color>");
            #endif

            foreach (var provider in _providers) {
                provider.SetUserProperty(key, value);
            }
        }

        private void CalculateRetentionDays() {
            var profile = _currentUserProfile;
            if (profile == null) return;

            if (profile.InstallTimestamp <= 0) {
                profile.InstallTimestamp = DateTime.UtcNow.Ticks;
            }

            try {
                DateTime installDate = new DateTime(profile.InstallTimestamp).Date;
                int daysDiff = (DateTime.UtcNow.Date - installDate).Days;
                int activeDay = Mathf.Max(0, daysDiff);

                UpdateUserProfile(p => {
                    p.DaySinceInstall = activeDay;
                    p.ActiveDayN = activeDay;
                });
            } catch (Exception ex) {
                SDKLogger.Warning(Tag, $"Error calculating retention: {ex.Message}");
            }
        }

        private void Update() {
            if (_isDirty && _currentUserProfile != null) {
                _isDirty = false;
                _currentUserProfile.Save();
            }
        }
    }
}
