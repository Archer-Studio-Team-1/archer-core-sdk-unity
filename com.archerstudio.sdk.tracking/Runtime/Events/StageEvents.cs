using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// V2: stage_start — only stage_id (removed category).
    /// </summary>
    public class StageStartEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_STAGE_START;

        private readonly string _stageId;

        public StageStartEvent(string stageId) {
            _stageId = stageId ?? "Null";
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_STAGE_ID, _stageId);
        }
    }

    /// <summary>
    /// V2: stage_end — stage_start_timestamp, stage_id, result (Win/Exit), duration (msec).
    /// </summary>
    public class StageEndEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_STAGE_END;

        private readonly int _stageStartTimestamp;
        private readonly string _stageId;
        private readonly string _result;
        private readonly int _duration;

        public StageEndEvent(int stageStartTimestamp, string stageId, string result, int durationMsec) {
            _stageStartTimestamp = stageStartTimestamp;
            _stageId = stageId ?? "Null";
            _result = result ?? "Null";
            _duration = durationMsec;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_STAGE_START_TIMESTAMP, _stageStartTimestamp);
            dict.Add(TrackingConstants.PAR_STAGE_ID, _stageId);
            dict.Add(TrackingConstants.PAR_RESULT, _result);
            dict.Add(TrackingConstants.PAR_DURATION, _duration);
        }
    }
}
