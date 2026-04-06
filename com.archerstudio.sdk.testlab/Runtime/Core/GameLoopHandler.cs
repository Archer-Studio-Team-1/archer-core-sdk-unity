using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ArcherStudio.SDK.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcherStudio.SDK.TestLab {

    /// <summary>
    /// Main runtime component for Firebase Test Lab Game Loop.
    /// Attach to a GameObject in your startup scene.
    /// Detects Test Lab launch, runs the requested scenario, logs results, and signals completion.
    /// </summary>
    public class GameLoopHandler : MonoBehaviour {
        private const string Tag = "TestLab";

#if UNITY_ANDROID
        private const string ResultsBasePath = "/sdcard/data/local/tmp/game_loop_results";
#else
        private const string ResultsBasePath = "";
#endif

        [SerializeField] private TestLabConfig _config;

        private readonly List<GameLoopResult> _results = new();
        private float _scenarioStartTime;
        private int _currentScenario;
        private bool _isRunning;

        /// <summary>
        /// Fired when a scenario starts. Passes the scenario number.
        /// </summary>
        public static event Action<int> OnScenarioStarted;

        /// <summary>
        /// Fired when a scenario completes. Passes the result.
        /// </summary>
        public static event Action<GameLoopResult> OnScenarioCompleted;

        /// <summary>
        /// Whether a Game Loop scenario is currently running.
        /// </summary>
        public bool IsRunning => _isRunning;

        private void Awake() {
            if (_config == null) {
                _config = Resources.Load<TestLabConfig>("TestLabConfig");
            }
        }

        private void Start() {
            if (_config == null || !_config.Enabled) return;

            // Check master toggle from SDKCoreConfig
            var coreConfig = Resources.Load<SDKCoreConfig>("SDKCoreConfig");
            if (coreConfig != null && !coreConfig.EnableTestLab) {
                SDKLogger.Debug(Tag, "Test Lab disabled in SDKCoreConfig");
                return;
            }

            if (!GameLoopDetector.IsRunningInTestLab) {
                SDKLogger.Debug(Tag, "Not launched by Test Lab, handler idle");
                return;
            }

            _currentScenario = GameLoopDetector.ScenarioNumber;
            SDKLogger.Info(Tag, $"Test Lab detected, starting scenario {_currentScenario}");
            StartCoroutine(RunScenarioCoroutine(_currentScenario));
        }

        private IEnumerator RunScenarioCoroutine(int scenarioNumber) {
            _isRunning = true;
            _scenarioStartTime = Time.realtimeSinceStartup;

            var entry = _config.GetScenario(scenarioNumber);
            if (entry == null || !entry.Enabled) {
                var result = new GameLoopResult(scenarioNumber, "Unknown", false, 0f, "Scenario not found or disabled");
                CompleteScenario(result);
                yield break;
            }

            SDKLogger.Info(Tag, $"Running scenario {scenarioNumber}: {entry.Name}");
            OnScenarioStarted?.Invoke(scenarioNumber);

            // Load the scenario scene if specified
            if (!string.IsNullOrEmpty(entry.SceneName)) {
                var loadOp = SceneManager.LoadSceneAsync(entry.SceneName, LoadSceneMode.Additive);
                if (loadOp != null) {
                    yield return loadOp;
                    SDKLogger.Info(Tag, $"Scene loaded: {entry.SceneName}");
                } else {
                    var result = new GameLoopResult(scenarioNumber, entry.Name, false, 0f, $"Failed to load scene: {entry.SceneName}");
                    CompleteScenario(result);
                    yield break;
                }
            }

            // Wait for timeout or manual completion
            float elapsed = 0f;
            while (_isRunning && elapsed < _config.ScenarioTimeoutSeconds) {
                elapsed = Time.realtimeSinceStartup - _scenarioStartTime;
                yield return null;
            }

            // If still running after timeout, complete with timeout result
            if (_isRunning) {
                float duration = Time.realtimeSinceStartup - _scenarioStartTime;
                var result = new GameLoopResult(scenarioNumber, entry.Name, true, duration, "Completed (timeout)");
                CompleteScenario(result);
            }
        }

        /// <summary>
        /// Call this from your game code to signal that the current scenario passed.
        /// </summary>
        public void CompleteCurrentScenario(bool passed, string message = null) {
            if (!_isRunning) return;

            float duration = Time.realtimeSinceStartup - _scenarioStartTime;
            var entry = _config.GetScenario(_currentScenario);
            string name = entry?.Name ?? "Unknown";
            var result = new GameLoopResult(_currentScenario, name, passed, duration, message);
            CompleteScenario(result);
        }

        private void CompleteScenario(GameLoopResult result) {
            _isRunning = false;
            _results.Add(result);

            SDKLogger.Info(Tag, $"Scenario {result.ScenarioNumber} completed: passed={result.Passed}, duration={result.DurationSeconds:F1}s, message={result.Message}");
            OnScenarioCompleted?.Invoke(result);

            if (_config.WriteResultFiles) {
                WriteResultFile(result);
            }

            SignalTestLabComplete();
        }

        private void WriteResultFile(GameLoopResult result) {
#if UNITY_ANDROID && !UNITY_EDITOR
            try {
                string dir = $"{ResultsBasePath}/scenario_{result.ScenarioNumber}";
                Directory.CreateDirectory(dir);

                string json = JsonUtility.ToJson(result, true);
                string path = $"{dir}/result.json";
                File.WriteAllText(path, json);
                SDKLogger.Info(Tag, $"Result written to {path}");
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Failed to write result file: {e.Message}");
            }
#endif
        }

        private static void SignalTestLabComplete() {
#if UNITY_ANDROID && !UNITY_EDITOR
            try {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("finish");
                SDKLogger.Info(Tag, "Android activity finished (Test Lab signal)");
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Failed to finish activity: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Application.OpenURL("firebase-game-loop-complete://");
            SDKLogger.Info(Tag, "iOS completion URL opened (Test Lab signal)");
#else
            SDKLogger.Debug(Tag, "Test Lab completion signaled (editor/standalone - no-op)");
#endif
        }

        /// <summary>
        /// Get all results from the current session.
        /// </summary>
        public IReadOnlyList<GameLoopResult> GetResults() => _results;
    }

    /// <summary>
    /// Result of a single Game Loop scenario execution.
    /// </summary>
    [Serializable]
    public class GameLoopResult {
        public int ScenarioNumber;
        public string ScenarioName;
        public bool Passed;
        public float DurationSeconds;
        public string Message;
        public string Timestamp;

        public GameLoopResult(int scenarioNumber, string scenarioName, bool passed, float durationSeconds, string message) {
            ScenarioNumber = scenarioNumber;
            ScenarioName = scenarioName;
            Passed = passed;
            DurationSeconds = durationSeconds;
            Message = message ?? "";
            Timestamp = DateTime.UtcNow.ToString("o");
        }
    }
}
