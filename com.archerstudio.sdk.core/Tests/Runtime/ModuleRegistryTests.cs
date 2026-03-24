using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ArcherStudio.SDK.Core.Tests {

    public class ModuleRegistryTests {

        private ModuleRegistry _registry;

        [SetUp]
        public void SetUp() {
            _registry = new ModuleRegistry();
        }

        [Test]
        public void Register_AddsModule() {
            var module = new StubModule("test");
            _registry.Register(module);

            Assert.AreEqual(1, _registry.Count);
            Assert.IsTrue(_registry.HasModule("test"));
        }

        [Test]
        public void Register_Null_DoesNotThrow() {
            Assert.DoesNotThrow(() => _registry.Register(null));
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void Register_Duplicate_IgnoresSecond() {
            var module = new StubModule("test");
            _registry.Register(module);
            _registry.Register(module);

            Assert.AreEqual(1, _registry.Count);
        }

        [Test]
        public void GetModule_ById_ReturnsCorrectModule() {
            var module = new StubModule("ads");
            _registry.Register(module);

            var retrieved = _registry.GetModule("ads");

            Assert.AreSame(module, retrieved);
        }

        [Test]
        public void GetModule_ByType_ReturnsCorrectModule() {
            var module = new StubModule("test");
            _registry.Register(module);

            var retrieved = _registry.GetModule<StubModule>();

            Assert.AreSame(module, retrieved);
        }

        [Test]
        public void GetModule_NotRegistered_ReturnsNull() {
            Assert.IsNull(_registry.GetModule("nonexistent"));
            Assert.IsNull(_registry.GetModule<StubModule>());
        }

        [Test]
        public void Unregister_RemovesModule() {
            _registry.Register(new StubModule("test"));
            _registry.Unregister("test");

            Assert.AreEqual(0, _registry.Count);
            Assert.IsFalse(_registry.HasModule("test"));
        }

        [Test]
        public void GetAll_ReturnsAllModules() {
            _registry.Register(new StubModule("a"));
            _registry.Register(new StubModule("b"));
            _registry.Register(new StubModule("c"));

            var all = _registry.GetAll();

            Assert.AreEqual(3, all.Count);
            Assert.IsTrue(all.ContainsKey("a"));
            Assert.IsTrue(all.ContainsKey("b"));
            Assert.IsTrue(all.ContainsKey("c"));
        }

        private class StubModule : ISDKModule {
            public string ModuleId { get; }
            public int InitializationPriority => 0;
            public IReadOnlyList<string> Dependencies => Array.Empty<string>();
            public ModuleState State => ModuleState.NotInitialized;

            public StubModule(string id) { ModuleId = id; }

            public void InitializeAsync(SDKCoreConfig config, Action<bool> onComplete) =>
                onComplete?.Invoke(true);
            public void OnConsentChanged(ConsentStatus consent) { }
            public void Dispose() { }
        }
    }
}
