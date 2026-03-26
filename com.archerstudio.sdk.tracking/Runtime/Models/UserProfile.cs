using System;
using System.Collections.Generic;
using System.IO;
using ArcherStudio.SDK.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking {

    [Serializable]
    public class UserProfile {
        public event Action<string, string> OnPropertyChanged;

        private void Notify(string key, string value) => OnPropertyChanged?.Invoke(key, value);

        // ─── Base properties (common across all games) ───
        private string _adjustId = "";
        private string _deviceId = "";
        private string _firebaseStorageId = "";
        private string _currentTask = "";
        private string _currentStage = "";
        private int _progressStage;
        private double _currentGem;

        // ─── Custom properties (game-specific, extensible) ───
        [JsonProperty]
        private Dictionary<string, string> _customProperties = new Dictionary<string, string>();

        // ─── Base Properties with change notification ───

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

        public double CurrentGem {
            get => _currentGem;
            set { if (Math.Abs(_currentGem - value) > 0.001) { _currentGem = value; Notify(TrackingConstants.UP_CURRENT_GEM, value.ToString("F0")); } }
        }

        // ─── Custom Properties (game-specific extension) ───

        /// <summary>
        /// Set a custom user property. Used by games to add game-specific properties
        /// beyond the base set. Fires OnPropertyChanged for provider sync.
        /// </summary>
        public void SetCustomProperty(string key, string value) {
            string normalized = value ?? "Null";
            if (_customProperties.TryGetValue(key, out var existing) && existing == normalized) return;
            _customProperties[key] = normalized;
            Notify(key, normalized);
        }

        /// <summary>
        /// Get a custom user property value. Returns null if not set.
        /// </summary>
        public string GetCustomProperty(string key) {
            return _customProperties.TryGetValue(key, out var v) ? v : null;
        }

        /// <summary>
        /// Check if a custom property exists.
        /// </summary>
        public bool HasCustomProperty(string key) {
            return _customProperties.ContainsKey(key);
        }

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
                case TrackingConstants.UP_CURRENT_GEM:
                    if (double.TryParse(value, out double gem)) { CurrentGem = gem; return true; } break;
                default:
                    SetCustomProperty(key, value);
                    return true;
            }
            return false;
        }

        public Dictionary<string, string> GetAllProperties() {
            var dict = new Dictionary<string, string>();

            string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? "Null" : s;

            // Base properties
            dict[TrackingConstants.UP_ADJUST_ID] = NullIfEmpty(AdjustId);
            dict[TrackingConstants.UP_DEVICE_ID] = NullIfEmpty(DeviceId);
            dict[TrackingConstants.UP_FIREBASE_STORAGE_ID] = NullIfEmpty(FirebaseStorageId);
            dict[TrackingConstants.UP_CURRENT_TASK] = NullIfEmpty(CurrentTask);
            dict[TrackingConstants.UP_CURRENT_STAGE] = NullIfEmpty(CurrentStage);
            dict[TrackingConstants.UP_PROGRESS_STAGE] = ProgressStage.ToString();
            dict[TrackingConstants.UP_CURRENT_GEM] = CurrentGem.ToString("F0");

            // Custom properties (game-specific)
            foreach (var kv in _customProperties) {
                dict[kv.Key] = kv.Value;
            }

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
                profile._customProperties ??= new Dictionary<string, string>();

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
