using System;

namespace ArcherStudio.SDK.CloudSave {

    public enum CloudSaveErrorCode {
        None,
        NotAuthenticated,
        NetworkError,
        DataTooLarge,
        NotFound,
        ProviderError
    }

    public readonly struct CloudSaveResult {
        public bool Success { get; }
        public string Data { get; }
        public string LocalData { get; }
        public bool HasConflict { get; }
        public DateTime ServerTimestamp { get; }
        public CloudSaveErrorCode ErrorCode { get; }

        private CloudSaveResult(
            bool success,
            string data,
            string localData,
            bool hasConflict,
            DateTime serverTimestamp,
            CloudSaveErrorCode errorCode) {
            Success = success;
            Data = data;
            LocalData = localData;
            HasConflict = hasConflict;
            ServerTimestamp = serverTimestamp;
            ErrorCode = errorCode;
        }

        public static CloudSaveResult Succeeded(string data, DateTime serverTimestamp)
            => new CloudSaveResult(true, data, null, false, serverTimestamp, CloudSaveErrorCode.None);

        public static CloudSaveResult Failed(CloudSaveErrorCode code)
            => new CloudSaveResult(false, null, null, false, DateTime.MinValue, code);

        /// <summary>
        /// Cloud is newer AND local has unsaved changes — game must choose which to keep.
        /// Data = cloud version, LocalData = local dirty version.
        /// </summary>
        public static CloudSaveResult WithConflict(string cloudData, string localData, DateTime serverTimestamp)
            => new CloudSaveResult(true, cloudData, localData, true, serverTimestamp, CloudSaveErrorCode.None);

        /// <summary>
        /// Returned when offline, stub provider, or cloud timestamp == local timestamp.
        /// Data contains local cached data.
        /// </summary>
        public static CloudSaveResult LocalOnly(string localData)
            => new CloudSaveResult(true, localData, null, false, DateTime.MinValue, CloudSaveErrorCode.None);
    }
}
