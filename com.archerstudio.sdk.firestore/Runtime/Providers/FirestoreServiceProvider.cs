#if HAS_FIREBASE_FIRESTORE && HAS_FIREBASE_AUTH
using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Real provider backed by Firebase.Firestore + Firebase.Auth.
    /// Assumes FirebaseAuth.DefaultInstance.CurrentUser is set (handled by FirebaseAuthBootstrap
    /// or by sibling CloudSave module which already signs in via Play Games credential).
    /// </summary>
    internal sealed class FirestoreServiceProvider : IFirestoreService {

        private const string Tag = "Firestore";

        private readonly FirebaseFirestore _db;
        private readonly FirebaseAuth _auth;
        private readonly FirestoreConfig _config;
        // Optional functions client — populated only if Firebase.Functions package is present.
        private readonly object _functions;

        public FirestoreServiceProvider(FirestoreConfig config, object functionsInstance) {
            _config = config;
            _db = FirebaseFirestore.DefaultInstance;
            _auth = FirebaseAuth.DefaultInstance;
            _functions = functionsInstance;
            if (config.EnableOfflinePersistence) {
                _db.Settings = new FirebaseFirestoreSettings {
                    Host = _db.Settings.Host,
                    PersistenceEnabled = true,
                    SslEnabled = true,
                };
            }
        }

        public bool IsAvailable => _auth?.CurrentUser != null;
        public string CurrentFirebaseUid => _auth?.CurrentUser?.UserId;

        public void GetDocumentAsync(string path,
                                     Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete) {
            var resolved = ResolvePath(path);
            if (resolved == null) {
                onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                    FirestoreErrorCode.NotAuthenticated, "no firebase uid"));
                return;
            }
            if (_config.VerboseLogging) SDKLogger.Info(Tag, $"GetDocumentAsync {resolved}");
            _db.Document(resolved).GetSnapshotAsync().ContinueWithOnMainThread(task => {
                if (task.IsFaulted) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        MapException(task.Exception), task.Exception?.Message));
                    return;
                }
                var snap = task.Result;
                if (!snap.Exists) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        FirestoreErrorCode.NotFound));
                    return;
                }
                onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Succeeded(
                    snap.ToDictionary()));
            });
        }

        public void SetDocumentAsync(string path, IReadOnlyDictionary<string, object> data,
                                     Action<FirestoreResult<bool>> onComplete) {
            var resolved = ResolvePath(path);
            if (resolved == null) {
                onComplete?.Invoke(FirestoreResult<bool>.Failed(
                    FirestoreErrorCode.NotAuthenticated, "no firebase uid"));
                return;
            }
            if (_config.VerboseLogging) SDKLogger.Info(Tag, $"SetDocumentAsync {resolved}");
            _db.Document(resolved).SetAsync((IDictionary<string, object>)data, SetOptions.MergeAll)
                .ContinueWithOnMainThread(task => {
                    if (task.IsFaulted) {
                        onComplete?.Invoke(FirestoreResult<bool>.Failed(
                            MapException(task.Exception), task.Exception?.Message));
                        return;
                    }
                    onComplete?.Invoke(FirestoreResult<bool>.Succeeded(true));
                });
        }

        public void CallFunctionAsync(string name, IReadOnlyDictionary<string, object> payload,
                                      Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete) {
            // Functions support is optional. If Firebase.Functions package is not installed,
            // the integration falls back to NotAvailable so the SDK degrades gracefully.
            if (_functions == null) {
                onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                    FirestoreErrorCode.Unavailable, "Firebase.Functions package not installed"));
                return;
            }
            FirebaseFunctionsBridge.CallAsync(_functions, name, _config.FunctionsRegion, payload, onComplete);
        }

        public IDisposable Listen(string path,
                                  Action<IReadOnlyDictionary<string, object>> onSnapshot) {
            var resolved = ResolvePath(path);
            if (resolved == null) return new NoopDisposable();
            var registration = _db.Document(resolved).Listen(snap => {
                if (snap.Exists) onSnapshot?.Invoke(snap.ToDictionary());
            });
            return new ListenerHandle(registration);
        }

        private string ResolvePath(string path) {
            if (string.IsNullOrEmpty(path)) return null;
            if (!path.Contains("{uid}")) return path;
            var uid = CurrentFirebaseUid;
            return string.IsNullOrEmpty(uid) ? null : path.Replace("{uid}", uid);
        }

        private static FirestoreErrorCode MapException(AggregateException ex) {
            if (ex == null) return FirestoreErrorCode.InternalError;
            foreach (var inner in ex.InnerExceptions) {
                if (inner is FirestoreException fe) {
                    return fe.ErrorCode switch {
                        FirestoreError.Cancelled => FirestoreErrorCode.Cancelled,
                        FirestoreError.NotFound => FirestoreErrorCode.NotFound,
                        FirestoreError.PermissionDenied => FirestoreErrorCode.PermissionDenied,
                        FirestoreError.InvalidArgument => FirestoreErrorCode.InvalidArgument,
                        FirestoreError.Unavailable => FirestoreErrorCode.Unavailable,
                        FirestoreError.ResourceExhausted => FirestoreErrorCode.QuotaExceeded,
                        FirestoreError.Unauthenticated => FirestoreErrorCode.NotAuthenticated,
                        _ => FirestoreErrorCode.InternalError,
                    };
                }
            }
            return FirestoreErrorCode.InternalError;
        }

        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }

        private sealed class ListenerHandle : IDisposable {
            private ListenerRegistration _reg;
            public ListenerHandle(ListenerRegistration reg) { _reg = reg; }
            public void Dispose() {
                _reg?.Stop();
                _reg = null;
            }
        }
    }
}
#endif
