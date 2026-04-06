using UnityEngine;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.TestLab {

    /// <summary>
    /// Detects whether the app was launched by Firebase Test Lab Game Loop.
    /// Handles both Android (intent action) and iOS (URL scheme).
    /// </summary>
    public static class GameLoopDetector {
        private const string Tag = "TestLab";
        private const string AndroidIntentAction = "com.google.intent.action.TEST_LOOP";
        private const string IosUrlScheme = "firebase-game-loop";

        private static bool? _cachedIsTestLab;
        private static int _cachedScenario = -1;

        /// <summary>
        /// Returns true if the app was launched by Firebase Test Lab.
        /// </summary>
        public static bool IsRunningInTestLab {
            get {
                _cachedIsTestLab ??= DetectTestLab();
                return _cachedIsTestLab.Value;
            }
        }

        /// <summary>
        /// Returns the scenario number requested by Test Lab (1-based).
        /// Returns 1 as default if not specified or not in Test Lab.
        /// </summary>
        public static int ScenarioNumber {
            get {
                if (_cachedScenario < 0) {
                    _cachedScenario = DetectScenarioNumber();
                }
                return _cachedScenario;
            }
        }

        /// <summary>
        /// Reset cached state. Useful for testing.
        /// </summary>
        public static void Reset() {
            _cachedIsTestLab = null;
            _cachedScenario = -1;
        }

        private static bool DetectTestLab() {
#if UNITY_ANDROID && !UNITY_EDITOR
            return DetectAndroid();
#elif UNITY_IOS && !UNITY_EDITOR
            return DetectIos();
#else
            SDKLogger.Debug(Tag, "Test Lab detection skipped (not on device)");
            return false;
#endif
        }

        private static int DetectScenarioNumber() {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GetAndroidScenario();
#elif UNITY_IOS && !UNITY_EDITOR
            return GetIosScenario();
#else
            return 1;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool DetectAndroid() {
            try {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                var action = intent.Call<string>("getAction");
                bool isTestLab = action == AndroidIntentAction;
                SDKLogger.Info(Tag, $"Android intent action: {action}, isTestLab: {isTestLab}");
                return isTestLab;
            } catch (System.Exception e) {
                SDKLogger.Error(Tag, $"Failed to detect Android Test Lab intent: {e.Message}");
                return false;
            }
        }

        private static int GetAndroidScenario() {
            try {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                int scenario = intent.Call<int>("getIntExtra", "scenario", 1);
                SDKLogger.Info(Tag, $"Android scenario number: {scenario}");
                return scenario;
            } catch (System.Exception e) {
                SDKLogger.Error(Tag, $"Failed to get Android scenario: {e.Message}");
                return 1;
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private static bool DetectIos() {
            string url = GetLaunchUrl();
            bool isTestLab = !string.IsNullOrEmpty(url) && url.StartsWith(IosUrlScheme + "://");
            SDKLogger.Info(Tag, $"iOS launch URL: {url ?? "(none)"}, isTestLab: {isTestLab}");
            return isTestLab;
        }

        private static int GetIosScenario() {
            string url = GetLaunchUrl();
            if (string.IsNullOrEmpty(url)) return 1;

            // URL format: firebase-game-loop://?scenario=N
            int queryIndex = url.IndexOf("scenario=", System.StringComparison.Ordinal);
            if (queryIndex < 0) return 1;

            string value = url.Substring(queryIndex + 9);
            int ampIndex = value.IndexOf('&');
            if (ampIndex >= 0) value = value.Substring(0, ampIndex);

            if (int.TryParse(value, out int scenario)) {
                SDKLogger.Info(Tag, $"iOS scenario number: {scenario}");
                return scenario;
            }
            return 1;
        }

        private static string GetLaunchUrl() {
            // Check Application.absoluteURL which is set on deep link launch
            return Application.absoluteURL;
        }
#endif
    }
}
