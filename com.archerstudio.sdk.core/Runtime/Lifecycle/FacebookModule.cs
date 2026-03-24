using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Facebook SDK initialization module.
    /// Calls FB.Init(), applies pending consent, and activates the app.
    /// Priority 30 = after consent (0), firebase (10), tracking (20).
    ///
    /// Consent flow:
    ///   1. ConsentManager.BroadcastConsent() → FB not init → stores pending consent
    ///   2. FacebookModule.InitializeAsync() → FB.Init() → applies pending consent
    /// </summary>
    public class FacebookModule : ISDKModule {
        private const string Tag = "Facebook";

        public string ModuleId => "facebook_sdk";
        public int InitializationPriority => 30;
        public IReadOnlyList<string> Dependencies => new[] { "consent" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        /// <summary>
        /// Static callback for applying pending consent after FB.Init().
        /// Set by ConsentManager (or any module that manages FB consent).
        /// </summary>
        public static Action OnFacebookInitialized;

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;

#if HAS_FACEBOOK_SDK
            SDKLogger.Info(Tag, "Initializing Facebook SDK...");

            try {
                if (Facebook.Unity.FB.IsInitialized) {
                    SDKLogger.Info(Tag, "Facebook SDK already initialized.");
                    NotifyInitialized();
                    State = ModuleState.Ready;
                    onComplete?.Invoke(true);
                    return;
                }

                Facebook.Unity.FB.Init(() => {
                    SDKLogger.Info(Tag, $"Facebook SDK initialized. AppId={Facebook.Unity.FB.AppId}");

                    NotifyInitialized();

                    Facebook.Unity.FB.ActivateApp();
                    SDKLogger.Info(Tag, "Facebook ActivateApp called.");

                    State = ModuleState.Ready;
                    onComplete?.Invoke(true);
                });
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Facebook SDK init failed: {e.Message}");
                State = ModuleState.Failed;
                onComplete?.Invoke(true); // Don't block SDK
            }
#else
            SDKLogger.Debug(Tag, "HAS_FACEBOOK_SDK not defined. Skipping.");
            State = ModuleState.Ready;
            onComplete?.Invoke(true);
#endif
        }

        private static void NotifyInitialized() {
            try {
                OnFacebookInitialized?.Invoke();
            } catch (Exception e) {
                SDKLogger.Warning(Tag, $"OnFacebookInitialized callback error: {e.Message}");
            }
        }

        public void OnConsentChanged(ConsentStatus consent) { }

        public void Dispose() {
            State = ModuleState.Disposed;
        }
    }
}
