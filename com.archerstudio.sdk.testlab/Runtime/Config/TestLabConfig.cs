using System.Collections.Generic;
using UnityEngine;

namespace ArcherStudio.SDK.TestLab {

    /// <summary>
    /// ScriptableObject config for Firebase Test Lab scenarios.
    /// Place in Assets/Resources/TestLabConfig.asset
    /// </summary>
    [CreateAssetMenu(fileName = "TestLabConfig", menuName = "ArcherStudio/SDK/Test Lab Config")]
    public class TestLabConfig : ScriptableObject {

        [Header("General")]
        [Tooltip("Enable Game Loop handler at runtime")]
        public bool Enabled = true;

        [Tooltip("Timeout per scenario in seconds (default 300 = 5 min)")]
        public int ScenarioTimeoutSeconds = 300;

        [Tooltip("Write results to device storage for Test Lab collection")]
        public bool WriteResultFiles = true;

        [Header("Scenarios")]
        [Tooltip("List of Game Loop scenarios. Index+1 maps to Firebase scenario number.")]
        public List<GameLoopScenarioEntry> Scenarios = new();

        [Header("gcloud Settings")]
        [Tooltip("Firebase project ID")]
        public string FirebaseProjectId = "";

        [Tooltip("Default Android device model (e.g. Pixel6)")]
        public string DefaultAndroidModel = "Pixel6";

        [Tooltip("Default Android API level")]
        public int DefaultAndroidApiLevel = 33;

        [Tooltip("Default iOS device model (e.g. iphone13pro)")]
        public string DefaultIosModel = "iphone13pro";

        [Tooltip("Default iOS version")]
        public string DefaultIosVersion = "15.7";

        [Tooltip("Test timeout in minutes for gcloud")]
        public int TestTimeoutMinutes = 10;

        public GameLoopScenarioEntry GetScenario(int scenarioNumber) {
            int index = scenarioNumber - 1;
            if (index >= 0 && index < Scenarios.Count) {
                return Scenarios[index];
            }
            return null;
        }
    }

    [System.Serializable]
    public class GameLoopScenarioEntry {
        public string Name;
        [TextArea(2, 4)]
        public string Description;
        public string SceneName;
        public bool Enabled = true;
    }
}
