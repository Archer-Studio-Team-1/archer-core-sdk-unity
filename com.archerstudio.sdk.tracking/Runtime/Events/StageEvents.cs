using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class StageStartEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_STAGE_START;

        private readonly string _category;
        private readonly string _stageId;

        public StageStartEvent(string category, string stageId) {
            _category = category;
            _stageId = stageId;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_CATEGORY, _category);
            dict.Add(TrackingConstants.PAR_STAGE_ID, _stageId);
        }
    }

    public class StageEndEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_STAGE_END;

        private readonly string _category;
        private readonly string _stageId;
        private readonly int _duration;

        public StageEndEvent(string category, string stageId, int duration) {
            _category = category;
            _stageId = stageId;
            _duration = duration;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_CATEGORY, _category);
            dict.Add(TrackingConstants.PAR_STAGE_ID, _stageId);
            dict.Add(TrackingConstants.PAR_DURATION, _duration);
        }
    }
}
