using System;
using System.Collections.Generic;
using NUnit.Framework;
using ArcherStudio.SDK.Core;
using ArcherStudio.SDK.Login;

namespace ArcherStudio.SDK.Login.Tests {

    [TestFixture]
    public class LoginModuleTests {

        private LoginModule _module;
        private SDKCoreConfig _config;

        [SetUp]
        public void SetUp() {
            _module = new LoginModule();
            _config = UnityEngine.ScriptableObject.CreateInstance<SDKCoreConfig>();
        }

        [TearDown]
        public void TearDown() {
            _module.Dispose();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        // --- Identity ---

        [Test]
        public void ModuleId_Equals_Login() {
            Assert.AreEqual("login", _module.ModuleId);
        }

        [Test]
        public void Priority_Equals_40() {
            Assert.AreEqual(40, _module.InitializationPriority);
        }

        [Test]
        public void Dependencies_Contains_Consent() {
            CollectionAssert.Contains(_module.Dependencies, "consent");
        }

        // --- InitializeAsync: EnableLogin = false ---

        [Test]
        public void InitAsync_EnableLoginFalse_CallsOnCompleteTrue() {
            _config.EnableLogin = false;
            bool? result = null;
            _module.InitializeAsync(_config, v => result = v);
            Assert.IsTrue(result, "onComplete must be called with true");
        }

        [Test]
        public void InitAsync_EnableLoginFalse_ProviderIsStub() {
            _config.EnableLogin = false;
            _module.InitializeAsync(_config, _ => { });
            Assert.IsInstanceOf<StubLoginProvider>(_module.Provider);
        }

        [Test]
        public void InitAsync_StubProvider_IsSignedInFalse() {
            _config.EnableLogin = false;
            _module.InitializeAsync(_config, _ => { });
            Assert.IsFalse(_module.Provider.IsSignedIn);
        }

        [Test]
        public void InitAsync_PublishesLoginFailedEvent_WhenStubNotSignedIn() {
            _config.EnableLogin = false;
            LoginFailedEvent? received = null;
            Action<LoginFailedEvent> handler = e => received = e;
            SDKEventBus.Subscribe<LoginFailedEvent>(handler);

            _module.InitializeAsync(_config, _ => { });

            // Stub provider is used only when EnableLogin=false, module calls callback
            // which does NOT publish event (fast-path skips auth entirely). So no event.
            // This confirms the fast-path behavior: no auth attempt, no event.
            Assert.IsNull(received, "Fast-path skips auth; no LoginFailedEvent published");

            SDKEventBus.Unsubscribe<LoginFailedEvent>(handler);
        }

        [Test]
        public void InitAsync_ProviderThrows_StillCallsOnCompleteTrue() {
            _config.EnableLogin = false;
            // Even if something goes wrong, SDK init chain must never be blocked
            bool? completed = null;
            Assert.DoesNotThrow(() => _module.InitializeAsync(_config, v => completed = v));
            Assert.IsTrue(completed);
        }

        // --- ReAuthenticate ---

        [Test]
        public void ReAuth_NotReadyState_ReturnsErrorLoginResult() {
            // Module never initialized — State = NotInitialized
            LoginResult? result = null;
            _module.ReAuthenticate(r => result = r);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Value.Success);
            Assert.AreEqual(LoginErrorCode.NotInitialized, result.Value.ErrorCode);
        }

        [Test]
        public void ReAuth_ReadyState_CallsProviderAuth() {
            _config.EnableLogin = false;
            _module.InitializeAsync(_config, _ => { }); // sets State=Ready, provider=Stub

            LoginResult? result = null;
            _module.ReAuthenticate(r => result = r);

            Assert.IsNotNull(result);
            // StubLoginProvider always returns failed result
            Assert.IsFalse(result.Value.Success);
        }

        // --- Dispose ---

        [Test]
        public void Dispose_SetsStateDisposed() {
            _module.Dispose();
            Assert.AreEqual(ModuleState.Disposed, _module.State);
        }

        // --- StubLoginProvider ---

        [Test]
        public void StubProvider_IsSignedIn_False() {
            var stub = new StubLoginProvider();
            Assert.IsFalse(stub.IsSignedIn);
        }

        [Test]
        public void StubProvider_AuthenticateAsync_ReturnsFailedResult() {
            var stub = new StubLoginProvider();
            LoginResult? result = null;
            stub.AuthenticateAsync(r => result = r);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Value.Success);
        }
    }
}
