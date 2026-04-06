using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Quickly switch all com.archerstudio.sdk.* packages between local (file:) and git sources.
    /// Menu: ArcherStudio > SDK > Source Switcher
    /// </summary>
    public class SDKSourceSwitcher : EditorWindow {
        private const string Tag = "SDKSourceSwitcher";
        private const string PackagePrefix = "com.archerstudio.sdk.";
        private const string PrefsKeyGitUrl = "ArcherSDK_GitRepoUrl";
        private const string PrefsKeyGitRef = "ArcherSDK_GitRef";
        private const string PrefsKeyLocalPath = "ArcherSDK_LocalPath";

        // ─── State ───
        private string _gitRepoUrl;
        private string _gitRef;
        private string _localRelativePath;
        private Vector2 _scrollPos;
        private List<PackageEntry> _packages = new();
        private SourceMode _currentMode = SourceMode.Unknown;
        private string _manifestPath;

        private enum SourceMode { Unknown, Local, Git, Mixed }

        [MenuItem("ArcherStudio/SDK/Source Switcher", false, 10)]
        public static void ShowWindow() {
            var window = GetWindow<SDKSourceSwitcher>("SDK Source Switcher");
            window.minSize = new Vector2(520, 400);
            window.Show();
        }

        private void OnEnable() {
            _gitRepoUrl = EditorPrefs.GetString(PrefsKeyGitUrl, "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git");
            _gitRef = EditorPrefs.GetString(PrefsKeyGitRef, "v0.1.4");
            _localRelativePath = EditorPrefs.GetString(PrefsKeyLocalPath, "../../archer-core-sdk-unity");
            _manifestPath = Path.GetFullPath("Packages/manifest.json");
            Refresh();
        }

        private void OnFocus() {
            Refresh();
        }

        private void Refresh() {
            _packages.Clear();
            if (!File.Exists(_manifestPath)) return;

            var content = File.ReadAllText(_manifestPath);
            // Match: "com.archerstudio.sdk.xxx": "value"
            var regex = new Regex($@"""({Regex.Escape(PackagePrefix)}[^""]+)""\s*:\s*""([^""]+)""");
            foreach (Match match in regex.Matches(content)) {
                var entry = new PackageEntry {
                    Name = match.Groups[1].Value,
                    CurrentSource = match.Groups[2].Value
                };
                entry.IsLocal = entry.CurrentSource.StartsWith("file:");
                entry.IsGit = entry.CurrentSource.Contains("github.com") || entry.CurrentSource.Contains(".git");
                _packages.Add(entry);
            }

            // Detect overall mode
            if (_packages.Count == 0) {
                _currentMode = SourceMode.Unknown;
            } else if (_packages.All(p => p.IsLocal)) {
                _currentMode = SourceMode.Local;
            } else if (_packages.All(p => p.IsGit)) {
                _currentMode = SourceMode.Git;
            } else {
                _currentMode = SourceMode.Mixed;
            }
        }

        private void OnGUI() {
            DrawHeader();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawCurrentStatus();
            EditorGUILayout.Space(8);
            DrawSettings();
            EditorGUILayout.Space(8);
            DrawSwitchButtons();
            EditorGUILayout.Space(12);
            DrawPackageList();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader() {
            EditorGUILayout.Space(4);
            var style = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("SDK Source Switcher", style);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void DrawCurrentStatus() {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Mode:", EditorStyles.boldLabel, GUILayout.Width(100));

            var (label, color) = _currentMode switch {
                SourceMode.Local => ("LOCAL (file:)", new Color(0.3f, 0.8f, 0.3f)),
                SourceMode.Git => ("GIT (remote)", new Color(0.4f, 0.7f, 1f)),
                SourceMode.Mixed => ("MIXED", new Color(0.9f, 0.8f, 0.2f)),
                _ => ("Unknown", Color.gray)
            };

            var statusStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color }, fontSize = 14 };
            EditorGUILayout.LabelField(label, statusStyle);
            EditorGUILayout.EndHorizontal();

            if (_currentMode == SourceMode.Mixed) {
                EditorGUILayout.HelpBox("Some packages use local, some use git. Use the buttons below to unify.", MessageType.Warning);
            }
        }

        private void DrawSettings() {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Git Source", EditorStyles.miniLabel);
            var newGitUrl = EditorGUILayout.TextField("Repository URL", _gitRepoUrl);
            var newGitRef = EditorGUILayout.TextField("Branch / Tag", _gitRef);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Local Source", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            var newLocalPath = EditorGUILayout.TextField("Relative Path", _localRelativePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60))) {
                var abs = EditorUtility.OpenFolderPanel("Select SDK Root Folder", "", "");
                if (!string.IsNullOrEmpty(abs)) {
                    // Convert to relative from Packages/
                    var packagesDir = Path.GetFullPath("Packages");
                    newLocalPath = GetRelativePath(packagesDir, abs).Replace('\\', '/');
                }
            }
            EditorGUILayout.EndHorizontal();

            // Validate local path
            var resolvedLocal = Path.GetFullPath(Path.Combine("Packages", newLocalPath));
            bool localExists = Directory.Exists(resolvedLocal);
            if (!localExists) {
                EditorGUILayout.HelpBox($"Local path not found: {resolvedLocal}", MessageType.Warning);
            } else {
                EditorGUILayout.HelpBox($"Resolved: {resolvedLocal}", MessageType.None);
            }

            EditorGUILayout.EndVertical();

            // Save if changed
            if (newGitUrl != _gitRepoUrl) { _gitRepoUrl = newGitUrl; EditorPrefs.SetString(PrefsKeyGitUrl, _gitRepoUrl); }
            if (newGitRef != _gitRef) { _gitRef = newGitRef; EditorPrefs.SetString(PrefsKeyGitRef, _gitRef); }
            if (newLocalPath != _localRelativePath) { _localRelativePath = newLocalPath; EditorPrefs.SetString(PrefsKeyLocalPath, _localRelativePath); }
        }

        private void DrawSwitchButtons() {
            EditorGUILayout.LabelField("Switch All Packages", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Switch to Local
            GUI.enabled = _currentMode != SourceMode.Local;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Switch to LOCAL", GUILayout.Height(35))) {
                if (EditorUtility.DisplayDialog("Switch to Local",
                    $"All SDK packages will use:\nfile:{_localRelativePath}/...\n\nUnity will reimport packages. Continue?",
                    "Switch", "Cancel")) {
                    SwitchAll(toLocal: true);
                }
            }

            // Switch to Git
            GUI.enabled = _currentMode != SourceMode.Git;
            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("Switch to GIT", GUILayout.Height(35))) {
                if (EditorUtility.DisplayDialog("Switch to Git",
                    $"All SDK packages will use:\n{_gitRepoUrl}?path=...#{_gitRef}\n\nUnity will reimport packages. Continue?",
                    "Switch", "Cancel")) {
                    SwitchAll(toLocal: false);
                }
            }

            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPackageList() {
            EditorGUILayout.LabelField($"Packages ({_packages.Count})", EditorStyles.boldLabel);

            foreach (var pkg in _packages) {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Status icon
                var iconColor = pkg.IsLocal ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.4f, 0.7f, 1f);
                var prevColor = GUI.contentColor;
                GUI.contentColor = iconColor;
                GUILayout.Label(pkg.IsLocal ? "L" : "G", EditorStyles.boldLabel, GUILayout.Width(16));
                GUI.contentColor = prevColor;

                // Package name (short)
                string shortName = pkg.Name.Replace(PackagePrefix, "");
                EditorGUILayout.LabelField(shortName, EditorStyles.boldLabel, GUILayout.Width(110));

                // Current source (truncated)
                string displaySource = pkg.CurrentSource;
                if (displaySource.Length > 60) displaySource = "..." + displaySource.Substring(displaySource.Length - 57);
                EditorGUILayout.LabelField(displaySource, EditorStyles.miniLabel);

                // Individual toggle button
                string toggleLabel = pkg.IsLocal ? "-> Git" : "-> Local";
                if (GUILayout.Button(toggleLabel, GUILayout.Width(65))) {
                    SwitchSingle(pkg, !pkg.IsLocal);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        // ─── Switch Logic ───

        private void SwitchAll(bool toLocal) {
            if (!File.Exists(_manifestPath)) {
                Debug.LogError($"[{Tag}] manifest.json not found");
                return;
            }

            var content = File.ReadAllText(_manifestPath);

            foreach (var pkg in _packages) {
                string newSource = toLocal
                    ? BuildLocalSource(pkg.Name)
                    : BuildGitSource(pkg.Name);

                content = content.Replace(
                    $"\"{pkg.Name}\": \"{pkg.CurrentSource}\"",
                    $"\"{pkg.Name}\": \"{newSource}\"");
            }

            File.WriteAllText(_manifestPath, content);
            Debug.Log($"[{Tag}] Switched all SDK packages to {(toLocal ? "LOCAL" : "GIT")}");

            AssetDatabase.Refresh();
            Refresh();

            UnityEditor.PackageManager.Client.Resolve();
        }

        private void SwitchSingle(PackageEntry pkg, bool toLocal) {
            if (!File.Exists(_manifestPath)) return;

            var content = File.ReadAllText(_manifestPath);
            string newSource = toLocal
                ? BuildLocalSource(pkg.Name)
                : BuildGitSource(pkg.Name);

            content = content.Replace(
                $"\"{pkg.Name}\": \"{pkg.CurrentSource}\"",
                $"\"{pkg.Name}\": \"{newSource}\"");

            File.WriteAllText(_manifestPath, content);
            Debug.Log($"[{Tag}] Switched {pkg.Name} to {(toLocal ? "LOCAL" : "GIT")}");

            AssetDatabase.Refresh();
            Refresh();

            UnityEditor.PackageManager.Client.Resolve();
        }

        private string BuildLocalSource(string packageName) {
            return $"file:{_localRelativePath}/{packageName}";
        }

        private string BuildGitSource(string packageName) {
            return $"{_gitRepoUrl}?path={packageName}#{_gitRef}";
        }

        // ─── Helpers ───

        private static string GetRelativePath(string fromPath, string toPath) {
            var fromUri = new Uri(fromPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var toUri = new Uri(toPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var result = Uri.UnescapeDataString(relativeUri.ToString());
            return result.TrimEnd('/');
        }

        private class PackageEntry {
            public string Name;
            public string CurrentSource;
            public bool IsLocal;
            public bool IsGit;
        }
    }
}
