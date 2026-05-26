using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using ArcherStudio.SDK.Login;
#if HAS_FIREBASE_FIRESTORE && HAS_FIREBASE_AUTH
using Firebase.Extensions;
#endif
using UnityEngine;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Lifecycle owner for Firestore + Functions integration.
    /// Initializes after Login is ready (depends on Firebase Auth state set by Login/CloudSave),
    /// then exposes IFirestoreService + IUserRepository + IIapCatalogService.
    /// </summary>
    public sealed class FirestoreModule : ISDKModule {

        private const string Tag = "Firestore";

        public string ModuleId => "firestore";
        public int InitializationPriority => 60;   // After Login (40) + CloudSave (50)
        public IReadOnlyList<string> Dependencies => new[] { "login" };
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        public static FirestoreModule Instance { get; private set; }

        public IFirestoreService Service { get; private set; }
        public IUserRepository UserRepository { get; private set; }
        public ISaveRepository SaveRepository { get; private set; }
        public IBackupUploader BackupUploader { get; private set; }
        public IIapCatalogService IapCatalog { get; private set; }
        public FeatureRegistry Features { get; private set; }

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;
            Instance = this;

            var config = Resources.Load<FirestoreConfig>("FirestoreConfig");
            if (config == null) {
                SDKLogger.Debug(Tag, "FirestoreConfig not found in Resources/. Using stub provider.");
                UseStub(config);
                CompleteInit(onComplete, success: true);
                return;
            }

#if HAS_FIREBASE_FIRESTORE && HAS_FIREBASE_AUTH
            // Firebase Auth is independent of GPGS Login. Even if Login hasn't
            // signed in (Editor, no Play Services, or user not tapped social
            // sign-in), we can still establish an anonymous Firebase Auth session
            // so Firestore reads/writes work end-to-end. When Login completes
            // later, we link the anonymous account to the social credential.
            var loginModule = LoginModule.Instance;
            EnsureFirebaseAuth(loginModule, config, onComplete);

            // Also subscribe to LoginSucceededEvent so an in-session sign-in
            // upgrades the anonymous account to a social-linked one.
            SubscribeToLogin(config, _ => { });
#else
            SDKLogger.Warning(Tag, "Firebase.Firestore/Auth not present. Using stub.");
            UseStub(config);
            CompleteInit(onComplete, success: true);
#endif
        }

#if HAS_FIREBASE_FIRESTORE && HAS_FIREBASE_AUTH
        private bool _waitingForLogin;
        private FirestoreConfig _pendingConfig;

        private void SubscribeToLogin(FirestoreConfig config, Action<bool> onComplete) {
            // Init returns ready=true immediately so the SDK boot chain isn't blocked.
            // Stub providers are already wired. When LoginSucceededEvent fires we
            // upgrade to real Firebase Auth + ProvisionProviders.
            _pendingConfig = config;
            if (!_waitingForLogin) {
                _waitingForLogin = true;
                ArcherStudio.SDK.Core.SDKEventBus.Subscribe<ArcherStudio.SDK.Login.LoginSucceededEvent>(OnLoginSucceeded);
                SDKLogger.Info(Tag, "Subscribed to LoginSucceededEvent — will upgrade to Firebase Auth when login completes.");
            }
            CompleteInit(onComplete, success: true);
        }

        private void OnLoginSucceeded(ArcherStudio.SDK.Login.LoginSucceededEvent evt) {
            SDKLogger.Info(Tag, $"LoginSucceededEvent received (playerId={evt.PlayerId}). Upgrading from stub to real provider.");
            var loginModule = LoginModule.Instance;
            if (loginModule == null || _pendingConfig == null) {
                SDKLogger.Warning(Tag, "OnLoginSucceeded: LoginModule or pending config missing — staying on stub.");
                return;
            }
            // Re-run the auth handshake. EnsureFirebaseAuth handles the "already
            // signed in" fast path which is the common case when CloudSave's
            // sign-in completed before this event arrived.
            EnsureFirebaseAuth(loginModule, _pendingConfig, _ => { });
        }

        private void EnsureFirebaseAuth(LoginModule loginModule, FirestoreConfig config, Action<bool> onComplete) {
            // CloudSave may have already signed into Firebase Auth. Check first.
            var existingUid = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (!string.IsNullOrEmpty(existingUid)) {
                SDKLogger.Info(Tag, $"Firebase Auth already established. UID={existingUid}");
                ProvisionProviders(config);
                CompleteInit(onComplete, success: true);
                return;
            }

            // No GPGS Login available (Editor without Play Services, user not yet
            // signed in via social). Go straight to anonymous Firebase Auth so
            // Firestore works immediately. LoginSucceededEvent handler will
            // upgrade to a linked credential when GPGS signs in later.
            if (loginModule == null || loginModule.Provider == null || !loginModule.Provider.IsSignedIn) {
                SDKLogger.Info(Tag, "Login not signed in — signing into Firebase Auth anonymously.");
                Firebase.Auth.FirebaseAuth.DefaultInstance.SignInAnonymouslyAsync()
                    .ContinueWithOnMainThread(task => OnSignInComplete(task, config, onComplete));
                return;
            }

            // Bootstrap Firebase Auth via GPGS server auth code (same flow as CloudSave).
            // If GPGS can't produce a code (Editor, no Play Services, user declined,
            // signed-in but offline), fall back to anonymous sign-in so cloud sync still
            // works. Anonymous accounts can be upgraded later via LinkWithCredential.
            loginModule.Provider.GetServerSideAccessCode(config.WebClientId, serverAuthCode => {
                if (string.IsNullOrEmpty(serverAuthCode)) {
                    SDKLogger.Warning(Tag, "No GPGS server auth code — signing into Firebase Auth anonymously.");
                    Firebase.Auth.FirebaseAuth.DefaultInstance.SignInAnonymouslyAsync()
                        .ContinueWithOnMainThread(task => OnSignInComplete(task, config, onComplete));
                    return;
                }

                // Try Play Games credential first; if package absent, fall back to anonymous.
                // Different Firebase Unity SDK versions return different result types
                // (AuthResult in v11+, FirebaseUser in earlier). Handle both via dynamic.
                var credential = FirebaseAuthBootstrap.BuildPlayGamesCredential(serverAuthCode);
                if (credential != null) {
                    Firebase.Auth.FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential)
                        .ContinueWithOnMainThread(task => OnSignInComplete(task, config, onComplete));
                } else {
                    Firebase.Auth.FirebaseAuth.DefaultInstance.SignInAnonymouslyAsync()
                        .ContinueWithOnMainThread(task => OnSignInComplete(task, config, onComplete));
                }
            });
        }

        private void OnSignInComplete(System.Threading.Tasks.Task task,
                                      FirestoreConfig config, Action<bool> onComplete) {
            if (task.IsFaulted || task.IsCanceled) {
                SDKLogger.Error(Tag, $"Firebase Auth sign-in failed: {task.Exception?.Message}");
                UseStub(config);
                CompleteInit(onComplete, success: true);
                return;
            }
            // Result may be FirebaseUser (older SDK) or AuthResult (newer). Extract UID via reflection.
            var resultObj = task.GetType().GetProperty("Result")?.GetValue(task);
            string uid = null;
            if (resultObj is Firebase.Auth.FirebaseUser user) {
                uid = user.UserId;
            } else if (resultObj != null) {
                // AuthResult.User.UserId
                var userProp = resultObj.GetType().GetProperty("User");
                var userVal = userProp?.GetValue(resultObj) as Firebase.Auth.FirebaseUser;
                uid = userVal?.UserId;
            }
            uid = uid ?? Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            SDKLogger.Info(Tag, $"Firebase Auth ready. UID={uid}");
            ProvisionProviders(config);
            CompleteInit(onComplete, success: true);
        }

        private void ProvisionProviders(FirestoreConfig config) {
            var functionsInstance = FirebaseFunctionsBridge.GetInstance(config.FunctionsRegion);
            Service = new FirestoreServiceProvider(config, functionsInstance);
            UserRepository = new UserRepository(Service);
            SaveRepository = new SaveRepository(Service);
            BackupUploader = new BackupUploader(Service);
            IapCatalog = new IapCatalogService(Service, config.IapCatalogCacheTtlMs);
            Features = new FeatureRegistry(Service, config.FeatureRegistryCacheTtlMs);
        }
#endif

        private void UseStub(FirestoreConfig config) {
            Service = new StubFirestoreServiceProvider();
            UserRepository = new UserRepository(Service);
            SaveRepository = new SaveRepository(Service);
            BackupUploader = new BackupUploader(Service);
            IapCatalog = new IapCatalogService(Service, config?.IapCatalogCacheTtlMs ?? 300_000);
            Features = new FeatureRegistry(Service, config?.FeatureRegistryCacheTtlMs ?? 3_600_000);
        }

        private void CompleteInit(Action<bool> onComplete, bool success) {
            State = success ? ModuleState.Ready : ModuleState.Failed;
            onComplete?.Invoke(true);
        }

        public void OnConsentChanged(ConsentStatus consent) {
            // Firestore + Auth không bị gate bởi consent (functional data, không phải analytics/ads).
            // No-op để satisfy ISDKModule contract.
        }

        public void Dispose() {
            State = ModuleState.Disposed;
            Service = null;
            UserRepository = null;
            IapCatalog = null;
            Features = null;
        }
    }
}
