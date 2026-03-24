using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// Ad impression event (legacy — use TrackingManager.TrackAdRevenue() instead).
    /// TrackAdRevenue routes to all providers: Firebase logs "ad_impression",
    /// Adjust uses AdjustAdRevenue API.
    /// </summary>
    public class AdImpressionEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_AD_IMPRESSION;

        private readonly string _adPlatform;
        private readonly string _adSource;
        private readonly string _adFormat;
        private readonly string _adUnitName;
        private readonly string _currency;
        private readonly double _value;
        private readonly string _placement;

        public AdImpressionEvent(
            string adPlatform, string adSource, string adFormat,
            string adUnitName, string currency, double value, string placement) {
            _adPlatform = adPlatform;
            _adSource = adSource;
            _adFormat = adFormat;
            _adUnitName = adUnitName;
            _currency = currency;
            _value = value;
            _placement = placement;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_AD_PLATFORM, _adPlatform);
            dict.Add(TrackingConstants.PAR_AD_SOURCE, _adSource);
            dict.Add(TrackingConstants.PAR_AD_FORMAT, _adFormat);
            dict.Add(TrackingConstants.PAR_AD_UNIT_NAME, _adUnitName);
            dict.Add(TrackingConstants.PAR_CURRENCY, _currency);
            dict.Add(TrackingConstants.PAR_VALUE, _value);
            dict.Add(TrackingConstants.PAR_PLACEMENT, _placement);
        }
    }
}
