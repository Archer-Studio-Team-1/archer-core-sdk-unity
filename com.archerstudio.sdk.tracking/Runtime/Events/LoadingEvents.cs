using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// V2: loading_result event (merged loading_start + loading_result).
    /// Params: loading_time (int, msec), fps (int), loading_status (string: fail/success)
    /// </summary>
    public class LoadingResultEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_LOADING_RESULT;

        private readonly int _loadingTime;
        private readonly int _fps;
        private readonly string _loadingStatus;

        public LoadingResultEvent(int loadingTimeMsec, int fps, string loadingStatus) {
            _loadingTime = loadingTimeMsec;
            _fps = fps;
            _loadingStatus = loadingStatus ?? "Null";
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_LOADING_TIME, _loadingTime);
            dict.Add(TrackingConstants.PAR_FPS, _fps);
            dict.Add(TrackingConstants.PAR_LOADING_STATUS, _loadingStatus);
        }
    }
}
