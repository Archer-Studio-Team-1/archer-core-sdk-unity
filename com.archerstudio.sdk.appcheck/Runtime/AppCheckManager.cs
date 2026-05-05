using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.AppCheck {

    public class AppCheckManager : ISDKModule {
        private const string Tag = "AppCheck";

        public string ModuleId => "appcheck";
        public int InitializationPriority => 10;
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        public static AppCheckManager Instance { get; private set; }

        private IAppCheckProvider _provider;
        private AppCheckConfig _config;

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;
            Instance = this;

            _config = Resources.Load<AppCheckConfig>("AppCheckConfig");
            if (_config == null) {
                SDKLogger.Warning(Tag,
                    "AppCheckConfig not found in Resources/. " +
                    "Create via: Assets > Create > ArcherStudio > SDK > App Check Config. " +
                    "App Check module will be inactive.");
                State = ModuleState.Ready;
                onComplete?.Invoke(true);
                return;
            }

            if (!_config.Enabled) {
                SDKLogger.Info(Tag, "AppCheckConfig.Enabled=false. App Check inactive.");
                State = ModuleState.Ready;
                onComplete?.Invoke(true);
                return;
            }

            _provider = CreateProvider();
            _provider.Initialize(_config, success => {
                State = success ? ModuleState.Ready : ModuleState.Failed;
                if (success) {
                    SDKLogger.Info(Tag, "AppCheckManager initialized.");
                } else {
                    SDKLogger.Error(Tag, "AppCheckManager failed to initialize.");
                }
                onComplete?.Invoke(success);
            });
        }

        public void OnConsentChanged(ConsentStatus consent) { }

        public void Dispose() {
            _provider?.Dispose();
            _provider = null;
            Instance = null;
            State = ModuleState.Disposed;
        }

        public void GetToken(Action<string> onToken) {
            if (_provider == null || State != ModuleState.Ready) {
                onToken?.Invoke(null);
                return;
            }
            _provider.GetToken(onToken);
        }

        private IAppCheckProvider CreateProvider() {
            #if HAS_FIREBASE_APP_CHECK
            return new FirebaseAppCheckProvider();
            #else
            SDKLogger.Warning(Tag, "HAS_FIREBASE_APP_CHECK not defined. Using stub.");
            return new StubAppCheckProvider();
            #endif
        }
    }
}
