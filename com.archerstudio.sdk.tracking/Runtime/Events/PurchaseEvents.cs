using System.Collections.Generic;

namespace ArcherStudio.SDK.Tracking.Events {

    /// <summary>
    /// Custom iap_revenue event per tracking_v2 spec.
    /// Trigger khi player mua IAP (thành công hoặc thất bại).
    /// </summary>
    public class IapRevenueEvent : GameTrackingEvent {
        public override string EventName => TrackingConstants.EVT_IAP_REVENUE;

        private readonly string _productId;
        private readonly int _iapRevenueMicro;
        private readonly string _purchaseStatus;
        private readonly string _failReason;
        private readonly string _resultCode;
        private readonly string _placement;

        public IapRevenueEvent(string productId, int iapRevenueMicro, string purchaseStatus,
            string failReason = null, string resultCode = null, string placement = null) {
            _productId = productId;
            _iapRevenueMicro = iapRevenueMicro;
            _purchaseStatus = purchaseStatus;
            _failReason = failReason;
            _resultCode = resultCode;
            _placement = placement;
        }

        protected override void BuildParams(Dictionary<string, object> dict) {
            dict.Add(TrackingConstants.PAR_PRODUCT_ID, _productId ?? "Null");
            dict.Add(TrackingConstants.PAR_IAP_REVENUE_MICRO, _iapRevenueMicro);
            dict.Add(TrackingConstants.PAR_PURCHASE_STATUS, _purchaseStatus ?? "Null");
            dict.Add(TrackingConstants.PAR_FAIL_REASON, _failReason ?? "Null");
            dict.Add(TrackingConstants.PAR_RESULT_CODE, _resultCode ?? "Null");
            dict.Add(TrackingConstants.PAR_PLACEMENT, _placement ?? "Null");
        }
    }
}
