using NUnit.Framework;

namespace ArcherStudio.SDK.Ads.Tests {

    [TestFixture]
    public class AdProviderRegistryTests {

        [TearDown]
        public void TearDown() {
            AdProviderRegistry.Clear();
        }

        [Test]
        public void Create_ReturnsNull_WhenPlatformNotRegistered() {
            Assert.IsFalse(AdProviderRegistry.IsRegistered(AdMediationPlatform.AdMob));
            Assert.IsNull(AdProviderRegistry.Create(AdMediationPlatform.AdMob));
        }

        [Test]
        public void Create_UsesRegisteredFactory() {
            var expected = new FakeAdProvider();
            AdProviderRegistry.Register(AdMediationPlatform.AppLovinMax, () => expected);

            Assert.IsTrue(AdProviderRegistry.IsRegistered(AdMediationPlatform.AppLovinMax));
            Assert.AreSame(expected, AdProviderRegistry.Create(AdMediationPlatform.AppLovinMax));
        }

        [Test]
        public void Register_LastRegistrationWins() {
            var first = new FakeAdProvider();
            var second = new FakeAdProvider();
            AdProviderRegistry.Register(AdMediationPlatform.AppLovinMax, () => first);
            AdProviderRegistry.Register(AdMediationPlatform.AppLovinMax, () => second);

            Assert.AreSame(second, AdProviderRegistry.Create(AdMediationPlatform.AppLovinMax));
        }

        [Test]
        public void Create_ReturnsNull_WhenFactoryThrows() {
            AdProviderRegistry.Register(
                AdMediationPlatform.IronSource,
                () => throw new System.InvalidOperationException("boom"));

            Assert.IsNull(AdProviderRegistry.Create(AdMediationPlatform.IronSource));
        }

        [Test]
        public void PackageNameFor_NamesThePackageThatShipsEachProvider() {
            Assert.AreEqual("com.archerstudio.sdk.ads.max",
                AdProviderRegistry.PackageNameFor(AdMediationPlatform.AppLovinMax));
            Assert.AreEqual("com.archerstudio.sdk.ads.admob",
                AdProviderRegistry.PackageNameFor(AdMediationPlatform.AdMob));
            Assert.AreEqual("com.archerstudio.sdk.ads.levelplay",
                AdProviderRegistry.PackageNameFor(AdMediationPlatform.IronSource));
        }

        private class FakeAdProvider : IAdProvider {
            public string ProviderId => "fake";
            public event System.Action<AdRevenueData> OnAdRevenuePaid;

            public void Initialize(AdConfig config, System.Action<bool> onComplete) {
                OnAdRevenuePaid?.Invoke(default);
                onComplete?.Invoke(true);
            }

            public void OnConsentChanged(ArcherStudio.SDK.Core.ConsentStatus consent) { }

            public void ShowBanner(AdPlacement placement, BannerPosition position) { }
            public void HideBanner(AdPlacement placement) { }
            public void DestroyBanner(AdPlacement placement) { }

            public bool IsInterstitialReady(AdPlacement placement) => false;
            public void LoadInterstitial(AdPlacement placement) { }
            public void ShowInterstitial(AdPlacement placement, System.Action<AdResult> onComplete) { }

            public bool IsRewardedReady(AdPlacement placement) => false;
            public void LoadRewarded(AdPlacement placement) { }
            public void ShowRewarded(AdPlacement placement, string trackPlacement,
                System.Action<AdResult> onComplete) { }

            public bool IsAppOpenReady(AdPlacement placement) => false;
            public void LoadAppOpen(AdPlacement placement) { }
            public void ShowAppOpen(AdPlacement placement, System.Action<AdResult> onComplete) { }
        }
    }
}
