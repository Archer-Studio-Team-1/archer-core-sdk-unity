namespace ArcherStudio.SDK.Firestore {

    public enum FirestoreErrorCode {
        None,
        NotAuthenticated,
        AppCheckFailed,
        PermissionDenied,
        NetworkError,
        NotFound,
        InvalidArgument,
        QuotaExceeded,
        Unavailable,
        Cancelled,
        InternalError,
    }

    /// <summary>
    /// Immutable result envelope. Success → Data populated. Failure → ErrorCode + ErrorMessage.
    /// </summary>
    public readonly struct FirestoreResult<T> {

        public bool Success { get; }
        public T Data { get; }
        public FirestoreErrorCode ErrorCode { get; }
        public string ErrorMessage { get; }

        private FirestoreResult(bool success, T data, FirestoreErrorCode code, string message) {
            Success = success;
            Data = data;
            ErrorCode = code;
            ErrorMessage = message;
        }

        public static FirestoreResult<T> Succeeded(T data) =>
            new FirestoreResult<T>(true, data, FirestoreErrorCode.None, null);

        public static FirestoreResult<T> Failed(FirestoreErrorCode code, string message = null) =>
            new FirestoreResult<T>(false, default, code, message);
    }
}
