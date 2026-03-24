using System;

namespace ArcherStudio.SDK.Push {

    /// <summary>
    /// Abstraction over push notification backends (Firebase, stub, etc.).
    /// </summary>
    public interface IPushProvider {
        bool IsInitialized { get; }
        void Initialize(Action<bool> onComplete);
        void RequestPermission(Action<bool> onComplete);
        void GetToken(Action<string> onComplete);
        void SubscribeToTopic(string topic);
        void UnsubscribeFromTopic(string topic);
        event Action<PushMessage> OnMessageReceived;
        event Action<string> OnTokenRefreshed;
    }
}
