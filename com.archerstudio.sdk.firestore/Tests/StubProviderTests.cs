using System.Collections.Generic;
using NUnit.Framework;
using ArcherStudio.SDK.Firestore;

namespace ArcherStudio.SDK.Firestore.Tests {

    [TestFixture]
    public sealed class StubProviderTests {

        [Test]
        public void Get_Returns_NotAuthenticated() {
            var stub = new FirestoreModule();
            stub.InitializeAsync(UnityEngine.ScriptableObject.CreateInstance<ArcherStudio.SDK.Core.SDKCoreConfig>(), _ => { });

            FirestoreResult<IReadOnlyDictionary<string, object>> captured = default;
            stub.Service.GetDocumentAsync("users/{uid}", r => { captured = r; });
            Assert.IsFalse(captured.Success);
            Assert.AreEqual(FirestoreErrorCode.NotAuthenticated, captured.ErrorCode);
        }

        [Test]
        public void CallFunction_Returns_NotAuthenticated() {
            var stub = new FirestoreModule();
            stub.InitializeAsync(UnityEngine.ScriptableObject.CreateInstance<ArcherStudio.SDK.Core.SDKCoreConfig>(), _ => { });

            FirestoreResult<IReadOnlyDictionary<string, object>> captured = default;
            stub.Service.CallFunctionAsync("ping", null, r => { captured = r; });
            Assert.IsFalse(captured.Success);
            Assert.AreEqual(FirestoreErrorCode.NotAuthenticated, captured.ErrorCode);
        }

        [Test]
        public void Listen_Returns_DisposableNoop() {
            var stub = new FirestoreModule();
            stub.InitializeAsync(UnityEngine.ScriptableObject.CreateInstance<ArcherStudio.SDK.Core.SDKCoreConfig>(), _ => { });

            using (var handle = stub.Service.Listen("users/{uid}", _ => { })) {
                Assert.IsNotNull(handle);
            }
        }
    }
}
