using System;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Login {

#if HAS_GPGS
    using GooglePlayGames;
    using GooglePlayGames.BasicApi;
#endif

    public class GPGSLoginProvider : ILoginProvider {
        private const string Tag = "Login-GPGS";

#if HAS_GPGS
        private static bool _gpgsInitialized = false;
#endif

        public bool IsSignedIn { get; private set; } = false;
        public string PlayerId { get; private set; } = null;
        public string DisplayName { get; private set; } = null;

        public void AuthenticateAsync(Action<LoginResult> onComplete) {
#if HAS_GPGS
            var loginConfig = Resources.Load<LoginConfig>("LoginConfig");
            if (loginConfig == null) {
                SDKLogger.Error(Tag,
                    "LoginConfig asset not found in Resources/. " +
                    "Create it via ArcherStudio/SDK/Login Config menu.");
                onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.ConfigError));
                return;
            }
            if (string.IsNullOrEmpty(loginConfig.AndroidClientId)) {
                SDKLogger.Error(Tag,
                    "LoginConfig.AndroidClientId is not set. " +
                    "Set it in Resources/LoginConfig.asset from " +
                    "Google Play Console > Games > Setup > Linked apps.");
                onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.ConfigError));
                return;
            }

            // GPGS v10+ 3-step init — order is mandatory, guard against re-init:
            // Step 1: InitializeInstance (must precede Activate)
            // Step 2: Activate         (must precede Authenticate)
            // Step 3: Authenticate
            if (!_gpgsInitialized) {
                var config = new PlayGamesClientConfiguration.Builder().Build();
                PlayGamesPlatform.InitializeInstance(config);
                PlayGamesPlatform.Activate();
                _gpgsInitialized = true;
            }

            Social.Active.Authenticate(success => {
                if (success) {
                    IsSignedIn = true;
                    PlayerId = PlayGamesPlatform.Instance.GetUserId();
                    DisplayName = Social.localUser.userName;
                    SDKLogger.Info(Tag, $"Signed in. PlayerId={PlayerId}");
                    onComplete?.Invoke(LoginResult.Succeeded(PlayerId, DisplayName));
                } else {
                    IsSignedIn = false;
                    SDKLogger.Info(Tag, "Silent sign-in failed — guest mode.");
                    onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotSignedIn));
                }
            });
#else
            SDKLogger.Debug(Tag, "HAS_GPGS not defined. Stub fallback.");
            onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotInstalled));
#endif
        }

        public void SignOut() {
#if HAS_GPGS
            PlayGamesPlatform.Instance.SignOut();
#endif
            IsSignedIn = false;
            PlayerId = null;
            DisplayName = null;
        }
    }
}
