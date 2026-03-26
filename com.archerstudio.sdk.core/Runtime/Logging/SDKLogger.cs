using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcherStudio.SDK.Core {

    public enum LogLevel {
        None = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Verbose = 99
    }

    /// <summary>
    /// Centralized logger for all SDK modules.
    /// Wraps UnityEngine.Debug with module-prefixed, color-coded messages
    /// and configurable log level. Supports runtime on-screen log viewer.
    /// </summary>
    public static class SDKLogger {
        private static LogLevel _minLevel = LogLevel.Debug;
        private static bool _enabled = true;

        /// <summary>
        /// On-screen log buffer for runtime debug overlay.
        /// Capped at MaxLogEntries to avoid memory issues on device.
        /// </summary>
        public static readonly List<LogEntry> LogBuffer = new List<LogEntry>(256);
        public static int MaxLogEntries = 200;

        /// <summary>
        /// Fired whenever a new log entry is added.
        /// UI components can subscribe to update in real-time.
        /// </summary>
        public static event Action<LogEntry> OnLogReceived;

        public static LogLevel CurrentMinLevel => _minLevel;
        public static bool IsEnabled => _enabled;

        public static void SetMinLevel(LogLevel level) {
            _minLevel = level;
        }

        public static void SetEnabled(bool enabled) {
            _enabled = enabled;
        }

        public static void Verbose(string module, string message) {
            Log(LogLevel.Verbose, module, message);
        }

        public static void Debug(string module, string message) {
            Log(LogLevel.Debug, module, message);
        }

        public static void Info(string module, string message) {
            Log(LogLevel.Info, module, message);
        }

        public static void Warning(string module, string message) {
            Log(LogLevel.Warning, module, message);
        }

        public static void Error(string module, string message) {
            Log(LogLevel.Error, module, message);
        }

        public static void Exception(string module, Exception exception) {
            if (!_enabled) return;
            AddToBuffer(new LogEntry(LogLevel.Error, module, exception.Message));
            UnityEngine.Debug.LogException(exception);
        }

        /// <summary>
        /// Clear the on-screen log buffer.
        /// </summary>
        public static void ClearBuffer() {
            LogBuffer.Clear();
        }

        private static void Log(LogLevel level, string module, string message) {
            if (!_enabled || level < _minLevel) return;

            AddToBuffer(new LogEntry(level, module, message));

            string color = GetColor(level);
            string formatted = $"<color={color}>[SDK:{module}]</color> {message}";

            switch (level) {
                case LogLevel.Verbose:
                case LogLevel.Debug:
                case LogLevel.Info:
                    UnityEngine.Debug.Log(formatted);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(formatted);
                    break;
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(formatted);
                    break;
            }
        }

        private static void AddToBuffer(LogEntry entry) {
            if (LogBuffer.Count >= MaxLogEntries) {
                LogBuffer.RemoveAt(0);
            }
            LogBuffer.Add(entry);
            OnLogReceived?.Invoke(entry);
        }

        private static string GetColor(LogLevel level) {
            switch (level) {
                case LogLevel.Verbose: return "#888888";
                case LogLevel.Debug:   return "#44AAFF";
                case LogLevel.Info:    return "#44FF44";
                case LogLevel.Warning: return "#FFAA00";
                case LogLevel.Error:   return "#FF4444";
                default:               return "#FFFFFF";
            }
        }

        /// <summary>
        /// A single log entry for the on-screen buffer.
        /// </summary>
        public readonly struct LogEntry {
            public readonly LogLevel Level;
            public readonly string Module;
            public readonly string Message;
            public readonly float Timestamp;

            public LogEntry(LogLevel level, string module, string message) {
                Level = level;
                Module = module;
                Message = message;
                Timestamp = Time.realtimeSinceStartup;
            }

            public override string ToString() {
                return $"[{Timestamp:F1}][{Level}][{Module}] {Message}";
            }
        }
    }
}
