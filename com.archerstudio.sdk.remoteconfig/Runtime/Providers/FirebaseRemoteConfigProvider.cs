#if HAS_FIREBASE_REMOTE_CONFIG
using System;
using System.Threading.Tasks;
using ArcherStudio.SDK.Core;
using Firebase.Extensions;
using Firebase.RemoteConfig;

namespace ArcherStudio.SDK.RemoteConfig {

    /// <summary>
    /// Firebase Remote Config provider. Requires HAS_FIREBASE_REMOTE_CONFIG define.
    /// </summary>
    public class FirebaseRemoteConfigProvider : IRemoteConfigProvider {
        private const string Tag = "RemoteConfig-Firebase";

        private readonly RemoteConfigConfig _config;
        private FirebaseRemoteConfig _remoteConfig;

        public bool IsInitialized { get; private set; }

        public FirebaseRemoteConfigProvider(RemoteConfigConfig config) {
            _config = config;
        }

        public void Initialize(Action<bool> onComplete) {
            FirebaseInitializer.EnsureInitialized(available => {
                try {
                    if (!available) {
                        SDKLogger.Error(Tag, "Firebase dependencies not available.");
                        onComplete?.Invoke(false);
                        return;
                    }

                    _remoteConfig = FirebaseRemoteConfig.DefaultInstance;

                    IsInitialized = true;
                    SDKLogger.Info(Tag, "Firebase Remote Config initialized.");
                    onComplete?.Invoke(true);
                } catch (Exception e) {
                    SDKLogger.Error(Tag, $"Init callback exception: {e.Message}");
                    onComplete?.Invoke(false);
                }
            });
        }

        public void FetchAndActivate(Action<bool> onComplete) {
            if (_remoteConfig == null) {
                SDKLogger.Error(Tag, "Cannot fetch: RemoteConfig not initialized.");
                onComplete?.Invoke(false);
                return;
            }

            _remoteConfig.FetchAsync(TimeSpan.FromSeconds(_config.MinimumFetchIntervalSeconds))
                .ContinueWithOnMainThread(fetchTask => {
                    if (fetchTask.IsFaulted) {
                        SDKLogger.Error(Tag,
                            $"Fetch failed: {fetchTask.Exception?.Message}");
                        onComplete?.Invoke(false);
                        return;
                    }

                    _remoteConfig.ActivateAsync().ContinueWithOnMainThread(activateTask => {
                        if (activateTask.IsFaulted) {
                            SDKLogger.Error(Tag,
                                $"Activate failed: {activateTask.Exception?.Message}");
                            onComplete?.Invoke(false);
                            return;
                        }

                        SDKLogger.Info(Tag, "Fetch and activate completed.");
                        onComplete?.Invoke(true);
                    });
                });
        }

        public string GetString(string key, string defaultValue = "") {
            if (_remoteConfig == null) return defaultValue;
            var value = _remoteConfig.GetValue(key);
            return value.Source == ValueSource.StaticValue ? defaultValue : value.StringValue;
        }

        public bool GetBool(string key, bool defaultValue = false) {
            if (_remoteConfig == null) return defaultValue;
            var value = _remoteConfig.GetValue(key);
            return value.Source == ValueSource.StaticValue ? defaultValue : value.BooleanValue;
        }

        public int GetInt(string key, int defaultValue = 0) {
            if (_remoteConfig == null) return defaultValue;
            var value = _remoteConfig.GetValue(key);
            return value.Source == ValueSource.StaticValue ? defaultValue : (int)value.LongValue;
        }

        public float GetFloat(string key, float defaultValue = 0f) {
            if (_remoteConfig == null) return defaultValue;
            var value = _remoteConfig.GetValue(key);
            return value.Source == ValueSource.StaticValue ? defaultValue : (float)value.DoubleValue;
        }

        public long GetLong(string key, long defaultValue = 0L) {
            if (_remoteConfig == null) return defaultValue;
            var value = _remoteConfig.GetValue(key);
            return value.Source == ValueSource.StaticValue ? defaultValue : value.LongValue;
        }
    }
}
#endif
