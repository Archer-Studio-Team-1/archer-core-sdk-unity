using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Login {

    public readonly struct LoginSucceededEvent : ISDKEvent {
        public string PlayerId { get; }
        public string DisplayName { get; }

        /// <summary>
        /// Which auth backend produced this login. Subscribers (Firestore, cloud
        /// sync) use this to decide whether to engage real Firebase Auth linking
        /// or treat the session as guest-only.
        /// </summary>
        public LoginProviderType ProviderType { get; }

        public LoginSucceededEvent(string playerId, string displayName, LoginProviderType providerType) {
            PlayerId = playerId;
            DisplayName = displayName;
            ProviderType = providerType;
        }
    }

    public readonly struct LoginFailedEvent : ISDKEvent {
        public LoginErrorCode ErrorCode { get; }

        public LoginFailedEvent(LoginErrorCode errorCode) {
            ErrorCode = errorCode;
        }
    }

    public readonly struct LoggedOutEvent : ISDKEvent { }
}
