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

        /// <summary>
        /// True only when a real auth provider (GPGS / Google / Facebook / Apple)
        /// has linked into Firebase Auth — i.e. cloud-save is eligible. Phase 6 v2
        /// drops the anonymous fallback, so this is the canonical gate downstream
        /// (LiveSync, restore service) checks before any Firestore write.
        /// </summary>
        public bool IsAuthenticatedWithProvider =>
            Service != null
            && Service.IsAvailable
            && _linkedProvider != LoginProviderType.None;

        private LoginProviderType _linkedProvider = LoginProviderType.None;

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
            // Phase 6 v2: no anonymous fallback. If Login already has a real
            // provider signed in, EnsureFirebaseAuth links it now; otherwise
            // cloud sync stays gated until LoginSucceededEvent fires.
            var loginModule = LoginModule.Instance;
            EnsureFirebaseAuth(loginModule, config, onComplete);

            // Persist the config + subscribe so in-session logins can drive
            // the credential link without rerunning the whole init path.
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
                ArcherStudio.SDK.Core.SDKEventBus.Subscribe<ArcherStudio.SDK.Login.LoggedOutEvent>(OnLoggedOut);
                SDKLogger.Info(Tag, "Subscribed to LoginSucceededEvent/LoggedOutEvent — Firebase Auth linking is gated on these.");
            }
            CompleteInit(onComplete, success: true);
        }

        private void OnLoginSucceeded(ArcherStudio.SDK.Login.LoginSucceededEvent evt) {
            SDKLogger.Info(Tag,
                $"LoginSucceededEvent received (playerId={evt.PlayerId}, provider={evt.ProviderType}). " +
                "Linking into Firebase Auth.");

            if (evt.ProviderType == LoginProviderType.None) {
                SDKLogger.Warning(Tag, "OnLoginSucceeded with ProviderType=None — ignoring (stub provider).");
                return;
            }

            var loginModule = LoginModule.Instance;
            if (loginModule == null || _pendingConfig == null) {
                SDKLogger.Warning(Tag, "OnLoginSucceeded: LoginModule or pending config missing — cannot link.");
                return;
            }

            // Phase B slice 2 will add GoogleAccount / Facebook credential paths.
            // For now only GooglePlayGames is wired end-to-end.
            if (evt.ProviderType != LoginProviderType.GooglePlayGames) {
                SDKLogger.Warning(Tag,
                    $"Provider {evt.ProviderType} is not yet supported by FirestoreModule. " +
                    "Cloud sync stays gated until this credential path is implemented.");
                return;
            }

            EnsureFirebaseAuth(loginModule, _pendingConfig, _ => { });
        }

        private void OnLoggedOut(ArcherStudio.SDK.Login.LoggedOutEvent _) {
            SDKLogger.Info(Tag, "LoggedOutEvent received — clearing Firebase Auth session and gating cloud sync.");
            _linkedProvider = LoginProviderType.None;
            try {
                Firebase.Auth.FirebaseAuth.DefaultInstance.SignOut();
            } catch (Exception e) {
                SDKLogger.Warning(Tag, $"FirebaseAuth.SignOut threw: {e.Message}");
            }
        }

        private void EnsureFirebaseAuth(LoginModule loginModule, FirestoreConfig config, Action<bool> onComplete) {
            // Firebase.App must finish dependency check before Auth calls succeed.
            // "An internal error has occurred" typically means SignIn ran before
            // FirebaseApp was ready. We gate every sign-in attempt on the check.
            Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(depTask => {
                if (depTask.IsFaulted) {
                    SDKLogger.Error(Tag, $"Firebase dependency check faulted: {depTask.Exception?.Message}");
                    UseStub(config);
                    CompleteInit(onComplete, success: true);
                    return;
                }
                if (depTask.Result != Firebase.DependencyStatus.Available) {
                    SDKLogger.Error(Tag, $"Firebase dependencies unavailable: {depTask.Result}");
                    UseStub(config);
                    CompleteInit(onComplete, success: true);
                    return;
                }
                ContinueEnsureFirebaseAuth(loginModule, config, onComplete);
            });
        }

        private void ContinueEnsureFirebaseAuth(LoginModule loginModule, FirestoreConfig config, Action<bool> onComplete) {
            // Phase 6 v2: no anonymous fallback. Cloud sync stays gated until a
            // real provider (GPGS / Google / Facebook / Apple) is linked into
            // Firebase Auth. The module still completes init so the rest of the
            // SDK boot chain proceeds — IsAuthenticatedWithProvider is the gate.

            var existingUser = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
            if (existingUser != null) {
                if (existingUser.IsAnonymous) {
                    // Leftover from a previous build that used anonymous fallback.
                    // Sign out so we don't write to a guest UID that no one can
                    // reach again. The user must log in to engage cloud save.
                    SDKLogger.Info(Tag,
                        $"Existing Firebase user is anonymous (UID={existingUser.UserId}). " +
                        "Signing out — Phase 6 v2 requires a real provider.");
                    Firebase.Auth.FirebaseAuth.DefaultInstance.SignOut();
                } else {
                    SDKLogger.Info(Tag, $"Firebase Auth already linked. UID={existingUser.UserId}");
                    ProvisionProviders(config);
                    // We don't know which provider linked previously without
                    // inspecting ProviderData; assume GPGS for now (only path wired).
                    _linkedProvider = LoginProviderType.GooglePlayGames;
                    CompleteInit(onComplete, success: true);
                    return;
                }
            }

            if (loginModule == null || loginModule.Provider == null || !loginModule.Provider.IsSignedIn) {
                SDKLogger.Info(Tag,
                    "No provider signed in. Cloud sync gated — waiting for LoginSucceededEvent.");
                ProvisionProviders(config);
                CompleteInit(onComplete, success: true);
                return;
            }

            var providerType = loginModule.Provider.ProviderType;
            if (providerType != LoginProviderType.GooglePlayGames) {
                SDKLogger.Warning(Tag,
                    $"Provider {providerType} is not yet wired into FirestoreModule. " +
                    "Cloud sync gated until its credential path lands (Phase B slice 2).");
                ProvisionProviders(config);
                CompleteInit(onComplete, success: true);
                return;
            }

            loginModule.Provider.GetServerSideAccessCode(config.WebClientId, serverAuthCode => {
                if (string.IsNullOrEmpty(serverAuthCode)) {
                    SDKLogger.Warning(Tag,
                        "No GPGS server auth code — cloud sync gated until next login attempt.");
                    ProvisionProviders(config);
                    CompleteInit(onComplete, success: true);
                    return;
                }

                var credential = FirebaseAuthBootstrap.BuildPlayGamesCredential(serverAuthCode);
                if (credential == null) {
                    SDKLogger.Warning(Tag,
                        "PlayGamesAuthProvider absent — cannot build credential. Cloud sync gated.");
                    ProvisionProviders(config);
                    CompleteInit(onComplete, success: true);
                    return;
                }

                Firebase.Auth.FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential)
                    .ContinueWithOnMainThread(task => OnSignInComplete(task, config, providerType, onComplete));
            });
        }

        private void OnSignInComplete(System.Threading.Tasks.Task task,
                                      FirestoreConfig config,
                                      LoginProviderType providerType,
                                      Action<bool> onComplete) {
            if (task.IsFaulted || task.IsCanceled) {
                SDKLogger.Error(Tag, $"Firebase Auth sign-in failed: {task.Exception?.Message}");
                // Phase 6 v2: do NOT fall back to stub on sign-in failure. Keep
                // the real providers wired so a retry (next LoginSucceededEvent)
                // can complete the link. Cloud sync stays gated meanwhile.
                ProvisionProviders(config);
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
            SDKLogger.Info(Tag, $"Firebase Auth ready. UID={uid}, provider={providerType}");
            _linkedProvider = providerType;
            ProvisionProviders(config);
            CompleteInit(onComplete, success: true);
        }

        private void ProvisionProviders(FirestoreConfig config) {
            Service = new FirestoreServiceProvider(config);
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
#if HAS_FIREBASE_FIRESTORE && HAS_FIREBASE_AUTH
            _linkedProvider = LoginProviderType.None;
            _waitingForLogin = false;
            _pendingConfig = null;
#endif
        }
    }
}
