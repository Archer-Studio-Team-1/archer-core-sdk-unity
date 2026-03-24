using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class ExplorationStartEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_EXPLORATION_START;

        private readonly string _bossLevel;

        public ExplorationStartEvent(string bossLevel) { _bossLevel = bossLevel; }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_BOSS_LEVEL, _bossLevel);
        }
    }

    public class ExplorationEndEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_EXPLORATION_END;

        private readonly string _bossLevel;
        private readonly int _result;

        public ExplorationEndEvent(string bossLevel, int result) {
            _bossLevel = bossLevel;
            _result = result;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_BOSS_LEVEL, _bossLevel);
            dict.Add(TrackingConstants.PAR_RESULT, _result);
        }
    }

    public class ExplorationRankUpEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_EXPLORATION_RANK_UP;

        private readonly string _bossLevel;
        private readonly string _rankLevel;

        public ExplorationRankUpEvent(string bossLevel, string rankLevel) {
            _bossLevel = bossLevel;
            _rankLevel = rankLevel;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_BOSS_LEVEL, _bossLevel);
            dict.Add(TrackingConstants.PAR_RANK_LEVEL, _rankLevel);
        }
    }
}
