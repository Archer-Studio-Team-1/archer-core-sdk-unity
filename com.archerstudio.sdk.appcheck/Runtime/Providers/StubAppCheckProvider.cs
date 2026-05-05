using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.AppCheck {

    public class StubAppCheckProvider : IAppCheckProvider {
        private const string Tag = "AppCheck.Stub";

        public void Initialize(AppCheckConfig config, Action<bool> onComplete) {
            SDKLogger.Warning(Tag,
                "Firebase App Check SDK not installed. App Check disabled. " +
                "Install Firebase Unity SDK and define HAS_FIREBASE_APP_CHECK.");
            onComplete?.Invoke(true);
        }

        public void GetToken(Action<string> onToken) {
            onToken?.Invoke(null);
        }

        public void Dispose() { }
    }
}
