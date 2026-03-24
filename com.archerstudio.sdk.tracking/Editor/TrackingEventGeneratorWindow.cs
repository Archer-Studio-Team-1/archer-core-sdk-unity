using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArcherStudio.SDK.Tracking.Editor {
    public class TrackingEventGeneratorWindow : EditorWindow {
        private string _fileName = "NewEvents";
        private readonly List<EventDefinition> _events = new List<EventDefinition>();
        private Vector2 _scrollPos;

        private enum DataType {
            String,
            Int,
            Double,
            Long,
            Bool
        }

        [Serializable]
        private class ParamData {
            public string Name = "paramName";
            public string Constant = "TrackingConstants.PAR_";
            public DataType Type = DataType.String;
            public bool UserEditedConstant;
        }

        [Serializable]
        private class EventDefinition {
            public string RawName = "event_name";
            public string ClassName = "EventNameEvent";
            public string EventConstant = "TrackingConstants.EVT_";
            public List<ParamData> Params = new List<ParamData>();
            public bool UserEditedClassName;
            public bool UserEditedConstant;
            public bool Foldout = true;
        }

        [MenuItem("ArcherStudio/Tracking/Event Generator")]
        public static void ShowWindow() {
            var window = GetWindow<TrackingEventGeneratorWindow>("Event Generator");
            window.minSize = new Vector2(500, 400);
        }

        private void OnEnable() {
            if (_events.Count == 0) {
                _events.Add(new EventDefinition());
            }
        }

        private void OnGUI() {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            _fileName = EditorGUILayout.TextField("File Name (.cs)", _fileName);
            if (GUILayout.Button("Load File", GUILayout.Width(80))) {
                LoadExistingFile();
            }
            if (GUILayout.Button("Reset", GUILayout.Width(60))) {
                ResetFields();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _events.Count; i++) {
                var evt = _events[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                evt.Foldout = EditorGUILayout.Foldout(evt.Foldout, $"Event: {evt.ClassName}", true);
                if (GUILayout.Button("Remove Event", GUILayout.Width(100))) {
                    _events.RemoveAt(i);
                    i--;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                if (evt.Foldout) {
                    EditorGUI.BeginChangeCheck();
                    evt.RawName = EditorGUILayout.TextField("Event Name (snake_case)", evt.RawName);
                    if (EditorGUI.EndChangeCheck()) {
                        UpdateAutoFields(evt);
                    }

                    EditorGUI.BeginChangeCheck();
                    evt.ClassName = EditorGUILayout.TextField("Class Name", evt.ClassName);
                    if (EditorGUI.EndChangeCheck()) {
                        evt.UserEditedClassName = true;
                    }

                    EditorGUI.BeginChangeCheck();
                    evt.EventConstant = EditorGUILayout.TextField("Event Constant", evt.EventConstant);
                    if (EditorGUI.EndChangeCheck()) {
                        evt.UserEditedConstant = true;
                    }

                    EditorGUILayout.Space();
                    GUILayout.Label("Parameters", EditorStyles.miniBoldLabel);

                    for (int j = 0; j < evt.Params.Count; j++) {
                        var p = evt.Params[j];
                        EditorGUILayout.BeginHorizontal();
                        
                        EditorGUI.BeginChangeCheck();
                        p.Name = EditorGUILayout.TextField(p.Name, GUILayout.Width(150));
                        if (EditorGUI.EndChangeCheck()) {
                            if (!p.UserEditedConstant) {
                                p.Constant = "TrackingConstants.PAR_" + p.Name.ToUpper();
                            }
                        }

                        EditorGUI.BeginChangeCheck();
                        p.Constant = EditorGUILayout.TextField(p.Constant);
                        if (EditorGUI.EndChangeCheck()) {
                            p.UserEditedConstant = true;
                        }

                        p.Type = (DataType)EditorGUILayout.EnumPopup(p.Type, GUILayout.Width(70));
                        if (GUILayout.Button("X", GUILayout.Width(20))) {
                            evt.Params.RemoveAt(j);
                            j--;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button("+ Add Param", GUILayout.Width(100))) {
                        evt.Params.Add(new ParamData());
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            if (GUILayout.Button("Add New Event to Group", GUILayout.Height(30))) {
                _events.Add(new EventDefinition());
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate File", GUILayout.Height(40))) {
                GenerateFile();
            }
        }

        private void ResetFields() {
            _fileName = "NewEvents";
            _events.Clear();
            _events.Add(new EventDefinition());
            GUI.FocusControl(null);
        }

        private void LoadExistingFile() {
            string path = EditorUtility.OpenFilePanel("Select Event File", "Assets/ArcherStudio/Tracking/Events", "cs");
            if (string.IsNullOrEmpty(path)) return;

            _fileName = Path.GetFileNameWithoutExtension(path);
            string content = File.ReadAllText(path);
            
            string constantsPath = "Assets/ArcherStudio/Tracking/Core/TrackingConstants.cs";
            string constantsContent = File.Exists(constantsPath) ? File.ReadAllText(constantsPath) : "";

            _events.Clear();
            
            var classMatches = System.Text.RegularExpressions.Regex.Matches(content, @"public class (\w+) : GameTrackingEvent");
            foreach (System.Text.RegularExpressions.Match match in classMatches) {
                var evt = new EventDefinition { 
                    ClassName = match.Groups[1].Value, 
                    UserEditedClassName = true, 
                    UserEditedConstant = true,
                    Foldout = false
                };
                
                int classStart = match.Index;
                int nextClassStart = content.IndexOf("public class", classStart + 1, StringComparison.Ordinal);
                string classBlock = nextClassStart > 0 ? content.Substring(classStart, nextClassStart - classStart) : content.Substring(classStart);

                // Extract EventConstant and RawName
                var constMatch = System.Text.RegularExpressions.Regex.Match(classBlock, @"EventName => ([\w\.]+)");
                if (constMatch.Success) {
                    evt.EventConstant = constMatch.Groups[1].Value;
                    string constName = evt.EventConstant.Replace("TrackingConstants.", "").Trim();
                    
                    // Try to find RawName locally
                    var localRawMatch = System.Text.RegularExpressions.Regex.Match(classBlock, $@"private const string {constName} = ""(.*?)""");
                    if (localRawMatch.Success) {
                        evt.RawName = localRawMatch.Groups[1].Value;
                    } else {
                        // Try global
                        var globalRawMatch = System.Text.RegularExpressions.Regex.Match(constantsContent, $@"public const string {constName} = ""(.*?)""");
                        if (globalRawMatch.Success) evt.RawName = globalRawMatch.Groups[1].Value;
                    }
                }

                // Extract Params from constructor
                var ctorMatch = System.Text.RegularExpressions.Regex.Match(classBlock, @"public \w+\((.*?)\)");
                if (ctorMatch.Success) {
                    string[] args = ctorMatch.Groups[1].Value.Split(',');
                    foreach (var arg in args) {
                        if (string.IsNullOrWhiteSpace(arg)) continue;
                        string[] parts = arg.Trim().Split(' ');
                        if (parts.Length >= 2) {
                            string typeStr = parts[0];
                            string nameStr = parts[1]; // camelCase field name
                            var dataType = DataType.String;
                            if (typeStr == "int") dataType = DataType.Int;
                            else if (typeStr == "double") dataType = DataType.Double;
                            else if (typeStr == "long") dataType = DataType.Long;
                            else if (typeStr == "bool") dataType = DataType.Bool;

                            // Find the constant in BuildParams
                            var paramConstMatch = System.Text.RegularExpressions.Regex.Match(classBlock, $@"dict\.Add\(([\w\.]+), _{nameStr}\)");
                            string pConst = paramConstMatch.Success ? paramConstMatch.Groups[1].Value : "TrackingConstants.PAR_";
                            
                            // Try to find the RawName for this param to restore original tool "Name"
                            string pConstName = pConst.Replace("TrackingConstants.", "").Trim();
                            string pRawName = nameStr; // fallback
                            
                            // Check local
                            var localPRawMatch = System.Text.RegularExpressions.Regex.Match(classBlock, $@"private const string {pConstName} = ""(.*?)""");
                            if (localPRawMatch.Success) {
                                pRawName = localPRawMatch.Groups[1].Value;
                            } else {
                                // Check global
                                var globalPRawMatch = System.Text.RegularExpressions.Regex.Match(constantsContent, $@"public const string {pConstName} = ""(.*?)""");
                                if (globalPRawMatch.Success) pRawName = globalPRawMatch.Groups[1].Value;
                            }

                            evt.Params.Add(new ParamData { 
                                Name = pRawName, 
                                Type = dataType, 
                                Constant = pConst,
                                UserEditedConstant = true
                            });
                        }
                    }
                }
                _events.Add(evt);
            }
            
            if (_events.Count == 0) _events.Add(new EventDefinition());
        }

        private void UpdateAutoFields(EventDefinition evt) {
            if (string.IsNullOrEmpty(evt.RawName)) return;

            string[] parts = evt.RawName.Split('_');

            if (!evt.UserEditedClassName) {
                StringBuilder sb = new StringBuilder();
                foreach (var p in parts) {
                    if (p.Length > 0) sb.Append(char.ToUpper(p[0]) + p.Substring(1).ToLower());
                }
                sb.Append("Event");
                evt.ClassName = sb.ToString();
            }

            if (!evt.UserEditedConstant) {
                evt.EventConstant = "TrackingConstants.EVT_" + evt.RawName.ToUpper();
            }
        }

        private string ToCamelCase(string str) {
            if (string.IsNullOrEmpty(str)) return str;
            string[] parts = str.Split('_');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++) {
                string part = parts[i];
                if (string.IsNullOrEmpty(part)) continue;
                if (i == 0) {
                    sb.Append(char.ToLower(part[0]) + part.Substring(1));
                } else {
                    sb.Append(char.ToUpper(part[0]) + part.Substring(1).ToLower());
                }
            }
            return sb.ToString();
        }

        private void GenerateFile() {
            if (string.IsNullOrEmpty(_fileName)) {
                EditorUtility.DisplayDialog("Error", "File name cannot be empty!", "OK");
                return;
            }

            string constantsPath = "Assets/ArcherStudio/Tracking/Core/TrackingConstants.cs";
            string constantsContent = File.Exists(constantsPath) ? File.ReadAllText(constantsPath) : "";

            string folderPath = "Assets/ArcherStudio/Tracking/Events";
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, _fileName + ".cs");
            if (File.Exists(filePath)) {
                if (!EditorUtility.DisplayDialog("Warning", $"File '{_fileName}.cs' already exists. Overwrite?", "Yes", "No")) return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using ArcherStudio.Game.Tracking;");
            sb.AppendLine("");
            sb.AppendLine("namespace ArcherStudio.Game.Tracking.Events {");

            foreach (var evt in _events) {
                sb.AppendLine($"    public class {evt.ClassName} : GameTrackingEvent {{");
                
                string evtConstClean = evt.EventConstant.Replace("TrackingConstants.", "").Trim();
                bool evtGlobal = constantsContent.Contains($"public const string {evtConstClean}");
                
                if (!evtGlobal) {
                    sb.AppendLine($"        private const string {evtConstClean} = \"{evt.RawName}\";");
                }
                
                sb.AppendLine($"        public override string EventName => {(evtGlobal ? evt.EventConstant : evtConstClean)};");
                sb.AppendLine("");

                // Local Constants for missing Params
                foreach (var p in evt.Params) {
                    string pConstClean = p.Constant.Replace("TrackingConstants.", "").Trim();
                    if (!constantsContent.Contains($"public const string {pConstClean}")) {
                        sb.AppendLine($"        private const string {pConstClean} = \"{p.Name}\";");
                    }
                }
                sb.AppendLine("");

                foreach (var p in evt.Params) {
                    string camelName = ToCamelCase(p.Name);
                    sb.AppendLine($"        private {p.Type.ToString().ToLower()} _{camelName};");
                }

                sb.AppendLine("");
                string args = string.Join(", ", evt.Params.Select(p => $"{p.Type.ToString().ToLower()} {ToCamelCase(p.Name)}"));
                sb.AppendLine($"        public {evt.ClassName}({args}) {{");
                foreach (var p in evt.Params) {
                    string camelName = ToCamelCase(p.Name);
                    sb.AppendLine($"            _{camelName} = {camelName};");
                }
                sb.AppendLine("        }");
                sb.AppendLine("");

                sb.AppendLine("        protected override void BuildParams(Dictionary<string, object> dict) {");
                foreach (var p in evt.Params) {
                    string pConstClean = p.Constant.Replace("TrackingConstants.", "").Trim();
                    bool pGlobal = constantsContent.Contains($"public const string {pConstClean}");
                    string finalConst = pGlobal ? p.Constant : pConstClean;
                    string camelName = ToCamelCase(p.Name);
                    sb.AppendLine($"            dict.Add({finalConst}, _{camelName});");
                }
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("");
            }

            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString());
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Generated events in '{_fileName}.cs'. Constants were resolved globally or locally.", "OK");
        }
    }
}