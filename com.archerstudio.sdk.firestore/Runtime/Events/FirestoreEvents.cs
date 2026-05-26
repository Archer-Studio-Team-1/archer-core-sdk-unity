namespace ArcherStudio.SDK.Firestore {

    public readonly struct FirestoreReadyEvent {
        public string FirebaseUid { get; }
        public FirestoreReadyEvent(string uid) { FirebaseUid = uid; }
    }

    public readonly struct FirestoreFailedEvent {
        public FirestoreErrorCode Code { get; }
        public string Message { get; }
        public FirestoreFailedEvent(FirestoreErrorCode code, string message) { Code = code; Message = message; }
    }

    public readonly struct CurrencyChangedEvent {
        public System.Collections.Generic.IReadOnlyDictionary<string, string> NewBalance { get; }
        public CurrencyChangedEvent(System.Collections.Generic.IReadOnlyDictionary<string, string> balance) {
            NewBalance = balance;
        }
    }
}
