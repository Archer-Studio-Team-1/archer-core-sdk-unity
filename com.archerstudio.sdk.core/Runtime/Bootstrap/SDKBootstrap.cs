using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;
#if HAS_UNITY_SERVICES
using Unity.Services.Core;
#endif

namespace ArcherStudio.SDK.Core {

    public enum BootstrapState {
        Idle,
        InitializingServices,
        AwaitingConsent,
        InitializingModules,
        Ready,
        Failed
    }

    /// <summary>
    /// Main orchestrator for SDK initialization.
    /// Attach this to a GameObject in your game's splash or entry scene.
    /// </summary>
    public class SDKBootstrap : MonoBehaviour {
        private const string Tag = "Bootstrap";

        private static readonly UniTaskCompletionSource _initTcs = new();
        public static UniTask WaitUntilInitialized => _initTcs.Task;

        // Thread-safe monotonic clock. Time.realtimeSinceStartup throws when called
        // off the main thread (e.g. Adjust SDK callbacks firing on a worker thread),
        // so use Stopwatch instead.
        private static readonly Stopwatch _clock = Stopwatch.StartNew();
        private static float NowSeconds => (float)_clock.Elapsed.TotalSeconds;

        [Header("Config")]
        [SerializeField]
        [Tooltip("Drag SDKBootstrapConfig here, or leave null to auto-load from Resources.")]
        private SDKBootstrapConfig _bootstrapConfig;

        [Header("Events")]
        [SerializeField]
        [Tooltip("Invoked when the SDK finishes initialization.")]
        private UnityEvent _onComplete;

        private SDKInitializer _initializer;
        private float _startTime;
        private bool _sdkReady;
        private bool _isComplete;
        private int _totalModules;
        private BootstrapState _currentState = BootstrapState.Idle;

        public BootstrapState CurrentState => _currentState;

        private void Awake() {
            _startTime = NowSeconds;

            // Load bootstrap config
            if (_bootstrapConfig == null) {
                _bootstrapConfig = Resources.Load<SDKBootstrapConfig>("SDKBootstrapConfig");
            }

            if (_bootstrapConfig == null) {
                SDKLogger.Error(Tag, "SDKBootstrapConfig not found.");
                ChangeState(BootstrapState.Failed);
                return;
            }

            EnsureInitializer();
        }

        private void Start() {
            if (_initializer == null) {
                SDKLogger.Error(Tag, "SDKInitializer not available.");
                ChangeState(BootstrapState.Failed);
                FinishBootstrap();
                return;
            }

            if (_initializer.IsInitialized) {
                SDKLogger.Info(Tag, "SDK already initialized.");
                FinishBootstrap();
                return;
            }

            StartCoroutine(InitializeSequence());
            StartCoroutine(TimeoutWatchdog());
        }

        private IEnumerator InitializeSequence() {
            // Step 1: Initialize Unity Gaming Services
            ChangeState(BootstrapState.InitializingServices);
            yield return InitializeUnityServices();

            // Step 2: Discover and register modules
            RegisterModules();

            // Step 3: Subscribe to events
            SDKEventBus.Subscribe<SDKReadyEvent>(OnSDKReady);

            // Step 4: Start SDK module initialization
            ChangeState(BootstrapState.AwaitingConsent);
            SDKLogger.Info(Tag, "Starting SDK initialization...");
            _initializer.Initialize();

            // Wait for initializer to finish
            while (!_initializer.IsInitialized) {
                var consentModule = _initializer.GetModule("consent");
                if (consentModule != null && consentModule.State == ModuleState.Ready) {
                    if (_currentState == BootstrapState.AwaitingConsent) {
                        ChangeState(BootstrapState.InitializingModules);
                    }
                }
                yield return null;
            }

            // Step 5: Ready
            if (_currentState != BootstrapState.Failed) {
                ChangeState(BootstrapState.Ready);
                FinishBootstrap();
            }
        }

        private void ChangeState(BootstrapState newState) {
            if (_currentState == newState) return;
            _currentState = newState;
            SDKLogger.Info(Tag, $"State changed: {newState}");
        }

        private IEnumerator InitializeUnityServices() {
            #if HAS_UNITY_SERVICES
            if (UnityServices.State == ServicesInitializationState.Initialized) {
                yield break;
            }

            if (UnityServices.State == ServicesInitializationState.Initializing) {
                while (UnityServices.State == ServicesInitializationState.Initializing) {
                    yield return null;
                }
                yield break;
            }

            SDKLogger.Info(Tag, "Initializing Unity Gaming Services...");
            var initTask = UnityServices.InitializeAsync();

            while (!initTask.IsCompleted) {
                yield return null;
            }

            if (initTask.IsFaulted) {
                SDKLogger.Error(Tag, $"Unity Gaming Services failed: {initTask.Exception?.InnerException?.Message}");
            } else {
                SDKLogger.Info(Tag, "Unity Gaming Services initialized.");
            }
            #else
            yield break;
            #endif
        }

        private void OnDestroy() {
            SDKEventBus.Unsubscribe<SDKReadyEvent>(OnSDKReady);
        }

        private void EnsureInitializer() {
            _initializer = SDKInitializer.Instance;
            if (_initializer != null) return;

            var coreConfig = Resources.Load<SDKCoreConfig>("SDKCoreConfig");
            if (coreConfig == null) {
                SDKLogger.Error(Tag, "SDKCoreConfig not found. Cannot create SDKInitializer.");
                return;
            }

            var go = new GameObject("[SDKInitializer]");
            _initializer = go.AddComponent<SDKInitializer>();

            var configField = typeof(SDKInitializer).GetField("_config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField?.SetValue(_initializer, coreConfig);
        }

        private void RegisterModules() {
            if (!_bootstrapConfig.AutoDiscoverModules) return;

            var config = _initializer.Config;
            if (config == null) return;

            var modules = SDKModuleFactory.DiscoverModules(config);
            _totalModules = modules.Count;

            foreach (var module in modules) {
                _initializer.RegisterModule(module);
            }

            SDKLogger.Info(Tag, $"Registered {_totalModules} modules.");
        }

        private void OnSDKReady(SDKReadyEvent e) {
            _sdkReady = true;
            float elapsed = NowSeconds - _startTime;
            SDKLogger.Info(Tag, $"SDK ready in {elapsed:F2}s. Success={e.Success}");
            
            if (_currentState != BootstrapState.Failed) {
                ChangeState(BootstrapState.Ready);
                FinishBootstrap();
            }
        }

        private void FinishBootstrap() {
            if (_isComplete) return;
            _isComplete = true;

            SDKLogger.Info(Tag, "SDK Bootstrap complete. Invoking completion event.");

            float totalElapsed = NowSeconds - _startTime;
            SDKEventBus.Publish(new BootstrapCompleteEvent(totalElapsed, _totalModules, 0));

            _onComplete?.Invoke();
            _initTcs.TrySetResult();
        }

        private IEnumerator TimeoutWatchdog() {
            yield return new WaitForSecondsRealtime(_bootstrapConfig.MaxInitTimeout);

            if (_sdkReady || _isComplete) yield break;

            SDKLogger.Warning(Tag, $"SDK initialization timed out after {_bootstrapConfig.MaxInitTimeout}s.");

            if (_bootstrapConfig.ContinueOnModuleFailure) {
                FinishBootstrap();
            } else {
                ChangeState(BootstrapState.Failed);
                SDKLogger.Error(Tag, "SDK initialization failed (timeout). Check logs.");
                _initTcs.TrySetResult(); // Still complete to avoid hanging the game splash
            }
        }
    }
}
