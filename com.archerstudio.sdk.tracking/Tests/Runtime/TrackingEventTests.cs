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

        // ─── PurchaseShowEvent ───

        [Test]
        public void PurchaseShowEvent_EventName_ReturnsPurchaseShow() {
            var evt = new PurchaseShowEvent("gem_pack_01", "shop", "low_gems");

            Assert.AreEqual(TrackingConstants.EVT_PURCHASE_SHOW, evt.EventName);
        }

        [Test]
        public void PurchaseShowEvent_ToParams_ContainsProductIdSourceAndReason() {
            var evt = new PurchaseShowEvent("gem_pack_01", "shop", "low_gems");

            var parameters = evt.ToParams();

            Assert.AreEqual("gem_pack_01", parameters[TrackingConstants.PAR_PRODUCT_ID]);
            Assert.AreEqual("shop", parameters[TrackingConstants.PAR_SOURCE]);
            Assert.AreEqual("low_gems", parameters[TrackingConstants.PAR_REASON]);
            Assert.AreEqual(3, parameters.Count);
        }

        // ─── PurchaseResultEvent ───

        [Test]
        public void PurchaseResultEvent_EventName_ReturnsPurchaseResult() {
            var evt = new PurchaseResultEvent("gem_pack_01", "shop", "low_gems", 1);

            Assert.AreEqual(TrackingConstants.EVT_PURCHASE_RESULT, evt.EventName);
        }

        [Test]
        public void PurchaseResultEvent_ToParams_ContainsProductIdSourceReasonAndStatus() {
            var evt = new PurchaseResultEvent("gem_pack_01", "shop", "low_gems", 1);

            var parameters = evt.ToParams();

            Assert.AreEqual("gem_pack_01", parameters[TrackingConstants.PAR_PRODUCT_ID]);
            Assert.AreEqual("shop", parameters[TrackingConstants.PAR_SOURCE]);
            Assert.AreEqual("low_gems", parameters[TrackingConstants.PAR_REASON]);
            Assert.AreEqual(1, parameters[TrackingConstants.PAR_STATUS]);
            Assert.AreEqual(4, parameters.Count);
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
