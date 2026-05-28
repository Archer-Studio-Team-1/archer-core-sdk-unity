#if HAS_FIREBASE_FIRESTORE && HAS_FIREBASE_AUTH
using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using Firebase.Auth;
using Firebase.Extensions;
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

        public FirestoreServiceProvider(FirestoreConfig config) {
            _config = config;
            _db = FirebaseFirestore.DefaultInstance;
            _auth = FirebaseAuth.DefaultInstance;
            // Online-only (Phase 6 v2): explicitly drive persistence from config.
            // Default config.EnableOfflinePersistence == false so the local cache is
            // off — every read/write hits the server, keeping state authoritative.
            // Older Firebase SDK exposes Settings as read-only, with mutable properties.
            _db.Settings.PersistenceEnabled = config.EnableOfflinePersistence;
        }

        // Phase 6 v2: anonymous Firebase users no longer count as available. Cloud
        // writes only engage once a real auth provider has linked (GPGS / Google /
        // Facebook / Apple) — guests stay local-only.
        public bool IsAvailable => _auth?.CurrentUser != null && !_auth.CurrentUser.IsAnonymous;
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
            // Cloud Functions invoked via plain HTTPS — no Firebase.Functions Unity SDK
            // dependency. See CallableHttpClient for wire protocol details.
            CallableHttpClient.CallAsync(_config, name, payload, onComplete);
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
