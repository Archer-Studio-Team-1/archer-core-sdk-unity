using ArcherStudio.SDK.Core;
using ArcherStudio.SDK.Tracking;

namespace ArcherStudio.SDK.Ads {

    /// <summary>
    /// Bridges ad revenue events to the tracking system.
    /// Routes ad revenue to all providers via TrackingManager.TrackAdRevenue().
    /// </summary>
    public class AdRevenueTracker {
        private const string Tag = "AdRevenue";

        public void OnRevenuePaid(AdRevenueData data) {
            SDKLogger.Debug(Tag,
                $"Revenue: {data.AdPlatform}/{data.AdSource} " +
                $"{data.Currency} {data.Value:F6} [{data.AdFormat}]");

            var trackingManager = TrackingManager.Instance;
            if (trackingManager != null) {
                // Track ad revenue through all providers (one call)
                // Firebase: logs "ad_impression" event
                // Adjust: uses AdjustAdRevenue API internally
                trackingManager.TrackAdRevenue(
                    data.AdPlatform, data.AdSource, data.AdFormat,
                    data.AdUnitName, data.Currency, data.Value,
                    data.Placement);

                // Update IAA status on user profile
                trackingManager.UpdateUserProfile(p => {
                    if (!p.IsIaaUser) p.IsIaaUser = true;
                    p.IaaCount = p.IaaCount + 1;
                });
            }

            // Publish SDK event for other modules
            SDKEventBus.Publish(new AdRevenueEvent(data));
        }
    }

    /// <summary>
    /// SDK event for ad revenue. Other modules can subscribe.
    /// </summary>
    public readonly struct AdRevenueEvent : ISDKEvent {
        public AdRevenueData Data { get; }

        public AdRevenueEvent(AdRevenueData data) { Data = data; }
    }
}
