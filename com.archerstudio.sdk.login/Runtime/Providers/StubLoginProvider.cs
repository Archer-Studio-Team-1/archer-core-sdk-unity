using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Login {

    public class StubLoginProvider : ILoginProvider {
        public bool IsSignedIn { get; private set; } = false;
        public string PlayerId { get; private set; } = null;
        public string DisplayName { get; private set; } = null;

        public void AuthenticateAsync(Action<LoginResult> onComplete) {
            SDKLogger.Warning("Login-Stub", "Using stub provider — no real authentication backend.");
            onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotSignedIn));
        }

        public void SignOut() {
            IsSignedIn = false;
            PlayerId = null;
            DisplayName = null;
        }
    }
}
