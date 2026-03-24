using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Thread-safe singleton MonoBehaviour that persists across scene loads.
    /// Uses DontDestroyOnLoad. Handles application quit to prevent ghost objects.
    /// </summary>
    public class SingletonMonoDontDestroy<T> : MonoBehaviour where T : MonoBehaviour {
        private static T _instance;
        private static readonly object Lock = new object();
        private static bool _applicationIsQuitting;

        public static T Instance {
            get {
                lock (Lock) {
                    if (_applicationIsQuitting) {
                        return null;
                    }

                    if (_instance == null) {
                        _instance = FindFirstObjectByType<T>();

                        if (FindObjectsByType<T>(FindObjectsSortMode.None).Length > 1) {
                            return _instance;
                        }

                        if (_instance == null) {
                            var go = new GameObject(typeof(T).Name);
                            _instance = go.AddComponent<T>();
                            DontDestroyOnLoad(go);
                        }
                    }
                    return _instance;
                }
            }
        }

        public virtual void OnDestroy() {
            if (Application.isEditor) {
                _applicationIsQuitting = false;
            }
        }

        private void OnApplicationQuit() {
            _applicationIsQuitting = true;
        }
    }
}
