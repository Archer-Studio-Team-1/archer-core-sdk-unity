using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.CloudSave {

    /// <summary>
    /// In-memory stub provider. Used when Firestore is not installed,
    /// user is not signed in, or EnableCloudSave=false.
    /// Data persists only for the lifetime of the session.
    /// </summary>
    public class StubCloudSaveProvider : ICloudSaveProvider {
        private const string Tag = "CloudSave-Stub";
        private readonly Dictionary<string, string> _store = new Dictionary<string, string>();

        public void InitAsync(Action<bool> onComplete) {
            SDKLogger.Debug(Tag, "Stub provider — no real cloud backend.");
            onComplete?.Invoke(true);
        }

        public void SaveAsync(string slotKey, string jsonData, Action<CloudSaveResult> onComplete) {
            _store[slotKey] = jsonData;
            onComplete?.Invoke(CloudSaveResult.LocalOnly(jsonData));
        }

        public void LoadAsync(string slotKey, Action<CloudSaveResult> onComplete) {
            if (_store.TryGetValue(slotKey, out var data)) {
                onComplete?.Invoke(CloudSaveResult.LocalOnly(data));
            } else {
                onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.NotFound));
            }
        }

        public void DeleteAsync(string slotKey, Action<CloudSaveResult> onComplete) {
            _store.Remove(slotKey);
            onComplete?.Invoke(CloudSaveResult.Succeeded(null, DateTime.MinValue));
        }
    }
}
