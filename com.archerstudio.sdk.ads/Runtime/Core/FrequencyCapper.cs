using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// Tracks ad show frequency per placement and format.
    /// Enforces cooldown and session limits from AdConfig.
    /// </summary>
    public class FrequencyCapper {
        private const string Tag = "FrequencyCap";

        private readonly int _interstitialCooldownSeconds;
        private readonly int _maxInterstitialsPerSession;
        private readonly int _maxRewardedPerSession;

        // Per-format session counts
        private readonly Dictionary<AdFormat, int> _sessionCounts = new Dictionary<AdFormat, int>();

        // Last show time per placement
        private readonly Dictionary<string, float> _lastShowTime = new Dictionary<string, float>();

        public FrequencyCapper(AdConfig config) {
            _interstitialCooldownSeconds = config != null ? config.InterstitialCooldownSeconds : 30;
            _maxInterstitialsPerSession = config != null ? config.MaxInterstitialsPerSession : 10;
            _maxRewardedPerSession = config != null ? config.MaxRewardedPerSession : 20;
        }

        public CapCheckResult CanShow(string placementId, AdFormat format) {
            // Check session limit
            int sessionCount = _sessionCounts.TryGetValue(format, out var count) ? count : 0;
            int maxPerSession = GetMaxPerSession(format);

            if (maxPerSession > 0 && sessionCount >= maxPerSession) {
                return new CapCheckResult(false,
                    $"Session limit reached ({sessionCount}/{maxPerSession}) for {format}.", 0);
            }

            // Check cooldown for interstitials
            if (format == AdFormat.Interstitial && _interstitialCooldownSeconds > 0) {
                if (_lastShowTime.TryGetValue(placementId, out float lastTime)) {
                    float elapsed = Time.realtimeSinceStartup - lastTime;
                    int remaining = Mathf.CeilToInt(_interstitialCooldownSeconds - elapsed);
                    if (remaining > 0) {
                        return new CapCheckResult(false,
                            $"Cooldown active ({remaining}s remaining).", remaining);
                    }
                }
            }

            return new CapCheckResult(true, null, 0);
        }

        public void RecordShow(string placementId, AdFormat format) {
            // Increment session count
            if (!_sessionCounts.ContainsKey(format)) _sessionCounts[format] = 0;
            _sessionCounts[format]++;

            // Record time
            _lastShowTime[placementId] = Time.realtimeSinceStartup;

            SDKLogger.Debug(Tag,
                $"Recorded show: {placementId} ({format}). " +
                $"Session count: {_sessionCounts[format]}");
        }

        private int GetMaxPerSession(AdFormat format) {
            return format switch {
                AdFormat.Interstitial => _maxInterstitialsPerSession,
                AdFormat.Rewarded => _maxRewardedPerSession,
                _ => 0 // No limit for banner, app open, native
            };
        }

        public readonly struct CapCheckResult {
            public bool IsAllowed { get; }
            public string Reason { get; }
            public int SecondsUntilAllowed { get; }

            public CapCheckResult(bool isAllowed, string reason, int secondsUntilAllowed) {
                IsAllowed = isAllowed;
                Reason = reason;
                SecondsUntilAllowed = secondsUntilAllowed;
            }
        }
    }
}
