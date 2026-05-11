using System;
using System.Collections;
using UnityEngine;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Lightweight coroutine runner for IAP timeout and retry logic.
    /// Auto-creates a hidden GameObject if needed. DontDestroyOnLoad.
    /// Handles Editor playmode transitions and application quit safely.
    /// </summary>
    internal class IAPCoroutineRunner : MonoBehaviour {
        private static IAPCoroutineRunner _instance;
        private static bool _applicationIsQuitting;

        private static IAPCoroutineRunner Instance {
            get {
                if (_applicationIsQuitting) return null;

                if (_instance == null) {
                    var go = new GameObject("[IAPCoroutineRunner]") {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<IAPCoroutineRunner>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Start a coroutine on the runner. Used by ServerReceiptValidator for async HTTP.
        /// Returns silently if the application is quitting.
        /// </summary>
        public static Coroutine Run(IEnumerator coroutine) {
            if (coroutine == null || _applicationIsQuitting) return null;
            var runner = Instance;
            return runner != null ? runner.StartCoroutine(coroutine) : null;
        }

        /// <summary>
        /// Invoke an action after a delay (in seconds) on the main thread.
        /// Returns silently if the application is quitting.
        /// </summary>
        public static void DelayedCall(float delaySeconds, Action action) {
            if (action == null || _applicationIsQuitting) return;
            var runner = Instance;
            if (runner == null) return;
            runner.StartCoroutine(DelayedCallCoroutine(delaySeconds, action));
        }

        /// <summary>
        /// Cancel all pending delayed calls.
        /// </summary>
        public static void CancelAll() {
            if (_instance != null) {
                _instance.StopAllCoroutines();
            }
        }

        private static IEnumerator DelayedCallCoroutine(float delaySeconds, Action action) {
            yield return new WaitForSecondsRealtime(delaySeconds);
            action?.Invoke();
        }

        /// <summary>
        /// Auto-refresh subscription state when app returns to foreground.
        /// Debounced to avoid rapid repeated calls.
        /// </summary>
        private float _lastFocusRefreshTime;
        private const float FocusRefreshCooldown = 5f;

        private void OnApplicationFocus(bool hasFocus) {
            if (!hasFocus) return;
            if (_applicationIsQuitting) return;
            if (Time.realtimeSinceStartup - _lastFocusRefreshTime < FocusRefreshCooldown) return;
            _lastFocusRefreshTime = Time.realtimeSinceStartup;

            var manager = IAPManager.Instance;
            if (manager == null || manager.State != Core.ModuleState.Ready) return;
            if (!manager.IsSubscriptionStateReady) return;

            Core.SDKLogger.Debug("IAP", "App resumed — refreshing subscription state from store.");
            manager.FetchSubscriptionProduct(null);
        }

        private void OnApplicationQuit() {
            _applicationIsQuitting = true;
        }

        private void OnDestroy() {
            if (_instance == this) {
                _instance = null;
            }
        }

        #if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticFields() {
            _applicationIsQuitting = false;
            _instance = null;
        }
        #endif
    }
}
