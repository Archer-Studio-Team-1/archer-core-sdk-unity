using System;

namespace ArcherStudio.SDK.DeepLink {

    /// <summary>
    /// Interface for deep link providers (Unity built-in, Firebase Dynamic Links, Adjust).
    /// Each provider listens for deep links from its source and raises OnDeepLinkReceived.
    /// </summary>
    public interface IDeepLinkProvider {
        bool IsInitialized { get; }
        void Initialize(Action<bool> onComplete);
        event Action<DeepLinkData> OnDeepLinkReceived;
    }
}
