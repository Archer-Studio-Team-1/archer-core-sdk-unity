using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Login {

    public readonly struct LoginSucceededEvent : ISDKEvent {
        public string PlayerId { get; }
        public string DisplayName { get; }

        public LoginSucceededEvent(string playerId, string displayName) {
            PlayerId = playerId;
            DisplayName = displayName;
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
