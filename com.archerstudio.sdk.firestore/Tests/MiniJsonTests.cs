using System.Collections.Generic;
using NUnit.Framework;

namespace ArcherStudio.SDK.Firestore.Tests {

    /// <summary>
    /// MiniJson powers CallableHttpClient response parsing. These tests cover the
    /// shapes that real Cloud Functions callable responses produce.
    /// </summary>
    [TestFixture]
    public sealed class MiniJsonTests {

        [Test]
        public void Deserialize_ResultEnvelope_ReturnsDict() {
            var parsed = MiniJson.Deserialize("{\"result\":{\"ok\":true,\"count\":42}}")
                as IDictionary<string, object>;
            Assert.IsNotNull(parsed);
            var result = parsed["result"] as IDictionary<string, object>;
            Assert.IsNotNull(result);
            Assert.AreEqual(true, result["ok"]);
            Assert.AreEqual(42L, result["count"]);
        }

        [Test]
        public void Deserialize_ErrorEnvelope_ReturnsDict() {
            var parsed = MiniJson.Deserialize(
                "{\"error\":{\"status\":\"PERMISSION_DENIED\",\"message\":\"not allowed\"}}")
                as IDictionary<string, object>;
            Assert.IsNotNull(parsed);
            var err = parsed["error"] as IDictionary<string, object>;
            Assert.AreEqual("PERMISSION_DENIED", err["status"]);
            Assert.AreEqual("not allowed", err["message"]);
        }

        [Test]
        public void Deserialize_NestedObjectsAndArrays_RoundTrip() {
            var parsed = MiniJson.Deserialize(
                "{\"a\":[1,2,3],\"b\":{\"c\":\"hi\",\"d\":null},\"e\":1.5}")
                as IDictionary<string, object>;
            Assert.IsNotNull(parsed);
            var list = parsed["a"] as List<object>;
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(1L, list[0]);
            var b = parsed["b"] as IDictionary<string, object>;
            Assert.AreEqual("hi", b["c"]);
            Assert.IsNull(b["d"]);
            Assert.AreEqual(1.5, (double)parsed["e"], 0.0001);
        }

        [Test]
        public void Deserialize_EscapedStrings_DecodedCorrectly() {
            var parsed = MiniJson.Deserialize("{\"s\":\"line1\\nline2\\t\\\"quoted\\\"\"}")
                as IDictionary<string, object>;
            Assert.AreEqual("line1\nline2\t\"quoted\"", parsed["s"]);
        }

        [Test]
        public void Deserialize_EmptyObject_ReturnsEmptyDict() {
            var parsed = MiniJson.Deserialize("{}") as IDictionary<string, object>;
            Assert.IsNotNull(parsed);
            Assert.AreEqual(0, parsed.Count);
        }

        [Test]
        public void Deserialize_NullInput_ReturnsNull() {
            Assert.IsNull(MiniJson.Deserialize(null));
        }
    }
}
