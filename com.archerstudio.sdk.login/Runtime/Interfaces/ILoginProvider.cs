using System;

namespace ArcherStudio.SDK.Login {

    public interface ILoginProvider {
        bool IsSignedIn { get; }
        string PlayerId { get; }
        string DisplayName { get; }
        void AuthenticateAsync(Action<LoginResult> onComplete);
        void SignOut();
    }
}
