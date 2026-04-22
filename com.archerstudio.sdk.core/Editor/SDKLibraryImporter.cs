using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Mapping giữa module config và thư viện bên thứ ba cần import.
    /// Cung cấp entry-point cho nút "Import Library" trong SDK Setup Wizard.
    ///
    /// Ba loại import:
    /// - Upm: Unity Package Manager (Client.Add) — auto add gói từ registry.
    /// - ExternalUrl: mở trình duyệt tới trang tải .unitypackage hoặc tài liệu.
    /// - Manual: chỉ hiển thị hướng dẫn (không có bước tự động).
    /// </summary>
    public static class SDKLibraryImporter {

        public enum LibraryType { Upm, ExternalUrl, Manual }

        public struct LibraryInfo {
            public string DisplayName;
            public LibraryType Type;
            // Với Upm: package id hoặc git URL ("com.unity.purchasing", "https://github.com/...").
            public string PackageIdentifier;
            // Với ExternalUrl: trang tải hoặc tài liệu.
            public string ExternalUrl;
            // Đường dẫn tương đối tới thư mục/package đại diện cho library đã cài.
            public string[] InstalledChecks;
            // Hướng dẫn hiển thị trong dialog.
            public string Instructions;
        }

        private static readonly Dictionary<string, LibraryInfo> Map = new Dictionary<string, LibraryInfo> {
            { "ConsentConfig", new LibraryInfo {
                DisplayName = "Google UMP (via Google Mobile Ads)",
                Type = LibraryType.ExternalUrl,
                ExternalUrl = "https://github.com/googleads/googleads-mobile-unity/releases",
                InstalledChecks = new[] { "Assets/GoogleMobileAds" },
                Instructions = "Tải GoogleMobileAds-*.unitypackage từ GitHub Releases rồi import vào project. UMP nằm trong gói GMA."
            }},
            { "LoginConfig", new LibraryInfo {
                DisplayName = "Google Play Games Plugin for Unity",
                Type = LibraryType.ExternalUrl,
                ExternalUrl = "https://github.com/playgameservices/play-games-plugin-for-unity/releases",
                InstalledChecks = new[] {
                    "Assets/GooglePlayGames",
                    "Assets/Public/GooglePlayGames",
                    "Assets/Plugins/Android/GooglePlayGamesManifest.androidlib",
                    "Packages/com.google.play.games"
                },
                Instructions =
                    "Tải GooglePlayGamesPlugin-*.unitypackage từ thư mục 'current-build/' trong GitHub Releases rồi import vào project.\n\n" +
                    "QUAN TRỌNG: KHÔNG dùng UPM Git URL với path 'Assets/Public/GooglePlayGames' — sẽ thiếu 'Assets/Plugins/Android/GooglePlayGamesManifest.androidlib' khiến build Android fail (hai folder nằm ở hai root khác nhau của repo).\n\n" +
                    "Chỉ .unitypackage official mới bao gồm đầy đủ cả com.google.play.games (Runtime/Editor) và Plugins/Android manifest stub.\n\n" +
                    "Sau khi import, HAS_GPGS sẽ được SDKSymbolDetector tự thêm vào Scripting Define Symbols khi phát hiện type GooglePlayGames.PlayGamesPlatform."
            }},
            { "TrackingConfig", new LibraryInfo {
                DisplayName = "Firebase Analytics + Adjust SDK",
                Type = LibraryType.ExternalUrl,
                ExternalUrl = "https://firebase.google.com/download/unity",
                InstalledChecks = new[] { "Assets/Firebase", "Assets/Adjust" },
                Instructions = "1) Firebase Unity SDK: import FirebaseAnalytics.unitypackage từ firebase.google.com/download/unity.\n2) Adjust SDK: tải từ https://github.com/adjust/unity_sdk/releases và import Adjust.unitypackage."
            }},
            { "AdConfig", new LibraryInfo {
                DisplayName = "AppLovin MAX",
                Type = LibraryType.ExternalUrl,
                ExternalUrl = "https://dash.applovin.com/documentation/mediation/unity/getting-started/integration",
                InstalledChecks = new[] { "Assets/MaxSdk" },
                Instructions = "Đăng nhập AppLovin dashboard, tải AppLovin MAX Unity Plugin (.unitypackage) rồi import vào project."
            }},
            { "IAPConfig", new LibraryInfo {
                DisplayName = "Unity IAP",
                Type = LibraryType.Upm,
                PackageIdentifier = "com.unity.purchasing",
                InstalledChecks = new[] { "Packages/com.unity.purchasing" },
                Instructions = "Sẽ thêm package com.unity.purchasing qua Unity Package Manager."
            }},
            { "RemoteConfigConfig", new LibraryInfo {
                DisplayName = "Firebase Remote Config",
                Type = LibraryType.ExternalUrl,
                ExternalUrl = "https://firebase.google.com/download/unity",
                InstalledChecks = new[] { "Assets/Firebase" },
                Instructions = "Firebase Unity SDK: import FirebaseRemoteConfig.unitypackage từ firebase.google.com/download/unity."
            }},
            { "PushConfig", new LibraryInfo {
                DisplayName = "Firebase Cloud Messaging",
                Type = LibraryType.ExternalUrl,
                ExternalUrl = "https://firebase.google.com/download/unity",
                InstalledChecks = new[] { "Assets/Firebase" },
                Instructions = "Firebase Unity SDK: import FirebaseMessaging.unitypackage từ firebase.google.com/download/unity."
            }},
            { "DeepLinkConfig", new LibraryInfo {
                DisplayName = "Adjust Deep Link / Firebase Dynamic Links",
                Type = LibraryType.Manual,
                Instructions = "Deep Link thường dùng Adjust (đã cài từ Tracking) hoặc Firebase Dynamic Links. Không cần plugin riêng nếu đã có Adjust hoặc Firebase."
            }},
            { "TestLabConfig", new LibraryInfo {
                DisplayName = "Firebase Test Lab",
                Type = LibraryType.Manual,
                Instructions = "Firebase Test Lab không cần plugin Unity — gửi build qua gcloud CLI (`gcloud firebase test android run`)."
            }}
        };

        private static AddRequest _addRequest;

        public static bool HasInfo(string configName) => Map.ContainsKey(configName);

        public static LibraryInfo GetInfo(string configName) => Map[configName];

        public static bool IsInstalled(string configName) {
            if (!Map.TryGetValue(configName, out var info)) return false;
            if (info.InstalledChecks == null) return false;
            foreach (var check in info.InstalledChecks) {
                if (string.IsNullOrEmpty(check)) continue;
                string fullPath = Path.GetFullPath(check);
                if (Directory.Exists(fullPath) || File.Exists(fullPath)) return true;
            }
            return false;
        }

        public static void ImportLibrary(string configName) {
            if (!Map.TryGetValue(configName, out var info)) return;

            switch (info.Type) {
                case LibraryType.Upm:
                    if (_addRequest != null && !_addRequest.IsCompleted) {
                        EditorUtility.DisplayDialog("Import In Progress", "Đang thêm package khác, vui lòng chờ.", "OK");
                        return;
                    }
                    _addRequest = Client.Add(info.PackageIdentifier);
                    EditorApplication.update += MonitorUpmRequest;
                    EditorUtility.DisplayProgressBar("Adding Package", $"Adding {info.PackageIdentifier}...", 0.5f);
                    break;

                case LibraryType.ExternalUrl:
                    if (EditorUtility.DisplayDialog(
                            $"Import {info.DisplayName}",
                            $"{info.Instructions}\n\nMở trang tải?",
                            "Mở trang", "Hủy")) {
                        Application.OpenURL(info.ExternalUrl);
                    }
                    break;

                case LibraryType.Manual:
                    EditorUtility.DisplayDialog(info.DisplayName, info.Instructions, "OK");
                    break;
            }
        }

        private static void MonitorUpmRequest() {
            if (_addRequest == null) {
                EditorApplication.update -= MonitorUpmRequest;
                return;
            }
            if (!_addRequest.IsCompleted) return;

            EditorUtility.ClearProgressBar();
            if (_addRequest.Status == StatusCode.Success) {
                EditorUtility.DisplayDialog(
                    "Import Complete",
                    $"Đã thêm {_addRequest.Result.packageId} (v{_addRequest.Result.version}).",
                    "OK");
                AssetDatabase.Refresh();
            } else {
                string err = _addRequest.Error != null ? _addRequest.Error.message : "Unknown error";
                EditorUtility.DisplayDialog("Import Failed", err, "OK");
            }
            _addRequest = null;
            EditorApplication.update -= MonitorUpmRequest;
        }
    }
}
