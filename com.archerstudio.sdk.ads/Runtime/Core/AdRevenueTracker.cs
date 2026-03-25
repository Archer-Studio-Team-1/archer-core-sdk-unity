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
                // 1. ad_impression (Firebase built-in) — kept for MAX mediation auto-collection
                //    Firebase logs this as ad_impression event; Adjust uses AdjustAdRevenue API
                trackingManager.TrackAdRevenue(
                    data.AdPlatform, data.AdSource, data.AdFormat,
                    data.AdUnitName, data.Currency, data.Value,
                    data.Placement);

                // 2. ad_revenue (custom event) — exported to BigQuery for data analysis
                //    ad_impression default Firebase event doesn't export to BQ
                int revenueMicro = (int)(data.Value * 1_000_000);
                trackingManager.TrackAdRevenueCustomEvent(
                    data.AdPlatform, data.AdSource, data.AdUnitName,
                    data.Placement, revenueMicro);

                // Update IAA count on user profile
                trackingManager.UpdateUserProfile(p => {
                    p.IaaCount += 1;
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
