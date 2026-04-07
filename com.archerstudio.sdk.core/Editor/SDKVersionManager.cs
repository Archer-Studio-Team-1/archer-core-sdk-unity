using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Bump all SDK package versions and auto-create a git tag.
    /// Menu: ArcherStudio > SDK > Version Manager
    /// </summary>
    public class SDKVersionManager : EditorWindow {
        private const string Tag = "SDKVersionManager";
        private const string PackagePrefix = "com.archerstudio.sdk.";

        private string _currentVersion = "?";
        private string _newVersion = "";
        private BumpType _bumpType = BumpType.Patch;
        private bool _autoTag = true;
        private bool _autoCommit = true;
        private bool _autoPush = false;
        private string _sdkRootPath;
        private Vector2 _scrollPos;
        private List<PackageVersionEntry> _packages = new();
        private string _lastOutput = "";

        private enum BumpType { Major, Minor, Patch, Custom }

        [MenuItem("ArcherStudio/SDK/Version Manager", false, 11)]
        public static void ShowWindow() {
            var window = GetWindow<SDKVersionManager>("SDK Version Manager");
            window.minSize = new Vector2(480, 420);
            window.Show();
        }

        private void OnEnable() {
            DetectSdkRoot();
            Refresh();
        }

        private void OnFocus() {
            Refresh();
        }

        private void DetectSdkRoot() {
            // Try to find SDK root from this package's location
            var guids = AssetDatabase.FindAssets("t:asmdef ArcherStudio.SDK.Core");
            if (guids.Length > 0) {
                var asmdefPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                // Go up from Runtime/ArcherStudio.SDK.Core.asmdef -> package root -> SDK root
                var packageDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(asmdefPath)));
                _sdkRootPath = Path.GetDirectoryName(packageDir);
            }

            // Fallback: check if Packages/manifest.json has a file: reference
            if (string.IsNullOrEmpty(_sdkRootPath) || !Directory.Exists(_sdkRootPath)) {
                string manifestPath = Path.GetFullPath("Packages/manifest.json");
                if (File.Exists(manifestPath)) {
                    var content = File.ReadAllText(manifestPath);
                    var match = Regex.Match(content, @"""com\.archerstudio\.sdk\.core""\s*:\s*""file:([^""]+)""");
                    if (match.Success) {
                        string relativePath = match.Groups[1].Value;
                        // file: paths are relative to Packages/
                        string corePath = Path.GetFullPath(Path.Combine("Packages", relativePath));
                        _sdkRootPath = Path.GetDirectoryName(corePath);
                    }
                }
            }
        }

        private void Refresh() {
            _packages.Clear();
            if (string.IsNullOrEmpty(_sdkRootPath) || !Directory.Exists(_sdkRootPath)) return;

            var dirs = Directory.GetDirectories(_sdkRootPath, "com.archerstudio.sdk.*");
            foreach (var dir in dirs.OrderBy(d => d)) {
                var pkgJsonPath = Path.Combine(dir, "package.json");
                if (!File.Exists(pkgJsonPath)) continue;

                var json = File.ReadAllText(pkgJsonPath);
                var nameMatch = Regex.Match(json, @"""name""\s*:\s*""([^""]+)""");
                var versionMatch = Regex.Match(json, @"""version""\s*:\s*""([^""]+)""");

                _packages.Add(new PackageVersionEntry {
                    Name = nameMatch.Success ? nameMatch.Groups[1].Value : Path.GetFileName(dir),
                    Version = versionMatch.Success ? versionMatch.Groups[1].Value : "?",
                    PackageJsonPath = pkgJsonPath
                });
            }

            // Current version = core package version (all should be aligned)
            var core = _packages.FirstOrDefault(p => p.Name.EndsWith(".core"));
            _currentVersion = core?.Version ?? (_packages.Count > 0 ? _packages[0].Version : "?");

            if (string.IsNullOrEmpty(_newVersion) || _bumpType != BumpType.Custom) {
                _newVersion = _currentVersion;
            }
        }

        private void OnGUI() {
            DrawHeader();
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawCurrentVersion();
            EditorGUILayout.Space(8);
            DrawBumpControls();
            EditorGUILayout.Space(8);
            DrawOptions();
            EditorGUILayout.Space(8);
            DrawPackageList();
            EditorGUILayout.Space(8);
            DrawOutput();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader() {
            EditorGUILayout.Space(4);
            var style = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("SDK Version Manager", style);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void DrawCurrentVersion() {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Version:", EditorStyles.boldLabel, GUILayout.Width(110));
            var versionStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 18,
                normal = { textColor = new Color(0.4f, 0.8f, 0.4f) }
            };
            EditorGUILayout.LabelField(_currentVersion, versionStyle);
            EditorGUILayout.EndHorizontal();

            // Show latest git tag
            string latestTag = RunGit("describe --tags --abbrev=0 2>/dev/null || echo (none)").Trim();
            EditorGUILayout.LabelField($"Latest Git Tag: {latestTag}", EditorStyles.miniLabel);
        }

        private void DrawBumpControls() {
            EditorGUILayout.LabelField("Version Bump", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Major", _bumpType == BumpType.Major ? GetActiveToolbarStyle() : EditorStyles.toolbarButton)) {
                _bumpType = BumpType.Major;
                _newVersion = BumpVersion(_currentVersion, BumpType.Major);
            }
            if (GUILayout.Button("Minor", _bumpType == BumpType.Minor ? GetActiveToolbarStyle() : EditorStyles.toolbarButton)) {
                _bumpType = BumpType.Minor;
                _newVersion = BumpVersion(_currentVersion, BumpType.Minor);
            }
            if (GUILayout.Button("Patch", _bumpType == BumpType.Patch ? GetActiveToolbarStyle() : EditorStyles.toolbarButton)) {
                _bumpType = BumpType.Patch;
                _newVersion = BumpVersion(_currentVersion, BumpType.Patch);
            }
            if (GUILayout.Button("Custom", _bumpType == BumpType.Custom ? GetActiveToolbarStyle() : EditorStyles.toolbarButton)) {
                _bumpType = BumpType.Custom;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("New Version:", GUILayout.Width(85));
            GUI.enabled = _bumpType == BumpType.Custom;
            _newVersion = EditorGUILayout.TextField(_newVersion);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"{_currentVersion}  ->  {_newVersion}", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawOptions() {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _autoCommit = EditorGUILayout.Toggle("Auto Commit", _autoCommit);
            _autoTag = EditorGUILayout.Toggle("Auto Tag (v{version})", _autoTag);
            _autoPush = EditorGUILayout.Toggle("Auto Push (commit + tag)", _autoPush);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            bool versionValid = IsValidVersion(_newVersion) && _newVersion != _currentVersion;
            GUI.enabled = versionValid;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button($"Bump to {_newVersion}", GUILayout.Height(35))) {
                if (EditorUtility.DisplayDialog("Bump Version",
                    $"This will:\n" +
                    $"- Update {_packages.Count} package.json files: {_currentVersion} -> {_newVersion}\n" +
                    (_autoCommit ? $"- Git commit\n" : "") +
                    (_autoTag ? $"- Git tag v{_newVersion}\n" : "") +
                    (_autoPush ? "- Git push (commit + tag)\n" : "") +
                    "\nContinue?", "Bump", "Cancel")) {
                    ExecuteBump();
                }
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private void DrawPackageList() {
            EditorGUILayout.LabelField($"Packages ({_packages.Count})", EditorStyles.boldLabel);

            foreach (var pkg in _packages) {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                string shortName = pkg.Name.Replace(PackagePrefix, "");
                EditorGUILayout.LabelField(shortName, EditorStyles.boldLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField(pkg.Version, GUILayout.Width(60));

                bool aligned = pkg.Version == _currentVersion;
                if (!aligned) {
                    var prevColor = GUI.contentColor;
                    GUI.contentColor = new Color(0.9f, 0.4f, 0.3f);
                    GUILayout.Label("(misaligned)", EditorStyles.miniLabel);
                    GUI.contentColor = prevColor;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawOutput() {
            if (string.IsNullOrEmpty(_lastOutput)) return;
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_lastOutput, GUILayout.MinHeight(60));
        }

        // ─── Bump Logic ───

        private void ExecuteBump() {
            var log = new System.Text.StringBuilder();
            string oldVersion = _currentVersion;

            // 1. Update all package.json files
            int updated = 0;
            foreach (var pkg in _packages) {
                var content = File.ReadAllText(pkg.PackageJsonPath);
                // Replace version field and dependency versions
                var newContent = content.Replace($"\"{oldVersion}\"", $"\"{_newVersion}\"");
                if (newContent != content) {
                    File.WriteAllText(pkg.PackageJsonPath, newContent);
                    updated++;
                    log.AppendLine($"  Updated {pkg.Name}");
                }
            }
            log.AppendLine($"Updated {updated}/{_packages.Count} packages: {oldVersion} -> {_newVersion}");

            // 2. Git commit
            if (_autoCommit) {
                RunGit("add -A");
                string commitOutput = RunGit($"commit -m \"chore: bump SDK version to {_newVersion}\"");
                log.AppendLine(commitOutput.Trim());
            }

            // 3. Git tag
            if (_autoTag) {
                string tagName = $"v{_newVersion}";
                string tagOutput = RunGit($"tag {tagName}");
                log.AppendLine(string.IsNullOrEmpty(tagOutput) ? $"Tagged {tagName}" : tagOutput.Trim());
            }

            // 4. Git push
            if (_autoPush) {
                string pushOutput = RunGit("push");
                log.AppendLine(pushOutput.Trim());
                if (_autoTag) {
                    string pushTagOutput = RunGit("push --tags");
                    log.AppendLine(pushTagOutput.Trim());
                }
            }

            _lastOutput = log.ToString();
            Debug.Log($"[{Tag}] {_lastOutput}");
            Refresh();
        }

        // ─── Helpers ───

        private static GUIStyle _activeToolbarStyle;
        private static GUIStyle GetActiveToolbarStyle() {
            if (_activeToolbarStyle == null) {
                _activeToolbarStyle = new GUIStyle(EditorStyles.toolbarButton) {
                    normal = EditorStyles.toolbarButton.onNormal,
                    fontStyle = FontStyle.Bold
                };
            }
            return _activeToolbarStyle;
        }

        private static string BumpVersion(string version, BumpType type) {
            var parts = version.Split('.');
            if (parts.Length != 3) return version;

            if (!int.TryParse(parts[0], out int major) ||
                !int.TryParse(parts[1], out int minor) ||
                !int.TryParse(parts[2], out int patch)) {
                return version;
            }

            switch (type) {
                case BumpType.Major: return $"{major + 1}.0.0";
                case BumpType.Minor: return $"{major}.{minor + 1}.0";
                case BumpType.Patch: return $"{major}.{minor}.{patch + 1}";
                default: return version;
            }
        }

        private static bool IsValidVersion(string version) {
            return Regex.IsMatch(version, @"^\d+\.\d+\.\d+$");
        }

        private string RunGit(string arguments) {
            try {
                var startInfo = new System.Diagnostics.ProcessStartInfo {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = _sdkRootPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return !string.IsNullOrEmpty(error) && process.ExitCode != 0
                    ? $"ERROR: {error}"
                    : output + error;
            } catch (Exception e) {
                return $"Git error: {e.Message}";
            }
        }

        private class PackageVersionEntry {
            public string Name;
            public string Version;
            public string PackageJsonPath;
        }
    }
}
