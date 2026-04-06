using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArcherStudio.SDK.TestLab.Editor {

    /// <summary>
    /// Editor window for Firebase Test Lab configuration and test execution.
    /// Menu: ArcherStudio > SDK > Firebase Test Lab
    /// </summary>
    public class TestLabWindow : EditorWindow {
        private const string ResourcesPath = "Assets/Resources";

        private Vector2 _scrollPos;
        private int _currentTab;
        private readonly string[] _tabNames = { "Config", "Run Tests", "Scenarios", "gcloud Help" };

        // Config tab
        private TestLabConfig _config;
        private UnityEditor.Editor _configEditor;

        // Run tab
        private string _apkPath = "";
        private string _ipaPath = "";
        private string _selectedScenarios = "1";
        private List<DeviceEntry> _androidDevices = new();
        private List<DeviceEntry> _iosDevices = new();
        private string _lastCommand = "";
        private Vector2 _commandScrollPos;

        // Run result
        private string _lastOutput = "";
        private Vector2 _outputScrollPos;

        [MenuItem("ArcherStudio/SDK/Firebase Test Lab", false, 50)]
        public static void ShowWindow() {
            var window = GetWindow<TestLabWindow>("Firebase Test Lab");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnEnable() {
            LoadConfig();
            if (_androidDevices.Count == 0) {
                _androidDevices.Add(new DeviceEntry { Model = "Pixel6", Version = "33" });
            }
            if (_iosDevices.Count == 0) {
                _iosDevices.Add(new DeviceEntry { Model = "iphone13pro", Version = "15.7" });
            }
        }

        private void OnDisable() {
            if (_configEditor != null) {
                DestroyImmediate(_configEditor);
            }
        }

        private void LoadConfig() {
            _config = AssetDatabase.LoadAssetAtPath<TestLabConfig>($"{ResourcesPath}/TestLabConfig.asset");
            if (_config != null && _configEditor == null) {
                _configEditor = UnityEditor.Editor.CreateEditor(_config);
            }
        }

        private void OnGUI() {
            DrawHeader();
            _currentTab = GUILayout.Toolbar(_currentTab, _tabNames);
            EditorGUILayout.Space(8);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_currentTab) {
                case 0: DrawConfigTab(); break;
                case 1: DrawRunTab(); break;
                case 2: DrawScenariosTab(); break;
                case 3: DrawHelpTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader() {
            EditorGUILayout.Space(4);
            var style = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Firebase Test Lab", style);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        // ─── Config Tab ───

        private void DrawConfigTab() {
            EditorGUILayout.LabelField("Test Lab Configuration", EditorStyles.boldLabel);

            if (_config == null) {
                EditorGUILayout.HelpBox("TestLabConfig asset not found. Create one to get started.", MessageType.Warning);
                if (GUILayout.Button("Create TestLabConfig", GUILayout.Height(30))) {
                    CreateConfig();
                }
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Config Asset")) {
                Selection.activeObject = _config;
                EditorGUIUtility.PingObject(_config);
            }
            if (GUILayout.Button("Refresh")) {
                LoadConfig();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            if (_configEditor != null) {
                _configEditor.OnInspectorGUI();
            }
        }

        private void CreateConfig() {
            if (!AssetDatabase.IsValidFolder(ResourcesPath)) {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            var config = ScriptableObject.CreateInstance<TestLabConfig>();
            AssetDatabase.CreateAsset(config, $"{ResourcesPath}/TestLabConfig.asset");
            AssetDatabase.SaveAssets();
            LoadConfig();
        }

        // ─── Run Tab ───

        private void DrawRunTab() {
            EditorGUILayout.LabelField("Run Tests on Firebase Test Lab", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generate gcloud commands to run Game Loop tests on real devices.\n" +
                "Requires: gcloud CLI installed and authenticated with a Firebase project.",
                MessageType.Info);

            EditorGUILayout.Space(8);

            // Android section
            EditorGUILayout.LabelField("Android", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _apkPath = EditorGUILayout.TextField("APK Path", _apkPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70))) {
                string path = EditorUtility.OpenFilePanel("Select APK", "", "apk");
                if (!string.IsNullOrEmpty(path)) _apkPath = path;
            }
            EditorGUILayout.EndHorizontal();

            DrawDeviceList(_androidDevices, "Android");

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Generate Android Command", GUILayout.Height(28))) {
                _lastCommand = GenerateAndroidCommand();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(12);

            // iOS section
            EditorGUILayout.LabelField("iOS", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _ipaPath = EditorGUILayout.TextField("IPA/ZIP Path", _ipaPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70))) {
                string path = EditorUtility.OpenFilePanel("Select IPA or ZIP", "", "ipa,zip");
                if (!string.IsNullOrEmpty(path)) _ipaPath = path;
            }
            EditorGUILayout.EndHorizontal();

            DrawDeviceList(_iosDevices, "iOS");

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Generate iOS Command", GUILayout.Height(28))) {
                _lastCommand = GenerateIosCommand();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            // Common settings
            EditorGUILayout.LabelField("Common Settings", EditorStyles.boldLabel);
            _selectedScenarios = EditorGUILayout.TextField("Scenario Numbers", _selectedScenarios);
            EditorGUILayout.HelpBox("Comma-separated scenario numbers (e.g. 1,2,3)", MessageType.None);

            // Command output
            if (!string.IsNullOrEmpty(_lastCommand)) {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Generated Command", EditorStyles.boldLabel);
                _commandScrollPos = EditorGUILayout.BeginScrollView(_commandScrollPos, GUILayout.Height(100));
                EditorGUILayout.TextArea(_lastCommand, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy to Clipboard")) {
                    GUIUtility.systemCopyBuffer = _lastCommand;
                    EditorUtility.DisplayDialog("Copied", "Command copied to clipboard.", "OK");
                }
                GUI.backgroundColor = new Color(1f, 0.6f, 0.3f);
                if (GUILayout.Button("Run Command")) {
                    RunGcloudCommand(_lastCommand);
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            // Output
            if (!string.IsNullOrEmpty(_lastOutput)) {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
                _outputScrollPos = EditorGUILayout.BeginScrollView(_outputScrollPos, GUILayout.Height(150));
                EditorGUILayout.TextArea(_lastOutput, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDeviceList(List<DeviceEntry> devices, string platform) {
            EditorGUILayout.LabelField($"  {platform} Devices:", EditorStyles.miniLabel);
            for (int i = 0; i < devices.Count; i++) {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                devices[i].Model = EditorGUILayout.TextField("Model", devices[i].Model);
                devices[i].Version = EditorGUILayout.TextField("Version", devices[i].Version);
                if (GUILayout.Button("-", GUILayout.Width(25)) && devices.Count > 1) {
                    devices.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            if (GUILayout.Button("+ Add Device", GUILayout.Width(120))) {
                devices.Add(new DeviceEntry {
                    Model = platform == "Android" ? "Pixel6" : "iphone13pro",
                    Version = platform == "Android" ? "33" : "15.7"
                });
            }
            EditorGUILayout.EndHorizontal();
        }

        private string GenerateAndroidCommand() {
            if (string.IsNullOrEmpty(_apkPath)) {
                EditorUtility.DisplayDialog("Error", "Please select an APK file.", "OK");
                return "";
            }

            var sb = new StringBuilder();
            sb.Append("gcloud firebase test android run");
            sb.Append(" --type game-loop");
            sb.Append($" --app \"{_apkPath}\"");

            if (!string.IsNullOrEmpty(_selectedScenarios)) {
                sb.Append($" --scenario-numbers {_selectedScenarios}");
            }

            foreach (var device in _androidDevices) {
                sb.Append($" --device model={device.Model},version={device.Version}");
            }

            string projectId = _config != null ? _config.FirebaseProjectId : "";
            if (!string.IsNullOrEmpty(projectId)) {
                sb.Append($" --project {projectId}");
            }

            int timeout = _config != null ? _config.TestTimeoutMinutes : 10;
            sb.Append($" --timeout {timeout}m");

            return sb.ToString();
        }

        private string GenerateIosCommand() {
            if (string.IsNullOrEmpty(_ipaPath)) {
                EditorUtility.DisplayDialog("Error", "Please select an IPA or ZIP file.", "OK");
                return "";
            }

            var sb = new StringBuilder();
            sb.Append("gcloud firebase test ios run");
            sb.Append(" --type game-loop");
            sb.Append($" --app \"{_ipaPath}\"");

            if (!string.IsNullOrEmpty(_selectedScenarios)) {
                sb.Append($" --scenario-numbers {_selectedScenarios}");
            }

            foreach (var device in _iosDevices) {
                sb.Append($" --device model={device.Model},version={device.Version}");
            }

            string projectId = _config != null ? _config.FirebaseProjectId : "";
            if (!string.IsNullOrEmpty(projectId)) {
                sb.Append($" --project {projectId}");
            }

            int timeout = _config != null ? _config.TestTimeoutMinutes : 10;
            sb.Append($" --timeout {timeout}m");

            return sb.ToString();
        }

        private void RunGcloudCommand(string command) {
            if (!EditorUtility.DisplayDialog("Run gcloud Command",
                $"This will execute:\n\n{command}\n\nContinue?", "Run", "Cancel")) {
                return;
            }

            try {
                var startInfo = new System.Diagnostics.ProcessStartInfo {
                    FileName = Application.platform == RuntimePlatform.WindowsEditor ? "cmd.exe" : "/bin/bash",
                    Arguments = Application.platform == RuntimePlatform.WindowsEditor
                        ? $"/c {command}"
                        : $"-c \"{command.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                _lastOutput = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error)) {
                    _lastOutput += "\n--- STDERR ---\n" + error;
                }
                _lastOutput += $"\n--- Exit code: {process.ExitCode} ---";
            } catch (System.Exception e) {
                _lastOutput = $"Failed to run command: {e.Message}\n\nMake sure gcloud CLI is installed and in PATH.";
            }

            Repaint();
        }

        // ─── Scenarios Tab ───

        private void DrawScenariosTab() {
            EditorGUILayout.LabelField("Scenario Management", EditorStyles.boldLabel);

            if (_config == null) {
                EditorGUILayout.HelpBox("Create a TestLabConfig first (Config tab).", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "Scenarios map to Firebase Test Lab scenario numbers (1-based).\n" +
                "Scenario 1 = first entry, Scenario 2 = second entry, etc.",
                MessageType.Info);

            EditorGUILayout.Space(8);

            for (int i = 0; i < _config.Scenarios.Count; i++) {
                var scenario = _config.Scenarios[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Scenario {i + 1}", EditorStyles.boldLabel, GUILayout.Width(80));
                scenario.Enabled = EditorGUILayout.Toggle("Enabled", scenario.Enabled);
                if (GUILayout.Button("Remove", GUILayout.Width(70))) {
                    _config.Scenarios.RemoveAt(i);
                    EditorUtility.SetDirty(_config);
                    i--;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                scenario.Name = EditorGUILayout.TextField("Name", scenario.Name);
                scenario.SceneName = EditorGUILayout.TextField("Scene Name", scenario.SceneName);
                scenario.Description = EditorGUILayout.TextField("Description", scenario.Description);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ Add Scenario", GUILayout.Height(25))) {
                _config.Scenarios.Add(new GameLoopScenarioEntry {
                    Name = $"Scenario {_config.Scenarios.Count + 1}",
                    Enabled = true
                });
                EditorUtility.SetDirty(_config);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Quick Add Built-in Scenarios", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Smoke Test")) {
                AddBuiltInScenario("Smoke Test", "Wait and verify no exceptions occur");
            }
            if (GUILayout.Button("FPS Test")) {
                AddBuiltInScenario("FPS Test", "Measure average FPS over time");
            }
            if (GUILayout.Button("Memory Test")) {
                AddBuiltInScenario("Memory Test", "Monitor peak memory usage");
            }
            EditorGUILayout.EndHorizontal();

            if (GUI.changed) {
                EditorUtility.SetDirty(_config);
            }
        }

        private void AddBuiltInScenario(string name, string description) {
            _config.Scenarios.Add(new GameLoopScenarioEntry {
                Name = name,
                Description = description,
                Enabled = true
            });
            EditorUtility.SetDirty(_config);
        }

        // ─── Help Tab ───

        private void DrawHelpTab() {
            EditorGUILayout.LabelField("gcloud CLI Reference", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox("Firebase Test Lab requires the gcloud CLI.\nInstall from: https://cloud.google.com/sdk/docs/install", MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            DrawCodeBlock(
                "# Login and set project\n" +
                "gcloud auth login\n" +
                "gcloud config set project YOUR_PROJECT_ID\n\n" +
                "# List available Android devices\n" +
                "gcloud firebase test android models list\n\n" +
                "# List available iOS devices\n" +
                "gcloud firebase test ios models list"
            );

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Android Game Loop", EditorStyles.boldLabel);
            DrawCodeBlock(
                "gcloud firebase test android run \\\n" +
                "  --type game-loop \\\n" +
                "  --app path/to/game.apk \\\n" +
                "  --scenario-numbers 1,2,3 \\\n" +
                "  --device model=Pixel6,version=33 \\\n" +
                "  --timeout 10m"
            );

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("iOS Game Loop", EditorStyles.boldLabel);
            DrawCodeBlock(
                "gcloud firebase test ios run \\\n" +
                "  --type game-loop \\\n" +
                "  --app path/to/game.ipa \\\n" +
                "  --scenario-numbers 1 \\\n" +
                "  --device model=iphone13pro,version=15.7 \\\n" +
                "  --timeout 10m"
            );

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Android Manifest Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The build processor automatically adds the required intent-filter to AndroidManifest.xml.\n" +
                "Manual entry (if needed):\n\n" +
                "<intent-filter>\n" +
                "  <action android:name=\"com.google.intent.action.TEST_LOOP\" />\n" +
                "  <category android:name=\"android.intent.category.DEFAULT\" />\n" +
                "  <data android:mimeType=\"application/javascript\" />\n" +
                "</intent-filter>",
                MessageType.None);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("iOS URL Scheme Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The build processor automatically adds the URL scheme to Info.plist.\n" +
                "Manual entry (if needed): Add 'firebase-game-loop' as a URL Type in Xcode.",
                MessageType.None);
        }

        private void DrawCodeBlock(string code) {
            var style = new GUIStyle(EditorStyles.textArea) {
                font = Font.CreateDynamicFontFromOSFont("Consolas", 12),
                wordWrap = true,
                richText = false
            };
            EditorGUILayout.TextArea(code, style, GUILayout.MinHeight(60));
        }

        [System.Serializable]
        private class DeviceEntry {
            public string Model;
            public string Version;
        }
    }
}
