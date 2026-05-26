using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// No-op provider used when Firebase SDK is absent, user not signed in, or running in
    /// CI/Editor without backend. Returns NotAuthenticated for every call so callers can
    /// branch deterministically.
    /// </summary>
    internal sealed class StubFirestoreServiceProvider : IFirestoreService {

        private const string Tag = "Firestore";

        public bool IsAvailable => false;
        public string CurrentFirebaseUid => null;

        public void GetDocumentAsync(string path,
                                     Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete) {
            SDKLogger.Debug(Tag, $"[Stub] GetDocumentAsync {path}");
            onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                FirestoreErrorCode.NotAuthenticated, "stub provider"));
        }

        public void SetDocumentAsync(string path, IReadOnlyDictionary<string, object> data,
                                     Action<FirestoreResult<bool>> onComplete) {
            SDKLogger.Debug(Tag, $"[Stub] SetDocumentAsync {path}");
            onComplete?.Invoke(FirestoreResult<bool>.Failed(
                FirestoreErrorCode.NotAuthenticated, "stub provider"));
        }

        public void CallFunctionAsync(string name, IReadOnlyDictionary<string, object> payload,
                                      Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete) {
            SDKLogger.Debug(Tag, $"[Stub] CallFunctionAsync {name}");
            onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                FirestoreErrorCode.NotAuthenticated, "stub provider"));
        }

        public IDisposable Listen(string path,
                                  Action<IReadOnlyDictionary<string, object>> onSnapshot) {
            SDKLogger.Debug(Tag, $"[Stub] Listen {path}");
            return new NoopDisposable();
        }

        private sealed class NoopDisposable : IDisposable {
            public void Dispose() { }
        }
    }
}
