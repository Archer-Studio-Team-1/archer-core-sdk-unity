using System;
using System.Collections;
using ArcherStudio.SDK.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcherStudio.SDK.Consent.Tests {

    /// <summary>
    /// ConsentManager.ShowPrivacyOptions against a fake provider. PlayMode because the manager hands
    /// provider callbacks to UnityMainThreadDispatcher, which only runs in Update.
    /// </summary>
    public class ConsentManagerPrivacyOptionsTests {
        private const int MaxFrames = 30;

        private FakeProvider _provider;
        private ConsentManager _manager;
        private int _published;
        private ConsentStatus _lastPublished;

        [UnitySetUp]
        public IEnumerator SetUp() {
            _published = 0;
            SDKEventBus.Subscribe<ConsentChangedEvent>(OnConsentChanged);

            _provider = new FakeProvider { Status = Granted(true) };
            _manager = new ConsentManager(_provider);

            bool? initialized = null;
            _manager.InitializeAsync(null, ScriptableObject.CreateInstance<ConsentConfig>(),
                ok => initialized = ok);
            yield return WaitFor(() => initialized.HasValue);

            Assert.IsTrue(initialized.Value);
            _published = 0;
        }

        [TearDown]
        public void TearDown() {
            SDKEventBus.Unsubscribe<ConsentChangedEvent>(OnConsentChanged);
            _manager.ResetConsent();   // drops the PlayerPrefs keys the manager cached
        }

        [Test]
        public void IsPrivacyOptionsRequired_FollowsTheProvider() {
            _provider.Required = false;
            Assert.IsFalse(_manager.IsPrivacyOptionsRequired);

            _provider.Required = true;
            Assert.IsTrue(_manager.IsPrivacyOptionsRequired);
        }

        [UnityTest]
        public IEnumerator ShowPrivacyOptions_ReadsTheNewAnswer_AndPublishesIt() {
            _provider.StatusAfterForm = Granted(false);

            string error = "not called";
            _manager.ShowPrivacyOptions(e => error = e);
            yield return WaitFor(() => error != "not called");

            Assert.IsNull(error);
            Assert.IsFalse(_manager.CurrentStatus.CanShowPersonalizedAds,
                "A withdrawn consent must reach CurrentStatus.");
            Assert.AreEqual(1, _published);
            Assert.IsFalse(_lastPublished.CanShowPersonalizedAds);
        }

        [UnityTest]
        public IEnumerator ShowPrivacyOptions_FormError_KeepsTheOldAnswer() {
            _provider.FormError = "network down";
            _provider.StatusAfterForm = Granted(false);

            string error = "not called";
            _manager.ShowPrivacyOptions(e => error = e);
            yield return WaitFor(() => error != "not called");

            Assert.AreEqual("network down", error);
            Assert.IsTrue(_manager.CurrentStatus.CanShowPersonalizedAds);
            Assert.AreEqual(0, _published);
        }

        [UnityTest]
        public IEnumerator ShowPrivacyOptions_ProviderWithoutSupport_ReportsAnError() {
            var manager = new ConsentManager(new PlainProvider());
            bool? initialized = null;
            manager.InitializeAsync(null, ScriptableObject.CreateInstance<ConsentConfig>(),
                ok => initialized = ok);
            yield return WaitFor(() => initialized.HasValue);

            string error = null;
            manager.ShowPrivacyOptions(e => error = e);

            Assert.IsFalse(manager.IsPrivacyOptionsRequired);
            Assert.IsNotNull(error);
            manager.ResetConsent();
        }

        // ------------------------------------------------------------------ helpers

        private void OnConsentChanged(ConsentChangedEvent evt) {
            _published++;
            _lastPublished = evt.Status;
        }

        private static IEnumerator WaitFor(Func<bool> condition) {
            for (int i = 0; i < MaxFrames && !condition(); i++) yield return null;
            Assert.IsTrue(condition(), $"Condition not met within {MaxFrames} frames.");
        }

        private static ConsentStatus Granted(bool granted) => new ConsentStatus(
            canShowPersonalizedAds: granted,
            canCollectAnalytics: granted,
            canTrackAttribution: granted,
            isEeaUser: true,
            hasAttConsent: true,
            source: ConsentSource.GoogleUMP,
            isDoNotSell: false,
            canStoreAdData: granted);

        private sealed class FakeProvider : IConsentProvider, IPrivacyOptionsProvider {
            public ConsentStatus Status;
            public ConsentStatus? StatusAfterForm;
            public string FormError;
            public bool Required;

            public bool IsConsentRequired => true;
            public bool IsPrivacyOptionsRequired => Required;

            public void RequestConsent(Action<ConsentStatus> onComplete) => onComplete(Status);
            public ConsentStatus GetCurrentStatus() => Status;
            public void ResetConsent() { }

            public void ShowPrivacyOptions(Action<string> onComplete) {
                if (FormError == null && StatusAfterForm.HasValue) Status = StatusAfterForm.Value;
                onComplete(FormError);
            }
        }

        private sealed class PlainProvider : IConsentProvider {
            public bool IsConsentRequired => true;
            public void RequestConsent(Action<ConsentStatus> onComplete) => onComplete(ConsentStatus.Default);
            public ConsentStatus GetCurrentStatus() => ConsentStatus.Default;
            public void ResetConsent() { }
        }
    }
}
