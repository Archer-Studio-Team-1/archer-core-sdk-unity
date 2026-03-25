using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// V2: button_click — khi người chơi click vào button.
    /// Params: category, name, desc (always included, Null default)
    /// </summary>
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
            _category = category ?? "Null";
            _name = name ?? "Null";
            _desc = desc ?? "Null";
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_CATEGORY, _category);
            dict.Add(TrackingConstants.PAR_NAME, _name);
            dict.Add(TrackingConstants.PAR_DESC, _desc);
        }
    }
}
