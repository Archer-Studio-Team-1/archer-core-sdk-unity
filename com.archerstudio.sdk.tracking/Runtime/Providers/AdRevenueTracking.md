Tracking Ad revenue Firebase cho AppLovin Max:

    // Attach callbacks based on the ad format(s) you are using
    MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;
    MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;
    MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;
    MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;
    private void OnAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo impressionData)
    {
    double revenue = impressionData.Revenue;
    var impressionParameters = new[] {
    new Firebase.Analytics.Parameter("ad_platform", "AppLovin"),
    new Firebase.Analytics.Parameter("ad_source", impressionData.NetworkName),
    new Firebase.Analytics.Parameter("ad_unit_name", impressionData.AdUnitIdentifier),
    new Firebase.Analytics.Parameter("ad_format", impressionData.AdFormat),
    new Firebase.Analytics.Parameter("value", revenue),
    new Firebase.Analytics.Parameter("currency", "USD"), // All AppLovin revenue is sent in USD
    };
    Firebase.Analytics.FirebaseAnalytics.LogEvent("ad_impression", impressionParameters);
    }

Tracking Ad revenue Adjust cho AppLovin Max:

var adRevenue = new AdjustAdRevenue(source);
adRevenue.SetRevenue(revenue, currency);

            if (adImpressionsCount.HasValue) {
                adRevenue.AdImpressionsCount = adImpressionsCount.Value;
            }
            if (!string.IsNullOrEmpty(adRevenueNetwork)) {
                adRevenue.AdRevenueNetwork = adRevenueNetwork;
            }
            if (!string.IsNullOrEmpty(adRevenueUnit)) {
                adRevenue.AdRevenueUnit = adRevenueUnit;
            }
            if (!string.IsNullOrEmpty(adRevenuePlacement)) {
                adRevenue.AdRevenuePlacement = adRevenuePlacement;
            }

            Adjust.TrackAdRevenue(adRevenue);
            SDKLogger.Debug("Adjust", $"Ad revenue tracked: {source} {currency} {revenue:F6}");



