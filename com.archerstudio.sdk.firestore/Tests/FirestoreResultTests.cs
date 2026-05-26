using NUnit.Framework;
using ArcherStudio.SDK.Firestore;

namespace ArcherStudio.SDK.Firestore.Tests {

    [TestFixture]
    public sealed class FirestoreResultTests {

        [Test]
        public void Succeeded_HasDataAndNoError() {
            var r = FirestoreResult<int>.Succeeded(42);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(42, r.Data);
            Assert.AreEqual(FirestoreErrorCode.None, r.ErrorCode);
        }

        [Test]
        public void Failed_HasErrorCodeAndNoData() {
            var r = FirestoreResult<string>.Failed(FirestoreErrorCode.NotFound, "missing");
            Assert.IsFalse(r.Success);
            Assert.IsNull(r.Data);
            Assert.AreEqual(FirestoreErrorCode.NotFound, r.ErrorCode);
            Assert.AreEqual("missing", r.ErrorMessage);
        }
    }
}
