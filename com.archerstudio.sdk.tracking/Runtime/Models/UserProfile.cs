using System;
using System.Collections.Generic;
using System.IO;
using ArcherStudio.SDK.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking {

    [Serializable]
    public class UserProfile {
        public Dictionary<string, ulong> TotalEarned = new Dictionary<string, ulong>();
        public Dictionary<string, ulong> TotalBought = new Dictionary<string, ulong>();
        public Dictionary<string, ulong> TotalSpent = new Dictionary<string, ulong>();

        public event Action<string, string> OnPropertyChanged;

        private void Notify(string key, string value) => OnPropertyChanged?.Invoke(key, value);

        // ─── Backing fields ───
        private string _userId = "";
        private string _adId = "";
        private string _currentStage = "";
        private int _progressStage;
        private int _exploreStage;
        private int _currentLevel;
        private int _daySinceInstall;
        private bool _isIapUser;
        private int _iapCount;
        private bool _isIaaUser;
        private int _iaaCount;
        private int _activeDayN;
        private ulong _remainingGem;
        private string _uaNetwork = "";
        private string _uaCampaign = "";
        private string _uaAdGroup = "";
        private string _uaCreative = "";
        private string _storeName = "";
        private string _storeAppId = "";
        private long _installTimestamp;

        // ─── Properties with change notification ───

        public string UserId {
            get => _userId;
            set { if (_userId != value) { _userId = value; Notify(TrackingConstants.UP_USER_ID, value); } }
        }

        public string AdId {
            get => _adId;
            set { if (_adId != value) { _adId = value; Notify(TrackingConstants.UP_AD_ID, value); } }
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

        public int CurrentLevel {
            get => _currentLevel;
            set {
                if (_currentLevel != value) {
                    _currentLevel = value;
                    string s = value.ToString();
                    Notify(TrackingConstants.UP_CURRENT_LEVEL, s);
                    Notify(TrackingConstants.UP_LEVEL, s);
                }
            }
        }

        public int DaySinceInstall {
            get => _daySinceInstall;
            set { if (_daySinceInstall != value) { _daySinceInstall = value; Notify(TrackingConstants.UP_DAY_SINCE_INSTALL, value.ToString()); } }
        }

        public bool IsIapUser {
            get => _isIapUser;
            set { if (_isIapUser != value) { _isIapUser = value; Notify(TrackingConstants.UP_IS_IAP_USER, value ? "1" : "0"); } }
        }

        public int IapCount {
            get => _iapCount;
            set { if (_iapCount != value) { _iapCount = value; Notify(TrackingConstants.UP_IAP_COUNT, value.ToString()); } }
        }

        public bool IsIaaUser {
            get => _isIaaUser;
            set { if (_isIaaUser != value) { _isIaaUser = value; Notify(TrackingConstants.UP_IS_IAA_USER, value ? "1" : "0"); } }
        }

        public int IaaCount {
            get => _iaaCount;
            set { if (_iaaCount != value) { _iaaCount = value; Notify(TrackingConstants.UP_IAA_COUNT, value.ToString()); } }
        }

        public int ActiveDayN {
            get => _activeDayN;
            set { if (_activeDayN != value) { _activeDayN = value; Notify(TrackingConstants.UP_ACTIVE_DAY_N, value.ToString()); } }
        }

        public ulong RemainingGem {
            get => _remainingGem;
            set { if (_remainingGem != value) { _remainingGem = value; Notify(TrackingConstants.UP_REMAINING_GEM, value.ToString()); } }
        }

        public string UaNetwork {
            get => _uaNetwork;
            set { if (_uaNetwork != value) { _uaNetwork = value; Notify(TrackingConstants.UP_UA_NETWORK, value); } }
        }

        public string UaCampaign {
            get => _uaCampaign;
            set { if (_uaCampaign != value) { _uaCampaign = value; Notify(TrackingConstants.UP_UA_CAMPAIGN, value); } }
        }

        public string UaAdGroup {
            get => _uaAdGroup;
            set { if (_uaAdGroup != value) { _uaAdGroup = value; Notify(TrackingConstants.UP_UA_ADGROUP, value); } }
        }

        public string UaCreative {
            get => _uaCreative;
            set { if (_uaCreative != value) { _uaCreative = value; Notify(TrackingConstants.UP_UA_CREATIVE, value); } }
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

        public ulong AddEarned(string resourceId, ulong amount) {
            if (!TotalEarned.ContainsKey(resourceId)) TotalEarned[resourceId] = 0;
            TotalEarned[resourceId] += amount;
            return TotalEarned[resourceId];
        }

        public ulong AddBought(string resourceId, ulong amount) {
            if (!TotalBought.ContainsKey(resourceId)) TotalBought[resourceId] = 0;
            TotalBought[resourceId] += amount;
            return TotalBought[resourceId];
        }

        public ulong AddSpent(string resourceId, ulong amount) {
            if (!TotalSpent.ContainsKey(resourceId)) TotalSpent[resourceId] = 0;
            TotalSpent[resourceId] += amount;
            return TotalSpent[resourceId];
        }

        public ulong GetTotalEarned(string resourceId) =>
            TotalEarned.TryGetValue(resourceId, out var v) ? v : 0;

        public ulong GetTotalBought(string resourceId) =>
            TotalBought.TryGetValue(resourceId, out var v) ? v : 0;

        public ulong GetTotalSpent(string resourceId) =>
            TotalSpent.TryGetValue(resourceId, out var v) ? v : 0;

        // ─── Property Access ───

        public bool SetProperty(string key, string value) {
            switch (key) {
                case TrackingConstants.UP_USER_ID: UserId = value; return true;
                case TrackingConstants.UP_AD_ID: AdId = value; return true;
                case TrackingConstants.UP_CURRENT_STAGE: CurrentStage = value; return true;
                case TrackingConstants.UP_PROGRESS_STAGE:
                    if (int.TryParse(value, out int progress)) { ProgressStage = progress; return true; } break;
                case TrackingConstants.UP_EXPLORE_STAGE:
                    if (int.TryParse(value, out int explore)) { ExploreStage = explore; return true; } break;
                case TrackingConstants.UP_CURRENT_LEVEL:
                case TrackingConstants.UP_LEVEL:
                    if (int.TryParse(value, out int level)) { CurrentLevel = level; return true; } break;
                case TrackingConstants.UP_DAY_SINCE_INSTALL:
                    if (int.TryParse(value, out int days)) { DaySinceInstall = days; return true; } break;
                case TrackingConstants.UP_IS_IAP_USER:
                    IsIapUser = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase); return true;
                case TrackingConstants.UP_IAP_COUNT:
                    if (int.TryParse(value, out int iapC)) { IapCount = iapC; return true; } break;
                case TrackingConstants.UP_IS_IAA_USER:
                    IsIaaUser = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase); return true;
                case TrackingConstants.UP_IAA_COUNT:
                    if (int.TryParse(value, out int iaaC)) { IaaCount = iaaC; return true; } break;
                case TrackingConstants.UP_ACTIVE_DAY_N:
                    if (int.TryParse(value, out int activeDay)) { ActiveDayN = activeDay; return true; } break;
                case TrackingConstants.UP_REMAINING_GEM:
                    if (ulong.TryParse(value, out ulong gems)) { RemainingGem = gems; return true; } break;
                case TrackingConstants.UP_UA_NETWORK: UaNetwork = value; return true;
                case TrackingConstants.UP_UA_CAMPAIGN: UaCampaign = value; return true;
                case TrackingConstants.UP_UA_ADGROUP: UaAdGroup = value; return true;
                case TrackingConstants.UP_UA_CREATIVE: UaCreative = value; return true;
                case TrackingConstants.UP_STORE_NAME: StoreName = value; return true;
                case TrackingConstants.UP_STORE_APP_ID: StoreAppId = value; return true;
            }
            return false;
        }

        public Dictionary<string, string> GetAllProperties() {
            var dict = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(UserId)) dict[TrackingConstants.UP_USER_ID] = UserId;
            if (!string.IsNullOrEmpty(AdId)) dict[TrackingConstants.UP_AD_ID] = AdId;
            if (!string.IsNullOrEmpty(CurrentStage)) dict[TrackingConstants.UP_CURRENT_STAGE] = CurrentStage;
            dict[TrackingConstants.UP_PROGRESS_STAGE] = ProgressStage.ToString();
            dict[TrackingConstants.UP_EXPLORE_STAGE] = ExploreStage.ToString();
            string levelStr = CurrentLevel.ToString();
            dict[TrackingConstants.UP_CURRENT_LEVEL] = levelStr;
            dict[TrackingConstants.UP_LEVEL] = levelStr;
            dict[TrackingConstants.UP_DAY_SINCE_INSTALL] = DaySinceInstall.ToString();
            dict[TrackingConstants.UP_IS_IAP_USER] = IsIapUser ? "1" : "0";
            dict[TrackingConstants.UP_IAP_COUNT] = IapCount.ToString();
            dict[TrackingConstants.UP_IS_IAA_USER] = IsIaaUser ? "1" : "0";
            dict[TrackingConstants.UP_IAA_COUNT] = IaaCount.ToString();
            dict[TrackingConstants.UP_ACTIVE_DAY_N] = ActiveDayN.ToString();
            dict[TrackingConstants.UP_REMAINING_GEM] = RemainingGem.ToString();
            if (!string.IsNullOrEmpty(UaNetwork)) dict[TrackingConstants.UP_UA_NETWORK] = UaNetwork;
            if (!string.IsNullOrEmpty(UaCampaign)) dict[TrackingConstants.UP_UA_CAMPAIGN] = UaCampaign;
            if (!string.IsNullOrEmpty(UaAdGroup)) dict[TrackingConstants.UP_UA_ADGROUP] = UaAdGroup;
            if (!string.IsNullOrEmpty(UaCreative)) dict[TrackingConstants.UP_UA_CREATIVE] = UaCreative;
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
                profile.TotalEarned ??= new Dictionary<string, ulong>();
                profile.TotalBought ??= new Dictionary<string, ulong>();
                profile.TotalSpent ??= new Dictionary<string, ulong>();

                if (string.IsNullOrEmpty(profile.UserId)) {
                    profile.UserId = SystemInfo.deviceUniqueIdentifier;
                }

                return profile;
            } catch (Exception e) {
                SDKLogger.Error("UserProfile", $"Failed to load: {e.Message}");
                return new UserProfile();
            }
        }
    }
}
