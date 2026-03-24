using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Thread-safe singleton MonoBehaviour. Destroyed when scene changes.
    /// For persistent singletons, use SingletonMonoDontDestroy.
    /// </summary>
    public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour {
        private static readonly object SyncRoot = new object();
        protected static volatile T instance;

        public static T Instance {
            get {
                lock (SyncRoot) {
                    if (instance == null) {
                        instance = FindFirstObjectByType<T>();
                        if (instance == null) {
                            var go = new GameObject(typeof(T).Name);
                            instance = go.AddComponent<T>();
                        }
                    }
                }
                return instance;
            }
        }

        public static bool IsNull => instance == null;

        protected virtual void Awake() {
            if (instance != null && instance.GetInstanceID() != GetInstanceID()) {
                Destroy(gameObject);
                return;
            }
            instance = this as T;
        }
    }
}
