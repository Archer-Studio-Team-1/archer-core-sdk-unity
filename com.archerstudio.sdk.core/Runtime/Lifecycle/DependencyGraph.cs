using System.Collections.Generic;
using System.Linq;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Resolves module initialization order using topological sort (Kahn's algorithm).
    /// Modules in the same batch have no interdependencies and can init in parallel.
    ///
    /// Dependencies are treated as soft by default: if a dependency is not registered,
    /// it is skipped with a warning instead of blocking the entire SDK initialization.
    /// Only circular dependencies are treated as hard errors.
    /// </summary>
    public class DependencyGraph {

        /// <summary>
        /// Resolve initialization order. Returns batches of modules.
        /// Modules within the same batch are independent and can init concurrently.
        /// </summary>
        /// <returns>Ordered list of batches. Each batch is a list of modules.</returns>
        public ResolveResult Resolve(IReadOnlyDictionary<string, ISDKModule> modules) {
            var inDegree = new Dictionary<string, int>();
            var adjacency = new Dictionary<string, List<string>>();
            var errors = new List<string>();
            var warnings = new List<string>();

            // Initialize graph
            foreach (var kvp in modules) {
                string id = kvp.Key;
                inDegree[id] = 0;
                adjacency[id] = new List<string>();
            }

            // Build edges: if A depends on B, then B -> A (B must init before A)
            foreach (var kvp in modules) {
                string id = kvp.Key;
                var deps = kvp.Value.Dependencies;
                if (deps == null) continue;

                foreach (string dep in deps) {
                    if (!modules.ContainsKey(dep)) {
                        // Soft dependency: warn but do not block initialization
                        warnings.Add(
                            $"Module '{id}' depends on '{dep}' which is not registered. " +
                            $"Skipping dependency — module will still initialize.");
                        continue;
                    }

                    adjacency[dep].Add(id);
                    inDegree[id]++;
                }
            }

            // Kahn's algorithm with priority-based batch ordering
            var queue = new List<string>();
            foreach (var kvp in inDegree) {
                if (kvp.Value == 0) {
                    queue.Add(kvp.Key);
                }
            }

            var batches = new List<IReadOnlyList<ISDKModule>>();
            int processedCount = 0;

            while (queue.Count > 0) {
                // Sort current batch by InitializationPriority
                var batch = queue
                    .OrderBy(id => modules[id].InitializationPriority)
                    .Select(id => modules[id])
                    .ToList();

                batches.Add(batch);
                var nextQueue = new List<string>();

                foreach (string id in queue) {
                    processedCount++;
                    foreach (string neighbor in adjacency[id]) {
                        inDegree[neighbor]--;
                        if (inDegree[neighbor] == 0) {
                            nextQueue.Add(neighbor);
                        }
                    }
                }

                queue = nextQueue;
            }

            // Detect circular dependencies — this is a hard error
            if (processedCount != modules.Count) {
                var remaining = modules.Keys.Where(id => inDegree[id] > 0).ToList();
                errors.Add(
                    $"Circular dependency detected among modules: {string.Join(", ", remaining)}");
                return new ResolveResult(null, errors, warnings);
            }

            return new ResolveResult(batches, errors, warnings);
        }
    }

    public class ResolveResult {
        public IReadOnlyList<IReadOnlyList<ISDKModule>> Batches { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool Success => Errors == null || Errors.Count == 0;

        public ResolveResult(
            IReadOnlyList<IReadOnlyList<ISDKModule>> batches,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings) {
            Batches = batches;
            Errors = errors;
            Warnings = warnings;
        }
    }
}
