using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Factory that auto-discovers and creates SDK module instances.
    /// Uses a two-phase strategy:
    ///   1. Scene-based: find MonoBehaviour modules already in the scene
    ///   2. Config-driven: create plain-class modules based on SDKCoreConfig toggles
    ///
    /// Modules register via SDKModuleFactory.RegisterCreator() for config-driven creation.
    /// This avoids hard references from sdk.core to other packages.
    /// </summary>
    public static class SDKModuleFactory {

        /// <summary>
        /// Delegate that creates a module instance. Return null to skip.
        /// </summary>
        public delegate ISDKModule ModuleCreator(SDKCoreConfig config);

        private static readonly List<ModuleCreator> Creators = new List<ModuleCreator>();

        /// <summary>
        /// Register a module creator. Called by each package's static initializer
        /// or by game code before bootstrap.
        ///
        /// Example (in sdk.ads):
        ///   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        ///   static void Register() {
        ///       SDKModuleFactory.RegisterCreator(config =>
        ///           config.EnableAds ? new AdManager() : null);
        ///   }
        /// </summary>
        public static void RegisterCreator(ModuleCreator creator) {
            if (creator != null && !Creators.Contains(creator)) {
                Creators.Add(creator);
            }
        }

        /// <summary>
        /// Clear all registered creators. Used in tests.
        /// </summary>
        public static void ClearCreators() {
            Creators.Clear();
        }

        /// <summary>
        /// Discover all ISDKModule instances:
        ///   1. Find MonoBehaviour-based modules already in scene
        ///   2. Create plain-class modules via registered creators
        /// Returns deduplicated list (by ModuleId).
        /// </summary>
        public static List<ISDKModule> DiscoverModules(SDKCoreConfig config) {
            var discovered = new Dictionary<string, ISDKModule>();

            // Phase 1: Scene-based discovery (MonoBehaviour modules)
            DiscoverSceneModules(discovered);

            // Phase 2: Config-driven creation (plain class modules)
            DiscoverFactoryModules(config, discovered);

            SDKLogger.Info("Bootstrap",
                $"Discovered {discovered.Count} modules: " +
                $"{string.Join(", ", discovered.Keys)}");

            return new List<ISDKModule>(discovered.Values);
        }

        private static void DiscoverSceneModules(Dictionary<string, ISDKModule> discovered) {
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsSortMode.None);

            foreach (var behaviour in behaviours) {
                if (behaviour is ISDKModule module) {
                    if (discovered.ContainsKey(module.ModuleId)) {
                        SDKLogger.Debug("Bootstrap",
                            $"Scene module '{module.ModuleId}' skipped (already registered).");
                        continue;
                    }

                    discovered[module.ModuleId] = module;
                    SDKLogger.Debug("Bootstrap",
                        $"Discovered scene module: {module.ModuleId}");
                }
            }
        }

        private static void DiscoverFactoryModules(
            SDKCoreConfig config,
            Dictionary<string, ISDKModule> discovered) {

            foreach (var creator in Creators) {
                try {
                    var module = creator(config);
                    if (module == null) continue;

                    if (discovered.ContainsKey(module.ModuleId)) {
                        SDKLogger.Debug("Bootstrap",
                            $"Factory module '{module.ModuleId}' skipped (already registered).");
                        continue;
                    }

                    discovered[module.ModuleId] = module;
                    SDKLogger.Debug("Bootstrap",
                        $"Created factory module: {module.ModuleId}");
                } catch (Exception e) {
                    SDKLogger.Error("Bootstrap",
                        $"Module creator failed: {e.Message}");
                }
            }
        }
    }
}
