using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Low-level Firestore + Cloud Functions facade. Auth-gated: every call requires the
    /// caller to be signed into Firebase Auth (via Login + LinkWithCredential or anonymous).
    /// All callbacks invoked on Unity main thread.
    /// </summary>
    public interface IFirestoreService {

        bool IsAvailable { get; }
        string CurrentFirebaseUid { get; }

        /// <summary>
        /// Read a document by path. Path may contain "{uid}" placeholder which is substituted
        /// with CurrentFirebaseUid.
        /// </summary>
        void GetDocumentAsync(string path, Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete);

        /// <summary>
        /// Write (set with merge) a document by path. Use only for T1/T2 features.
        /// </summary>
        void SetDocumentAsync(string path, IReadOnlyDictionary<string, object> data,
                              Action<FirestoreResult<bool>> onComplete);

        /// <summary>
        /// Invoke a Cloud Function in the configured region. Payload + response are
        /// arbitrary JSON-serialisable maps.
        /// </summary>
        void CallFunctionAsync(string name, IReadOnlyDictionary<string, object> payload,
                               Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete);

        /// <summary>
        /// Subscribe to changes on a doc. Returns an IDisposable that detaches the listener.
        /// </summary>
        IDisposable Listen(string path, Action<IReadOnlyDictionary<string, object>> onSnapshot);
    }
}
