using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// Custom ad_revenue event per tracking_v2 spec.
    /// Tracks ad revenue data to BigQuery (ad_impression default Firebase event doesn't export to BQ).
    /// </summary>
    public class AdRevenueCustomEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_AD_REVENUE;

        private readonly string _adMediation;
        private readonly string _adSource;
        private readonly string _adUnitId;
        private readonly string _placement;
        private readonly int _iaaRevenueMicro;

        public AdRevenueCustomEvent(string adMediation, string adSource, string adUnitId,
            string placement, int iaaRevenueMicro) {
            _adMediation = adMediation;
            _adSource = adSource;
            _adUnitId = adUnitId;
            _placement = placement;
            _iaaRevenueMicro = iaaRevenueMicro;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_AD_MEDIATION, _adMediation ?? "Null");
            dict.Add(TrackingConstants.PAR_AD_SOURCE, _adSource ?? "Null");
            dict.Add(TrackingConstants.PAR_AD_UNIT_ID, _adUnitId ?? "Null");
            dict.Add(TrackingConstants.PAR_PLACEMENT, _placement ?? "Null");
            dict.Add(TrackingConstants.PAR_IAA_REVENUE_MICRO, _iaaRevenueMicro);
        }
    }
}
