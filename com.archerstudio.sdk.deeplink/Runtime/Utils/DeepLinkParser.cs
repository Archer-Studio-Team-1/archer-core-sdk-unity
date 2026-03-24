using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.DeepLink {

    /// <summary>
    /// Static utility for parsing deep link URLs into their components.
    /// </summary>
    public static class DeepLinkParser {

        /// <summary>
        /// Parse a URL string into its scheme, host, path, and query parameters.
        /// Returns false if the URL is null, empty, or malformed.
        /// </summary>
        public static bool TryParse(
            string url,
            out string scheme,
            out string host,
            out string path,
            out IReadOnlyDictionary<string, string> queryParams) {

            scheme = null;
            host = null;
            path = null;
            queryParams = null;

            if (string.IsNullOrEmpty(url)) {
                return false;
            }

            try {
                var uri = new Uri(url);
                scheme = uri.Scheme;
                host = uri.Host;
                path = uri.AbsolutePath;
                queryParams = ParseQueryString(uri.Query);
                return true;
            } catch (UriFormatException) {
                return false;
            }
        }

        /// <summary>
        /// Extract query parameters from a URL string into a dictionary.
        /// Returns an empty dictionary if no parameters are found.
        /// </summary>
        public static IReadOnlyDictionary<string, string> ExtractQueryParameters(string url) {
            if (string.IsNullOrEmpty(url)) {
                return new Dictionary<string, string>();
            }

            try {
                var uri = new Uri(url);
                return ParseQueryString(uri.Query);
            } catch (UriFormatException) {
                return new Dictionary<string, string>();
            }
        }

        private static Dictionary<string, string> ParseQueryString(string query) {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(query)) {
                return result;
            }

            // Remove leading '?'
            string queryTrimmed = query.StartsWith("?") ? query.Substring(1) : query;

            if (string.IsNullOrEmpty(queryTrimmed)) {
                return result;
            }

            string[] pairs = queryTrimmed.Split('&');
            foreach (string pair in pairs) {
                if (string.IsNullOrEmpty(pair)) {
                    continue;
                }

                int equalsIndex = pair.IndexOf('=');
                if (equalsIndex < 0) {
                    string decodedKey = Uri.UnescapeDataString(pair);
                    result[decodedKey] = string.Empty;
                } else {
                    string key = Uri.UnescapeDataString(pair.Substring(0, equalsIndex));
                    string value = Uri.UnescapeDataString(pair.Substring(equalsIndex + 1));
                    result[key] = value;
                }
            }

            return result;
        }
    }
}
