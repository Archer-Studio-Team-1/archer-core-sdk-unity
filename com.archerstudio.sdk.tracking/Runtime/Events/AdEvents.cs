using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class AdClickEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_AD_CLICK;

        private readonly string _adPlatform;
        private readonly string _adSource;
        private readonly string _adFormat;
        private readonly string _adUnitName;
        private readonly string _placement;

        public AdClickEvent(string adPlatform, string adSource, string adFormat,
            string adUnitName, string placement) {
            _adPlatform = adPlatform;
            _adSource = adSource;
            _adFormat = adFormat;
            _adUnitName = adUnitName;
            _placement = placement;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_AD_PLATFORM, _adPlatform);
            dict.Add(TrackingConstants.PAR_AD_SOURCE, _adSource);
            dict.Add(TrackingConstants.PAR_AD_FORMAT, _adFormat);
            dict.Add(TrackingConstants.PAR_AD_UNIT_NAME, _adUnitName);
            dict.Add(TrackingConstants.PAR_PLACEMENT, _placement);
        }
    }

    public class AdCompleteEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_AD_COMPLETE;

        private readonly string _adPlatform;
        private readonly string _adSource;
        private readonly string _adFormat;
        private readonly string _adUnitName;
        private readonly string _placement;
        private readonly string _endType;
        private readonly int _adDuration;

        public AdCompleteEvent(string adPlatform, string adSource, string adFormat,
            string adUnitName, string placement, string endType, int adDuration) {
            _adPlatform = adPlatform;
            _adSource = adSource;
            _adFormat = adFormat;
            _adUnitName = adUnitName;
            _placement = placement;
            _endType = endType;
            _adDuration = adDuration;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_AD_PLATFORM, _adPlatform);
            dict.Add(TrackingConstants.PAR_AD_SOURCE, _adSource);
            dict.Add(TrackingConstants.PAR_AD_FORMAT, _adFormat);
            dict.Add(TrackingConstants.PAR_AD_UNIT_NAME, _adUnitName);
            dict.Add(TrackingConstants.PAR_PLACEMENT, _placement);
            dict.Add(TrackingConstants.PAR_END_TYPE, _endType);
            dict.Add(TrackingConstants.PAR_AD_DURATION, _adDuration);
        }
    }
}
