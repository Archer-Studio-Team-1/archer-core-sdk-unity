#if HAS_FIREBASE_DYNAMIC_LINKS
using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using Firebase.DynamicLinks;

namespace ArcherStudio.SDK.DeepLink {

    /// <summary>
    /// Firebase Dynamic Links deep link provider.
    /// Only compiled when HAS_FIREBASE_DYNAMIC_LINKS is defined.
    /// </summary>
    public class FirebaseDeepLinkProvider : IDeepLinkProvider {
        private const string Tag = "DeepLink:Firebase";

        public bool IsInitialized { get; private set; }
        public event Action<DeepLinkData> OnDeepLinkReceived;

        public void Initialize(Action<bool> onComplete) {
            try {
                DynamicLinks.DynamicLinkReceived += OnDynamicLinkReceived;
                IsInitialized = true;
                SDKLogger.Info(Tag, "Firebase Dynamic Links provider initialized.");
                onComplete?.Invoke(true);
            } catch (Exception ex) {
                SDKLogger.Error(Tag, $"Failed to initialize Firebase Dynamic Links: {ex.Message}");
                SDKLogger.Exception(Tag, ex);
                IsInitialized = false;
                onComplete?.Invoke(false);
            }
        }

        private void OnDynamicLinkReceived(object sender, ReceivedDynamicLinkEventArgs args) {
            string url = args.ReceivedDynamicLink.Url?.ToString();
            if (string.IsNullOrEmpty(url)) {
                SDKLogger.Warning(Tag, "Received dynamic link with empty URL.");
                return;
            }

            SDKLogger.Debug(Tag, $"Firebase dynamic link received: {url}");

            IReadOnlyDictionary<string, string> parameters =
                DeepLinkParser.ExtractQueryParameters(url);

            var data = new DeepLinkData(url, "firebase", parameters);
            OnDeepLinkReceived?.Invoke(data);
        }

        public void Dispose() {
            DynamicLinks.DynamicLinkReceived -= OnDynamicLinkReceived;
            IsInitialized = false;
        }
    }
}
#endif
