using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ArcherStudio.SDK.Core.Tests {

    public class DependencyGraphTests {

        [Test]
        public void Resolve_EmptyModules_ReturnsEmptyBatches() {
            var graph = new DependencyGraph();
            var modules = new Dictionary<string, ISDKModule>();

            var result = graph.Resolve(modules);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Batches.Count);
        }

        [Test]
        public void Resolve_SingleModule_ReturnsSingleBatch() {
            var graph = new DependencyGraph();
            var modules = new Dictionary<string, ISDKModule> {
                { "core", new StubModule("core", 0) }
            };

            var result = graph.Resolve(modules);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Batches.Count);
            Assert.AreEqual("core", result.Batches[0][0].ModuleId);
        }

        [Test]
        public void Resolve_LinearDependencies_ReturnsCorrectOrder() {
            var graph = new DependencyGraph();
            var modules = new Dictionary<string, ISDKModule> {
                { "consent", new StubModule("consent", 0) },
                { "tracking", new StubModule("tracking", 20, "consent") },
                { "ads", new StubModule("ads", 50, "tracking") }
            };

            var result = graph.Resolve(modules);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.Batches.Count);
            Assert.AreEqual("consent", result.Batches[0][0].ModuleId);
            Assert.AreEqual("tracking", result.Batches[1][0].ModuleId);
            Assert.AreEqual("ads", result.Batches[2][0].ModuleId);
        }

        [Test]
        public void Resolve_ParallelModules_SameBatch() {
            var graph = new DependencyGraph();
            var modules = new Dictionary<string, ISDKModule> {
                { "core", new StubModule("core", 0) },
                { "analytics", new StubModule("analytics", 10, "core") },
                { "crash", new StubModule("crash", 10, "core") }
            };

            var result = graph.Resolve(modules);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Batches.Count);
            Assert.AreEqual("core", result.Batches[0][0].ModuleId);
            Assert.AreEqual(2, result.Batches[1].Count);
        }

        [Test]
        public void Resolve_CircularDependency_ReturnsError() {
            var graph = new DependencyGraph();
            var modules = new Dictionary<string, ISDKModule> {
                { "a", new StubModule("a", 0, "b") },
                { "b", new StubModule("b", 0, "a") }
            };

            var result = graph.Resolve(modules);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Errors[0].Contains("Circular"));
        }

        [Test]
        public void Resolve_MissingDependency_WarnsButStillSucceeds() {
            var graph = new DependencyGraph();
            var modules = new Dictionary<string, ISDKModule> {
                { "ads", new StubModule("ads", 50, "missing_module") }
            };

            var result = graph.Resolve(modules);

            // Missing dependency is soft — resolve succeeds with warning
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Batches.Count);
            Assert.AreEqual("ads", result.Batches[0][0].ModuleId);
            Assert.AreEqual(1, result.Warnings.Count);
            Assert.IsTrue(result.Warnings[0].Contains("missing_module"));
        }

        [Test]
        public void Resolve_PriorityOrdering_WithinBatch() {
            var graph = new DependencyGraph();
            var modules = new Dictionary<string, ISDKModule> {
                { "low", new StubModule("low", 50) },
                { "high", new StubModule("high", 0) },
                { "mid", new StubModule("mid", 20) }
            };

            var result = graph.Resolve(modules);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Batches.Count);
            var batch = result.Batches[0];
            Assert.AreEqual("high", batch[0].ModuleId);
            Assert.AreEqual("mid", batch[1].ModuleId);
            Assert.AreEqual("low", batch[2].ModuleId);
        }

        /// <summary>
        /// Minimal ISDKModule stub for testing.
        /// </summary>
        private class StubModule : ISDKModule {
            public string ModuleId { get; }
            public int InitializationPriority { get; }
            public IReadOnlyList<string> Dependencies { get; }
            public ModuleState State => ModuleState.NotInitialized;

            public StubModule(string id, int priority, params string[] deps) {
                ModuleId = id;
                InitializationPriority = priority;
                Dependencies = deps.Length > 0 ? deps : Array.Empty<string>();
            }

            public void InitializeAsync(SDKCoreConfig config, Action<bool> onComplete) {
                onComplete?.Invoke(true);
            }

            public void OnConsentChanged(ConsentStatus consent) { }
            public void Dispose() { }
        }
    }
}
