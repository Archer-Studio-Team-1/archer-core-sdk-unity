using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ArcherStudio.SDK.Tracking.Events {

    public abstract class ResourceEvent : GameTrackingEvent {
        protected readonly ResourceTrackingData Data;
        protected readonly ulong Value;
        protected readonly ulong RemainingValue;

        protected ResourceEvent(ResourceTrackingData data, ulong value, ulong remainingValue) {
            Data = data;
            Value = value;
            RemainingValue = remainingValue;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_ITEM_CATEGORY, Data.ItemCategory);
            dict.Add(TrackingConstants.PAR_ITEM_ID, Data.ItemId);
            dict.Add(TrackingConstants.PAR_SOURCE, Data.TrackingSource.Source);
            if (!string.IsNullOrEmpty(Data.TrackingSource.SourceId)) {
                dict.Add(TrackingConstants.PAR_SOURCE_ID, Data.TrackingSource.SourceId);
            }
            dict.Add(TrackingConstants.PAR_VALUE, Value);
            dict.Add(TrackingConstants.PAR_REMAINING_VALUE, RemainingValue);
        }
    }

    public class EarnResourceEvent : ResourceEvent {
        public override string EventName => TrackingConstants.EVT_EARN_RESOURCE;

        private readonly ulong _totalEarnValue;

        public EarnResourceEvent(ResourceTrackingData data, ulong value,
            ulong remainingValue, ulong totalEarnValue)
            : base(data, value, remainingValue) {
            _totalEarnValue = totalEarnValue;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            base.BuildParams(dict);
            dict.Add(TrackingConstants.PAR_TOTAL_EARN_VALUE, _totalEarnValue);
        }
    }

    public class BuyResourceEvent : ResourceEvent {
        public override string EventName => TrackingConstants.EVT_BUY_RESOURCE;

        private readonly ulong _totalBoughtValue;

        public BuyResourceEvent(ResourceTrackingData data, ulong value,
            ulong remainingValue, ulong totalBoughtValue)
            : base(data, value, remainingValue) {
            _totalBoughtValue = totalBoughtValue;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            base.BuildParams(dict);
            dict.Add(TrackingConstants.PAR_TOTAL_BOUGHT_VALUE, _totalBoughtValue);
        }
    }

    public class SpendResourceEvent : ResourceEvent {
        public override string EventName => TrackingConstants.EVT_SPEND_RESOURCE;

        private readonly ulong _totalSpentValue;

        public SpendResourceEvent(ResourceTrackingData data, ulong value,
            ulong remainingValue, ulong totalSpentValue)
            : base(data, value, remainingValue) {
            _totalSpentValue = totalSpentValue;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            base.BuildParams(dict);
            dict.Add(TrackingConstants.PAR_TOTAL_SPENT_VALUE, _totalSpentValue);
        }
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

        public TrackingSource(ResourceEventType type, string source, string sourceId) {
            Type = type;
            Source = source;
            SourceId = sourceId;
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
