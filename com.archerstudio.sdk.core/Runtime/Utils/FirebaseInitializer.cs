#if HAS_FIREBASE_APP
using Firebase;
using Firebase.Extensions;
#endif

#if HAS_FIREBASE_CRASHLYTICS
using Firebase.Crashlytics;
#endif

using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Ensures FirebaseApp.CheckAndFixDependenciesAsync() is called exactly once.
    /// Thread-safe: callers may come from different threads (e.g., UMP callback thread).
    /// After resolution, IsAvailable can be checked synchronously by any provider.
    /// </summary>
    public static class FirebaseInitializer {
#if HAS_FIREBASE_APP
        private static readonly object Lock = new object();
        private static readonly List<Action<bool>> PendingCallbacks = new List<Action<bool>>();
        private static bool _isRunning;
        private static bool _isResolved;
        private static bool _isAvailable;

        /// <summary>
        /// True after CheckAndFixDependenciesAsync completes successfully.
        /// Safe to read from any thread after EnsureInitialized callback has fired.
        /// </summary>
        public static bool IsAvailable {
            get {
                lock (Lock) { return _isAvailable; }
            }
        }

        /// <summary>
        /// True after CheckAndFixDependenciesAsync has completed (success or failure).
        /// </summary>
        public static bool IsResolved {
            get {
                lock (Lock) { return _isResolved; }
            }
        }

        public static void EnsureInitialized(Action<bool> onComplete) {
            bool alreadyResolved;
            bool cachedAvailable;

            lock (Lock) {
                alreadyResolved = _isResolved;
                cachedAvailable = _isAvailable;

                if (!alreadyResolved) {
                    PendingCallbacks.Add(onComplete);

                    if (_isRunning) return;
                    _isRunning = true;
                }
            }

            // Invoke OUTSIDE lock to prevent potential deadlocks
            if (alreadyResolved) {
                onComplete?.Invoke(cachedAvailable);
                return;
            }

            SDKLogger.Info("Firebase", "CheckAndFixDependenciesAsync — starting (shared)...");

            try {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
                    bool available;

                    if (task.IsFaulted) {
                        available = false;
                        SDKLogger.Error("Firebase",
                            $"CheckAndFixDependenciesAsync — EXCEPTION: {task.Exception?.GetBaseException().Message}");
                    } else if (task.IsCanceled) {
                        available = false;
                        SDKLogger.Error("Firebase", "CheckAndFixDependenciesAsync — cancelled.");
                    } else {
                        available = task.Result == DependencyStatus.Available;
                        if (available) {
                            SDKLogger.Info("Firebase", "CheckAndFixDependenciesAsync — dependencies available.");
                        } else {
                            SDKLogger.Error("Firebase",
                                $"CheckAndFixDependenciesAsync — NOT available: {task.Result}");
                        }
                    }

                    if (available) {
                        try {
                            var app = FirebaseApp.DefaultInstance;
                            #if HAS_FIREBASE_CRASHLYTICS
                            Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                            #endif
                            if (app != null) {
                                SDKLogger.Info("Firebase",
                                    $"  App: {app.Name} | Project: {app.Options?.ProjectId ?? "(null)"} | AppId: {app.Options?.AppId ?? "(null)"}");
                            }
                        } catch (Exception e) {
                            SDKLogger.Error("Firebase", $"  Failed to read FirebaseApp info: {e.Message}");
                        }
                    }

                    FirePendingCallbacks(available);
                });
            } catch (Exception e) {
                // Firebase SDK failed to even start the async operation
                SDKLogger.Error("Firebase", $"CheckAndFixDependenciesAsync failed to start: {e.Message}");
                FirePendingCallbacks(false);
            }
        }

        private static void FirePendingCallbacks(bool available) {
            List<Action<bool>> callbacks;
            lock (Lock) {
                _isAvailable = available;
                _isResolved = true;
                callbacks = new List<Action<bool>>(PendingCallbacks);
                PendingCallbacks.Clear();
            }

            SDKLogger.Info("Firebase", $"Firing {callbacks.Count} pending callback(s)...");

            foreach (var cb in callbacks) {
                try {
                    cb?.Invoke(available);
                } catch (Exception e) {
                    SDKLogger.Error("Firebase", $"Exception in callback: {e.Message}");
                }
            }
        }
#else
        public static bool IsAvailable => false;
        public static bool IsResolved => true;

        public static void EnsureInitialized(Action<bool> onComplete) {
            SDKLogger.Debug("Firebase", "Firebase not installed — stub.");
            onComplete?.Invoke(false);
        }
#endif
    }
}
