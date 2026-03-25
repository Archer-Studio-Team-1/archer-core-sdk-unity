using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// Base class for v2 resource events (earn_resource, spend_resource).
    /// Params: resource_id, source_id, source_type, value
    /// </summary>
    public abstract class ResourceEvent : GameTrackingEvent {
        protected readonly string ResourceId;
        protected readonly string SourceId;
        protected readonly string SourceType;
        protected readonly int Value;

        protected ResourceEvent(string resourceId, string sourceId, string sourceType, int value) {
            ResourceId = resourceId ?? "Null";
            SourceId = sourceId ?? "Null";
            SourceType = sourceType ?? "Null";
            Value = value;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_RESOURCE_ID, ResourceId);
            dict.Add(TrackingConstants.PAR_SOURCE_ID, SourceId);
            dict.Add(TrackingConstants.PAR_SOURCE_TYPE, SourceType);
            dict.Add(TrackingConstants.PAR_VALUE, Value);
        }
    }

    public class EarnResourceEvent : ResourceEvent {
        public override string EventName => TrackingConstants.EVT_EARN_RESOURCE;

        public EarnResourceEvent(string resourceId, string sourceId, string sourceType, int value)
            : base(resourceId, sourceId, sourceType, value) { }
    }

    public class SpendResourceEvent : ResourceEvent {
        public override string EventName => TrackingConstants.EVT_SPEND_RESOURCE;

        public SpendResourceEvent(string resourceId, string sourceId, string sourceType, int value)
            : base(resourceId, sourceId, sourceType, value) { }
    }

    [Serializable]
    public enum ResourceEventType {
        Undefined = 0,
        Earn = 1,
        Buy = 2,
        Spend = 3
    }

    [Serializable]
    public enum ResourceCategory {
        [Description("Undefined")] Undefined = 0,
        [Description("Currency")] Currency = 1,
        [Description("Fragment")] Fragment = 2,
        [Description("Equipment")] Equipment = 3,
        [Description("Item")] Item = 4
    }

    public struct TrackingSource : IEquatable<TrackingSource> {
        public ResourceEventType Type;
        public string Source;
        public string SourceId;
        public string SourceType;

        public TrackingSource(ResourceEventType type, string source, string sourceId,
            string sourceType = null) {
            Type = type;
            Source = source;
            SourceId = sourceId;
            SourceType = sourceType ?? TrackingConstants.SOURCE_TYPE_FREE;
        }

        public static TrackingSource Null => new(ResourceEventType.Undefined, null, null);
        public static TrackingSource Zero => new(ResourceEventType.Undefined, "", "");

        public bool Equals(TrackingSource other) {
            return Type == other.Type && Source == other.Source && SourceId == other.SourceId;
        }

        public override bool Equals(object obj) {
            return obj is TrackingSource other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine((int)Type, Source, SourceId);
        }
    }

    public struct ResourceTrackingData {
        public string ItemCategory;
        public string ItemId;
        public TrackingSource TrackingSource;

        public ResourceTrackingData(ResourceCategory itemCategory, string itemId,
            TrackingSource trackingSource) {
            ItemCategory = itemCategory.ToString();
            ItemId = itemId;
            TrackingSource = trackingSource;
        }

        public static ResourceTrackingData Null =>
            new(ResourceCategory.Undefined, null, TrackingSource.Null);

        public static ResourceTrackingData Zero =>
            new(ResourceCategory.Undefined, "", TrackingSource.Null);
    }
}
