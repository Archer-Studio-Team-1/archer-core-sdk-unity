using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.CloudSave {

    public readonly struct CloudSaveSyncedEvent : ISDKEvent {
        public string SlotKey { get; }
        public bool HasConflict { get; }

        public CloudSaveSyncedEvent(string slotKey, bool hasConflict) {
            SlotKey = slotKey;
            HasConflict = hasConflict;
        }
    }

    public readonly struct CloudSaveFailedEvent : ISDKEvent {
        public string SlotKey { get; }
        public CloudSaveErrorCode ErrorCode { get; }

        public CloudSaveFailedEvent(string slotKey, CloudSaveErrorCode errorCode) {
            SlotKey = slotKey;
            ErrorCode = errorCode;
        }
    }
}
