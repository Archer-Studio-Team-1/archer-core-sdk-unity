using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking {

    /// <summary>
    /// Base class for all tracking events.
    /// Subclasses define EventName and build parameters via BuildParams().
    /// </summary>
    public abstract class GameTrackingEvent {
        public abstract string EventName { get; }

        /// <summary>
        /// Adjust event token for custom events only.
        /// Do NOT use for purchase (use TrackIAPRevenue) or
        /// ad revenue (use TrackAdRevenue).
        /// </summary>
        public virtual string AdjustToken => null;

        /// <summary>
        /// Deduplication ID to prevent duplicate Adjust events.
        /// </summary>
        public virtual string DeduplicationId => null;

        /// <summary>
        /// Callback ID for tracking event success/failure in Adjust.
        /// </summary>
        public virtual string CallbackId => null;

        public Dictionary<string, object> ToParams() {
            var dict = new Dictionary<string, object>();
            BuildParams(dict);
            return dict;
        }

        public void FillParams(Dictionary<string, object> dict) {
            BuildParams(dict);
        }

        protected abstract void BuildParams(Dictionary<string, object> dict);
    }
}
