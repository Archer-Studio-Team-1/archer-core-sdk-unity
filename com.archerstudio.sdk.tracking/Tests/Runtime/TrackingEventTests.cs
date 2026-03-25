using System.Collections.Generic;
using NUnit.Framework;
using ArcherStudio.SDK.Tracking;
using ArcherStudio.SDK.Tracking.Events;

namespace ArcherStudio.SDK.Tracking.Tests {

    [TestFixture]
    public class TrackingEventTests {

        // ─── StageStartEvent (v2) ───

        [Test]
        public void StageStartEvent_EventName_ReturnsStageStart() {
            var evt = new StageStartEvent("1_3");

            Assert.AreEqual(TrackingConstants.EVT_STAGE_START, evt.EventName);
        }

        [Test]
        public void StageStartEvent_ToParams_ContainsOnlyStageId() {
            var evt = new StageStartEvent("1_3");

            var parameters = evt.ToParams();

            Assert.AreEqual("1_3", parameters[TrackingConstants.PAR_STAGE_ID]);
            Assert.AreEqual(1, parameters.Count);
        }

        [Test]
        public void StageStartEvent_NullDefaults() {
            var evt = new StageStartEvent(null);

            var parameters = evt.ToParams();

            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_STAGE_ID]);
        }

        // ─── StageEndEvent (v2) ───

        [Test]
        public void StageEndEvent_EventName_ReturnsStageEnd() {
            var evt = new StageEndEvent(1711360000, "1_3", "Win", 120000);

            Assert.AreEqual(TrackingConstants.EVT_STAGE_END, evt.EventName);
        }

        [Test]
        public void StageEndEvent_ToParams_ContainsV2Params() {
            var evt = new StageEndEvent(1711360000, "1_3", "Win", 120000);

            var parameters = evt.ToParams();

            Assert.AreEqual(1711360000, parameters[TrackingConstants.PAR_STAGE_START_TIMESTAMP]);
            Assert.AreEqual("1_3", parameters[TrackingConstants.PAR_STAGE_ID]);
            Assert.AreEqual("Win", parameters[TrackingConstants.PAR_RESULT]);
            Assert.AreEqual(120000, parameters[TrackingConstants.PAR_DURATION]);
            Assert.AreEqual(4, parameters.Count);
        }

        [Test]
        public void StageEndEvent_Exit_Result() {
            var evt = new StageEndEvent(1711360000, "2_1", "Exit", 5000);

            var parameters = evt.ToParams();

            Assert.AreEqual("Exit", parameters[TrackingConstants.PAR_RESULT]);
        }

        [Test]
        public void StageEndEvent_NullDefaults() {
            var evt = new StageEndEvent(0, null, null, 0);

            var parameters = evt.ToParams();

            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_STAGE_ID]);
            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_RESULT]);
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

        // ─── TaskEndEvent (v2 — new) ───

        [Test]
        public void TaskEndEvent_EventName_ReturnsTaskEnd() {
            var evt = new TaskEndEvent("task_001", "UpgradeStaff25", "1_3");

            Assert.AreEqual(TrackingConstants.EVT_TASK_END, evt.EventName);
        }

        [Test]
        public void TaskEndEvent_ToParams_ContainsAllParams() {
            var evt = new TaskEndEvent("task_001", "UpgradeStaff25", "1_3");

            var parameters = evt.ToParams();

            Assert.AreEqual("task_001", parameters[TrackingConstants.PAR_TASK_ID]);
            Assert.AreEqual("UpgradeStaff25", parameters[TrackingConstants.PAR_TASK_NAME]);
            Assert.AreEqual("1_3", parameters[TrackingConstants.PAR_STAGE_ID]);
            Assert.AreEqual(3, parameters.Count);
        }

        [Test]
        public void TaskEndEvent_NullDefaults() {
            var evt = new TaskEndEvent(null, null, null);

            var parameters = evt.ToParams();

            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_TASK_ID]);
            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_TASK_NAME]);
            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_STAGE_ID]);
        }

        // ─── LoadingResultEvent (v2) ───

        [Test]
        public void LoadingResultEvent_EventName_ReturnsLoadingResult() {
            var evt = new LoadingResultEvent(5000, 60, "success");

            Assert.AreEqual(TrackingConstants.EVT_LOADING_RESULT, evt.EventName);
        }

        [Test]
        public void LoadingResultEvent_ToParams_ContainsV2Params() {
            var evt = new LoadingResultEvent(5000, 60, "success");

            var parameters = evt.ToParams();

            Assert.AreEqual(5000, parameters[TrackingConstants.PAR_LOADING_TIME]);
            Assert.AreEqual(60, parameters[TrackingConstants.PAR_FPS]);
            Assert.AreEqual("success", parameters[TrackingConstants.PAR_LOADING_STATUS]);
            Assert.AreEqual(3, parameters.Count);
        }

        [Test]
        public void LoadingResultEvent_Fail_Status() {
            var evt = new LoadingResultEvent(12000, 30, "fail");

            var parameters = evt.ToParams();

            Assert.AreEqual("fail", parameters[TrackingConstants.PAR_LOADING_STATUS]);
            Assert.AreEqual(12000, parameters[TrackingConstants.PAR_LOADING_TIME]);
        }

        [Test]
        public void LoadingResultEvent_NullStatus_DefaultsToNull() {
            var evt = new LoadingResultEvent(0, 0, null);

            var parameters = evt.ToParams();

            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_LOADING_STATUS]);
        }

        [Test]
        public void LoadingStartEvent_ShouldNotExist() {
            var type = typeof(TrackingConstants);
            Assert.IsNull(type.GetField("EVT_LOADING_START"), "EVT_LOADING_START should be removed");
        }

        [Test]
        public void OldLoadingParams_ShouldNotExist() {
            var type = typeof(TrackingConstants);
            Assert.IsNull(type.GetField("PAR_TIMEOUT_MSEC"), "PAR_TIMEOUT_MSEC should be removed");
            Assert.IsNull(type.GetField("PAR_STATUS"), "PAR_STATUS should be removed");
            Assert.IsNull(type.GetField("PAR_IS_USER_ACTION"), "PAR_IS_USER_ACTION should be removed");
        }

        // ─── TutorialEvent ───

        [Test]
        public void TutorialEvent_EventName_ReturnsTutorial() {
            var evt = new TutorialEvent("onboarding", "tap_button", 1);

            Assert.AreEqual(TrackingConstants.EVT_TUTORIAL, evt.EventName);
        }

        [Test]
        public void TutorialEvent_ToParams_ContainsAllParams() {
            var evt = new TutorialEvent("onboarding", "tap_button", 1);

            var parameters = evt.ToParams();

            Assert.AreEqual("onboarding", parameters[TrackingConstants.PAR_TUT_CATEGORY]);
            Assert.AreEqual("tap_button", parameters[TrackingConstants.PAR_TUT_NAME]);
            Assert.AreEqual(1, parameters[TrackingConstants.PAR_TUT_INDEX]);
            Assert.AreEqual(3, parameters.Count);
        }

        [Test]
        public void TutorialCompleteEvent_HasFixedValues() {
            var evt = new TutorialCompleteEvent();

            var parameters = evt.ToParams();

            Assert.AreEqual("tutorial", parameters[TrackingConstants.PAR_TUT_CATEGORY]);
            Assert.AreEqual("finish", parameters[TrackingConstants.PAR_TUT_NAME]);
            Assert.AreEqual(100, parameters[TrackingConstants.PAR_TUT_INDEX]);
        }

        // ─── EarnResourceEvent (v2) ───

        [Test]
        public void EarnResourceEvent_EventName_ReturnsEarnResource() {
            var evt = new EarnResourceEvent("Gem", "gacha", "free", 100);

            Assert.AreEqual(TrackingConstants.EVT_EARN_RESOURCE, evt.EventName);
        }

        [Test]
        public void EarnResourceEvent_ToParams_ContainsV2Params() {
            var evt = new EarnResourceEvent("Gem", "gacha", "free", 100);

            var parameters = evt.ToParams();

            Assert.AreEqual("Gem", parameters[TrackingConstants.PAR_RESOURCE_ID]);
            Assert.AreEqual("gacha", parameters[TrackingConstants.PAR_SOURCE_ID]);
            Assert.AreEqual("free", parameters[TrackingConstants.PAR_SOURCE_TYPE]);
            Assert.AreEqual(100, parameters[TrackingConstants.PAR_VALUE]);
            Assert.AreEqual(4, parameters.Count);
        }

        [Test]
        public void EarnResourceEvent_NullDefaults() {
            var evt = new EarnResourceEvent(null, null, null, 0);

            var parameters = evt.ToParams();

            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_RESOURCE_ID]);
            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_SOURCE_ID]);
            Assert.AreEqual("Null", parameters[TrackingConstants.PAR_SOURCE_TYPE]);
            Assert.AreEqual(0, parameters[TrackingConstants.PAR_VALUE]);
        }

        // ─── SpendResourceEvent (v2) ───

        [Test]
        public void SpendResourceEvent_EventName_ReturnsSpendResource() {
            var evt = new SpendResourceEvent("Gem", "forge", "free", 50);

            Assert.AreEqual(TrackingConstants.EVT_SPEND_RESOURCE, evt.EventName);
        }

        [Test]
        public void SpendResourceEvent_ToParams_ContainsV2Params() {
            var evt = new SpendResourceEvent("Gem", "forge", "free", 50);

            var parameters = evt.ToParams();

            Assert.AreEqual("Gem", parameters[TrackingConstants.PAR_RESOURCE_ID]);
            Assert.AreEqual("forge", parameters[TrackingConstants.PAR_SOURCE_ID]);
            Assert.AreEqual("free", parameters[TrackingConstants.PAR_SOURCE_TYPE]);
            Assert.AreEqual(50, parameters[TrackingConstants.PAR_VALUE]);
            Assert.AreEqual(4, parameters.Count);
        }

        [Test]
        public void SpendResourceEvent_IapSourceType() {
            var evt = new SpendResourceEvent("Gem", "shop", "iap", 200);

            var parameters = evt.ToParams();

            Assert.AreEqual("iap", parameters[TrackingConstants.PAR_SOURCE_TYPE]);
        }

        [Test]
        public void SpendResourceEvent_AdsSourceType() {
            var evt = new SpendResourceEvent("Gem", "ads_gold_offer", "ads", 30);

            var parameters = evt.ToParams();

            Assert.AreEqual("ads", parameters[TrackingConstants.PAR_SOURCE_TYPE]);
        }

        // ─── Removed: BuyResourceEvent, old resource params ───

        [Test]
        public void BuyResourceEvent_ShouldNotExist() {
            var type = typeof(TrackingConstants);
            Assert.IsNull(type.GetField("EVT_BUY_RESOURCE"), "EVT_BUY_RESOURCE should be removed");
        }

        [Test]
        public void OldResourceParams_ShouldNotExist() {
            var type = typeof(TrackingConstants);
            string[] removed = {
                "PAR_ITEM_CATEGORY", "PAR_ITEM_ID", "PAR_SOURCE",
                "PAR_REMAINING_VALUE", "PAR_TOTAL_EARN_VALUE",
                "PAR_TOTAL_BOUGHT_VALUE", "PAR_TOTAL_SPENT_VALUE"
            };
            foreach (var name in removed) {
                Assert.IsNull(type.GetField(name), $"Removed constant '{name}' should not exist");
            }
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
            var evt = new StageStartEvent("stage_01");

            var first = evt.ToParams();
            var second = evt.ToParams();

            Assert.AreNotSame(first, second);
            Assert.AreEqual(first[TrackingConstants.PAR_STAGE_ID], second[TrackingConstants.PAR_STAGE_ID]);
        }

        [Test]
        public void FillParams_PopulatesExistingDict() {
            var evt = new StageStartEvent("stage_01");
            var dict = new Dictionary<string, object> {
                { "existing_key", "existing_value" }
            };

            evt.FillParams(dict);

            Assert.AreEqual("existing_value", dict["existing_key"]);
            Assert.AreEqual("stage_01", dict[TrackingConstants.PAR_STAGE_ID]);
            Assert.AreEqual(2, dict.Count);
        }

        [Test]
        public void FillParams_DoesNotClearExistingEntries() {
            var evt = new StageStartEvent("stage_01");
            var dict = new Dictionary<string, object> {
                { "pre_existing", 999 }
            };

            evt.FillParams(dict);

            Assert.IsTrue(dict.ContainsKey("pre_existing"));
            Assert.AreEqual(999, dict["pre_existing"]);
        }
    }
}
