using System.Collections.Generic;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Registry that holds all discovered SDK modules.
    /// Modules register themselves or are discovered by SDKInitializer.
    /// </summary>
    public class ModuleRegistry {
        private readonly Dictionary<string, ISDKModule> _modules = new Dictionary<string, ISDKModule>();

        public void Register(ISDKModule module) {
            if (module == null) return;

            if (_modules.ContainsKey(module.ModuleId)) {
                SDKLogger.Warning("Registry",
                    $"Module '{module.ModuleId}' already registered. Skipping duplicate.");
                return;
            }

            _modules[module.ModuleId] = module;
            SDKLogger.Debug("Registry", $"Registered module: {module.ModuleId}");
        }

        public void Unregister(string moduleId) {
            _modules.Remove(moduleId);
        }

        public ISDKModule GetModule(string moduleId) {
            _modules.TryGetValue(moduleId, out var module);
            return module;
        }

        public T GetModule<T>() where T : class, ISDKModule {
            foreach (var module in _modules.Values) {
                if (module is T typed) {
                    return typed;
                }
            }
            return null;
        }

        public IReadOnlyDictionary<string, ISDKModule> GetAll() {
            return _modules;
        }

        public bool HasModule(string moduleId) {
            return _modules.ContainsKey(moduleId);
        }

        public int Count => _modules.Count;
    }
}
