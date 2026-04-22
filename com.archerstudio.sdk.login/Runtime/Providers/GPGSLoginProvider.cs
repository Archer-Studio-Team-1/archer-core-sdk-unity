using System;
using ArcherStudio.SDK.Core;
using UnityEngine;

#if HAS_GPGS && (UNITY_ANDROID || UNITY_EDITOR)
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace ArcherStudio.SDK.Login {

    /// <summary>
    /// GPGS v2+ provider (plugin v11+). API cũ (PlayGamesClientConfiguration,
    /// InitializeInstance, Activate, 3-step init) đã bị loại bỏ — xem
    /// https://github.com/playgameservices/play-games-plugin-for-unity/blob/master/UPGRADING.txt
    ///
    /// Silent sign-in giờ gọi trực tiếp PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication).
    /// Callback nhận SignInStatus enum (BasicApi).
    ///
    /// Android OAuth 2.0 Client ID cấu hình qua Unity menu:
    ///   Window > Google Play Games > Setup > Android Setup
    /// (không phải runtime config). LoginConfig.AndroidClientId chỉ cần khi gọi
    /// RequestServerSideAccess cho server-side auth.
    ///
    /// SignOut native không còn trong v2+ — user quản lý sign-out qua cài đặt
    /// thiết bị Google Play. SignOut() ở đây chỉ reset local state.
    /// </summary>
    public class GPGSLoginProvider : ILoginProvider {
        private const string Tag = "Login-GPGS";

        
        public bool IsSignedIn { get; private set; }
        public string PlayerId { get; private set; }
        public string DisplayName { get; private set; }

        public void AuthenticateAsync(Action<LoginResult> onComplete) {
#if HAS_GPGS && (UNITY_ANDROID || UNITY_EDITOR)
            var loginConfig = Resources.Load<LoginConfig>("LoginConfig");
            if (loginConfig == null) {
                SDKLogger.Warning(Tag,
                    "LoginConfig asset not found in Resources/. " +
                    "Silent sign-in vẫn tiếp tục, nhưng server-side access code sẽ không khả dụng. " +
                    "Tạo asset qua menu ArcherStudio/SDK/Login Config.");
            } else if (string.IsNullOrEmpty(loginConfig.AndroidClientId)) {
                SDKLogger.Warning(Tag,
                    "LoginConfig.AndroidClientId chưa set — RequestServerSideAccess sẽ fail. " +
                    "Silent sign-in vẫn tiếp tục nếu Web Client ID đã config qua " +
                    "Window > Google Play Games > Setup > Android Setup.");
            }

            try {
                PlayGamesPlatform.Instance.Authenticate(status => {
                    if (status == SignInStatus.Success) {
                        IsSignedIn = true;
                        PlayerId = PlayGamesPlatform.Instance.GetUserId();
                        DisplayName = Social.localUser.userName;
                        SDKLogger.Info(Tag, $"Signed in. PlayerId={PlayerId}");
                        onComplete?.Invoke(LoginResult.Succeeded(PlayerId, DisplayName));
                    } else {
                        IsSignedIn = false;
                        SDKLogger.Info(Tag, $"Silent sign-in failed (status={status}) — guest mode.");
                        onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotSignedIn));
                    }
                });
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"GPGS Authenticate exception: {e.Message}");
                onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotInitialized));
            }
#else
            SDKLogger.Debug(Tag, "HAS_GPGS not defined or not Android/Editor. Stub fallback.");
            onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotInstalled));
#endif
        }

        public void ManuallyAuthenticate(Action<LoginResult> onComplete) {
#if HAS_GPGS && (UNITY_ANDROID || UNITY_EDITOR)
            try {
                PlayGamesPlatform.Instance.ManuallyAuthenticate(status => {
                    if (status == SignInStatus.Success) {
                        IsSignedIn = true;
                        PlayerId = PlayGamesPlatform.Instance.GetUserId();
                        DisplayName = Social.localUser.userName;
                        SDKLogger.Info(Tag, $"Manual sign-in success. PlayerId={PlayerId}");
                        onComplete?.Invoke(LoginResult.Succeeded(PlayerId, DisplayName));
                    } else {
                        IsSignedIn = false;
                        SDKLogger.Info(Tag, $"Manual sign-in failed (status={status}).");
                        onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotSignedIn));
                    }
                });
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"GPGS ManuallyAuthenticate exception: {e.Message}");
                onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotInitialized));
            }
#else
            SDKLogger.Debug(Tag, "HAS_GPGS not defined or not Android/Editor. Stub fallback.");
            onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotInstalled));
#endif
        }

        public void SignOut() {
            // GPGS v2+ không cung cấp API SignOut. Reset local state để module
            // không giữ thông tin user cũ. User sign-out thật qua cài đặt Google Play.
            IsSignedIn = false;
            PlayerId = null;
            DisplayName = null;
        }

        public void GetServerSideAccessCode(string webClientId, Action<string> onComplete) {
#if HAS_GPGS && (UNITY_ANDROID || UNITY_EDITOR)
            if (!IsSignedIn) {
                SDKLogger.Warning(Tag, "GetServerSideAccessCode called but user not signed in.");
                onComplete?.Invoke(null);
                return;
            }
            if (string.IsNullOrEmpty(webClientId)) {
                SDKLogger.Warning(Tag, "GetServerSideAccessCode: webClientId is empty.");
                onComplete?.Invoke(null);
                return;
            }
            try {
                PlayGamesPlatform.Instance.RequestServerSideAccess(
                    code => {
                        if (string.IsNullOrEmpty(code)) {
                            SDKLogger.Warning(Tag, "RequestServerSideAccess returned empty code.");
                        } else {
                            SDKLogger.Info(Tag, "Server-side access code obtained.");
                        }
                        onComplete?.Invoke(code);
                    });
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"RequestServerSideAccess exception: {e.Message}");
                onComplete?.Invoke(null);
            }
#else
            SDKLogger.Debug(Tag, "HAS_GPGS not defined or not Android/Editor. Stub fallback.");
            onComplete?.Invoke(null);
#endif
        }
    }
}
