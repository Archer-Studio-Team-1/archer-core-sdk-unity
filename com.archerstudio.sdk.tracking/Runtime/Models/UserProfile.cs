using System;
using System.Collections.Generic;
using System.IO;
using ArcherStudio.SDK.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking {

    [Serializable]
    public class UserProfile {
        public Dictionary<string, double> TotalEarned = new Dictionary<string, double>();
        public Dictionary<string, double> TotalBought = new Dictionary<string, double>();
        public Dictionary<string, double> TotalSpent = new Dictionary<string, double>();

        public event Action<string, string> OnPropertyChanged;

        private void Notify(string key, string value) => OnPropertyChanged?.Invoke(key, value);

        // ─── Backing fields (v2) ───
        private string _adjustId = "";
        private string _deviceId = "";
        private string _firebaseStorageId = "";
        private string _currentTask = "";
        private string _currentStage = "";
        private int _progressStage;
        private int _exploreStage;
        private string _currentExploreTicket = "";
        private float _currentForgeShopLevel;
        private string _currentExploreBossLevel = "";
        private double _currentGem;
        private int _level;
        private int _daySinceInstall;
        private int _iapCount;
        private int _iaaCount;
        private string _storeName = "";
        private string _storeAppId = "";
        private long _installTimestamp;

        // ─── Properties with change notification (v2) ───

        public string AdjustId {
            get => _adjustId;
            set { if (_adjustId != value) { _adjustId = value; Notify(TrackingConstants.UP_ADJUST_ID, value); } }
        }

        public string DeviceId {
            get => _deviceId;
            set { if (_deviceId != value) { _deviceId = value; Notify(TrackingConstants.UP_DEVICE_ID, value); } }
        }

        public string FirebaseStorageId {
            get => _firebaseStorageId;
            set { if (_firebaseStorageId != value) { _firebaseStorageId = value; Notify(TrackingConstants.UP_FIREBASE_STORAGE_ID, value); } }
        }

        public string CurrentTask {
            get => _currentTask;
            set { if (_currentTask != value) { _currentTask = value; Notify(TrackingConstants.UP_CURRENT_TASK, value); } }
        }

        public string CurrentStage {
            get => _currentStage;
            set { if (_currentStage != value) { _currentStage = value; Notify(TrackingConstants.UP_CURRENT_STAGE, value); } }
        }

        public int ProgressStage {
            get => _progressStage;
            set { if (_progressStage != value) { _progressStage = value; Notify(TrackingConstants.UP_PROGRESS_STAGE, value.ToString()); } }
        }

        public int ExploreStage {
            get => _exploreStage;
            set { if (_exploreStage != value) { _exploreStage = value; Notify(TrackingConstants.UP_EXPLORE_STAGE, value.ToString()); } }
        }

        public string CurrentExploreTicket {
            get => _currentExploreTicket;
            set { if (_currentExploreTicket != value) { _currentExploreTicket = value; Notify(TrackingConstants.UP_CURRENT_EXPLORE_TICKET, value); } }
        }

        public float CurrentForgeShopLevel {
            get => _currentForgeShopLevel;
            set { if (Math.Abs(_currentForgeShopLevel - value) > 0.001f) { _currentForgeShopLevel = value; Notify(TrackingConstants.UP_CURRENT_FORGE_SHOP_LEVEL, value.ToString("F0")); } }
        }

        public string CurrentExploreBossLevel {
            get => _currentExploreBossLevel;
            set { if (_currentExploreBossLevel != value) { _currentExploreBossLevel = value; Notify(TrackingConstants.UP_CURRENT_EXPLORE_BOSS_LEVEL, value); } }
        }

        public double CurrentGem {
            get => _currentGem;
            set { if (Math.Abs(_currentGem - value) > 0.001f) { _currentGem = value; Notify(TrackingConstants.UP_CURRENT_GEM, value.ToString("F0")); } }
        }

        public int Level {
            get => _level;
            set { if (_level != value) { _level = value; Notify(TrackingConstants.UP_LEVEL, value.ToString()); } }
        }

        public int DaySinceInstall {
            get => _daySinceInstall;
            set { if (_daySinceInstall != value) { _daySinceInstall = value; Notify(TrackingConstants.UP_DAY_SINCE_INSTALL, value.ToString()); } }
        }

        public int IapCount {
            get => _iapCount;
            set { if (_iapCount != value) { _iapCount = value; Notify(TrackingConstants.UP_IAP_COUNT, value.ToString()); } }
        }

        public int IaaCount {
            get => _iaaCount;
            set { if (_iaaCount != value) { _iaaCount = value; Notify(TrackingConstants.UP_IAA_COUNT, value.ToString()); } }
        }

        public string StoreName {
            get => _storeName;
            set { if (_storeName != value) { _storeName = value; Notify(TrackingConstants.UP_STORE_NAME, value); } }
        }

        public string StoreAppId {
            get => _storeAppId;
            set { if (_storeAppId != value) { _storeAppId = value; Notify(TrackingConstants.UP_STORE_APP_ID, value); } }
        }

        public long InstallTimestamp {
            get => _installTimestamp;
            set { if (_installTimestamp != value) { _installTimestamp = value; Notify("install_timestamp", value.ToString()); } }
        }

        // ─── Resource Helpers ───

        public double AddEarned(string resourceId, double amount) {
            if (!TotalEarned.ContainsKey(resourceId)) TotalEarned[resourceId] = 0;
            TotalEarned[resourceId] += amount;
            return TotalEarned[resourceId];
        }

        public double AddBought(string resourceId, double amount) {
            if (!TotalBought.ContainsKey(resourceId)) TotalBought[resourceId] = 0;
            TotalBought[resourceId] += amount;
            return TotalBought[resourceId];
        }

        public double AddSpent(string resourceId, double amount) {
            if (!TotalSpent.ContainsKey(resourceId)) TotalSpent[resourceId] = 0;
            TotalSpent[resourceId] += amount;
            return TotalSpent[resourceId];
        }

        public double GetTotalEarned(string resourceId) =>
            TotalEarned.TryGetValue(resourceId, out var v) ? v : 0;

        public double GetTotalBought(string resourceId) =>
            TotalBought.TryGetValue(resourceId, out var v) ? v : 0;

        public double GetTotalSpent(string resourceId) =>
            TotalSpent.TryGetValue(resourceId, out var v) ? v : 0;

        // ─── Property Access ───

        public bool SetProperty(string key, string value) {
            switch (key) {
                case TrackingConstants.UP_ADJUST_ID: AdjustId = value; return true;
                case TrackingConstants.UP_DEVICE_ID: DeviceId = value; return true;
                case TrackingConstants.UP_FIREBASE_STORAGE_ID: FirebaseStorageId = value; return true;
                case TrackingConstants.UP_CURRENT_TASK: CurrentTask = value; return true;
                case TrackingConstants.UP_CURRENT_STAGE: CurrentStage = value; return true;
                case TrackingConstants.UP_PROGRESS_STAGE:
                    if (int.TryParse(value, out int progress)) { ProgressStage = progress; return true; } break;
                case TrackingConstants.UP_EXPLORE_STAGE:
                    if (int.TryParse(value, out int explore)) { ExploreStage = explore; return true; } break;
                case TrackingConstants.UP_CURRENT_EXPLORE_TICKET: CurrentExploreTicket = value; return true;
                case TrackingConstants.UP_CURRENT_FORGE_SHOP_LEVEL:
                    if (float.TryParse(value, out float forgeLevel)) { CurrentForgeShopLevel = forgeLevel; return true; } break;
                case TrackingConstants.UP_CURRENT_EXPLORE_BOSS_LEVEL: CurrentExploreBossLevel = value; return true;
                case TrackingConstants.UP_CURRENT_GEM:
                    if (float.TryParse(value, out float gem)) { CurrentGem = gem; return true; } break;
                case TrackingConstants.UP_LEVEL:
                    if (int.TryParse(value, out int level)) { Level = level; return true; } break;
                case TrackingConstants.UP_DAY_SINCE_INSTALL:
                    if (int.TryParse(value, out int days)) { DaySinceInstall = days; return true; } break;
                case TrackingConstants.UP_IAP_COUNT:
                    if (int.TryParse(value, out int iapC)) { IapCount = iapC; return true; } break;
                case TrackingConstants.UP_IAA_COUNT:
                    if (int.TryParse(value, out int iaaC)) { IaaCount = iaaC; return true; } break;
                case TrackingConstants.UP_STORE_NAME: StoreName = value; return true;
                case TrackingConstants.UP_STORE_APP_ID: StoreAppId = value; return true;
            }
            return false;
        }

        public Dictionary<string, string> GetAllProperties() {
            var dict = new Dictionary<string, string>();

            string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? "Null" : s;

            dict[TrackingConstants.UP_ADJUST_ID] = NullIfEmpty(AdjustId);
            dict[TrackingConstants.UP_DEVICE_ID] = NullIfEmpty(DeviceId);
            dict[TrackingConstants.UP_FIREBASE_STORAGE_ID] = NullIfEmpty(FirebaseStorageId);
            dict[TrackingConstants.UP_CURRENT_TASK] = NullIfEmpty(CurrentTask);
            dict[TrackingConstants.UP_CURRENT_STAGE] = NullIfEmpty(CurrentStage);
            dict[TrackingConstants.UP_PROGRESS_STAGE] = ProgressStage.ToString();
            dict[TrackingConstants.UP_EXPLORE_STAGE] = ExploreStage.ToString();
            dict[TrackingConstants.UP_CURRENT_EXPLORE_TICKET] = NullIfEmpty(CurrentExploreTicket);
            dict[TrackingConstants.UP_CURRENT_FORGE_SHOP_LEVEL] = CurrentForgeShopLevel > 0 ? CurrentForgeShopLevel.ToString("F0") : "0";
            dict[TrackingConstants.UP_CURRENT_EXPLORE_BOSS_LEVEL] = NullIfEmpty(CurrentExploreBossLevel);
            dict[TrackingConstants.UP_CURRENT_GEM] = CurrentGem.ToString("F0");
            dict[TrackingConstants.UP_LEVEL] = Level.ToString();
            dict[TrackingConstants.UP_DAY_SINCE_INSTALL] = DaySinceInstall.ToString();
            dict[TrackingConstants.UP_IAP_COUNT] = IapCount.ToString();
            dict[TrackingConstants.UP_IAA_COUNT] = IaaCount.ToString();
            if (!string.IsNullOrEmpty(StoreName)) dict[TrackingConstants.UP_STORE_NAME] = StoreName;
            if (!string.IsNullOrEmpty(StoreAppId)) dict[TrackingConstants.UP_STORE_APP_ID] = StoreAppId;

            return dict;
        }

        // ─── Persistence ───

        private const string FolderName = "Data";
        private const string FileName = "user_profile.dat";

        public static string PersistentDataPath { get; set; }

        public void Save() {
            try {
                string persistentPath = PersistentDataPath;
                if (string.IsNullOrEmpty(persistentPath)) {
                    if (UnityMainThreadDispatcher.IsMainThread()) {
                        persistentPath = Application.persistentDataPath;
                    } else {
                        return;
                    }
                }

                string folderPath = Path.Combine(persistentPath, FolderName);
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                string filePath = Path.Combine(folderPath, FileName);
                File.WriteAllText(filePath, json);
            } catch (Exception e) {
                SDKLogger.Error("UserProfile", $"Failed to save: {e.Message}");
            }
        }

        public static UserProfile Load() {
            try {
                string persistentPath = PersistentDataPath;
                if (string.IsNullOrEmpty(persistentPath)) {
                    if (UnityMainThreadDispatcher.IsMainThread()) {
                        persistentPath = Application.persistentDataPath;
                    } else {
                        return new UserProfile();
                    }
                }

                string filePath = Path.Combine(persistentPath, FolderName, FileName);
                if (!File.Exists(filePath)) return new UserProfile();

                string content = File.ReadAllText(filePath);
                var profile = JsonConvert.DeserializeObject<UserProfile>(content) ?? new UserProfile();
                profile.TotalEarned ??= new Dictionary<string, double>();
                profile.TotalBought ??= new Dictionary<string, double>();
                profile.TotalSpent ??= new Dictionary<string, double>();

                if (string.IsNullOrEmpty(profile.DeviceId)) {
                    profile.DeviceId = SystemInfo.deviceUniqueIdentifier;
                }

                return profile;
            } catch (Exception e) {
                SDKLogger.Error("UserProfile", $"Failed to load: {e.Message}");
                return new UserProfile();
            }
        }
    }
}
