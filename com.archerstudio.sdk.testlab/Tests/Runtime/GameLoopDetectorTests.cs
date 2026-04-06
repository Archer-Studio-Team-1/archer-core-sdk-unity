using NUnit.Framework;

namespace ArcherStudio.SDK.TestLab.Tests {

    [TestFixture]
    public class GameLoopDetectorTests {

        [SetUp]
        public void SetUp() {
            GameLoopDetector.Reset();
        }

        [Test]
        public void IsRunningInTestLab_InEditor_ReturnsFalse() {
            Assert.IsFalse(GameLoopDetector.IsRunningInTestLab);
        }

        [Test]
        public void ScenarioNumber_InEditor_ReturnsDefault() {
            Assert.AreEqual(1, GameLoopDetector.ScenarioNumber);
        }

        [Test]
        public void Reset_ClearsCachedState() {
            // Access to populate cache
            _ = GameLoopDetector.IsRunningInTestLab;
            _ = GameLoopDetector.ScenarioNumber;

            // Reset and verify re-evaluation
            GameLoopDetector.Reset();
            Assert.IsFalse(GameLoopDetector.IsRunningInTestLab);
            Assert.AreEqual(1, GameLoopDetector.ScenarioNumber);
        }
    }

    [TestFixture]
    public class TestLabConfigTests {

        [Test]
        public void GetScenario_ValidIndex_ReturnsEntry() {
            var config = UnityEngine.ScriptableObject.CreateInstance<TestLabConfig>();
            config.Scenarios.Add(new GameLoopScenarioEntry { Name = "Test1", Enabled = true });
            config.Scenarios.Add(new GameLoopScenarioEntry { Name = "Test2", Enabled = true });

            var result = config.GetScenario(1);
            Assert.IsNotNull(result);
            Assert.AreEqual("Test1", result.Name);

            var result2 = config.GetScenario(2);
            Assert.IsNotNull(result2);
            Assert.AreEqual("Test2", result2.Name);

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void GetScenario_InvalidIndex_ReturnsNull() {
            var config = UnityEngine.ScriptableObject.CreateInstance<TestLabConfig>();
            config.Scenarios.Add(new GameLoopScenarioEntry { Name = "Test1" });

            Assert.IsNull(config.GetScenario(0));
            Assert.IsNull(config.GetScenario(5));

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void GameLoopResult_StoresCorrectValues() {
            var result = new GameLoopResult(2, "FPS Test", true, 15.5f, "Passed");

            Assert.AreEqual(2, result.ScenarioNumber);
            Assert.AreEqual("FPS Test", result.ScenarioName);
            Assert.IsTrue(result.Passed);
            Assert.AreEqual(15.5f, result.DurationSeconds, 0.01f);
            Assert.AreEqual("Passed", result.Message);
            Assert.IsFalse(string.IsNullOrEmpty(result.Timestamp));
        }
    }
}
