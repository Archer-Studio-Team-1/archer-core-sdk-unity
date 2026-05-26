using System.Collections.Generic;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Response of prepareUserBundle callable. Contains user root + private state + all saves.
    /// </summary>
    public sealed class UserBundle {
        public bool Exists { get; set; }
        public IReadOnlyDictionary<string, object> User { get; set; }
        public IReadOnlyDictionary<string, object> Private { get; set; }
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> Saves { get; set; }
        public long ServerTs { get; set; }
    }

    /// <summary>
    /// Request for mutateResource callable.
    /// </summary>
    public sealed class ResourceMutationRequest {
        public IReadOnlyDictionary<string, string> Deltas { get; set; }   // {"gem": "100"} or {"gold": "-50"}
        public string Reason { get; set; }
        public string Type { get; set; }                                   // "gameplay_earn"|"gameplay_spend"|"quest_reward"|"daily_login"|"ad_reward"
        public string ClientTxnId { get; set; }                            // Optional idempotency key
    }

    /// <summary>
    /// Response of mutateResource callable.
    /// </summary>
    public sealed class ResourceMutationResponse {
        public IReadOnlyDictionary<string, string> NewBalance { get; set; }
        public string TxnId { get; set; }
        public bool Duplicate { get; set; }
    }
}
