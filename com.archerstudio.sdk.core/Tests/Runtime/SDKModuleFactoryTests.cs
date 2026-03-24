using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ArcherStudio.SDK.Core.Tests {

    public class SDKModuleFactoryTests {

        private SDKCoreConfig _config;

        [SetUp]
        public void SetUp() {
            SDKModuleFactory.ClearCreators();
            _config = ScriptableObject.CreateInstance<SDKCoreConfig>();
            _config.EnableConsent = true;
            _config.EnableTracking = true;
            _config.EnableAds = true;
            _config.EnableIAP = false;
            _config.EnableRemoteConfig = false;
            _config.EnablePush = false;
            _config.EnableDeepLink = false;
        }

        [TearDown]
        public void TearDown() {
            SDKModuleFactory.ClearCreators();
            if (_config != null) {
                UnityEngine.Object.DestroyImmediate(_config);
            }
        }

        [Test]
        public void DiscoverModules_NoCreators_ReturnsEmpty() {
            var modules = SDKModuleFactory.DiscoverModules(_config);
            Assert.AreEqual(0, modules.Count);
        }

        [Test]
        public void RegisterCreator_EnabledModule_IsDiscovered() {
            SDKModuleFactory.RegisterCreator(config =>
                config.EnableAds ? new StubModule("ads", 50) : null);

            var modules = SDKModuleFactory.DiscoverModules(_config);

            Assert.AreEqual(1, modules.Count);
            Assert.AreEqual("ads", modules[0].ModuleId);
        }

        [Test]
        public void RegisterCreator_DisabledModule_IsNotDiscovered() {
            SDKModuleFactory.RegisterCreator(config =>
                config.EnableIAP ? new StubModule("iap", 50) : null);

            var modules = SDKModuleFactory.DiscoverModules(_config);

            Assert.AreEqual(0, modules.Count);
        }

        [Test]
        public void RegisterCreator_MultipleModules_AllDiscovered() {
            SDKModuleFactory.RegisterCreator(config =>
                config.EnableConsent ? new StubModule("consent", 0) : null);
            SDKModuleFactory.RegisterCreator(config =>
                config.EnableTracking ? new StubModule("tracking", 20) : null);
            SDKModuleFactory.RegisterCreator(config =>
                config.EnableAds ? new StubModule("ads", 50) : null);

            var modules = SDKModuleFactory.DiscoverModules(_config);

            Assert.AreEqual(3, modules.Count);
        }

        [Test]
        public void RegisterCreator_NullCreator_IsIgnored() {
            SDKModuleFactory.RegisterCreator(null);

            var modules = SDKModuleFactory.DiscoverModules(_config);

            Assert.AreEqual(0, modules.Count);
        }

        [Test]
        public void RegisterCreator_DuplicateCreator_IsIgnored() {
            SDKModuleFactory.ModuleCreator creator = config =>
                config.EnableAds ? new StubModule("ads", 50) : null;

            SDKModuleFactory.RegisterCreator(creator);
            SDKModuleFactory.RegisterCreator(creator);

            var modules = SDKModuleFactory.DiscoverModules(_config);

            Assert.AreEqual(1, modules.Count);
        }

        [Test]
        public void RegisterCreator_ThrowingCreator_DoesNotBreakOthers() {
            SDKModuleFactory.RegisterCreator(config =>
                throw new Exception("Boom"));
            SDKModuleFactory.RegisterCreator(config =>
                config.EnableAds ? new StubModule("ads", 50) : null);

            var modules = SDKModuleFactory.DiscoverModules(_config);

            Assert.AreEqual(1, modules.Count);
            Assert.AreEqual("ads", modules[0].ModuleId);
        }

        [Test]
        public void ClearCreators_RemovesAll() {
            SDKModuleFactory.RegisterCreator(config => new StubModule("test", 0));
            SDKModuleFactory.ClearCreators();

            var modules = SDKModuleFactory.DiscoverModules(_config);

            Assert.AreEqual(0, modules.Count);
        }

        [Test]
        public void DiscoverModules_DuplicateModuleId_SecondIsSkipped() {
            SDKModuleFactory.RegisterCreator(config =>
                new StubModule("ads", 50));
            SDKModuleFactory.RegisterCreator(config =>
                new StubModule("ads", 99));

            var modules = SDKModuleFactory.DiscoverModules(_config);

            Assert.AreEqual(1, modules.Count);
            Assert.AreEqual(50, modules[0].InitializationPriority);
        }

        private class StubModule : ISDKModule {
            public string ModuleId { get; }
            public int InitializationPriority { get; }
            public IReadOnlyList<string> Dependencies => Array.Empty<string>();
            public ModuleState State => ModuleState.NotInitialized;

            public StubModule(string id, int priority) {
                ModuleId = id;
                InitializationPriority = priority;
            }

            public void InitializeAsync(SDKCoreConfig config, Action<bool> onComplete) {
                onComplete?.Invoke(true);
            }

            public void OnConsentChanged(ConsentStatus consent) { }
            public void Dispose() { }
        }
    }
}
