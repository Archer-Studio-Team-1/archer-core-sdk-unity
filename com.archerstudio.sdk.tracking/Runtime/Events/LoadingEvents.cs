using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class LoadingStartEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_LOADING_START;

        protected override void BuildParams(Dictionary<string, object> dict) { }
    }

    public class LoadingResultEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_LOADING_RESULT;

        private readonly int _timeoutMsec;
        private readonly int _fps;
        private readonly int _status;

        public LoadingResultEvent(int timeoutMsec, int fps, int status) {
            _timeoutMsec = timeoutMsec;
            _fps = fps;
            _status = status;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_TIMEOUT_MSEC, _timeoutMsec);
            dict.Add(TrackingConstants.PAR_FPS, _fps);
            dict.Add(TrackingConstants.PAR_STATUS, _status);
        }
    }
}
