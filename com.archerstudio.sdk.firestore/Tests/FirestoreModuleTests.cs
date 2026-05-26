using System.Collections.Generic;
using NUnit.Framework;
using ArcherStudio.SDK.Firestore;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Firestore.Tests {

    [TestFixture]
    public sealed class FirestoreModuleTests {

        [Test]
        public void ModuleId_Equals_Firestore() {
            var m = new FirestoreModule();
            Assert.AreEqual("firestore", m.ModuleId);
        }

        [Test]
        public void Dependencies_IncludeLogin() {
            var m = new FirestoreModule();
            Assert.Contains("login", new List<string>(m.Dependencies));
        }

        [Test]
        public void Priority_AfterLoginAndCloudSave() {
            var m = new FirestoreModule();
            Assert.GreaterOrEqual(m.InitializationPriority, 50,
                "Firestore must initialize after Login (40) and CloudSave (50).");
        }

        [Test]
        public void InitializeAsync_NoConfig_UsesStubProvider() {
            var m = new FirestoreModule();
            var coreConfig = ScriptableObject.CreateInstance<SDKCoreConfig>();
            bool completed = false;
            m.InitializeAsync(coreConfig, _ => { completed = true; });
            Assert.IsTrue(completed, "InitializeAsync must always invoke onComplete.");
            Assert.IsNotNull(m.Service, "Service must be provisioned (stub).");
            Assert.IsNotNull(m.UserRepository, "UserRepository must be provisioned.");
            Assert.IsFalse(m.Service.IsAvailable, "Stub provider should report IsAvailable=false.");
        }
    }
}
