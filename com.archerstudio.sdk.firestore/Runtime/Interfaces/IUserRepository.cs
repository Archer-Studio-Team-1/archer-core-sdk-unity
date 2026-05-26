using System;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// High-level user data operations. Wraps callable Functions (createUserProfile,
    /// mutateResource, prepareUserBundle) and direct doc reads.
    /// </summary>
    public interface IUserRepository {

        /// <summary>Idempotent profile bootstrap. SDK calls on first launch after sign-in.</summary>
        void CreateUserProfileAsync(Action<FirestoreResult<bool>> onComplete);

        /// <summary>Fetch user + private state + all save features in a single RPC.</summary>
        void PrepareUserBundleAsync(Action<FirestoreResult<UserBundle>> onComplete);

        /// <summary>Mutate T0 currency via Cloud Function. Server-authoritative.</summary>
        void MutateResourceAsync(ResourceMutationRequest request,
                                 Action<FirestoreResult<ResourceMutationResponse>> onComplete);

        /// <summary>Subscribe to real-time currency changes from private/state doc.</summary>
        IDisposable ListenPrivateState(Action<PrivateStateSnapshot> onChange);
    }

    /// <summary>Snapshot of users/{uid}/private/state.</summary>
    public sealed class PrivateStateSnapshot {
        public System.Collections.Generic.IReadOnlyDictionary<string, string> Currencies { get; set; }
        public System.Collections.Generic.IReadOnlyDictionary<string, object> IapEntitlements { get; set; }
        public System.Collections.Generic.IReadOnlyDictionary<string, object> VipSubscription { get; set; }
    }
}
