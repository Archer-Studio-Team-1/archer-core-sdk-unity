using NUnit.Framework;

namespace ArcherStudio.SDK.Ads.Tests {

    [TestFixture]
    public class AdRevenueDataTests {

        [Test]
        public void Constructor_SetsAllFields_Correctly() {
            var data = new AdRevenueData(
                adPlatform: "AppLovin",
                adSource: "AdMob",
                adFormat: "Rewarded",
                adUnitName: "rewarded_unit_01",
                currency: "USD",
                value: 0.0125,
                placement: "level_complete"
            );

            Assert.AreEqual("AppLovin", data.AdPlatform);
            Assert.AreEqual("AdMob", data.AdSource);
            Assert.AreEqual("Rewarded", data.AdFormat);
            Assert.AreEqual("rewarded_unit_01", data.AdUnitName);
            Assert.AreEqual("USD", data.Currency);
            Assert.AreEqual(0.0125, data.Value, 0.0001);
            Assert.AreEqual("level_complete", data.Placement);
        }

        [Test]
        public void TwoInstances_AreIndependent() {
            var a = new AdRevenueData("P1", "S1", "F1", "U1", "USD", 1.0, "place_a");
            var b = new AdRevenueData("P2", "S2", "F2", "U2", "EUR", 2.0, "place_b");

            Assert.AreEqual("P1", a.AdPlatform);
            Assert.AreEqual("P2", b.AdPlatform);
            Assert.AreEqual(1.0, a.Value, 0.0001);
            Assert.AreEqual(2.0, b.Value, 0.0001);
        }

        [Test]
        public void DefaultStruct_HasNullStringsAndZeroValue() {
            var data = default(AdRevenueData);

            Assert.IsNull(data.AdPlatform);
            Assert.IsNull(data.AdSource);
            Assert.IsNull(data.AdFormat);
            Assert.IsNull(data.AdUnitName);
            Assert.IsNull(data.Currency);
            Assert.IsNull(data.Placement);
            Assert.AreEqual(0.0, data.Value, 0.0001);
        }
    }

    [TestFixture]
    public class RewardDataTests {

        [Test]
        public void Constructor_SetsTypeAndAmount() {
            var reward = new RewardData("coins", 100);

            Assert.AreEqual("coins", reward.Type);
            Assert.AreEqual(100, reward.Amount);
        }

        [Test]
        public void TwoInstances_AreIndependent() {
            var a = new RewardData("coins", 50);
            var b = new RewardData("gems", 10);

            Assert.AreEqual("coins", a.Type);
            Assert.AreEqual(50, a.Amount);
            Assert.AreEqual("gems", b.Type);
            Assert.AreEqual(10, b.Amount);
        }

        [Test]
        public void DefaultStruct_HasNullTypeAndZeroAmount() {
            var reward = default(RewardData);

            Assert.IsNull(reward.Type);
            Assert.AreEqual(0, reward.Amount);
        }
    }

    [TestFixture]
    public class AdResultTests {

        [Test]
        public void Succeeded_ReturnsSuccess_WithNoReward() {
            var result = AdResult.Succeeded("interstitial_01");

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.WasRewarded);
            Assert.IsNull(result.Error);
            Assert.AreEqual("interstitial_01", result.PlacementId);
        }

        [Test]
        public void Rewarded_ReturnsSuccess_WithRewardData() {
            var reward = new RewardData("coins", 200);
            var result = AdResult.Rewarded("rewarded_01", reward);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.WasRewarded);
            Assert.AreEqual("coins", result.Reward.Type);
            Assert.AreEqual(200, result.Reward.Amount);
            Assert.IsNull(result.Error);
            Assert.AreEqual("rewarded_01", result.PlacementId);
        }

        [Test]
        public void Failed_ReturnsFailure_WithErrorMessage() {
            var result = AdResult.Failed("banner_01", "No fill");

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.WasRewarded);
            Assert.AreEqual("No fill", result.Error);
            Assert.AreEqual("banner_01", result.PlacementId);
        }

        [Test]
        public void Failed_HasDefaultReward() {
            var result = AdResult.Failed("banner_01", "Timeout");

            Assert.IsNull(result.Reward.Type);
            Assert.AreEqual(0, result.Reward.Amount);
        }

        [Test]
        public void Succeeded_HasDefaultReward() {
            var result = AdResult.Succeeded("interstitial_02");

            Assert.IsNull(result.Reward.Type);
            Assert.AreEqual(0, result.Reward.Amount);
        }

        [Test]
        public void TwoResults_AreIndependent() {
            var success = AdResult.Succeeded("placement_a");
            var failure = AdResult.Failed("placement_b", "Network error");

            Assert.IsTrue(success.Success);
            Assert.IsFalse(failure.Success);
            Assert.AreEqual("placement_a", success.PlacementId);
            Assert.AreEqual("placement_b", failure.PlacementId);
        }
    }
}
