using System.Collections.Generic;
using NUnit.Framework;

namespace ArcherStudio.SDK.DeepLink.Tests {

    [TestFixture]
    public class DeepLinkParserTests {

        // ───────────────────────────────────────────────
        //  TryParse
        // ───────────────────────────────────────────────

        [Test]
        public void TryParse_FullHttpsUrl_ParsesAllComponents() {
            const string url = "https://example.com/path?key=value&foo=bar";

            bool result = DeepLinkParser.TryParse(url,
                out string scheme, out string host, out string path,
                out IReadOnlyDictionary<string, string> queryParams);

            Assert.IsTrue(result);
            Assert.AreEqual("https", scheme);
            Assert.AreEqual("example.com", host);
            Assert.AreEqual("/path", path);
            Assert.IsNotNull(queryParams);
            Assert.AreEqual(2, queryParams.Count);
            Assert.AreEqual("value", queryParams["key"]);
            Assert.AreEqual("bar", queryParams["foo"]);
        }

        [Test]
        public void TryParse_CustomScheme_ParsesCorrectly() {
            const string url = "myapp://open/game?level=5";

            bool result = DeepLinkParser.TryParse(url,
                out string scheme, out string host, out string path,
                out IReadOnlyDictionary<string, string> queryParams);

            Assert.IsTrue(result);
            Assert.AreEqual("myapp", scheme);
            Assert.AreEqual("open", host);
            Assert.AreEqual("/game", path);
            Assert.IsNotNull(queryParams);
            Assert.AreEqual(1, queryParams.Count);
            Assert.AreEqual("5", queryParams["level"]);
        }

        [Test]
        public void TryParse_NullUrl_ReturnsFalse() {
            bool result = DeepLinkParser.TryParse(null,
                out string scheme, out string host, out string path,
                out IReadOnlyDictionary<string, string> queryParams);

            Assert.IsFalse(result);
            Assert.IsNull(scheme);
            Assert.IsNull(host);
            Assert.IsNull(path);
            Assert.IsNull(queryParams);
        }

        [Test]
        public void TryParse_EmptyUrl_ReturnsFalse() {
            bool result = DeepLinkParser.TryParse(string.Empty,
                out string scheme, out string host, out string path,
                out IReadOnlyDictionary<string, string> queryParams);

            Assert.IsFalse(result);
            Assert.IsNull(scheme);
            Assert.IsNull(host);
            Assert.IsNull(path);
            Assert.IsNull(queryParams);
        }

        [Test]
        public void TryParse_MalformedUrl_ReturnsFalse() {
            const string url = "not a valid url ://???";

            bool result = DeepLinkParser.TryParse(url,
                out string scheme, out string host, out string path,
                out IReadOnlyDictionary<string, string> queryParams);

            Assert.IsFalse(result);
            Assert.IsNull(scheme);
            Assert.IsNull(host);
            Assert.IsNull(path);
            Assert.IsNull(queryParams);
        }

        [Test]
        public void TryParse_NoQueryParams_ReturnsEmptyDictionary() {
            const string url = "https://example.com/path";

            bool result = DeepLinkParser.TryParse(url,
                out string scheme, out string host, out string path,
                out IReadOnlyDictionary<string, string> queryParams);

            Assert.IsTrue(result);
            Assert.AreEqual("https", scheme);
            Assert.AreEqual("example.com", host);
            Assert.AreEqual("/path", path);
            Assert.IsNotNull(queryParams);
            Assert.AreEqual(0, queryParams.Count);
        }

        [Test]
        public void TryParse_QueryParamWithoutValue_SetsEmptyString() {
            const string url = "https://x.com?flag";

            bool result = DeepLinkParser.TryParse(url,
                out string scheme, out string host, out string path,
                out IReadOnlyDictionary<string, string> queryParams);

            Assert.IsTrue(result);
            Assert.IsNotNull(queryParams);
            Assert.IsTrue(queryParams.ContainsKey("flag"));
            Assert.AreEqual(string.Empty, queryParams["flag"]);
        }

        // ───────────────────────────────────────────────
        //  ExtractQueryParameters
        // ───────────────────────────────────────────────

        [Test]
        public void ExtractQueryParameters_ValidUrl_ReturnsCorrectDictionary() {
            const string url = "https://example.com/path?key=value&foo=bar";

            IReadOnlyDictionary<string, string> result =
                DeepLinkParser.ExtractQueryParameters(url);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("value", result["key"]);
            Assert.AreEqual("bar", result["foo"]);
        }

        [Test]
        public void ExtractQueryParameters_NullUrl_ReturnsEmptyDictionary() {
            IReadOnlyDictionary<string, string> result =
                DeepLinkParser.ExtractQueryParameters(null);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ExtractQueryParameters_NoParams_ReturnsEmptyDictionary() {
            const string url = "https://example.com/path";

            IReadOnlyDictionary<string, string> result =
                DeepLinkParser.ExtractQueryParameters(url);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ExtractQueryParameters_UrlEncodedParams_DecodesCorrectly() {
            const string url = "https://x.com?name=hello%20world";

            IReadOnlyDictionary<string, string> result =
                DeepLinkParser.ExtractQueryParameters(url);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("hello world", result["name"]);
        }
    }
}
