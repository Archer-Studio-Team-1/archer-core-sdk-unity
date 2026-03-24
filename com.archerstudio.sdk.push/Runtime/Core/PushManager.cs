using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Push {

    /// <summary>
    /// Central push notification manager. Implements ISDKModule for SDK lifecycle.
    /// Delegates to the active IPushProvider (Firebase or Stub).
    /// </summary>
    public class PushManager : ISDKModule {
        private const string Tag = "Push";

        // --- ISDKModule ---
        public string ModuleId => "push";
        public int InitializationPriority => 60;
        public IReadOnlyList<string> Dependencies => new[] { "consent" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        // --- Singleton access ---
        public static PushManager Instance { get; private set; }

        // --- Internal ---
        private IPushProvider _provider;
        private PushConfig _config;

        // --- Events ---
        public event Action<PushMessage> OnMessageReceived;
        public event Action<string> OnTokenRefreshed;

        // --- ISDKModule Lifecycle ---

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;
            Instance = this;

            _config = Resources.Load<PushConfig>("PushConfig");
            if (_config == null) {
                SDKLogger.Error(Tag, "PushConfig not found in Resources. Cannot initialize.");
                State = ModuleState.Failed;
                onComplete?.Invoke(false);
                return;
            }

            SDKLogger.Info(Tag, "┌─── Push Config ───");
            SDKLogger.Info(Tag, $"│ Enabled:             {_config.Enabled}");
            SDKLogger.Info(Tag, $"│ AutoRequestPerm:     {_config.AutoRequestPermission}");
            if (_config.DefaultTopics != null && _config.DefaultTopics.Length > 0) {
                SDKLogger.Info(Tag, $"│ DefaultTopics:       [{string.Join(", ", _config.DefaultTopics)}]");
            } else {
                SDKLogger.Info(Tag, $"│ DefaultTopics:       (none)");
            }
            SDKLogger.Info(Tag, "└────────────────────");

            if (!_config.Enabled) {
                SDKLogger.Info(Tag, "Push module disabled via config.");
                State = ModuleState.Failed;
                onComplete?.Invoke(false);
                return;
            }

            _provider = CreateProvider();
            _provider.OnMessageReceived += HandleMessageReceived;
            _provider.OnTokenRefreshed += HandleTokenRefreshed;

            _provider.Initialize(success => {
                if (!success) {
                    SDKLogger.Error(Tag, "Push provider failed to initialize.");
                    State = ModuleState.Failed;
                    onComplete?.Invoke(false);
                    return;
                }

                SDKLogger.Info(Tag, "Push provider initialized successfully.");

                if (_config.AutoRequestPermission) {
                    _provider.RequestPermission(granted => {
                        SDKLogger.Info(Tag, $"Auto permission request result: {granted}");
                    });
                }

                SubscribeDefaultTopics();

                State = ModuleState.Ready;
                onComplete?.Invoke(true);
            });
        }

        public void OnConsentChanged(ConsentStatus consent) {
            // FCM does not typically require consent gating; no-op.
            SDKLogger.Verbose(Tag, $"Consent changed: {consent}. No action required for push.");
        }

        public void Dispose() {
            if (_provider != null) {
                _provider.OnMessageReceived -= HandleMessageReceived;
                _provider.OnTokenRefreshed -= HandleTokenRefreshed;
            }

            Instance = null;
            State = ModuleState.Disposed;
        }

        // --- Public API ---

        /// <summary>
        /// Request notification permission from the user.
        /// </summary>
        public void RequestPermission(Action<bool> onComplete) {
            if (State != ModuleState.Ready) {
                SDKLogger.Warning(Tag, "Cannot request permission: module not ready.");
                onComplete?.Invoke(false);
                return;
            }

            _provider.RequestPermission(onComplete);
        }

        /// <summary>
        /// Retrieve the current FCM registration token.
        /// </summary>
        public void GetToken(Action<string> onComplete) {
            if (State != ModuleState.Ready) {
                SDKLogger.Warning(Tag, "Cannot get token: module not ready.");
                onComplete?.Invoke(null);
                return;
            }

            _provider.GetToken(onComplete);
        }

        /// <summary>
        /// Subscribe to a push notification topic.
        /// </summary>
        public void SubscribeToTopic(string topic) {
            if (State != ModuleState.Ready) {
                SDKLogger.Warning(Tag, $"Cannot subscribe to '{topic}': module not ready.");
                return;
            }

            if (string.IsNullOrEmpty(topic)) {
                SDKLogger.Warning(Tag, "Cannot subscribe to empty topic.");
                return;
            }

            SDKLogger.Debug(Tag, $"Subscribing to topic: {topic}");
            _provider.SubscribeToTopic(topic);
        }

        /// <summary>
        /// Unsubscribe from a push notification topic.
        /// </summary>
        public void UnsubscribeFromTopic(string topic) {
            if (State != ModuleState.Ready) {
                SDKLogger.Warning(Tag, $"Cannot unsubscribe from '{topic}': module not ready.");
                return;
            }

            if (string.IsNullOrEmpty(topic)) {
                SDKLogger.Warning(Tag, "Cannot unsubscribe from empty topic.");
                return;
            }

            SDKLogger.Debug(Tag, $"Unsubscribing from topic: {topic}");
            _provider.UnsubscribeFromTopic(topic);
        }

        // --- Internal ---

        private static IPushProvider CreateProvider() {
            #if HAS_FIREBASE_MESSAGING
            SDKLogger.Info(Tag, "Using FirebasePushProvider.");
            return new FirebasePushProvider();
            #else
            SDKLogger.Info(Tag, "Using StubPushProvider (HAS_FIREBASE_MESSAGING not defined).");
            return new StubPushProvider();
            #endif
        }

        private void SubscribeDefaultTopics() {
            if (_config.DefaultTopics == null) return;

            foreach (var topic in _config.DefaultTopics) {
                if (!string.IsNullOrEmpty(topic)) {
                    SDKLogger.Debug(Tag, $"Auto-subscribing to default topic: {topic}");
                    _provider.SubscribeToTopic(topic);
                }
            }
        }

        private void HandleMessageReceived(PushMessage message) {
            SDKLogger.Debug(Tag, $"Message received: {message}");
            OnMessageReceived?.Invoke(message);
        }

        private void HandleTokenRefreshed(string token) {
            SDKLogger.Debug(Tag, $"Token refreshed: {token}");
            OnTokenRefreshed?.Invoke(token);
        }
    }
}
