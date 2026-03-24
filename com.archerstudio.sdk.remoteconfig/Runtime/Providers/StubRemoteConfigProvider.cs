using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.RemoteConfig {

    /// <summary>
    /// Stub provider that returns default values when Firebase Remote Config is not available.
    /// </summary>
    public class StubRemoteConfigProvider : IRemoteConfigProvider {
        private const string Tag = "RemoteConfig-Stub";

        public bool IsInitialized { get; private set; }

        public void Initialize(Action<bool> onComplete) {
            SDKLogger.Info(Tag, "Stub provider initialized. All values will return defaults.");
            IsInitialized = true;
            onComplete?.Invoke(true);
        }

        public void FetchAndActivate(Action<bool> onComplete) {
            SDKLogger.Debug(Tag, "FetchAndActivate called on stub provider. No-op.");
            onComplete?.Invoke(true);
        }

        public string GetString(string key, string defaultValue = "") {
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false) {
            return defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0) {
            return defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f) {
            return defaultValue;
        }

        public long GetLong(string key, long defaultValue = 0L) {
            return defaultValue;
        }
    }
}
