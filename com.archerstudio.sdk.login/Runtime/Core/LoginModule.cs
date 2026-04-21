using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Login {

    public class LoginModule : ISDKModule {
        private const string Tag = "Login";

        public string ModuleId => "login";
        public int InitializationPriority => 40;
        public IReadOnlyList<string> Dependencies => new[] { "consent" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        public static LoginModule Instance { get; private set; }

        private ILoginProvider _provider;
        public ILoginProvider Provider => _provider;

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;

            if (!coreConfig.EnableLogin) {
                SDKLogger.Debug(Tag, "EnableLogin=false. Skipping.");
                _provider = new StubLoginProvider();
                State = ModuleState.Ready;
                Instance = this;
                onComplete?.Invoke(true);
                return;
            }

#if HAS_GPGS
            _provider = new GPGSLoginProvider();
#else
            _provider = new StubLoginProvider();
#endif
            SDKLogger.Info(Tag, $"Silent sign-in via {_provider.GetType().Name}...");

            try {
                _provider.AuthenticateAsync(result => {
                    if (State == ModuleState.Disposed) return;
                    try {
                        if (result.Success) {
                            SDKLogger.Info(Tag, $"Signed in. PlayerId={result.PlayerId}");
                            SDKEventBus.Publish(new LoginSucceededEvent(result.PlayerId, result.DisplayName));
                        } else {
                            SDKLogger.Info(Tag, $"Not signed in (code={result.ErrorCode}). Guest mode.");
                            SDKEventBus.Publish(new LoginFailedEvent(result.ErrorCode));
                        }
                    } catch (Exception cbEx) {
                        SDKLogger.Error(Tag, $"Login callback error: {cbEx.Message}");
                    }

                    // CRITICAL: always complete — never block SDK init chain
                    State = ModuleState.Ready;
                    Instance = this;
                    onComplete?.Invoke(true);
                });
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Login init exception: {e.Message}");
                State = ModuleState.Failed;
                onComplete?.Invoke(true); // CRITICAL: still complete — Instance remains null
            }
        }

        public void SignOut() {
            if (State != ModuleState.Ready) return;
            _provider.SignOut();
            SDKEventBus.Publish(new LoggedOutEvent());
        }

        public void ReAuthenticate(Action<LoginResult> onComplete = null) {
            if (State == ModuleState.Initializing) {
                SDKLogger.Warning(Tag, "ReAuthenticate called while initializing.");
                onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotInitialized));
                return;
            }
            if (State != ModuleState.Ready) {
                onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotInitialized));
                return;
            }
            try {
                _provider.AuthenticateAsync(result => {
                    try {
                        if (result.Success)
                            SDKEventBus.Publish(new LoginSucceededEvent(result.PlayerId, result.DisplayName));
                        else
                            SDKEventBus.Publish(new LoginFailedEvent(result.ErrorCode));
                    } catch (Exception cbEx) {
                        SDKLogger.Error(Tag, $"ReAuth callback error: {cbEx.Message}");
                    }
                    onComplete?.Invoke(result);
                });
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"ReAuth exception: {e.Message}");
                onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotInitialized));
            }
        }

        public void OnConsentChanged(ConsentStatus consent) { }

        public void Dispose() {
            Instance = null;
            State = ModuleState.Disposed;
        }
    }
}
