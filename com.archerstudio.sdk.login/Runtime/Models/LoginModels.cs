namespace ArcherStudio.SDK.Login {

    public enum LoginErrorCode {
        None,
        NotInstalled,
        NotSignedIn,
        ConfigError,
        NotInitialized
    }

    public readonly struct LoginResult {
        public bool Success { get; }
        public string PlayerId { get; }
        public string DisplayName { get; }
        public LoginErrorCode ErrorCode { get; }

        private LoginResult(bool success, string playerId, string displayName, LoginErrorCode errorCode) {
            Success = success;
            PlayerId = playerId;
            DisplayName = displayName;
            ErrorCode = errorCode;
        }

        public static LoginResult Succeeded(string playerId, string displayName)
            => new LoginResult(true, playerId, displayName, LoginErrorCode.None);

        public static LoginResult Failed(LoginErrorCode code)
            => new LoginResult(false, null, null, code);
    }
}
