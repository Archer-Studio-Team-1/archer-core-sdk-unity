using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArcherStudio.SDK.Core.UI {

    /// <summary>
    /// Full-screen blocking loading overlay for SDK async operations.
    ///
    /// Project places a prefab at Resources/SDKLoadingOverlay with:
    ///   - Canvas (overlay, sortingOrder 9999)
    ///   - Blocker image (full-screen, blocks input)
    ///   - Spinner (optional, auto-rotated if assigned)
    ///   - Any custom visuals the project wants
    ///
    /// SDK only handles: show/hide canvas, rotate spinner, timeout.
    /// All visual design is owned by the project.
    ///
    /// If no prefab exists, falls back to a minimal code-built overlay.
    ///
    /// Usage:
    ///   var handle = SDKLoadingOverlay.Show();
    ///   handle.Dismiss();
    /// </summary>
    public class SDKLoadingOverlay : MonoBehaviour {

        private const string PrefabPath = "SDKLoadingOverlay";

        [Header("References (wire in prefab)")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _spinnerTransform;

        private static SDKLoadingOverlay _instance;

        private float _timeout;
        private float _elapsed;
        private bool _active;
        private Action _onTimeout;
        private float _spinnerAngle;

        // ─── Public API ───

        public static SDKLoadingOverlay Show(
            Action onTimeout = null,
            float timeoutOverride = -1f) {

            var config = Resources.Load<SDKCoreConfig>("SDKCoreConfig");

            bool show = config == null || config.ShowLoadingOverlay;
            if (!show) return null;

            float timeout = timeoutOverride >= 0f
                ? timeoutOverride
                : (config != null ? config.LoadingOverlayTimeout : 15f);

            var instance = GetOrCreateInstance();
            if (instance == null) return null;

            instance._timeout = timeout;
            instance._elapsed = 0f;
            instance._onTimeout = onTimeout;
            instance._active = true;
            instance._spinnerAngle = 0f;
            instance._canvas.enabled = true;

            return instance;
        }

        public void Dismiss() {
            if (!_active) return;
            _active = false;
            _elapsed = 0f;
            _onTimeout = null;
            if (_canvas != null) _canvas.enabled = false;
        }

        public static void Shutdown() {
            if (_instance == null) return;
            Destroy(_instance.gameObject);
            _instance = null;
        }

        // ─── Lifecycle ───

        private void Update() {
            if (!_active) return;

            if (_spinnerTransform != null) {
                _spinnerAngle -= 360f * Time.unscaledDeltaTime;
                _spinnerTransform.localRotation = Quaternion.Euler(0, 0, _spinnerAngle);
            }

            if (_timeout > 0f) {
                _elapsed += Time.unscaledDeltaTime;
                if (_elapsed >= _timeout) {
                    var callback = _onTimeout;
                    Dismiss();
                    callback?.Invoke();
                }
            }
        }

        private void OnDestroy() {
            if (_instance == this) _instance = null;
        }

        // ─── Internal ───

        private static SDKLoadingOverlay GetOrCreateInstance() {
            if (_instance != null) return _instance;

            var prefab = Resources.Load<SDKLoadingOverlay>(PrefabPath);
            if (prefab != null) {
                _instance = Instantiate(prefab);
                _instance.gameObject.name = "[SDK] LoadingOverlay";
            } else {
                _instance = BuildFallback();
            }

            DontDestroyOnLoad(_instance.gameObject);
            _instance._canvas.enabled = false;
            return _instance;
        }

        private static SDKLoadingOverlay BuildFallback() {
            var go = new GameObject("[SDK] LoadingOverlay");
            var overlay = go.AddComponent<SDKLoadingOverlay>();

            overlay._canvas = go.AddComponent<Canvas>();
            overlay._canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay._canvas.sortingOrder = 9999;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            var blockerGo = new GameObject("Blocker", typeof(RectTransform));
            blockerGo.transform.SetParent(go.transform, false);
            var blockerImg = blockerGo.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.5f);
            blockerImg.raycastTarget = true;
            var rect = blockerGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return overlay;
        }
    }
}
