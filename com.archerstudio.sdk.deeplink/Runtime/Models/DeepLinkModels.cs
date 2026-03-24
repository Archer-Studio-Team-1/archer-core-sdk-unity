using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.DeepLink {

    /// <summary>
    /// Immutable value object representing a received deep link with parsed metadata.
    /// </summary>
    public readonly struct DeepLinkData {
        public string Url { get; }
        public string Source { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }
        public DateTime ReceivedAt { get; }

        public DeepLinkData(string url, string source, IReadOnlyDictionary<string, string> parameters) {
            Url = url;
            Source = source;
            Parameters = parameters;
            ReceivedAt = DateTime.UtcNow;
        }

        public override string ToString() {
            return $"[DeepLink source={Source} url={Url} params={Parameters?.Count ?? 0}]";
        }
    }
}
