using NUnit.Framework;
using ArcherStudio.SDK.Firestore;

namespace ArcherStudio.SDK.Firestore.Tests {

    [TestFixture]
    public sealed class ChecksumHelperTests {

        [Test]
        public void Sha256Hex_EmptyInput_ReturnsEmpty() {
            Assert.AreEqual(string.Empty, ChecksumHelper.Sha256Hex(string.Empty));
            Assert.AreEqual(string.Empty, ChecksumHelper.Sha256Hex(null));
        }

        [Test]
        public void Sha256Hex_KnownVector_MatchesRfcValue() {
            // SHA256("abc") == ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
            var hex = ChecksumHelper.Sha256Hex("abc");
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hex);
        }

        [Test]
        public void Sha256Hex_IsStableAcrossCalls() {
            const string input = "{\"hello\":\"world\"}";
            var a = ChecksumHelper.Sha256Hex(input);
            var b = ChecksumHelper.Sha256Hex(input);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void PreNormalize_StripsBomAndNormalizesLineEndings() {
            var withBom = "﻿{\r\n  \"a\": 1\r\n}";
            var normalized = ChecksumHelper.PreNormalize(withBom);
            Assert.IsFalse(normalized.StartsWith("﻿"));
            Assert.IsFalse(normalized.Contains("\r"));
        }
    }
}
