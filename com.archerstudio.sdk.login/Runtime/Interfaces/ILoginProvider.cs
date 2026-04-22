using System;

namespace ArcherStudio.SDK.Login {

    public interface ILoginProvider {
        bool IsSignedIn { get; }
        string PlayerId { get; }
        string DisplayName { get; }

        /// <summary>
        /// Silent sign-in attempt. Không hiển thị UI, thất bại lặng lẽ nếu user
        /// chưa từng sign-in hoặc không muốn auto sign-in.
        /// </summary>
        void AuthenticateAsync(Action<LoginResult> onComplete);

        /// <summary>
        /// Sign-in với UI prompt — gọi khi user click nút đăng nhập. Với GPGS
        /// v2+ map sang PlayGamesPlatform.Instance.ManuallyAuthenticate(...).
        /// </summary>
        void ManuallyAuthenticate(Action<LoginResult> onComplete);

        /// <summary>
        /// Reset state local. GPGS v2+ không có native sign-out API nên chỉ
        /// xóa cache in-memory; user sign-out thật qua cài đặt Google Play.
        /// </summary>
        void SignOut();

        /// <summary>
        /// Requests a server-side access code (OAuth authorization code) from GPGS.
        /// CloudSave module uses this code to sign into Firebase Auth via
        /// PlayGamesAuthProvider.GetCredential(serverAuthCode).
        /// Returns null if not signed in or provider doesn't support it.
        /// </summary>
        void GetServerSideAccessCode(string webClientId, Action<string> onComplete);
    }
}
