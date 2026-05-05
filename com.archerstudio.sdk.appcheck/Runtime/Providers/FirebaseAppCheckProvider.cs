#if HAS_FIREBASE_APP_CHECK
using System;
using ArcherStudio.SDK.Core;
using Firebase.AppCheck;
using Firebase.Extensions;

namespace ArcherStudio.SDK.AppCheck {

    public class FirebaseAppCheckProvider : IAppCheckProvider {
        private const string Tag = "AppCheck.Firebase";
        private FirebaseAppCheck _appCheck;

        public void Initialize(AppCheckConfig config, Action<bool> onComplete) {
            try {
                #if UNITY_ANDROID
                FirebaseAppCheck.SetAppCheckProviderFactory(
                    PlayIntegrityProviderFactory.Instance);
                SDKLogger.Info(Tag, "Using Play Integrity provider (Android).");
                #elif UNITY_IOS
                FirebaseAppCheck.SetAppCheckProviderFactory(
                    DeviceCheckProviderFactory.Instance);
                SDKLogger.Info(Tag, "Using DeviceCheck provider (iOS).");
                #else
                FirebaseAppCheck.SetAppCheckProviderFactory(
                    DebugProviderFactory.Instance);
                SDKLogger.Warning(Tag, "Using Debug provider (Editor/unsupported platform).");
                #endif

                _appCheck = FirebaseAppCheck.DefaultInstance;
                SDKLogger.Info(Tag, "Firebase App Check initialized.");
                onComplete?.Invoke(true);
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Failed to init App Check: {e.Message}");
                onComplete?.Invoke(false);
            }
        }

        public void GetToken(Action<string> onToken) {
            if (_appCheck == null) {
                SDKLogger.Warning(Tag, "App Check not initialized. Returning null token.");
                onToken?.Invoke(null);
                return;
            }

            _appCheck.GetAppCheckTokenAsync(forceRefresh: false)
                .ContinueWithOnMainThread(task => {
                    if (task.IsFaulted || task.IsCanceled) {
                        SDKLogger.Warning(Tag,
                            $"Failed to get App Check token: {task.Exception?.Message}");
                        onToken?.Invoke(null);
                        return;
                    }

                    var result = task.Result;
                    SDKLogger.Debug(Tag, "Got App Check token.");
                    onToken?.Invoke(result.Token);
                });
        }

        public void Dispose() {
            _appCheck = null;
        }
    }
}
#endif
