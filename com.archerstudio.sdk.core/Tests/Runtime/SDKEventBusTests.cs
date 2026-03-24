using NUnit.Framework;

namespace ArcherStudio.SDK.Core.Tests {

    public class SDKEventBusTests {

        private struct TestEvent : ISDKEvent {
            public int Value { get; }
            public TestEvent(int value) { Value = value; }
        }

        [SetUp]
        public void SetUp() {
            SDKEventBus.Clear<TestEvent>();
        }

        [Test]
        public void Subscribe_And_Publish_ReceivesEvent() {
            int received = -1;
            SDKEventBus.Subscribe<TestEvent>(e => received = e.Value);

            SDKEventBus.Publish(new TestEvent(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Unsubscribe_StopsReceiving() {
            int callCount = 0;
            void Handler(TestEvent e) => callCount++;

            SDKEventBus.Subscribe<TestEvent>(Handler);
            SDKEventBus.Publish(new TestEvent(1));
            Assert.AreEqual(1, callCount);

            SDKEventBus.Unsubscribe<TestEvent>(Handler);
            SDKEventBus.Publish(new TestEvent(2));
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void Publish_MultipleSubscribers_AllReceive() {
            int count = 0;
            SDKEventBus.Subscribe<TestEvent>(_ => count++);
            SDKEventBus.Subscribe<TestEvent>(_ => count++);
            SDKEventBus.Subscribe<TestEvent>(_ => count++);

            SDKEventBus.Publish(new TestEvent(1));

            Assert.AreEqual(3, count);
        }

        [Test]
        public void Publish_NoSubscribers_NoError() {
            Assert.DoesNotThrow(() => SDKEventBus.Publish(new TestEvent(1)));
        }

        [Test]
        public void Subscribe_Duplicate_IgnoredSecondTime() {
            int count = 0;
            void Handler(TestEvent e) => count++;

            SDKEventBus.Subscribe<TestEvent>(Handler);
            SDKEventBus.Subscribe<TestEvent>(Handler);

            SDKEventBus.Publish(new TestEvent(1));

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Clear_RemovesAllSubscribers() {
            int count = 0;
            SDKEventBus.Subscribe<TestEvent>(_ => count++);
            SDKEventBus.Subscribe<TestEvent>(_ => count++);

            SDKEventBus.Clear<TestEvent>();
            SDKEventBus.Publish(new TestEvent(1));

            Assert.AreEqual(0, count);
        }

        [Test]
        public void ConsentChangedEvent_CarriesStatus() {
            ConsentStatus received = default;
            SDKEventBus.Subscribe<ConsentChangedEvent>(e => received = e.Status);

            var status = new ConsentStatus(
                canShowPersonalizedAds: false,
                canCollectAnalytics: true,
                canTrackAttribution: false,
                isEeaUser: true,
                hasAttConsent: false,
                source: ConsentSource.GoogleUMP);

            SDKEventBus.Publish(new ConsentChangedEvent(status));

            Assert.IsFalse(received.CanShowPersonalizedAds);
            Assert.IsTrue(received.CanCollectAnalytics);
            Assert.IsTrue(received.IsEeaUser);
            Assert.AreEqual(ConsentSource.GoogleUMP, received.Source);

            SDKEventBus.Clear<ConsentChangedEvent>();
        }
    }
}
