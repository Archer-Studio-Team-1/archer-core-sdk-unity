using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.DeepLink {

    /// <summary>
    /// Central deep link manager. Implements ISDKModule for SDK lifecycle.
    /// Aggregates deep links from multiple providers (Unity, Firebase, Adjust).
    /// </summary>
    public class DeepLinkManager : ISDKModule {
        private const string Tag = "DeepLink";

        // ─── ISDKModule ───
        public string ModuleId => "deeplink";
        public int InitializationPriority => 70;
        public IReadOnlyList<string> Dependencies => new[] { "consent" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        // ─── Singleton access ───
        public static DeepLinkManager Instance { get; private set; }

        // ─── Internal ───
        private DeepLinkConfig _config;
        private readonly List<IDeepLinkProvider> _providers = new List<IDeepLinkProvider>();

        /// <summary>
        /// Raised when any provider receives a deep link.
        /// </summary>
        public event Action<DeepLinkData> OnDeepLinkReceived;

        /// <summary>
        /// Stores the last received deep link for late subscribers.
        /// Null if no deep link has been received yet.
        /// </summary>
        public DeepLinkData? LastDeepLink { get; private set; }

        // ─── ISDKModule Lifecycle ───

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;
            Instance = this;

            _config = Resources.Load<DeepLinkConfig>("DeepLinkConfig");
            if (_config == null) {
                SDKLogger.Error(Tag, "DeepLinkConfig not found in Resources. Cannot initialize.");
                State = ModuleState.Failed;
                onComplete?.Invoke(false);
                return;
            }

            SDKLogger.Info(Tag, "┌─── DeepLink Config ───");
            SDKLogger.Info(Tag, $"│ Enabled:             {_config.Enabled}");
            SDKLogger.Info(Tag, $"│ URI Scheme:          {(_config.UriScheme ?? "(not set)")}");
            SDKLogger.Info(Tag, $"│ Dynamic Links Domain:{(_config.DynamicLinksDomain ?? "(not set)")}");
            SDKLogger.Info(Tag, $"│ DeferredDeepLinks:   {_config.EnableDeferredDeepLinks}");
            SDKLogger.Info(Tag, "└────────────────────────");

            if (!_config.Enabled) {
                SDKLogger.Info(Tag, "Deep link module disabled via config.");
                State = ModuleState.Failed;
                onComplete?.Invoke(false);
                return;
            }

            CreateProviders();

            int totalProviders = _providers.Count;
            int initializedCount = 0;
            bool anyFailed = false;

            if (totalProviders == 0) {
                SDKLogger.Warning(Tag, "No deep link providers configured.");
                State = ModuleState.Ready;
                onComplete?.Invoke(true);
                return;
            }

            foreach (var provider in _providers) {
                provider.OnDeepLinkReceived += OnProviderDeepLinkReceived;
                provider.Initialize(success => {
                    if (!success) {
                        anyFailed = true;
                        SDKLogger.Warning(Tag, "A deep link provider failed to initialize.");
                    }

                    initializedCount++;
                    if (initializedCount >= totalProviders) {
                        OnAllProvidersInitialized(anyFailed, onComplete);
                    }
                });
            }
        }

        public void OnConsentChanged(ConsentStatus consent) {
            // Deep link module does not change behavior based on consent.
        }

        public void Dispose() {
            foreach (var provider in _providers) {
                provider.OnDeepLinkReceived -= OnProviderDeepLinkReceived;
                if (provider is IDisposable disposable) {
                    disposable.Dispose();
                }
            }

            _providers.Clear();
            LastDeepLink = null;
            Instance = null;
            State = ModuleState.Disposed;
        }

        // ─── Internal ───

        private void CreateProviders() {
            _providers.Clear();

            // Unity built-in provider is always available
            _providers.Add(new UnityDeepLinkProvider());

            #if HAS_FIREBASE_DYNAMIC_LINKS
            _providers.Add(new FirebaseDeepLinkProvider());
            SDKLogger.Debug(Tag, "Firebase Dynamic Links provider added.");
            #endif

            #if HAS_ADJUST_SDK
            _providers.Add(new AdjustDeepLinkProvider(_config.EnableDeferredDeepLinks));
            SDKLogger.Debug(Tag, "Adjust deep link provider added.");
            #endif
        }

        private void OnAllProvidersInitialized(bool anyFailed, Action<bool> onComplete) {
            if (anyFailed) {
                SDKLogger.Warning(Tag,
                    $"DeepLinkManager initialized with warnings. " +
                    $"{_providers.Count} providers registered (some may have failed).");
            } else {
                SDKLogger.Info(Tag,
                    $"DeepLinkManager initialized. {_providers.Count} providers registered.");
            }

            State = ModuleState.Ready;
            onComplete?.Invoke(true);
        }

        private void OnProviderDeepLinkReceived(DeepLinkData data) {
            SDKLogger.Info(Tag, $"Deep link received: {data}");
            LastDeepLink = data;
            OnDeepLinkReceived?.Invoke(data);
        }
    }
}
