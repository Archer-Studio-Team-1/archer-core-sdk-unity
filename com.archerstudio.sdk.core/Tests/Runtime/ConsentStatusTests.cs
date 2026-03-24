using NUnit.Framework;

namespace ArcherStudio.SDK.Core.Tests {

    public class ConsentStatusTests {

        [Test]
        public void Default_AllGranted() {
            var status = ConsentStatus.Default;

            Assert.IsTrue(status.CanShowPersonalizedAds);
            Assert.IsTrue(status.CanCollectAnalytics);
            Assert.IsTrue(status.CanTrackAttribution);
            Assert.IsFalse(status.IsEeaUser);
            Assert.IsTrue(status.HasAttConsent);
            Assert.AreEqual(ConsentSource.Default, status.Source);
        }

        [Test]
        public void FromLegacy_Granted_AllTrue() {
            var status = ConsentStatus.FromLegacy(true, false);

            Assert.IsTrue(status.CanShowPersonalizedAds);
            Assert.IsTrue(status.CanCollectAnalytics);
            Assert.IsTrue(status.CanTrackAttribution);
            Assert.IsFalse(status.IsEeaUser);
            Assert.IsTrue(status.HasAttConsent);
            Assert.AreEqual(ConsentSource.Manual, status.Source);
        }

        [Test]
        public void FromLegacy_Denied_AllFalse() {
            var status = ConsentStatus.FromLegacy(false, true);

            Assert.IsFalse(status.CanShowPersonalizedAds);
            Assert.IsFalse(status.CanCollectAnalytics);
            Assert.IsFalse(status.CanTrackAttribution);
            Assert.IsTrue(status.IsEeaUser);
            Assert.IsFalse(status.HasAttConsent);
            Assert.AreEqual(ConsentSource.Manual, status.Source);
        }

        [Test]
        public void Constructor_GranularConsent_PreservesValues() {
            var status = new ConsentStatus(
                canShowPersonalizedAds: false,
                canCollectAnalytics: true,
                canTrackAttribution: false,
                isEeaUser: true,
                hasAttConsent: false,
                source: ConsentSource.GoogleUMP);

            Assert.IsFalse(status.CanShowPersonalizedAds);
            Assert.IsTrue(status.CanCollectAnalytics);
            Assert.IsFalse(status.CanTrackAttribution);
            Assert.IsTrue(status.IsEeaUser);
            Assert.IsFalse(status.HasAttConsent);
            Assert.AreEqual(ConsentSource.GoogleUMP, status.Source);
        }

        [Test]
        public void Immutability_TwoInstances_Independent() {
            var a = ConsentStatus.FromLegacy(true, false);
            var b = ConsentStatus.FromLegacy(false, true);

            Assert.IsTrue(a.CanShowPersonalizedAds);
            Assert.IsFalse(b.CanShowPersonalizedAds);
        }

        [Test]
        public void ToString_ContainsAllFields() {
            var status = ConsentStatus.Default;
            string str = status.ToString();

            Assert.IsTrue(str.Contains("ads="));
            Assert.IsTrue(str.Contains("analytics="));
            Assert.IsTrue(str.Contains("attribution="));
            Assert.IsTrue(str.Contains("eea="));
            Assert.IsTrue(str.Contains("att="));
            Assert.IsTrue(str.Contains("source="));
        }
    }
}
