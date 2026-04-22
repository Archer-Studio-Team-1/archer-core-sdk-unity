using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Login {

    public class StubLoginProvider : ILoginProvider {
        private const string Tag = "Login-Stub";

        public bool IsSignedIn { get; private set; }
        public string PlayerId { get; private set; }
        public string DisplayName { get; private set; }

        public void AuthenticateAsync(Action<LoginResult> onComplete) {
            SDKLogger.Warning(Tag, "Using stub provider — no real authentication backend.");
            onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotSignedIn));
        }

        public void ManuallyAuthenticate(Action<LoginResult> onComplete) {
            SDKLogger.Warning(Tag, "ManuallyAuthenticate called on stub provider — no UI flow available.");
            onComplete?.Invoke(LoginResult.Failed(LoginErrorCode.NotInstalled));
        }

        public void SignOut() {
            IsSignedIn = false;
            PlayerId = null;
            DisplayName = null;
        }

        public void GetServerSideAccessCode(string webClientId, Action<string> onComplete) {
            onComplete?.Invoke(null);
        }
    }
}
