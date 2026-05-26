using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Firestore {

    public sealed class UserRepository : IUserRepository {

        private const string Tag = "Firestore";
        private readonly IFirestoreService _service;

        public UserRepository(IFirestoreService service) {
            _service = service;
        }

        public void CreateUserProfileAsync(Action<FirestoreResult<bool>> onComplete) {
            _service.CallFunctionAsync("createUserProfile", null, r => {
                if (!r.Success) {
                    onComplete?.Invoke(FirestoreResult<bool>.Failed(r.ErrorCode, r.ErrorMessage));
                    return;
                }
                onComplete?.Invoke(FirestoreResult<bool>.Succeeded(true));
            });
        }

        public void PrepareUserBundleAsync(Action<FirestoreResult<UserBundle>> onComplete) {
            _service.CallFunctionAsync("prepareUserBundle", null, r => {
                if (!r.Success) {
                    onComplete?.Invoke(FirestoreResult<UserBundle>.Failed(r.ErrorCode, r.ErrorMessage));
                    return;
                }
                var bundle = new UserBundle {
                    Exists = r.Data.TryGet<bool>("exists"),
                    ServerTs = r.Data.TryGet<long>("serverTs"),
                    User = r.Data.TryGet<IReadOnlyDictionary<string, object>>("user"),
                    Private = r.Data.TryGet<IReadOnlyDictionary<string, object>>("private"),
                    Saves = ParseSaves(r.Data),
                };
                onComplete?.Invoke(FirestoreResult<UserBundle>.Succeeded(bundle));
            });
        }

        public void MutateResourceAsync(ResourceMutationRequest request,
                                        Action<FirestoreResult<ResourceMutationResponse>> onComplete) {
            if (request?.Deltas == null || request.Deltas.Count == 0) {
                onComplete?.Invoke(FirestoreResult<ResourceMutationResponse>.Failed(
                    FirestoreErrorCode.InvalidArgument, "deltas required"));
                return;
            }
            var payload = new Dictionary<string, object> {
                { "deltas", request.Deltas },
                { "reason", request.Reason ?? "unspecified" },
                { "type", request.Type ?? "gameplay_earn" },
            };
            if (!string.IsNullOrEmpty(request.ClientTxnId)) payload["clientTxnId"] = request.ClientTxnId;

            _service.CallFunctionAsync("mutateResource", payload, r => {
                if (!r.Success) {
                    onComplete?.Invoke(FirestoreResult<ResourceMutationResponse>.Failed(r.ErrorCode, r.ErrorMessage));
                    return;
                }
                var resp = new ResourceMutationResponse {
                    NewBalance = r.Data.TryGet<IReadOnlyDictionary<string, string>>("newBalance"),
                    TxnId = r.Data.TryGet<string>("txnId"),
                    Duplicate = r.Data.TryGet<bool>("duplicate"),
                };
                onComplete?.Invoke(FirestoreResult<ResourceMutationResponse>.Succeeded(resp));
            });
        }

        public IDisposable ListenPrivateState(Action<PrivateStateSnapshot> onChange) {
            return _service.Listen("users/{uid}/private/state", data => {
                onChange?.Invoke(new PrivateStateSnapshot {
                    Currencies = data.TryGet<IReadOnlyDictionary<string, string>>("currencies"),
                    IapEntitlements = data.TryGet<IReadOnlyDictionary<string, object>>("iapEntitlements"),
                    VipSubscription = data.TryGet<IReadOnlyDictionary<string, object>>("vipSubscription"),
                });
            });
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> ParseSaves(
            IReadOnlyDictionary<string, object> bundle) {
            var raw = bundle.TryGet<IReadOnlyDictionary<string, object>>("saves");
            if (raw == null) return new Dictionary<string, IReadOnlyDictionary<string, object>>();
            var result = new Dictionary<string, IReadOnlyDictionary<string, object>>(raw.Count);
            foreach (var kv in raw) {
                if (kv.Value is IReadOnlyDictionary<string, object> dict) result[kv.Key] = dict;
                else result[kv.Key] = null;
            }
            return result;
        }
    }
}
