using System;
using System.Collections.Generic;
using NUnit.Framework;
using ArcherStudio.SDK.Firestore;

namespace ArcherStudio.SDK.Firestore.Tests {

    [TestFixture]
    public sealed class SaveRepositoryTests {

        private sealed class FakeService : IFirestoreService {
            public string LastWritePath;
            public IReadOnlyDictionary<string, object> LastWriteData;
            public string LastReadPath;
            public IReadOnlyDictionary<string, object> StubReadResponse;

            public bool IsAvailable => true;
            public string CurrentFirebaseUid => "test-uid";

            public void GetDocumentAsync(string path,
                                          Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete) {
                LastReadPath = path;
                onComplete?.Invoke(StubReadResponse != null
                    ? FirestoreResult<IReadOnlyDictionary<string, object>>.Succeeded(StubReadResponse)
                    : FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(FirestoreErrorCode.NotFound));
            }

            public void SetDocumentAsync(string path, IReadOnlyDictionary<string, object> data,
                                          Action<FirestoreResult<bool>> onComplete) {
                LastWritePath = path;
                LastWriteData = data;
                onComplete?.Invoke(FirestoreResult<bool>.Succeeded(true));
            }

            public void CallFunctionAsync(string name, IReadOnlyDictionary<string, object> payload,
                                           Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete)
                => onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                    FirestoreErrorCode.Unavailable, "not used in this test"));

            public IDisposable Listen(string path, Action<IReadOnlyDictionary<string, object>> onSnapshot)
                => new NoOp();
            private sealed class NoOp : IDisposable { public void Dispose() { } }
        }

        [Test]
        public void SaveFeatureAsync_NullFeatureName_ReturnsInvalidArgument() {
            var svc = new FakeService();
            var repo = new SaveRepository(svc);
            FirestoreResult<bool> result = default;
            repo.SaveFeatureAsync(null, new Dictionary<string, object>(), 1, r => result = r);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(FirestoreErrorCode.InvalidArgument, result.ErrorCode);
        }

        [Test]
        public void SaveFeatureAsync_WritesCorrectPathAndShape() {
            var svc = new FakeService();
            var repo = new SaveRepository(svc);
            FirestoreResult<bool> result = default;
            repo.SaveFeatureAsync("stage", new Dictionary<string, object> {
                { "Id", 5L }, { "score", 1234L },
            }, 2, r => result = r);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("users/{uid}/saves/stage", svc.LastWritePath);
            Assert.AreEqual(2L, svc.LastWriteData["schemaVersion"]);
            Assert.AreEqual("client", svc.LastWriteData["updatedBy"]);
            var dataField = (IReadOnlyDictionary<string, object>)svc.LastWriteData["data"];
            Assert.AreEqual(5L, dataField["Id"]);
        }

        [Test]
        public void LoadFeatureAsync_NotFound_ReturnsNotFound() {
            var svc = new FakeService();
            var repo = new SaveRepository(svc);
            FirestoreResult<SavedFeatureSnapshot> result = default;
            repo.LoadFeatureAsync("stage", r => result = r);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(FirestoreErrorCode.NotFound, result.ErrorCode);
        }

        [Test]
        public void LoadFeatureAsync_ExistingDoc_ParsesShape() {
            var svc = new FakeService {
                StubReadResponse = new Dictionary<string, object> {
                    { "schemaVersion", 3L },
                    { "updatedBy", "client" },
                    { "data", new Dictionary<string, object> { { "Id", 5L } } },
                },
            };
            var repo = new SaveRepository(svc);
            FirestoreResult<SavedFeatureSnapshot> result = default;
            repo.LoadFeatureAsync("stage", r => result = r);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("stage", result.Data.FeatureName);
            Assert.AreEqual(3, result.Data.SchemaVersion);
            Assert.AreEqual("client", result.Data.UpdatedBy);
            Assert.AreEqual(5L, result.Data.Data["Id"]);
        }
    }
}
