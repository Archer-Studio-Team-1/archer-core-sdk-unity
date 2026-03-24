using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Dispatches actions to the Unity main thread from background threads.
    /// Automatically initializes before any scene loads.
    /// </summary>
    public class UnityMainThreadDispatcher : SingletonMono<UnityMainThreadDispatcher> {
        private static readonly Queue<Action> ExecutionQueue = new Queue<Action>();
        private static int _mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInitialized() {
            if (instance == null) {
                var dispatcher = Instance;
                dispatcher.Init();
                DontDestroyOnLoad(dispatcher.gameObject);
            }
        }

        public void Init() {
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            lock (ExecutionQueue) {
                ExecutionQueue.Clear();
            }
        }

        public static bool IsMainThread() {
            return System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        public void Enqueue(Action action) {
            if (action == null) return;
            lock (ExecutionQueue) {
                ExecutionQueue.Enqueue(action);
            }
        }

        /// <summary>
        /// Execute an action on the main thread after a delay (in seconds).
        /// Uses a coroutine internally.
        /// </summary>
        public void EnqueueDelayed(float delaySeconds, Action action) {
            if (action == null) return;
            StartCoroutine(DelayedCoroutine(delaySeconds, action));
        }

        private static IEnumerator DelayedCoroutine(float delay, Action action) {
            yield return new WaitForSecondsRealtime(delay);
            action?.Invoke();
        }

        private void Update() {
            lock (ExecutionQueue) {
                while (ExecutionQueue.Count > 0) {
                    ExecutionQueue.Dequeue()?.Invoke();
                }
            }
        }
    }
}
