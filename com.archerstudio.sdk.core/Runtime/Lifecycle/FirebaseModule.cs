using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Shared Firebase initialization module.
    /// Runs CheckAndFixDependenciesAsync once, before any module that depends on Firebase.
    /// Priority 10 = after consent (0), before tracking/crashreporting (20).
    /// </summary>
    public class FirebaseModule : ISDKModule {
        private const string Tag = "Firebase";

        public string ModuleId => "firebase";
        public int InitializationPriority => 10;
        public IReadOnlyList<string> Dependencies => new[] { "consent" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;

            SDKLogger.Info(Tag, "┌─── Firebase Module ───");
            SDKLogger.Info(Tag, "│ Initializing shared Firebase dependencies...");
            SDKLogger.Info(Tag, "└───────────────────────");

            FirebaseInitializer.EnsureInitialized(available => {
                if (available) {
                    State = ModuleState.Ready;
                    SDKLogger.Info(Tag, "Firebase Module ready — all providers can proceed.");
                } else {
                    State = ModuleState.Failed;
                    SDKLogger.Error(Tag, "Firebase Module failed — providers will use stubs.");
                }

                // Always complete — don't block SDK init even if Firebase fails
                onComplete?.Invoke(true);
            });
        }

        public void OnConsentChanged(ConsentStatus consent) {
            // Consent is handled by individual providers (Analytics, Crashlytics)
        }

        public void Dispose() {
            State = ModuleState.Disposed;
        }
    }
}
