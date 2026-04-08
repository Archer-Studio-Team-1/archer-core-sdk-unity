using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Main entry point for SDK initialization.
    /// Place this component on a GameObject in your first scene.
    /// It orchestrates all module initialization in dependency order.
    /// </summary>
    public class SDKInitializer : MonoBehaviour {

        [SerializeField]
        private SDKCoreConfig _config;

        public static SDKInitializer Instance { get; private set; }

        public SDKCoreConfig Config => _config;
        public bool IsInitialized { get; private set; }

        private readonly ModuleRegistry _registry = new ModuleRegistry();
        private readonly DependencyGraph _graph = new DependencyGraph();

        private int _failedModuleCount;

        public event Action OnSDKReady;

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_config != null) {
                #if PRODUCTION
                // Production: disable SDK debug logs, only show warnings and errors
                SDKLogger.SetMinLevel(LogLevel.Warning);
                SDKLogger.SetEnabled(true);
                #else
                SDKLogger.SetMinLevel(_config.DebugMode ? LogLevel.Verbose : _config.MinLogLevel);
                SDKLogger.SetEnabled(true);
                #endif
            }

            SDKLogger.Info("Core", "SDKInitializer Awake.");
        }

        /// <summary>
        /// Register a module before calling Initialize().
        /// Typically called from module MonoBehaviours in Awake().
        /// </summary>
        public void RegisterModule(ISDKModule module) {
            _registry.Register(module);
        }

        /// <summary>
        /// Retrieve a registered module by type.
        /// </summary>
        public T GetModule<T>() where T : class, ISDKModule {
            return _registry.GetModule<T>();
        }

        /// <summary>
        /// Retrieve a registered module by ID.
        /// </summary>
        public ISDKModule GetModule(string moduleId) {
            return _registry.GetModule(moduleId);
        }

        /// <summary>
        /// Start SDK initialization. Call this after all modules have been registered.
        /// Modules are initialized in dependency order (topological sort).
        /// </summary>
        public void Initialize() {
            if (IsInitialized) {
                SDKLogger.Warning("Core", "SDK already initialized. Ignoring.");
                return;
            }

            if (_config == null) {
                SDKLogger.Error("Core", "SDKCoreConfig is null. Cannot initialize.");
                return;
            }

            LogCoreConfig();
            SDKLogger.Info("Core",
                $"Starting SDK initialization with {_registry.Count} modules.");

            var result = _graph.Resolve(_registry.GetAll());

            // Log warnings for missing soft dependencies
            if (result.Warnings != null) {
                foreach (string warning in result.Warnings) {
                    SDKLogger.Warning("Core", warning);
                }
            }

            if (!result.Success) {
                foreach (string error in result.Errors) {
                    SDKLogger.Error("Core", error);
                }
                return;
            }

            _failedModuleCount = 0;
            InitializeBatches(result.Batches, 0);
        }

        /// <summary>
        /// Initialize modules in dependency-ordered batches.
        /// All module callbacks are marshalled to the Unity main thread via
        /// UnityMainThreadDispatcher to prevent cross-thread issues
        /// (e.g., Google UMP fires callbacks on Android UI thread).
        /// </summary>
        private void InitializeBatches(
            IReadOnlyList<IReadOnlyList<ISDKModule>> batches,
            int batchIndex) {

            if (batchIndex >= batches.Count) {
                OnAllModulesInitialized();
                return;
            }

            var batch = batches[batchIndex];
            int remaining = batch.Count;

            SDKLogger.Info("Core",
                $"Initializing batch {batchIndex + 1}/{batches.Count} " +
                $"({batch.Count} module(s): {string.Join(", ", BatchModuleIds(batch))}).");

            foreach (var module in batch) {
                SDKLogger.Info("Core",
                    $"Initializing module: {module.ModuleId}");

                try {
                    module.InitializeAsync(_config, success => {
                        // Marshal to Unity main thread — callbacks may fire from any thread.
                        RunOnMainThread(() => {
                            try {
                                if (success) {
                                    SDKLogger.Info("Core",
                                        $"Module '{module.ModuleId}' initialized successfully.");
                                } else {
                                    SDKLogger.Error("Core",
                                        $"Module '{module.ModuleId}' failed to initialize.");
                                    _failedModuleCount++;
                                }

                                SDKEventBus.Publish(new ModuleInitializedEvent(
                                    module.ModuleId, success));
                            } catch (Exception cbEx) {
                                SDKLogger.Error("Core",
                                    $"Exception in module '{module.ModuleId}' callback: {cbEx.Message}");
                                _failedModuleCount++;
                            }

                            remaining--;
                            if (remaining <= 0) {
                                InitializeBatches(batches, batchIndex + 1);
                            }
                        });
                    });
                } catch (Exception e) {
                    SDKLogger.Error("Core",
                        $"Exception initializing module '{module.ModuleId}': {e.Message}");
                    SDKLogger.Exception("Core", e);
                    _failedModuleCount++;

                    SDKEventBus.Publish(new ModuleInitializedEvent(
                        module.ModuleId, false));

                    remaining--;
                    if (remaining <= 0) {
                        InitializeBatches(batches, batchIndex + 1);
                    }
                }
            }
        }

        private static IEnumerable<string> BatchModuleIds(IReadOnlyList<ISDKModule> batch) {
            var ids = new string[batch.Count];
            for (int i = 0; i < batch.Count; i++) ids[i] = batch[i].ModuleId;
            return ids;
        }

        /// <summary>
        /// Run action on Unity main thread. Falls back to direct invoke if dispatcher unavailable.
        /// </summary>
        private static void RunOnMainThread(Action action) {
            var dispatcher = UnityMainThreadDispatcher.Instance;
            if (dispatcher != null) {
                dispatcher.Enqueue(action);
            } else {
                // Dispatcher destroyed — invoke directly to avoid stuck
                SDKLogger.Warning("Core", "MainThreadDispatcher unavailable, invoking directly.");
                action?.Invoke();
            }
        }

        private void OnAllModulesInitialized() {
            IsInitialized = true;
            bool allSuccess = _failedModuleCount == 0;

            SDKLogger.Info("Core",
                $"SDK initialization complete. " +
                $"Success={allSuccess}, Failed={_failedModuleCount}.");

            SDKEventBus.Publish(new SDKReadyEvent(allSuccess, _failedModuleCount));
            OnSDKReady?.Invoke();
        }

        private void LogCoreConfig() {
            SDKLogger.Info("Core", "┌─── SDK Core Config ───");
            SDKLogger.Info("Core", $"│ AppId:              {(_config.AppId ?? "(not set)")}");
            SDKLogger.Info("Core", $"│ DebugMode:          {_config.DebugMode}");
            SDKLogger.Info("Core", $"│ MinLogLevel:        {_config.MinLogLevel}");
            SDKLogger.Info("Core", $"│ EnableConsent:      {_config.EnableConsent}");
            SDKLogger.Info("Core", $"│ EnableTracking:     {_config.EnableTracking}");
            SDKLogger.Info("Core", $"│ EnableAnalytics:    {_config.EnableAnalytics}");
            SDKLogger.Info("Core", $"│ EnableAds:          {_config.EnableAds}");
            SDKLogger.Info("Core", $"│ EnableIAP:          {_config.EnableIAP}");
            SDKLogger.Info("Core", $"│ EnableRemoteConfig: {_config.EnableRemoteConfig}");
            SDKLogger.Info("Core", $"│ EnablePush:         {_config.EnablePush}");
            SDKLogger.Info("Core", $"│ EnableDeepLink:     {_config.EnableDeepLink}");
            SDKLogger.Info("Core", $"│ Platform:           {Application.platform}");
            SDKLogger.Info("Core", $"│ Unity:              {Application.unityVersion}");
            SDKLogger.Info("Core", $"│ Bundle:             {Application.identifier}");
            SDKLogger.Info("Core", $"│ Version:            {Application.version}");
            SDKLogger.Info("Core", "└───────────────────────");
        }

        private void OnDestroy() {
            if (Instance != this) return;

            foreach (var module in _registry.GetAll().Values) {
                try {
                    module.Dispose();
                } catch (Exception e) {
                    SDKLogger.Error("Core",
                        $"Error disposing module '{module.ModuleId}': {e.Message}");
                }
            }

            Instance = null;
        }
    }
}
