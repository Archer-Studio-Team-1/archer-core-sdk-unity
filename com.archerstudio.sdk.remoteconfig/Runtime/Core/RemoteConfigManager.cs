using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.RemoteConfig {

    /// <summary>
    /// Central remote config manager. Implements ISDKModule for SDK lifecycle.
    /// Delegates to the active IRemoteConfigProvider.
    /// </summary>
    public class RemoteConfigManager : ISDKModule {
        private const string Tag = "RemoteConfig";

        // ─── ISDKModule ───
        public string ModuleId => "remoteconfig";
        public int InitializationPriority => 40;
        public IReadOnlyList<string> Dependencies => new[] { "consent" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        // ─── Singleton access ───
        public static RemoteConfigManager Instance { get; private set; }

        // ─── Internal ───
        private IRemoteConfigProvider _provider;
        private RemoteConfigConfig _config;

        // ─── ISDKModule Lifecycle ───

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;
            Instance = this;

            _config = Resources.Load<RemoteConfigConfig>("RemoteConfigConfig");
            if (_config == null) {
                SDKLogger.Error(Tag, "RemoteConfigConfig not found in Resources. Cannot initialize.");
                State = ModuleState.Failed;
                onComplete?.Invoke(false);
                return;
            }

            SDKLogger.Info(Tag, "┌─── RemoteConfig Config ───");
            SDKLogger.Info(Tag, $"│ Enabled:             {_config.Enabled}");
            SDKLogger.Info(Tag, $"│ AutoFetchOnInit:     {_config.AutoFetchOnInit}");
            SDKLogger.Info(Tag, $"│ FetchInterval:       {_config.MinimumFetchIntervalSeconds}s");
            SDKLogger.Info(Tag, $"│ LocalDefaults:       {_config.EnableLocalDefaults}");
            SDKLogger.Info(Tag, "└───────────────────────────");

            _provider = CreateProvider();

            _provider.Initialize(success => {
                if (!success) {
                    SDKLogger.Error(Tag, "Remote config provider failed to initialize.");
                    State = ModuleState.Failed;
                    onComplete?.Invoke(false);
                    return;
                }

                SDKLogger.Info(Tag, "RemoteConfigManager initialized.");

                if (_config.AutoFetchOnInit) {
                    _provider.FetchAndActivate(fetchSuccess => {
                        if (fetchSuccess) {
                            SDKLogger.Info(Tag, "Initial fetch and activate completed.");
                        } else {
                            SDKLogger.Warning(Tag, "Initial fetch failed. Using cached/default values.");
                        }

                        State = ModuleState.Ready;
                        onComplete?.Invoke(true);
                    });
                } else {
                    State = ModuleState.Ready;
                    onComplete?.Invoke(true);
                }
            });
        }

        public void OnConsentChanged(ConsentStatus consent) {
            // No-op: Remote config does not depend on consent status.
        }

        public void Dispose() {
            Instance = null;
            State = ModuleState.Disposed;
        }

        // ─── Public API ───

        /// <summary>
        /// Fetch latest values from the remote config backend and activate them.
        /// </summary>
        public void FetchAndActivate(Action<bool> onComplete = null) {
            if (State != ModuleState.Ready) {
                SDKLogger.Warning(Tag, "Cannot fetch: module is not ready.");
                onComplete?.Invoke(false);
                return;
            }

            _provider.FetchAndActivate(onComplete);
        }

        /// <summary>
        /// Get a string value for the given key.
        /// </summary>
        public string GetString(string key, string defaultValue = "") {
            if (State != ModuleState.Ready) return defaultValue;
            return _provider.GetString(key, defaultValue);
        }

        /// <summary>
        /// Get a boolean value for the given key.
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false) {
            if (State != ModuleState.Ready) return defaultValue;
            return _provider.GetBool(key, defaultValue);
        }

        /// <summary>
        /// Get an integer value for the given key.
        /// </summary>
        public int GetInt(string key, int defaultValue = 0) {
            if (State != ModuleState.Ready) return defaultValue;
            return _provider.GetInt(key, defaultValue);
        }

        /// <summary>
        /// Get a float value for the given key.
        /// </summary>
        public float GetFloat(string key, float defaultValue = 0f) {
            if (State != ModuleState.Ready) return defaultValue;
            return _provider.GetFloat(key, defaultValue);
        }

        /// <summary>
        /// Get a long value for the given key.
        /// </summary>
        public long GetLong(string key, long defaultValue = 0L) {
            if (State != ModuleState.Ready) return defaultValue;
            return _provider.GetLong(key, defaultValue);
        }

        // ─── Internal ───

        private IRemoteConfigProvider CreateProvider() {
            #if HAS_FIREBASE_REMOTE_CONFIG
            SDKLogger.Info(Tag, "Using FirebaseRemoteConfigProvider.");
            return new FirebaseRemoteConfigProvider(_config);
            #else
            SDKLogger.Info(Tag, "Firebase Remote Config not available. Using StubRemoteConfigProvider.");
            return new StubRemoteConfigProvider();
            #endif
        }
    }
}
