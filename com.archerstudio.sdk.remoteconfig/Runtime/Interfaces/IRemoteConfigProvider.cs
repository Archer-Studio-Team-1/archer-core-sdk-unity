using System;

namespace ArcherStudio.SDK.RemoteConfig {

    /// <summary>
    /// Abstraction for remote config backends (Firebase, stub, etc.).
    /// </summary>
    public interface IRemoteConfigProvider {
        bool IsInitialized { get; }
        void Initialize(Action<bool> onComplete);
        void FetchAndActivate(Action<bool> onComplete);
        string GetString(string key, string defaultValue = "");
        bool GetBool(string key, bool defaultValue = false);
        int GetInt(string key, int defaultValue = 0);
        float GetFloat(string key, float defaultValue = 0f);
        long GetLong(string key, long defaultValue = 0L);
    }
}
