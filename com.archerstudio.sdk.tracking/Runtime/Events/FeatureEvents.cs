using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// V2: feature_unlock — trigger khi player unlock feature.
    /// Removed: feature_open, feature_close (v2 spec)
    /// </summary>
    public class FeatureUnlockEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_FEATURE_UNLOCK;

        private readonly string _featureId;

        public FeatureUnlockEvent(string featureId) {
            _featureId = featureId ?? "Null";
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_FEATURE_ID, _featureId);
        }
    }
}
