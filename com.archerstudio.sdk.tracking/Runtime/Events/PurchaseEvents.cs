using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    public class PurchaseShowEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_PURCHASE_SHOW;

        private readonly string _productId;
        private readonly string _source;
        private readonly string _reason;

        public PurchaseShowEvent(string productId, string source, string reason) {
            _productId = productId;
            _source = source;
            _reason = reason;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_PRODUCT_ID, _productId);
            dict.Add(TrackingConstants.PAR_SOURCE, _source);
            dict.Add(TrackingConstants.PAR_REASON, _reason);
        }
    }

    public class PurchaseResultEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_PURCHASE_RESULT;

        private readonly string _productId;
        private readonly string _source;
        private readonly string _reason;
        private readonly int _status;

        public PurchaseResultEvent(string productId, string source, string reason, int status) {
            _productId = productId;
            _source = source;
            _reason = reason;
            _status = status;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_PRODUCT_ID, _productId);
            dict.Add(TrackingConstants.PAR_SOURCE, _source);
            dict.Add(TrackingConstants.PAR_REASON, _reason);
            dict.Add(TrackingConstants.PAR_STATUS, _status);
        }
    }
}
