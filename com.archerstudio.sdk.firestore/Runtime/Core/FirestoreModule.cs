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

            if (_pendingConfig == null) {
                SDKLogger.Warning(Tag, "OnLoginSucceeded: pending config missing — cannot link.");
                return;
            }

            switch (evt.ProviderType) {
                case LoginProviderType.GooglePlayGames:
                    var loginModule = LoginModule.Instance;
                    if (loginModule == null) {
                        SDKLogger.Warning(Tag, "OnLoginSucceeded: LoginModule.Instance missing — cannot link GPGS.");
                        return;
                    }
                    EnsureFirebaseAuth(loginModule, _pendingConfig, _ => { });
                    break;

                case LoginProviderType.Facebook:
                    SignInWithFacebookCredential(_pendingConfig, _ => { });
                    break;

                case LoginProviderType.GoogleAccount:
                    SignInWithGoogleCredential(_pendingConfig, _ => { });
                    break;

                case LoginProviderType.AppleSignIn:
                    SDKLogger.Warning(Tag, "AppleSignIn not yet wired (deferred). Cloud sync stays gated.");
                    break;

                default:
                    SDKLogger.Warning(Tag, $"Unknown provider {evt.ProviderType} — cloud sync stays gated.");
                    break;
            }
        }

        private void SignInWithFacebookCredential(FirestoreConfig config, Action<bool> onComplete) {
            var token = ReadStaticToken(
                "Facebook.Unity.AccessToken, Facebook.Unity",
                staticPropertyName: "CurrentAccessToken",
                tokenPropertyName: "TokenString");
            if (string.IsNullOrEmpty(token)) {
                SDKLogger.Warning(Tag,
                    "Facebook AccessToken.CurrentAccessToken not available — cloud sync stays gated. " +
                    "Provider must complete FB.LogInWithReadPermissions before publishing LoginSucceededEvent.");
                onComplete?.Invoke(false);
                return;
            }
            var credential = FirebaseAuthBootstrap.BuildFacebookCredential(token);
            if (credential == null) {
                SDKLogger.Warning(Tag, "FacebookAuthProvider absent — cloud sync stays gated.");
                onComplete?.Invoke(false);
                return;
            }
            Firebase.Auth.FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential)
                .ContinueWithOnMainThread(task => OnSignInComplete(task, config, LoginProviderType.Facebook, onComplete));
        }

        private void SignInWithGoogleCredential(FirestoreConfig config, Action<bool> onComplete) {
            // Google Sign-In Unity plugin exposes
            //   GoogleSignIn.DefaultInstance.CurrentUser as GoogleSignInUser
            // with .IdToken + .AuthCode. Read reflectively so this SDK does not
            // require the plugin to be present at compile time.
            var defaultInstance = ReadStaticValue("Google.GoogleSignIn, Google.SignIn", "DefaultInstance");
            if (defaultInstance == null) {
                SDKLogger.Warning(Tag,
                    "Google.SignIn package not installed — cloud sync stays gated for GoogleAccount.");
                onComplete?.Invoke(false);
                return;
            }
            var user = defaultInstance.GetType().GetProperty("CurrentUser")?.GetValue(defaultInstance);
            if (user == null) {
                SDKLogger.Warning(Tag,
                    "GoogleSignIn.CurrentUser is null — provider must complete SignIn() before publishing event.");
                onComplete?.Invoke(false);
                return;
            }
            var idToken = user.GetType().GetProperty("IdToken")?.GetValue(user) as string;
            var accessToken = user.GetType().GetProperty("AuthCode")?.GetValue(user) as string;
            if (string.IsNullOrEmpty(idToken)) {
                SDKLogger.Warning(Tag, "GoogleSignInUser.IdToken empty — cloud sync stays gated.");
                onComplete?.Invoke(false);
                return;
            }
            var credential = FirebaseAuthBootstrap.BuildGoogleCredential(idToken, accessToken);
            if (credential == null) {
                SDKLogger.Warning(Tag, "GoogleAuthProvider absent — cloud sync stays gated.");
                onComplete?.Invoke(false);
                return;
            }
            Firebase.Auth.FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential)
                .ContinueWithOnMainThread(task => OnSignInComplete(task, config, LoginProviderType.GoogleAccount, onComplete));
        }

        private static string ReadStaticToken(string typeAssemblyQualifiedName, string staticPropertyName, string tokenPropertyName) {
            var holder = ReadStaticValue(typeAssemblyQualifiedName, staticPropertyName);
            if (holder == null) return null;
            return holder.GetType().GetProperty(tokenPropertyName)?.GetValue(holder) as string;
        }

        private static object ReadStaticValue(string typeAssemblyQualifiedName, string staticMemberName) {
            var type = System.Type.GetType(typeAssemblyQualifiedName);
            if (type == null) return null;
            var prop = type.GetProperty(staticMemberName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null) return prop.GetValue(null);
            var field = type.GetField(staticMemberName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return field?.GetValue(null);
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
                    // Legacy anonymous user (no longer minted — guests now use device-keyed custom
                    // tokens, uid `guest_<deviceId>`). Sign out the orphan so it doesn't linger.
                    SDKLogger.Info(Tag,
                        $"Existing Firebase user is anonymous (UID={existingUser.UserId}). " +
                        "Signing out leftover guest — Phase 6 v2 requires a real provider for cloud-save.");
                    Firebase.Auth.FirebaseAuth.DefaultInstance.SignOut();
                } else if (existingUser.UserId != null && existingUser.UserId.StartsWith("guest_")) {
                    // Device-keyed guest (custom token). Authenticated but NOT a cloud-save provider:
                    // keep it signed in as the stable leaderboard identity, but do NOT set
                    // _linkedProvider so cloud-save stays gated (IsAuthenticatedWithProvider requires
                    // a real provider). The leaderboard reads this uid via the SDK Service directly.
                    SDKLogger.Info(Tag,
                        $"Guest custom-token user (UID={existingUser.UserId}). " +
                        "Keeping as leaderboard identity; cloud-save gated on a real provider.");
                    // fall through to the no-provider gated path below.
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
