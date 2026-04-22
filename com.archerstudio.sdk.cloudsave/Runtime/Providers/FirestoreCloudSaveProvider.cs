#if HAS_FIREBASE_FIRESTORE
using System;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Firestore;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.CloudSave {

    /// <summary>
    /// Firestore-backed cloud save provider.
    /// Auth: Firebase Auth via Google Play Games server-side access code (no Anonymous Auth).
    /// Schema: /saves/{firebaseUid}/slots/{slotKey}
    ///   - data:       string     — JSON blob (encrypted by game layer before SDK call)
    ///   - updatedAt:  Timestamp  — set by Firestore server, used for conflict detection
    ///   - appVersion: string     — Application.version, for future migration hooks
    ///
    /// Conflict logic (state tracked per-slot in PlayerPrefs):
    ///   cloud.updatedAt > local.ts &amp;&amp; isDirty  → WithConflict (game must resolve)
    ///   cloud.updatedAt > local.ts &amp;&amp; !isDirty → Succeeded with cloud data
    ///   cloud.updatedAt &lt;= local.ts            → LocalOnly with cached local data
    /// </summary>
    public class FirestoreCloudSaveProvider : ICloudSaveProvider {
        private const string Tag = "CloudSave-Firestore";
        private const int MaxDataLength = 900_000;

        private readonly string _serverAuthCode;
        private string _firebaseUid;
        private FirebaseFirestore _db;

        public FirestoreCloudSaveProvider(string serverAuthCode) {
            _serverAuthCode = serverAuthCode;
        }

        public void InitAsync(Action<bool> onComplete) {
            if (string.IsNullOrEmpty(_serverAuthCode)) {
                SDKLogger.Warning(Tag, "No server auth code — aborting Firestore init.");
                onComplete?.Invoke(false);
                return;
            }

            var credential = PlayGamesAuthProvider.GetCredential(_serverAuthCode);
            FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential).ContinueWith(task => {
                if (task.IsFaulted || task.IsCanceled) {
                    SDKLogger.Error(Tag, $"Firebase sign-in failed: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }
                _firebaseUid = task.Result.User.UserId;
                _db = FirebaseFirestore.DefaultInstance;
                SDKLogger.Info(Tag, $"Firebase signed in. UID={_firebaseUid}");
                onComplete?.Invoke(true);
            });
        }

        public void SaveAsync(string slotKey, string jsonData, Action<CloudSaveResult> onComplete) {
            if (string.IsNullOrEmpty(_firebaseUid)) {
                onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.NotAuthenticated));
                return;
            }
            if (jsonData == null || jsonData.Length >= MaxDataLength) {
                SDKLogger.Warning(Tag, $"Save '{slotKey}': data exceeds {MaxDataLength} byte limit.");
                onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.DataTooLarge));
                return;
            }

            SetDirtyFlag(slotKey, true);

            var docRef = GetSlotDocument(slotKey);
            var payload = new Dictionary<string, object> {
                ["data"]       = jsonData,
                ["updatedAt"]  = FieldValue.ServerTimestamp,
                ["appVersion"] = Application.version
            };

            docRef.SetAsync(payload).ContinueWith(writeTask => {
                if (writeTask.IsFaulted || writeTask.IsCanceled) {
                    SDKLogger.Error(Tag, $"Save '{slotKey}' failed: {writeTask.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.NetworkError));
                    return;
                }

                // Read back to get Firestore ServerTimestamp for accurate conflict tracking
                docRef.GetSnapshotAsync().ContinueWith(readTask => {
                    var ts = GetServerTimestamp(readTask.IsCompleted && !readTask.IsFaulted ? readTask.Result : null);
                    SetDirtyFlag(slotKey, false);
                    SaveLocalTimestamp(slotKey, ts);
                    SaveLocalData(slotKey, jsonData);
                    onComplete?.Invoke(CloudSaveResult.Succeeded(jsonData, ts));
                });
            });
        }

        public void LoadAsync(string slotKey, Action<CloudSaveResult> onComplete) {
            if (string.IsNullOrEmpty(_firebaseUid)) {
                onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.NotAuthenticated));
                return;
            }

            GetSlotDocument(slotKey).GetSnapshotAsync().ContinueWith(task => {
                if (task.IsFaulted || task.IsCanceled) {
                    SDKLogger.Error(Tag, $"Load '{slotKey}' failed: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.NetworkError));
                    return;
                }

                var snapshot = task.Result;
                if (!snapshot.Exists) {
                    onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.NotFound));
                    return;
                }

                if (!snapshot.TryGetValue<string>("data", out var cloudData)) {
                    SDKLogger.Error(Tag, $"Load '{slotKey}': 'data' field missing in document.");
                    onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.ProviderError));
                    return;
                }

                DateTime cloudTime = DateTime.MinValue;
                if (snapshot.TryGetValue<Timestamp>("updatedAt", out var cloudTs)) {
                    cloudTime = cloudTs.ToDateTime();
                }

                var localTime = LoadLocalTimestamp(slotKey);
                var isDirty   = GetDirtyFlag(slotKey);

                if (cloudTime > localTime && isDirty) {
                    onComplete?.Invoke(CloudSaveResult.WithConflict(cloudData, LoadLocalData(slotKey), cloudTime));
                    return;
                }

                if (cloudTime > localTime) {
                    SetDirtyFlag(slotKey, false);
                    SaveLocalTimestamp(slotKey, cloudTime);
                    SaveLocalData(slotKey, cloudData);
                    onComplete?.Invoke(CloudSaveResult.Succeeded(cloudData, cloudTime));
                    return;
                }

                // Local is current or newer — return cached local data
                var cached = LoadLocalData(slotKey);
                onComplete?.Invoke(!string.IsNullOrEmpty(cached)
                    ? CloudSaveResult.LocalOnly(cached)
                    : CloudSaveResult.Succeeded(cloudData, cloudTime));
            });
        }

        public void DeleteAsync(string slotKey, Action<CloudSaveResult> onComplete) {
            if (string.IsNullOrEmpty(_firebaseUid)) {
                onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.NotAuthenticated));
                return;
            }
            GetSlotDocument(slotKey).DeleteAsync().ContinueWith(task => {
                if (task.IsFaulted || task.IsCanceled) {
                    SDKLogger.Error(Tag, $"Delete '{slotKey}' failed: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(CloudSaveResult.Failed(CloudSaveErrorCode.NetworkError));
                    return;
                }
                ClearLocalMetadata(slotKey);
                onComplete?.Invoke(CloudSaveResult.Succeeded(null, DateTime.MinValue));
            });
        }

        // ── Firestore helpers ─────────────────────────────────────

        private DocumentReference GetSlotDocument(string slotKey) =>
            _db.Collection("saves").Document(_firebaseUid).Collection("slots").Document(slotKey);

        private static DateTime GetServerTimestamp(DocumentSnapshot snapshot) {
            if (snapshot == null || !snapshot.Exists) return DateTime.UtcNow;
            return snapshot.TryGetValue<Timestamp>("updatedAt", out var ts) ? ts.ToDateTime() : DateTime.UtcNow;
        }

        // ── PlayerPrefs helpers (keyed per slot) ──────────────────

        private static void SetDirtyFlag(string slot, bool dirty) =>
            PlayerPrefs.SetInt($"cloudsave_{slot}_dirty", dirty ? 1 : 0);

        private static bool GetDirtyFlag(string slot) =>
            PlayerPrefs.GetInt($"cloudsave_{slot}_dirty", 0) == 1;

        private static void SaveLocalTimestamp(string slot, DateTime dt) =>
            PlayerPrefs.SetString($"cloudsave_{slot}_ts", dt.ToBinary().ToString());

        private static DateTime LoadLocalTimestamp(string slot) {
            var raw = PlayerPrefs.GetString($"cloudsave_{slot}_ts", null);
            if (string.IsNullOrEmpty(raw) || !long.TryParse(raw, out var bin)) return DateTime.MinValue;
            return DateTime.FromBinary(bin);
        }

        private static void SaveLocalData(string slot, string data) =>
            PlayerPrefs.SetString($"cloudsave_{slot}_data", data ?? string.Empty);

        private static string LoadLocalData(string slot) =>
            PlayerPrefs.GetString($"cloudsave_{slot}_data", null);

        private static void ClearLocalMetadata(string slot) {
            PlayerPrefs.DeleteKey($"cloudsave_{slot}_dirty");
            PlayerPrefs.DeleteKey($"cloudsave_{slot}_ts");
            PlayerPrefs.DeleteKey($"cloudsave_{slot}_data");
        }
    }
}
#endif
