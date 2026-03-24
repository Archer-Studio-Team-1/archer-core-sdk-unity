using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class ButtonClickEvent : GameTrackingEvent {

        [Serializable]
        public struct TrackingParam {
            public bool enableTracking;
            public string category;
            public string name;
            public string desc;
        }

        public override string EventName => TrackingConstants.EVT_BUTTON_CLICK;

        private readonly string _category;
        private readonly string _name;
        private readonly string _desc;

        public ButtonClickEvent(string category, string name, string desc = null) {
            _category = category;
            _name = name;
            _desc = desc;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_CATEGORY, _category);
            dict.Add(TrackingConstants.PAR_NAME, _name);
            if (!string.IsNullOrEmpty(_desc)) dict.Add(TrackingConstants.PAR_DESC, _desc);
        }
    }
}
