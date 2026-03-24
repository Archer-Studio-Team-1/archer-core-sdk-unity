using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// Generic event for ad-hoc tracking without creating a dedicated class.
    /// </summary>
    public class GenericGameTrackingEvent : GameTrackingEvent {
        private readonly string _eventName;
        private readonly Dictionary<string, object> _params;

        public override string EventName => _eventName;

        public GenericGameTrackingEvent(string eventName, Dictionary<string, object> parameters = null) {
            _eventName = eventName;
            _params = parameters;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            if (_params == null) return;
            foreach (var kvp in _params) {
                dict[kvp.Key] = kvp.Value;
            }
        }
    }
}
