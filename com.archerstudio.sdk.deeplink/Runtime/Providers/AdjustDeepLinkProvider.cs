#if HAS_ADJUST_SDK
using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using AdjustSdk;

namespace ArcherStudio.SDK.DeepLink {

    /// <summary>
    /// Adjust deep link provider. Uses Adjust deferred deep link callback
    /// and supports ProcessDeeplink for reattribution.
    /// Only compiled when HAS_ADJUST_SDK is defined.
    /// </summary>
    public class AdjustDeepLinkProvider : IDeepLinkProvider {
        private const string Tag = "DeepLink:Adjust";

        public bool IsInitialized { get; private set; }
        public event Action<DeepLinkData> OnDeepLinkReceived;

        private readonly bool _enableDeferredDeepLinks;

        public AdjustDeepLinkProvider(bool enableDeferredDeepLinks) {
            _enableDeferredDeepLinks = enableDeferredDeepLinks;
        }

        public void Initialize(Action<bool> onComplete) {
            try {
                // Note: Deep link callbacks are typically set on AdjustConfig before InitSdk.
                // This provider handles processing deep links that arrive at runtime.
                IsInitialized = true;
                SDKLogger.Info(Tag, "Adjust deep link provider initialized.");
                onComplete?.Invoke(true);
            } catch (Exception ex) {
                SDKLogger.Error(Tag, $"Failed to initialize Adjust deep links: {ex.Message}");
                SDKLogger.Exception(Tag, ex);
                IsInitialized = false;
                onComplete?.Invoke(false);
            }
        }

        /// <summary>
        /// Process an incoming deep link URL for Adjust reattribution.
        /// Call this when the app receives a deep link from any source.
        /// </summary>
        public void ProcessDeeplink(string url) {
            if (string.IsNullOrEmpty(url)) return;

            var deeplink = new AdjustDeeplink(url);
            Adjust.ProcessDeeplink(deeplink);
            SDKLogger.Debug(Tag, $"Deep link sent to Adjust for processing: {url}");

            ProcessUrl(url);
        }

        /// <summary>
        /// Process a deep link and resolve the final URL via callback.
        /// Useful for shortened/wrapped URLs.
        /// </summary>
        public void ProcessAndResolveDeeplink(string url, Action<string> callback) {
            if (string.IsNullOrEmpty(url)) {
                callback?.Invoke(null);
                return;
            }

            var deeplink = new AdjustDeeplink(url);
            Adjust.ProcessAndResolveDeeplink(deeplink, resolvedUrl => {
                SDKLogger.Debug(Tag, $"Resolved URL: {resolvedUrl}");
                callback?.Invoke(resolvedUrl);

                if (!string.IsNullOrEmpty(resolvedUrl)) {
                    ProcessUrl(resolvedUrl);
                }
            });
        }

        /// <summary>
        /// Handle deep link data from Adjust's deferred deep link callback.
        /// Called internally when AdjustConfig delivers a deferred deep link.
        /// </summary>
        public void OnAdjustDeepLink(AdjustDeeplink deeplink) {
            string url = deeplink?.Deeplink;
            if (string.IsNullOrEmpty(url)) {
                SDKLogger.Warning(Tag, "Received Adjust deep link with empty URL.");
                return;
            }

            SDKLogger.Debug(Tag, $"Adjust deep link received: {url}");
            ProcessUrl(url);
        }

        /// <summary>
        /// Handle deferred deep link from Adjust.
        /// </summary>
        public void OnAdjustDeferredDeepLink(AdjustDeeplink deeplink) {
            string url = deeplink?.Deeplink;
            if (string.IsNullOrEmpty(url)) {
                SDKLogger.Warning(Tag, "Received Adjust deferred deep link with empty URL.");
                return;
            }

            SDKLogger.Debug(Tag, $"Adjust deferred deep link received: {url}");
            ProcessUrl(url);
        }

        /// <summary>
        /// Get the last deep link URL that opened the app.
        /// </summary>
        public void GetLastDeeplink(Action<string> callback) {
            Adjust.GetLastDeeplink(callback);
        }

        private void ProcessUrl(string url) {
            IReadOnlyDictionary<string, string> parameters =
                DeepLinkParser.ExtractQueryParameters(url);

            var data = new DeepLinkData(url, "adjust", parameters);
            OnDeepLinkReceived?.Invoke(data);
        }

        public void Dispose() {
            IsInitialized = false;
        }
    }
}
#endif
