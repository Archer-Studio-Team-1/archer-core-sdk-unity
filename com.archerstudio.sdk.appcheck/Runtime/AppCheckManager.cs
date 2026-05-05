using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.AppCheck {

    /// <summary>
    /// App Check module lifecycle:
    ///
    /// PRODUCTION build:
    ///   → Play Integrity (Android) / DeviceCheck (iOS)
    ///   → Real attestation tokens sent to server
    ///
    /// Dev build (non-PRODUCTION) + UseDebugProviderInDev=true:
    ///   → Firebase Debug Provider (register debug token in Firebase Console)
    ///   → Allows testing App Check flow on device without real attestation
    ///
    /// Dev build + UseDebugProviderInDev=false, OR Editor:
    ///   → Stub provider (returns null token)
    ///   → IAP works normally, server skips App Check verification (soft mode)
    /// </summary>
    public class AppCheckManager : ISDKModule {
        private const string Tag = "AppCheck";

        public string ModuleId => "appcheck";
        public int InitializationPriority => 10;
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        public static AppCheckManager Instance { get; private set; }

        private IAppCheckProvider _provider;

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;
            Instance = this;

            var security = coreConfig.GetActiveSecurityConfig();
            if (!security.EnableAppCheck) {
                SDKLogger.Info(Tag, "App Check disabled for this environment.");
                InitStub(onComplete);
                return;
            }

            var config = Resources.Load<AppCheckConfig>("AppCheckConfig");
            if (config == null || !config.Enabled) {
                SDKLogger.Info(Tag, config == null
                    ? "AppCheckConfig not found. App Check inactive."
                    : "AppCheckConfig.Enabled=false. App Check inactive.");
                InitStub(onComplete);
                return;
            }

            _provider = CreateProvider(config);
            _provider.Initialize(config, success => {
                State = success ? ModuleState.Ready : ModuleState.Failed;
                SDKLogger.Info(Tag, success
                    ? "AppCheckManager initialized."
                    : "AppCheckManager failed. IAP will work without App Check token.");
                onComplete?.Invoke(true);
            });
        }

        public void OnConsentChanged(ConsentStatus consent) { }

        public void Dispose() {
            _provider?.Dispose();
            _provider = null;
            Instance = null;
            State = ModuleState.Disposed;
        }

        private void InitStub(Action<bool> onComplete) {
            _provider = new StubAppCheckProvider();
            _provider.Initialize(null, _ => {
                State = ModuleState.Ready;
                onComplete?.Invoke(true);
            });
        }

        public void GetToken(Action<string> onToken) {
            if (_provider == null || State != ModuleState.Ready) {
                onToken?.Invoke(null);
                return;
            }
            _provider.GetToken(onToken);
        }

        private static IAppCheckProvider CreateProvider(AppCheckConfig config) {
            #if PRODUCTION && HAS_FIREBASE_APP_CHECK
            SDKLogger.Info(Tag, "PRODUCTION build. Using real attestation provider.");
            return new FirebaseAppCheckProvider(useDebugProvider: false);
            #elif HAS_FIREBASE_APP_CHECK
            if (config.UseDebugProviderInDev) {
                SDKLogger.Warning(Tag, "Dev build with UseDebugProviderInDev=true. Using Debug provider.");
                return new FirebaseAppCheckProvider(useDebugProvider: true);
            }
            SDKLogger.Info(Tag, "Dev build, debug provider disabled. Using stub.");
            return new StubAppCheckProvider();
            #else
            SDKLogger.Warning(Tag, "HAS_FIREBASE_APP_CHECK not defined. Using stub.");
            return new StubAppCheckProvider();
            #endif
        }
    }
}
