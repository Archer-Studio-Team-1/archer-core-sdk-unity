using System.Collections;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.TestLab.Scenarios {

    /// <summary>
    /// Built-in scenario helpers that can be used directly or as reference for custom scenarios.
    /// </summary>
    public static class BuiltInScenarios {
        private const string Tag = "TestLab";

        /// <summary>
        /// Smoke test: wait for N seconds, check for no exceptions, signal pass.
        /// Attach a GameLoopHandler and call this from Start().
        /// </summary>
        public static IEnumerator SmokeTest(GameLoopHandler handler, float waitSeconds = 10f) {
            SDKLogger.Info(Tag, $"Smoke test: waiting {waitSeconds}s for stability");

            bool hadError = false;
            void OnLogMessage(string condition, string stackTrace, LogType type) {
                if (type == LogType.Exception) hadError = true;
            }

            Application.logMessageReceived += OnLogMessage;
            yield return new WaitForSeconds(waitSeconds);
            Application.logMessageReceived -= OnLogMessage;

            handler.CompleteCurrentScenario(!hadError, hadError ? "Exception detected during smoke test" : "Smoke test passed");
        }

        /// <summary>
        /// FPS test: measure average FPS over N seconds, fail if below threshold.
        /// </summary>
        public static IEnumerator FpsTest(GameLoopHandler handler, float durationSeconds = 15f, float minFps = 24f) {
            SDKLogger.Info(Tag, $"FPS test: measuring for {durationSeconds}s, min threshold: {minFps}");

            int frameCount = 0;
            float elapsed = 0f;

            while (elapsed < durationSeconds) {
                frameCount++;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float avgFps = frameCount / elapsed;
            bool passed = avgFps >= minFps;
            string message = $"Average FPS: {avgFps:F1} (threshold: {minFps})";

            SDKLogger.Info(Tag, message);
            handler.CompleteCurrentScenario(passed, message);
        }

        /// <summary>
        /// Load test: load a scene and verify it completes without errors.
        /// </summary>
        public static IEnumerator SceneLoadTest(GameLoopHandler handler, string sceneName, float maxLoadTimeSeconds = 30f) {
            SDKLogger.Info(Tag, $"Scene load test: loading '{sceneName}', max time: {maxLoadTimeSeconds}s");

            float startTime = Time.realtimeSinceStartup;
            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
            if (op == null) {
                handler.CompleteCurrentScenario(false, $"Failed to start loading scene: {sceneName}");
                yield break;
            }

            while (!op.isDone) {
                if (Time.realtimeSinceStartup - startTime > maxLoadTimeSeconds) {
                    handler.CompleteCurrentScenario(false, $"Scene load timed out after {maxLoadTimeSeconds}s");
                    yield break;
                }
                yield return null;
            }

            float loadTime = Time.realtimeSinceStartup - startTime;
            handler.CompleteCurrentScenario(true, $"Scene '{sceneName}' loaded in {loadTime:F2}s");
        }

        /// <summary>
        /// Memory test: check that memory usage stays below a threshold.
        /// </summary>
        public static IEnumerator MemoryTest(GameLoopHandler handler, float durationSeconds = 10f, long maxMemoryMb = 512) {
            SDKLogger.Info(Tag, $"Memory test: monitoring for {durationSeconds}s, max: {maxMemoryMb}MB");

            float elapsed = 0f;
            long peakMemory = 0;

            while (elapsed < durationSeconds) {
                long currentMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
                if (currentMemory > peakMemory) peakMemory = currentMemory;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            bool passed = peakMemory <= maxMemoryMb;
            string message = $"Peak memory: {peakMemory}MB (threshold: {maxMemoryMb}MB)";

            SDKLogger.Info(Tag, message);
            handler.CompleteCurrentScenario(passed, message);
        }
    }
}
