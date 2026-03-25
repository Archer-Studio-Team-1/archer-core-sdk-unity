using System.Collections.Generic;
using NUnit.Framework;
using ArcherStudio.SDK.Tracking;
using ArcherStudio.SDK.Tracking.Events;

namespace ArcherStudio.SDK.Tracking.Tests {

    [TestFixture]
    public class TrackingEventTests {

        // ─── StageStartEvent ───

        [Test]
        public void StageStartEvent_EventName_ReturnsStageStart() {
            var evt = new StageStartEvent("adventure", "stage_01");

            Assert.AreEqual(TrackingConstants.EVT_STAGE_START, evt.EventName);
        }

        [Test]
        public void StageStartEvent_ToParams_ContainsCategoryAndStageId() {
            var evt = new StageStartEvent("adventure", "stage_01");

            var parameters = evt.ToParams();

            Assert.AreEqual("adventure", parameters[TrackingConstants.PAR_CATEGORY]);
            Assert.AreEqual("stage_01", parameters[TrackingConstants.PAR_STAGE_ID]);
            Assert.AreEqual(2, parameters.Count);
        }

        // ─── StageEndEvent ───

        [Test]
        public void StageEndEvent_EventName_ReturnsStageEnd() {
            var evt = new StageEndEvent("adventure", "stage_01", 120);

            Assert.AreEqual(TrackingConstants.EVT_STAGE_END, evt.EventName);
        }

        [Test]
        public void StageEndEvent_ToParams_ContainsCategoryStageIdAndDuration() {
            var evt = new StageEndEvent("adventure", "stage_01", 120);

            var parameters = evt.ToParams();

            Assert.AreEqual("adventure", parameters[TrackingConstants.PAR_CATEGORY]);
            Assert.AreEqual("stage_01", parameters[TrackingConstants.PAR_STAGE_ID]);
            Assert.AreEqual(120, parameters[TrackingConstants.PAR_DURATION]);
            Assert.AreEqual(3, parameters.Count);
        }

        // ─── IapRevenueEvent (v2) ───

        [Test]
        public void IapRevenueEvent_EventName_ReturnsIapRevenue() {
            var evt = new IapRevenueEvent("gem_pack_01", 990000, "success");

            Assert.AreEqual(TrackingConstants.EVT_IAP_REVENUE, evt.EventName);
        }

        [Test]
        public void IapRevenueEvent_ToParams_ContainsAllParams() {
            var evt = new IapRevenueEvent("gem_pack_01", 990000, "success", null, null, "click");

            var parameters = evt.ToParams();

            Assert.AreEqual("gem_pack_01", parameters[TrackingConstants.PAR_PRODUCT_ID]);
            Assert.AreEqual(990000, parameters[TrackingConstants.PAR_IAP_REVENUE_MICRO]);
            Assert.AreEqual("success", parameters[TrackingConstants.PAR_PURCHASE_STATUS]);
            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_FAIL_REASON]);
            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_RESULT_CODE]);
            Assert.AreEqual("click", parameters[TrackingConstants.PAR_PLACEMENT]);
            Assert.AreEqual(6, parameters.Count);
        }

        [Test]
        public void IapRevenueEvent_Failed_ContainsFailReasonAndResultCode() {
            var evt = new IapRevenueEvent("gem_pack_01", 0, "fail",
                "User cancelled the purchase", "USER_CANCELED", "popup");

            var parameters = evt.ToParams();

            Assert.AreEqual("fail", parameters[TrackingConstants.PAR_PURCHASE_STATUS]);
            Assert.AreEqual("User cancelled the purchase", parameters[TrackingConstants.PAR_FAIL_REASON]);
            Assert.AreEqual("USER_CANCELED", parameters[TrackingConstants.PAR_RESULT_CODE]);
            Assert.AreEqual(0, parameters[TrackingConstants.PAR_IAP_REVENUE_MICRO]);
        }

        // ─── GenericGameTrackingEvent ───

        [Test]
        public void GenericGameTrackingEvent_EventName_ReturnsCustomName() {
            var evt = new GenericGameTrackingEvent("custom_event");

            Assert.AreEqual("custom_event", evt.EventName);
        }

        [Test]
        public void GenericGameTrackingEvent_ToParams_ContainsCustomParams() {
            var customParams = new Dictionary<string, object> {
                { "key_a", "value_a" },
                { "key_b", 42 }
            };
            var evt = new GenericGameTrackingEvent("custom_event", customParams);

            var parameters = evt.ToParams();

            Assert.AreEqual("value_a", parameters["key_a"]);
            Assert.AreEqual(42, parameters["key_b"]);
            Assert.AreEqual(2, parameters.Count);
        }

        [Test]
        public void GenericGameTrackingEvent_NullParams_ReturnsEmptyDict() {
            var evt = new GenericGameTrackingEvent("custom_event", null);

            var parameters = evt.ToParams();

            Assert.IsNotNull(parameters);
            Assert.AreEqual(0, parameters.Count);
        }

        // ─── GameTrackingEvent base behavior ───

        [Test]
        public void ToParams_ReturnsNewDictEachCall() {
            var evt = new StageStartEvent("adventure", "stage_01");

            var first = evt.ToParams();
            var second = evt.ToParams();

            Assert.AreNotSame(first, second);
            Assert.AreEqual(first[TrackingConstants.PAR_CATEGORY], second[TrackingConstants.PAR_CATEGORY]);
        }

        [Test]
        public void FillParams_PopulatesExistingDict() {
            var evt = new StageStartEvent("adventure", "stage_01");
            var dict = new Dictionary<string, object> {
                { "existing_key", "existing_value" }
            };

            evt.FillParams(dict);

            Assert.AreEqual("existing_value", dict["existing_key"]);
            Assert.AreEqual("adventure", dict[TrackingConstants.PAR_CATEGORY]);
            Assert.AreEqual("stage_01", dict[TrackingConstants.PAR_STAGE_ID]);
            Assert.AreEqual(3, dict.Count);
        }

        [Test]
        public void FillParams_DoesNotClearExistingEntries() {
            var evt = new StageStartEvent("adventure", "stage_01");
            var dict = new Dictionary<string, object> {
                { "pre_existing", 999 }
            };

            evt.FillParams(dict);

            Assert.IsTrue(dict.ContainsKey("pre_existing"));
            Assert.AreEqual(999, dict["pre_existing"]);
        }
    }
}
