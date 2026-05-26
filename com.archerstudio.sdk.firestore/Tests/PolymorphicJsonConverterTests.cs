using System.Collections.Generic;
using NUnit.Framework;
using ArcherStudio.SDK.Firestore;

namespace ArcherStudio.SDK.Firestore.Tests {

    [TestFixture]
    public sealed class PolymorphicJsonConverterTests {

        [Test]
        public void NormalizeJson_SortsKeysDeterministically() {
            var a = new Dictionary<string, object> { { "b", 1L }, { "a", 2L } };
            var b = new Dictionary<string, object> { { "a", 2L }, { "b", 1L } };
            Assert.AreEqual(PolymorphicJsonConverter.NormalizeJson(a),
                            PolymorphicJsonConverter.NormalizeJson(b));
        }

        [Test]
        public void NormalizeJson_HandlesNestedDictionaries() {
            var nested = new Dictionary<string, object> {
                { "outer", new Dictionary<string, object> {
                    { "z", 1L },
                    { "a", "hello" },
                }},
            };
            var json = PolymorphicJsonConverter.NormalizeJson(nested);
            // Inner keys must also be sorted: "a" before "z"
            var aIdx = json.IndexOf("\"a\":\"hello\"");
            var zIdx = json.IndexOf("\"z\":1");
            Assert.Greater(aIdx, 0);
            Assert.Greater(zIdx, aIdx);
        }

        [Test]
        public void ToFirestoreDict_PromotesIntToLong() {
            var input = new Dictionary<string, object> { { "n", 42 } };
            var result = PolymorphicJsonConverter.ToFirestoreDict(input);
            Assert.IsTrue(result["n"] is long);
            Assert.AreEqual(42L, result["n"]);
        }

        [Test]
        public void ToFirestoreDict_PromotesFloatToDouble() {
            var input = new Dictionary<string, object> { { "f", 1.5f } };
            var result = PolymorphicJsonConverter.ToFirestoreDict(input);
            Assert.IsTrue(result["f"] is double);
        }

        [Test]
        public void RoundTrip_PreservesDiscriminatorField() {
            // Simulate a polymorphic ability dict carrying its own _kind discriminator.
            var input = new Dictionary<string, object> {
                { "abilities", new List<object> {
                    new Dictionary<string, object> {
                        { "_kind", "duration" }, { "id", 1L }, { "duration", 5.0 },
                    },
                    new Dictionary<string, object> {
                        { "_kind", "permanent" }, { "id", 2L },
                    },
                }},
            };
            var firestoreShape = PolymorphicJsonConverter.ToFirestoreDict(input);
            var back = PolymorphicJsonConverter.FromFirestoreDict(firestoreShape);

            var abilities = (System.Collections.Generic.List<object>)back["abilities"];
            var first = (IDictionary<string, object>)abilities[0];
            Assert.AreEqual("duration", first["_kind"]);
            Assert.AreEqual(1L, first["id"]);
        }

        [Test]
        public void NormalizeJson_ChecksumStableAcrossEquivalentMaps() {
            var a = new Dictionary<string, object> {
                { "score", 100L },
                { "items", new List<object> { 1L, 2L, 3L } },
            };
            var b = new Dictionary<string, object> {
                { "items", new List<object> { 1L, 2L, 3L } },
                { "score", 100L },
            };
            var hashA = ChecksumHelper.Sha256Hex(PolymorphicJsonConverter.NormalizeJson(a));
            var hashB = ChecksumHelper.Sha256Hex(PolymorphicJsonConverter.NormalizeJson(b));
            Assert.AreEqual(hashA, hashB);
        }

        [Test]
        public void NormalizeJson_EscapesSpecialCharacters() {
            var input = new Dictionary<string, object> { { "s", "line1\nline2\t\"quote\"" } };
            var json = PolymorphicJsonConverter.NormalizeJson(input);
            Assert.IsTrue(json.Contains("\\n"));
            Assert.IsTrue(json.Contains("\\t"));
            Assert.IsTrue(json.Contains("\\\""));
        }
    }
}
