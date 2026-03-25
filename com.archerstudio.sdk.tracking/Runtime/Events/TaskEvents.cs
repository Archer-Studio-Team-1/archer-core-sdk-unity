using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// V2: task_end event — trigger khi player claim task.
    /// Dùng để đo drop_rate qua từng task.
    /// </summary>
    public class TaskEndEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_TASK_END;

        private readonly string _taskId;
        private readonly string _taskName;
        private readonly string _stageId;

        public TaskEndEvent(string taskId, string taskName, string stageId) {
            _taskId = taskId ?? "Null";
            _taskName = taskName ?? "Null";
            _stageId = stageId ?? "Null";
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_TASK_ID, _taskId);
            dict.Add(TrackingConstants.PAR_TASK_NAME, _taskName);
            dict.Add(TrackingConstants.PAR_STAGE_ID, _stageId);
        }
    }
}
