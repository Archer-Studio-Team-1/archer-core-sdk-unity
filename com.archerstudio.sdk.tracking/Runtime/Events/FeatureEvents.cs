using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class FeatureUnlockEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_FEATURE_UNLOCK;

        private readonly string _featureId;

        public FeatureUnlockEvent(string featureId) { _featureId = featureId; }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_FEATURE_ID, _featureId);
        }
    }

    public class FeatureOpenEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_FEATURE_OPEN;

        private readonly string _featureId;

        public FeatureOpenEvent(string featureId) { _featureId = featureId; }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_FEATURE_ID, _featureId);
        }
    }

    public class FeatureCloseEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_FEATURE_CLOSE;

        private readonly string _featureId;
        private readonly int _durationFeature;

        public FeatureCloseEvent(string featureId, int durationFeature) {
            _featureId = featureId;
            _durationFeature = durationFeature;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_FEATURE_ID, _featureId);
            dict.Add(TrackingConstants.PAR_DURATION_FEATURE, _durationFeature);
        }
    }
}
