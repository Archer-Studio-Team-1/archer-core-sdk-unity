using System;

namespace ArcherStudio.SDK.CloudSave {

    public interface ICloudSaveProvider {
        /// <summary>
        /// Authenticate and connect to the cloud backend.
        /// Called by CloudSaveModule after acquiring a server-side auth code from LoginModule.
        /// </summary>
        void InitAsync(Action<bool> onComplete);

        /// <summary>
        /// Write jsonData to the given slot. Validates size &lt; 900KB before writing.
        /// </summary>
        void SaveAsync(string slotKey, string jsonData, Action<CloudSaveResult> onComplete);

        /// <summary>
        /// Read slot from cloud. Returns WithConflict when cloud is newer but local is dirty.
        /// </summary>
        void LoadAsync(string slotKey, Action<CloudSaveResult> onComplete);

        /// <summary>
        /// Delete a slot from cloud and clear its local metadata.
        /// </summary>
        void DeleteAsync(string slotKey, Action<CloudSaveResult> onComplete);
    }
}
