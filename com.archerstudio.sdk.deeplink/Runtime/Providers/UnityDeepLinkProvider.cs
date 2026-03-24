using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.DeepLink {

    /// <summary>
    /// Built-in Unity deep link provider. Always available without conditional compilation.
    /// Uses Application.deepLinkActivated and Application.absoluteURL.
    /// </summary>
    public class UnityDeepLinkProvider : IDeepLinkProvider {
        private const string Tag = "DeepLink:Unity";

        public bool IsInitialized { get; private set; }
        public event Action<DeepLinkData> OnDeepLinkReceived;

        public void Initialize(Action<bool> onComplete) {
            Application.deepLinkActivated += OnDeepLinkActivated;

            // Check if app was launched via deep link
            string launchUrl = Application.absoluteURL;
            if (!string.IsNullOrEmpty(launchUrl)) {
                SDKLogger.Info(Tag, $"App launched with deep link: {launchUrl}");
                ProcessDeepLink(launchUrl);
            }

            IsInitialized = true;
            SDKLogger.Info(Tag, "Unity deep link provider initialized.");
            onComplete?.Invoke(true);
        }

        private void OnDeepLinkActivated(string url) {
            SDKLogger.Debug(Tag, $"Deep link activated: {url}");
            ProcessDeepLink(url);
        }

        private void ProcessDeepLink(string url) {
            if (string.IsNullOrEmpty(url)) {
                return;
            }

            IReadOnlyDictionary<string, string> parameters =
                DeepLinkParser.ExtractQueryParameters(url);

            var data = new DeepLinkData(url, "unity", parameters);
            OnDeepLinkReceived?.Invoke(data);
        }

        public void Dispose() {
            Application.deepLinkActivated -= OnDeepLinkActivated;
            IsInitialized = false;
        }
    }
}
