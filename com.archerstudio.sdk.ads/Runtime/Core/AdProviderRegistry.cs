using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// Maps a mediation platform to the provider that serves it.
    ///
    /// Each provider ships in its own package (com.archerstudio.sdk.ads.max, .admob,
    /// .levelplay) and registers here from a RuntimeInitializeOnLoadMethod. That keeps
    /// this package free of vendor references: a project installs only the mediation it
    /// actually ships, and AdManager still resolves the provider by config.
    /// </summary>
    public static class AdProviderRegistry {

        public delegate IAdProvider ProviderFactory();

        private static readonly Dictionary<AdMediationPlatform, ProviderFactory> Factories =
            new Dictionary<AdMediationPlatform, ProviderFactory>();

        /// <summary>
        /// Register the factory for a platform. Last registration wins, so a game can
        /// override a shipped provider with its own before AdManager initializes.
        /// </summary>
        public static void Register(AdMediationPlatform platform, ProviderFactory factory) {
            if (factory == null) {
                SDKLogger.Error(Tag, $"Null factory registered for {platform}. Ignored.");
                return;
            }

            if (Factories.ContainsKey(platform)) {
                SDKLogger.Debug(Tag, $"Provider factory for {platform} replaced.");
            }

            Factories[platform] = factory;
            SDKLogger.Debug(Tag, $"Provider factory registered: {platform}");
        }

        /// <summary>
        /// Drop every registration. Tests only.
        /// </summary>
        public static void Clear() {
            Factories.Clear();
        }

        public static bool IsRegistered(AdMediationPlatform platform) {
            return Factories.ContainsKey(platform);
        }

        /// <summary>
        /// Create the provider for a platform, or null when its package is not installed.
        /// </summary>
        public static IAdProvider Create(AdMediationPlatform platform) {
            if (!Factories.TryGetValue(platform, out var factory)) return null;

            try {
                return factory();
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Provider factory for {platform} threw: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Package that supplies a platform's provider, for actionable "not installed" errors.
        /// </summary>
        public static string PackageNameFor(AdMediationPlatform platform) {
            switch (platform) {
                case AdMediationPlatform.AppLovinMax: return "com.archerstudio.sdk.ads.max";
                case AdMediationPlatform.AdMob:       return "com.archerstudio.sdk.ads.admob";
                case AdMediationPlatform.IronSource:  return "com.archerstudio.sdk.ads.levelplay";
                default:                              return "(unknown)";
            }
        }

        private const string Tag = "Ads";
    }
}
