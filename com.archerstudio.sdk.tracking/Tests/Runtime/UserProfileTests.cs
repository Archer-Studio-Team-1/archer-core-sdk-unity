using NUnit.Framework;
using ArcherStudio.SDK.Tracking;

namespace ArcherStudio.SDK.Tracking.Tests {

    [TestFixture]
    public class UserProfileTests {

        [Test]
        public void GetAllProperties_OmitsLinkIds_WhenEmpty() {
            var p = new UserProfile { DeviceId = "dev-123" };

            var props = p.GetAllProperties();

            Assert.IsFalse(props.ContainsKey(TrackingConstants.UP_ADJUST_ID));
            Assert.IsFalse(props.ContainsKey(TrackingConstants.UP_FIREBASE_STORAGE_ID));
            Assert.IsFalse(props.ContainsKey(TrackingConstants.UP_FIREBASE_APP_INSTANCE_ID));
            Assert.IsFalse(props.ContainsKey(TrackingConstants.UP_LOGIN_ID));
        }

        [Test]
        public void GetAllProperties_AlwaysIncludesDeviceId() {
            var p = new UserProfile { DeviceId = "dev-123" };

            var props = p.GetAllProperties();

            Assert.AreEqual("dev-123", props[TrackingConstants.UP_DEVICE_ID]);
        }

        [Test]
        public void GetAllProperties_IncludesLinkIds_WhenSet() {
            var p = new UserProfile {
                DeviceId = "dev-123",
                AdjustId = "adid-1",
                FirebaseStorageId = "uid-1",
                FirebaseAppInstanceId = "aiid-1",
                LoginId = "gpgs-1"
            };

            var props = p.GetAllProperties();

            Assert.AreEqual("adid-1", props[TrackingConstants.UP_ADJUST_ID]);
            Assert.AreEqual("uid-1", props[TrackingConstants.UP_FIREBASE_STORAGE_ID]);
            Assert.AreEqual("aiid-1", props[TrackingConstants.UP_FIREBASE_APP_INSTANCE_ID]);
            Assert.AreEqual("gpgs-1", props[TrackingConstants.UP_LOGIN_ID]);
        }

        [Test]
        public void SetProperty_LoginId_RoundTrips() {
            var p = new UserProfile();

            Assert.IsTrue(p.SetProperty(TrackingConstants.UP_LOGIN_ID, "gpgs-9"));
            Assert.AreEqual("gpgs-9", p.LoginId);
        }

        [Test]
        public void SetProperty_FirebaseAppInstanceId_RoundTrips() {
            var p = new UserProfile();

            Assert.IsTrue(p.SetProperty(TrackingConstants.UP_FIREBASE_APP_INSTANCE_ID, "aiid-9"));
            Assert.AreEqual("aiid-9", p.FirebaseAppInstanceId);
        }

        [Test]
        public void LoginId_Set_FiresOnPropertyChanged_WithLoginIdKey() {
            var p = new UserProfile();
            string capturedKey = null;
            string capturedValue = null;
            p.OnPropertyChanged += (key, value) => { capturedKey = key; capturedValue = value; };

            p.LoginId = "gpgs-9";

            Assert.AreEqual(TrackingConstants.UP_LOGIN_ID, capturedKey);
            Assert.AreEqual("gpgs-9", capturedValue);
        }

        [Test]
        public void FirebaseAppInstanceId_Set_FiresOnPropertyChanged_WithFirebaseAppInstanceIdKey() {
            var p = new UserProfile();
            string capturedKey = null;
            string capturedValue = null;
            p.OnPropertyChanged += (key, value) => { capturedKey = key; capturedValue = value; };

            p.FirebaseAppInstanceId = "aiid-9";

            Assert.AreEqual(TrackingConstants.UP_FIREBASE_APP_INSTANCE_ID, capturedKey);
            Assert.AreEqual("aiid-9", capturedValue);
        }

        [Test]
        public void GetAllProperties_DeviceId_UsesNullSentinel_WhenEmpty() {
            var p = new UserProfile();

            var props = p.GetAllProperties();

            Assert.AreEqual("Null", props[TrackingConstants.UP_DEVICE_ID]);
        }
    }
}
