using System;
using NUnit.Framework;
using ArcherStudio.SDK.Core;
using ArcherStudio.SDK.CloudSave;

namespace ArcherStudio.SDK.CloudSave.Tests {

    [TestFixture]
    public class CloudSaveModuleTests {

        private CloudSaveModule _module;
        private SDKCoreConfig _config;

        [SetUp]
        public void SetUp() {
            _module = new CloudSaveModule();
            _config = UnityEngine.ScriptableObject.CreateInstance<SDKCoreConfig>();
        }

        [TearDown]
        public void TearDown() {
            _module.Dispose();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        // ── Identity ──────────────────────────────────────────

        [Test]
        public void ModuleId_Equals_CloudSave() {
            Assert.AreEqual("cloudsave", _module.ModuleId);
        }

        [Test]
        public void Priority_Equals_50() {
            Assert.AreEqual(50, _module.InitializationPriority);
        }

        [Test]
        public void Dependencies_Contains_Login() {
            CollectionAssert.Contains(_module.Dependencies, "login");
        }

        // ── InitializeAsync: EnableCloudSave = false ──────────

        [Test]
        public void InitAsync_EnableCloudSaveFalse_CallsOnCompleteTrue() {
            _config.EnableCloudSave = false;
            bool? result = null;
            _module.InitializeAsync(_config, v => result = v);
            Assert.IsTrue(result, "onComplete must be called with true even when disabled");
        }

        [Test]
        public void InitAsync_EnableCloudSaveFalse_StateIsReady() {
            _config.EnableCloudSave = false;
            _module.InitializeAsync(_config, _ => { });
            Assert.AreEqual(ModuleState.Ready, _module.State);
        }

        [Test]
        public void InitAsync_EnableCloudSaveFalse_InstanceSet() {
            _config.EnableCloudSave = false;
            _module.InitializeAsync(_config, _ => { });
            Assert.AreSame(_module, CloudSaveModule.Instance);
        }

        [Test]
        public void InitAsync_NeverBlocks_AlwaysCallsOnComplete() {
            _config.EnableCloudSave = false;
            bool completed = false;
            Assert.DoesNotThrow(() => _module.InitializeAsync(_config, _ => completed = true));
            Assert.IsTrue(completed, "onComplete must always be invoked — never block SDK init chain");
        }

        // ── SaveAsync guards ──────────────────────────────────

        [Test]
        public void SaveAsync_NotReady_ReturnsProviderError() {
            CloudSaveResult? result = null;
            _module.SaveAsync("slot_main", "{}", r => result = r);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Value.Success);
            Assert.AreEqual(CloudSaveErrorCode.ProviderError, result.Value.ErrorCode);
        }

        [Test]
        public void SaveAsync_DataTooLarge_ReturnsDataTooLarge() {
            _config.EnableCloudSave = false;
            _module.InitializeAsync(_config, _ => { });

            var bigData = new string('x', 900_000);
            CloudSaveResult? result = null;
            _module.SaveAsync("slot_main", bigData, r => result = r);

            Assert.IsFalse(result.Value.Success);
            Assert.AreEqual(CloudSaveErrorCode.DataTooLarge, result.Value.ErrorCode);
        }

        [Test]
        public void SaveAsync_WithStub_ReturnsSuccess() {
            _config.EnableCloudSave = false;
            _module.InitializeAsync(_config, _ => { });

            CloudSaveResult? result = null;
            _module.SaveAsync("slot_main", "{\"level\":1}", r => result = r);

            Assert.IsTrue(result.Value.Success);
            Assert.IsFalse(result.Value.HasConflict);
        }

        // ── LoadAsync guards ──────────────────────────────────

        [Test]
        public void LoadAsync_NotReady_ReturnsProviderError() {
            CloudSaveResult? result = null;
            _module.LoadAsync("slot_main", r => result = r);
            Assert.IsFalse(result.Value.Success);
            Assert.AreEqual(CloudSaveErrorCode.ProviderError, result.Value.ErrorCode);
        }

        [Test]
        public void LoadAsync_SlotNotFound_ReturnsNotFound() {
            _config.EnableCloudSave = false;
            _module.InitializeAsync(_config, _ => { });

            CloudSaveResult? result = null;
            _module.LoadAsync("slot_missing", r => result = r);

            Assert.IsFalse(result.Value.Success);
            Assert.AreEqual(CloudSaveErrorCode.NotFound, result.Value.ErrorCode);
        }

        [Test]
        public void LoadAsync_AfterSave_ReturnsData() {
            _config.EnableCloudSave = false;
            _module.InitializeAsync(_config, _ => { });

            _module.SaveAsync("slot_main", "{\"level\":5}", _ => { });

            CloudSaveResult? loaded = null;
            _module.LoadAsync("slot_main", r => loaded = r);

            Assert.IsTrue(loaded.Value.Success);
            Assert.AreEqual("{\"level\":5}", loaded.Value.Data);
        }

        // ── DeleteAsync ───────────────────────────────────────

        [Test]
        public void DeleteAsync_NotReady_ReturnsProviderError() {
            CloudSaveResult? result = null;
            _module.DeleteAsync("slot_main", r => result = r);
            Assert.IsFalse(result.Value.Success);
            Assert.AreEqual(CloudSaveErrorCode.ProviderError, result.Value.ErrorCode);
        }

        // ── Dispose ───────────────────────────────────────────

        [Test]
        public void Dispose_SetsStateDisposed() {
            _module.Dispose();
            Assert.AreEqual(ModuleState.Disposed, _module.State);
        }

        [Test]
        public void Dispose_ClearsInstance() {
            _config.EnableCloudSave = false;
            _module.InitializeAsync(_config, _ => { });
            _module.Dispose();
            Assert.IsNull(CloudSaveModule.Instance);
        }
    }

    [TestFixture]
    public class CloudSaveResultTests {

        [Test]
        public void Succeeded_HasSuccessTrueAndNoConflict() {
            var r = CloudSaveResult.Succeeded("{}", DateTime.UtcNow);
            Assert.IsTrue(r.Success);
            Assert.IsFalse(r.HasConflict);
            Assert.AreEqual(CloudSaveErrorCode.None, r.ErrorCode);
        }

        [Test]
        public void Failed_HasSuccessFalse() {
            var r = CloudSaveResult.Failed(CloudSaveErrorCode.NetworkError);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(CloudSaveErrorCode.NetworkError, r.ErrorCode);
        }

        [Test]
        public void WithConflict_HasConflictTrue_BothDataPresent() {
            var r = CloudSaveResult.WithConflict("{\"cloud\":1}", "{\"local\":2}", DateTime.UtcNow);
            Assert.IsTrue(r.Success);
            Assert.IsTrue(r.HasConflict);
            Assert.AreEqual("{\"cloud\":1}", r.Data);
            Assert.AreEqual("{\"local\":2}", r.LocalData);
        }

        [Test]
        public void LocalOnly_HasSuccessTrueNoConflict() {
            var r = CloudSaveResult.LocalOnly("{\"level\":3}");
            Assert.IsTrue(r.Success);
            Assert.IsFalse(r.HasConflict);
            Assert.AreEqual("{\"level\":3}", r.Data);
        }
    }

    [TestFixture]
    public class StubCloudSaveProviderTests {

        private StubCloudSaveProvider _provider;

        [SetUp]
        public void SetUp() {
            _provider = new StubCloudSaveProvider();
        }

        [Test]
        public void InitAsync_CallsOnCompleteTrue() {
            bool? result = null;
            _provider.InitAsync(v => result = v);
            Assert.IsTrue(result);
        }

        [Test]
        public void Save_ThenLoad_ReturnsSameData() {
            _provider.SaveAsync("slot_a", "{\"score\":100}", _ => { });
            CloudSaveResult? loaded = null;
            _provider.LoadAsync("slot_a", r => loaded = r);
            Assert.IsTrue(loaded.Value.Success);
            Assert.AreEqual("{\"score\":100}", loaded.Value.Data);
        }

        [Test]
        public void Load_MissingSlot_ReturnsNotFound() {
            CloudSaveResult? result = null;
            _provider.LoadAsync("slot_missing", r => result = r);
            Assert.IsFalse(result.Value.Success);
            Assert.AreEqual(CloudSaveErrorCode.NotFound, result.Value.ErrorCode);
        }

        [Test]
        public void Delete_ThenLoad_ReturnsNotFound() {
            _provider.SaveAsync("slot_b", "{}", _ => { });
            _provider.DeleteAsync("slot_b", _ => { });
            CloudSaveResult? result = null;
            _provider.LoadAsync("slot_b", r => result = r);
            Assert.IsFalse(result.Value.Success);
            Assert.AreEqual(CloudSaveErrorCode.NotFound, result.Value.ErrorCode);
        }
    }
}
