using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ArcherStudio.SDK.DeepLink.Tests {

    [TestFixture]
    public class DeepLinkModelsTests {

        [Test]
        public void Constructor_SetsAllFields() {
            var parameters = new Dictionary<string, string> {
                { "key", "value" },
                { "foo", "bar" }
            };

            var data = new DeepLinkData(
                url: "https://example.com/path",
                source: "firebase",
                parameters: parameters);

            Assert.AreEqual("https://example.com/path", data.Url);
            Assert.AreEqual("firebase", data.Source);
            Assert.IsNotNull(data.Parameters);
            Assert.AreEqual(2, data.Parameters.Count);
            Assert.AreEqual("value", data.Parameters["key"]);
            Assert.AreEqual("bar", data.Parameters["foo"]);
        }

        [Test]
        public void ReceivedAt_IsSetToApproximatelyUtcNow() {
            DateTime before = DateTime.UtcNow;

            var data = new DeepLinkData(
                url: "https://example.com",
                source: "adjust",
                parameters: new Dictionary<string, string>());

            DateTime after = DateTime.UtcNow;

            Assert.GreaterOrEqual(data.ReceivedAt, before);
            Assert.LessOrEqual(data.ReceivedAt, after);
        }

        [Test]
        public void ToString_ContainsSourceAndUrl() {
            var data = new DeepLinkData(
                url: "myapp://open/game",
                source: "unity",
                parameters: new Dictionary<string, string>());

            string result = data.ToString();

            StringAssert.Contains("unity", result);
            StringAssert.Contains("myapp://open/game", result);
        }

        [Test]
        public void Constructor_NullParameters_DoesNotThrow() {
            var data = new DeepLinkData(
                url: "https://example.com",
                source: "test",
                parameters: null);

            Assert.AreEqual("https://example.com", data.Url);
            Assert.AreEqual("test", data.Source);
            Assert.IsNull(data.Parameters);
        }

        [Test]
        public void ToString_NullParameters_DoesNotThrow() {
            var data = new DeepLinkData(
                url: "https://example.com",
                source: "test",
                parameters: null);

            string result = data.ToString();

            Assert.IsNotNull(result);
            StringAssert.Contains("test", result);
            StringAssert.Contains("params=0", result);
        }
    }
}
