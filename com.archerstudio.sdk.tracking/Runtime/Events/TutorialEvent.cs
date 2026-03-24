using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class TutorialEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_TUTORIAL;

        private readonly string _tutCategory;
        private readonly string _tutName;
        private readonly int _tutIndex;

        public TutorialEvent(string tutCategory, string tutName, int tutIndex) {
            _tutCategory = tutCategory;
            _tutName = tutName;
            _tutIndex = tutIndex;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_TUT_CATEGORY, _tutCategory);
            dict.Add(TrackingConstants.PAR_TUT_NAME, _tutName);
            dict.Add(TrackingConstants.PAR_TUT_INDEX, _tutIndex);
        }
    }

    public class TutorialCompleteEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_TUTORIAL;

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_TUT_CATEGORY, "tutorial");
            dict.Add(TrackingConstants.PAR_TUT_NAME, "finish");
            dict.Add(TrackingConstants.PAR_TUT_INDEX, 100);
        }
    }
}
