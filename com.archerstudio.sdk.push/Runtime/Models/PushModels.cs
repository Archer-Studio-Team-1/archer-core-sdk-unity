using System.Collections.Generic;

namespace ArcherStudio.SDK.Push {

    /// <summary>
    /// Immutable value object representing a received push notification message.
    /// </summary>
    public readonly struct PushMessage {
        public string Title { get; }
        public string Body { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        public PushMessage(string title, string body, IReadOnlyDictionary<string, string> data) {
            Title = title;
            Body = body;
            Data = data;
        }

        public override string ToString() {
            return $"[PushMessage title=\"{Title}\" body=\"{Body}\" dataKeys={Data?.Count ?? 0}]";
        }
    }
}
