using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Push {

    /// <summary>
    /// Stub push provider used when HAS_FIREBASE_MESSAGING is not defined.
    /// All operations log via SDKLogger and succeed with dummy values.
    /// </summary>
    public class StubPushProvider : IPushProvider {
        private const string Tag = "Push-Stub";

        public bool IsInitialized { get; private set; }

        public event Action<PushMessage> OnMessageReceived;
        public event Action<string> OnTokenRefreshed;

        public void Initialize(Action<bool> onComplete) {
            SDKLogger.Info(Tag, "Stub provider initialized.");
            IsInitialized = true;
            onComplete?.Invoke(true);
        }

        public void RequestPermission(Action<bool> onComplete) {
            SDKLogger.Info(Tag, "RequestPermission called (stub: always granted).");
            onComplete?.Invoke(true);
        }

        public void GetToken(Action<string> onComplete) {
            SDKLogger.Info(Tag, "GetToken called (stub: returning stub_token).");
            onComplete?.Invoke("stub_token");
        }

        public void SubscribeToTopic(string topic) {
            SDKLogger.Info(Tag, $"SubscribeToTopic called: {topic} (stub: no-op).");
        }

        public void UnsubscribeFromTopic(string topic) {
            SDKLogger.Info(Tag, $"UnsubscribeFromTopic called: {topic} (stub: no-op).");
        }
    }
}
