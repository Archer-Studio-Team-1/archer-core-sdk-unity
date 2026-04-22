using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using ArcherStudio.SDK.Login;
using UnityEngine;

namespace ArcherStudio.SDK.CloudSave {

    public class CloudSaveModule : ISDKModule {
        private const string Tag = "CloudSave";

        public string ModuleId => "cloudsave";
        public int InitializationPriority => 50;
        public IReadOnlyList<string> Dependencies => new[] { "login" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        public static CloudSaveModule Instance { get; private set; }

        private ICloudSaveProvider _provider;

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;

            if (!coreConfig.EnableCloudSave) {
                SDKLogger.Debug(Tag, "EnableCloudSave=false. Skipping.");
                _provider = new StubCloudSaveProvider();
                CompleteInit(onComplete);
                return;
            }

            var config = Resources.Load<CloudSaveConfig>("CloudSaveConfig");
            if (config == null) {
                SDKLogger.Error(Tag, "CloudSaveConfig not found in Resources/. Using stub provider.");
                _provider = new StubCloudSaveProvider();
                CompleteInit(onComplete);
                return;
            }

#if HAS_FIREBASE_FIRESTORE && HAS_GPGS && (UNITY_ANDROID || UNITY_EDITOR)
            var loginModule = LoginModule.Instance;
            if (loginModule == null || loginModule.Provider == null || !loginModule.Provider.IsSignedIn) {
                SDKLogger.Warning(Tag, "User not signed in via GPGS. Using stub provider.");
                _provider = new StubCloudSaveProvider();
                CompleteInit(onComplete);
                return;
            }

            loginModule.Provider.GetServerSideAccessCode(config.WebClientId, serverAuthCode => {
                if (string.IsNullOrEmpty(serverAuthCode)) {
                    SDKLogger.Warning(Tag, "No server auth code. Using stub provider.");
                    _provider = new StubCloudSaveProvider();
                    CompleteInit(onComplete);
                    return;
                }

                _provider = new FirestoreCloudSaveProvider(serverAuthCode);
                _provider.InitAsync(success => {
                    if (!success) {
                        SDKLogger.Warning(Tag, "Firestore provider init failed. Falling back to stub.");
                        _provider = new StubCloudSaveProvider();
                    }
                    CompleteInit(onComplete);
                });
            });
#else
            SDKLogger.Debug(Tag, "HAS_FIREBASE_FIRESTORE or HAS_GPGS not defined. Using stub provider.");
            _provider = new StubCloudSaveProvider();
            CompleteInit(onComplete);
#endif
        }

        private void CompleteInit(Action<bool> onComplete) {
            State = ModuleState.Ready;
            Instance = this;
            SDKEventBus.Subscribe<AppPauseEvent>(OnAppPause);
            onComplete?.Invoke(true);
        }

        private void OnAppPause(AppPauseEvent e) {
            // TODO(v1.1): flush pending debounced writes on pause
        }

        public void SaveAsync(string slotKey, string jsonData, Action<CloudSaveResult> onComplete) {
            if (State != ModuleState.Ready) {
                onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.ProviderError));
                return;
            }
            if (jsonData != null && jsonData.Length >= 900_000) {
                onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.DataTooLarge));
                return;
            }
            _provider.SaveAsync(slotKey, jsonData, result => {
                if (result.HasConflict) SDKEventBus.Publish(new CloudSaveSyncedEvent(slotKey, true));
                else if (result.Success)  SDKEventBus.Publish(new CloudSaveSyncedEvent(slotKey, false));
                else                      SDKEventBus.Publish(new CloudSaveFailedEvent(slotKey, result.ErrorCode));
                onComplete?.Invoke(result);
            });
        }

        public void LoadAsync(string slotKey, Action<CloudSaveResult> onComplete) {
            if (State != ModuleState.Ready) {
                onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.ProviderError));
                return;
            }
            _provider.LoadAsync(slotKey, result => {
                if (!result.Success && result.ErrorCode != CloudSaveErrorCode.NotFound)
                    SDKEventBus.Publish(new CloudSaveFailedEvent(slotKey, result.ErrorCode));
                onComplete?.Invoke(result);
            });
        }

        public void DeleteAsync(string slotKey, Action<CloudSaveResult> onComplete) {
            if (State != ModuleState.Ready) {
                onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.ProviderError));
                return;
            }
            _provider.DeleteAsync(slotKey, onComplete);
        }

        public void OnConsentChanged(ConsentStatus consent) { }

        public void Dispose() {
            SDKEventBus.Unsubscribe<AppPauseEvent>(OnAppPause);
            Instance = null;
            State = ModuleState.Disposed;
        }
    }
}
