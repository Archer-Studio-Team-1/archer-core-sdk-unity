using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace ArcherStudio.SDK.Core.Editor {

    /// <summary>
    /// Detects installed third-party SDKs by checking for known types
    /// and can add/remove the corresponding scripting define symbols.
    ///
    /// Supports two symbol scopes:
    ///   - Unity 6000+: Build Profile symbols (per-profile) via BuildProfile.scriptingDefines
    ///   - All versions: PlayerSettings symbols (global, per BuildTargetGroup)
    ///
    /// NOTE: Most symbols are now auto-defined per-assembly via versionDefines in each
    /// package's .asmdef file. This detector serves as:
    ///   1. A UI dashboard to view SDK detection status (Symbols tab in Setup Wizard)
    ///   2. A manual tool for adding symbols when needed
    ///      (e.g., for SDKs not distributed as UPM packages like Facebook SDK)
    ///
    /// Symbol mapping:
    ///   HAS_FIREBASE_SDK           -> Firebase.FirebaseApp
    ///   HAS_FIREBASE_CRASHLYTICS   -> Firebase.Crashlytics.Crashlytics
    ///   HAS_FIREBASE_REMOTE_CONFIG -> Firebase.RemoteConfig.FirebaseRemoteConfig
    ///   HAS_FIREBASE_MESSAGING     -> Firebase.Messaging.FirebaseMessaging
    ///   HAS_FIREBASE_DYNAMIC_LINKS -> Firebase.DynamicLinks.DynamicLinks
    ///   HAS_FIREBASE_FIRESTORE     -> Firebase.Firestore.FirebaseFirestore
    ///   HAS_ADJUST_SDK             -> AdjustSdk.Adjust
    ///   HAS_APPLOVIN_MAX_SDK       -> MaxSdkBase
    ///   HAS_GOOGLE_UMP             -> GoogleMobileAds.Ump.Api.ConsentInformation
    ///   HAS_UNITY_IAP              -> UnityEngine.Purchasing.UnityPurchasing
    ///   HAS_IRONSOURCE_SDK         -> IronSource
    ///   HAS_FACEBOOK_SDK           -> Facebook.Unity.FB
    ///   HAS_GPGS                   -> GooglePlayGames.PlayGamesPlatform
    /// </summary>
    [InitializeOnLoad]
    public static class SDKSymbolDetector {

        private const string Tag = "SDKSymbolDetector";

        /// <summary>
        /// Auto-detect SDK symbols on domain reload (compile, play mode, editor start).
        /// </summary>
        static SDKSymbolDetector() {
            // Delay to avoid issues during domain reload
            EditorApplication.delayCall += () => {
                var changes = DetectChanges();
                if (changes.Count > 0) {
                    RunDetection();
                }
            };
        }

        /// <summary>
        /// Where to apply symbol changes.
        /// </summary>
        public enum SymbolScope {
            /// <summary>Active Build Profile only (Unity 6000+). Falls back to ActivePlatform on older Unity.</summary>
            ActiveProfile,
            /// <summary>Active platform via PlayerSettings (Android/iOS + current).</summary>
            ActivePlatform,
            /// <summary>All mobile platforms (Android + iOS) via PlayerSettings.</summary>
            AllMobilePlatforms,
        }

        /// <summary>
        /// Each entry maps a scripting define symbol to the fully-qualified type name
        /// that indicates the SDK is installed.
        /// </summary>
        public static readonly SDKSymbolEntry[] Entries = {
            // Firebase
            new SDKSymbolEntry("HAS_FIREBASE_SDK",           "Firebase.FirebaseApp",                       "Firebase Analytics / Core"),
            new SDKSymbolEntry("HAS_FIREBASE_CRASHLYTICS",   "Firebase.Crashlytics.Crashlytics",           "Firebase Crashlytics"),
            new SDKSymbolEntry("HAS_FIREBASE_REMOTE_CONFIG", "Firebase.RemoteConfig.FirebaseRemoteConfig", "Firebase Remote Config"),
            new SDKSymbolEntry("HAS_FIREBASE_MESSAGING",     "Firebase.Messaging.FirebaseMessaging",       "Firebase Cloud Messaging"),
            new SDKSymbolEntry("HAS_FIREBASE_DYNAMIC_LINKS", "Firebase.DynamicLinks.DynamicLinks",         "Firebase Dynamic Links"),
            new SDKSymbolEntry("HAS_FIREBASE_FIRESTORE",     "Firebase.Firestore.FirebaseFirestore",        "Firebase Firestore"),

            // Attribution & Mediation
            new SDKSymbolEntry("HAS_ADJUST_SDK",             "AdjustSdk.Adjust",                           "Adjust SDK v5"),
            new SDKSymbolEntry("HAS_APPLOVIN_MAX_SDK",       "MaxSdkBase",                                 "AppLovin MAX"),
            new SDKSymbolEntry("HAS_IRONSOURCE_SDK",         "IronSource",                                 "IronSource / LevelPlay"),

            // Ads & Consent
            new SDKSymbolEntry("HAS_GOOGLE_UMP",             "GoogleMobileAds.Ump.Api.ConsentInformation", "Google UMP"),

            // IAP
            new SDKSymbolEntry("HAS_UNITY_IAP",              "UnityEngine.Purchasing.UnityPurchasing",     "Unity IAP"),

            // Social
            new SDKSymbolEntry("HAS_FACEBOOK_SDK",           "Facebook.Unity.FB",                          "Facebook / Meta SDK"),

            // Login
            new SDKSymbolEntry("HAS_GPGS",                   "GooglePlayGames.PlayGamesPlatform",          "Google Play Games Services"),

            // Testing
            new SDKSymbolEntry("HAS_TESTLAB",                "ArcherStudio.SDK.TestLab.GameLoopHandler",   "Firebase Test Lab"),
        };

        // ═══════════════════════════════════════════════════════
        //  PUBLIC API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Run full detection and sync symbols using the given scope.
        /// </summary>
        public static void RunDetection(SymbolScope scope = SymbolScope.ActiveProfile) {
            var currentSymbols = GetCurrentSymbols(scope);
            var changes = DetectChangesInternal(currentSymbols);
            if (changes.Count == 0) {
                Debug.Log($"[{Tag}] All symbols are in sync. No changes needed.");
                return;
            }

            ApplyChanges(changes, scope);
        }

        /// <summary>
        /// Detect which symbols need to be added or removed based on current scope.
        /// </summary>
        public static List<SymbolChange> DetectChanges(SymbolScope scope = SymbolScope.ActiveProfile) {
            var currentSymbols = GetCurrentSymbols(scope);
            return DetectChangesInternal(currentSymbols);
        }

        /// <summary>
        /// Check if a given symbol's SDK is currently detected in loaded assemblies.
        /// </summary>
        public static bool IsSDKDetected(string symbol) {
            foreach (var entry in Entries) {
                if (entry.Symbol == symbol) {
                    return IsTypeAvailable(entry.DetectionType);
                }
            }
            return false;
        }

        /// <summary>
        /// Check if a symbol is currently defined for the given scope.
        /// </summary>
        public static bool IsSymbolDefined(string symbol, SymbolScope scope = SymbolScope.ActiveProfile) {
            return GetCurrentSymbols(scope).Contains(symbol);
        }

        /// <summary>
        /// Manually add a symbol.
        /// </summary>
        public static void AddSymbol(string symbol, SymbolScope scope = SymbolScope.ActiveProfile) {
            switch (scope) {
                case SymbolScope.ActiveProfile:
                    AddSymbolToActiveProfile(symbol);
                    break;
                case SymbolScope.ActivePlatform:
                    AddSymbolToGroup(GetPrimaryBuildTargetGroup(), symbol);
                    break;
                case SymbolScope.AllMobilePlatforms:
                    foreach (var group in GetMobileBuildTargetGroups()) {
                        AddSymbolToGroup(group, symbol);
                    }
                    break;
            }
        }

        /// <summary>
        /// Apply multiple symbol changes (adds and removes) in a single batch.
        /// This triggers only ONE recompile instead of one per symbol.
        /// </summary>
        public static void ApplyBulkChanges(IReadOnlyList<SymbolChange> changes, SymbolScope scope = SymbolScope.ActiveProfile) {
            if (changes == null || changes.Count == 0) return;

            switch (scope) {
                case SymbolScope.ActiveProfile:
                    ApplyBulkToActiveProfile(changes);
                    break;
                case SymbolScope.ActivePlatform:
                    ApplyBulkToGroup(GetPrimaryBuildTargetGroup(), changes);
                    break;
                case SymbolScope.AllMobilePlatforms:
                    foreach (var group in GetMobileBuildTargetGroups()) {
                        ApplyBulkToGroup(group, changes);
                    }
                    break;
            }
        }

        /// <summary>
        /// Manually remove a symbol.
        /// </summary>
        public static void RemoveSymbol(string symbol, SymbolScope scope = SymbolScope.ActiveProfile) {
            switch (scope) {
                case SymbolScope.ActiveProfile:
                    RemoveSymbolFromActiveProfile(symbol);
                    break;
                case SymbolScope.ActivePlatform:
                    RemoveSymbolFromGroup(GetPrimaryBuildTargetGroup(), symbol);
                    break;
                case SymbolScope.AllMobilePlatforms:
                    foreach (var group in GetMobileBuildTargetGroups()) {
                        RemoveSymbolFromGroup(group, symbol);
                    }
                    break;
            }
        }

        /// <summary>
        /// Get the raw scripting define symbols string for display.
        /// </summary>
        public static string GetCurrentSymbolsRaw(SymbolScope scope = SymbolScope.ActiveProfile) {
            var symbols = GetCurrentSymbols(scope);
            return symbols.Count > 0 ? string.Join(";", symbols) : "";
        }

        /// <summary>
        /// Returns a human-readable label for the current scope target.
        /// </summary>
        public static string GetScopeLabel(SymbolScope scope) {
            switch (scope) {
                case SymbolScope.ActiveProfile:
#if UNITY_6000_0_OR_NEWER
                    return "Active Build Profile";
#else
                    return $"Active Platform ({GetPrimaryBuildTargetGroup()})";
#endif
                case SymbolScope.ActivePlatform:
                    return $"Active Platform ({GetPrimaryBuildTargetGroup()})";
                case SymbolScope.AllMobilePlatforms:
                    return "All Mobile Platforms (Android + iOS)";
                default:
                    return scope.ToString();
            }
        }

        /// <summary>
        /// Whether the current Unity version supports Build Profiles.
        /// </summary>
        public static bool SupportsBuildProfiles {
            get {
#if UNITY_6000_0_OR_NEWER
                return true;
#else
                return false;
#endif
            }
        }

        // ═══════════════════════════════════════════════════════
        //  INTERNAL — Detection
        // ═══════════════════════════════════════════════════════

        private static List<SymbolChange> DetectChangesInternal(HashSet<string> currentSymbols) {
            var changes = new List<SymbolChange>();

            foreach (var entry in Entries) {
                bool sdkPresent = IsTypeAvailable(entry.DetectionType);
                bool symbolDefined = currentSymbols.Contains(entry.Symbol);

                if (sdkPresent && !symbolDefined) {
                    changes.Add(new SymbolChange(entry.Symbol, true, entry.DisplayName));
                } else if (!sdkPresent && symbolDefined) {
                    changes.Add(new SymbolChange(entry.Symbol, false, entry.DisplayName));
                }
            }

            return changes;
        }

        private static bool IsTypeAvailable(string fullyQualifiedTypeName) {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (assembly.GetType(fullyQualifiedTypeName, false) != null) {
                    return true;
                }
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════
        //  INTERNAL — Symbol Read/Write
        // ═══════════════════════════════════════════════════════

        private static HashSet<string> GetCurrentSymbols(SymbolScope scope) {
            switch (scope) {
                case SymbolScope.ActiveProfile:
                    return GetActiveProfileSymbols();
                case SymbolScope.ActivePlatform:
                    return new HashSet<string>(GetSymbolsForGroup(GetPrimaryBuildTargetGroup()));
                case SymbolScope.AllMobilePlatforms: {
                    var combined = new HashSet<string>();
                    foreach (var group in GetMobileBuildTargetGroups()) {
                        foreach (var s in GetSymbolsForGroup(group)) {
                            combined.Add(s);
                        }
                    }
                    return combined;
                }
                default:
                    return new HashSet<string>();
            }
        }

        private static void ApplyChanges(List<SymbolChange> changes, SymbolScope scope) {
            ApplyBulkChanges(changes, scope);
        }

        // ── Build Profile (Unity 6000+) ──

        private static HashSet<string> GetActiveProfileSymbols() {
#if UNITY_6000_0_OR_NEWER
            var profileType = Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor");
            if (profileType != null) {
                var getActiveMethod = profileType.GetMethod("GetActiveBuildProfile",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (getActiveMethod != null) {
                    var activeProfile = getActiveMethod.Invoke(null, null);
                    if (activeProfile != null) {
                        var definesProperty = profileType.GetProperty("scriptingDefines");
                        if (definesProperty != null) {
                            var defines = definesProperty.GetValue(activeProfile) as string[];
                            if (defines != null) {
                                return new HashSet<string>(defines.Where(s => !string.IsNullOrEmpty(s)));
                            }
                        }
                    }
                }
            }
            // Fallback: no active profile or API not available
#endif
            return new HashSet<string>(GetSymbolsForGroup(GetPrimaryBuildTargetGroup()));
        }

        private static void AddSymbolToActiveProfile(string symbol) {
#if UNITY_6000_0_OR_NEWER
            if (TryModifyActiveProfileSymbols(symbol, true)) return;
#endif
            // Fallback to PlayerSettings
            AddSymbolToGroup(GetPrimaryBuildTargetGroup(), symbol);
        }

        private static void RemoveSymbolFromActiveProfile(string symbol) {
#if UNITY_6000_0_OR_NEWER
            if (TryModifyActiveProfileSymbols(symbol, false)) return;
#endif
            RemoveSymbolFromGroup(GetPrimaryBuildTargetGroup(), symbol);
        }

#if UNITY_6000_0_OR_NEWER
        private static bool TryModifyActiveProfileSymbols(string symbol, bool add) {
            var profileType = Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor");
            if (profileType == null) return false;

            var getActiveMethod = profileType.GetMethod("GetActiveBuildProfile",
                BindingFlags.Public | BindingFlags.Static);
            if (getActiveMethod == null) return false;

            var activeProfile = getActiveMethod.Invoke(null, null);
            if (activeProfile == null) return false;

            var definesProperty = profileType.GetProperty("scriptingDefines");
            if (definesProperty == null) return false;

            var defines = definesProperty.GetValue(activeProfile) as string[];
            var list = defines != null ? new List<string>(defines) : new List<string>();

            if (add) {
                if (list.Contains(symbol)) return true;
                list.Add(symbol);
            } else {
                if (!list.Remove(symbol)) {
                    Debug.Log($"[{Tag}] '{symbol}' not present on Active Build Profile — nothing to remove");
                    return true;
                }
            }

            // Profile's scriptingDefines only take effect when this flag is on
            EnsureProfileOverrideEnabled(profileType, activeProfile);

            definesProperty.SetValue(activeProfile, list.ToArray());

            FinalizeProfileWrite(activeProfile as ScriptableObject);
            Debug.Log($"[{Tag}] {(add ? "Added" : "Removed")} '{symbol}' on Active Build Profile");
            return true;
        }
#endif

        // ── PlayerSettings (per BuildTargetGroup) ──

        private static void AddSymbolToGroup(BuildTargetGroup group, string symbol) {
            var symbols = GetSymbolsForGroup(group);
            if (symbols.Contains(symbol)) return;
            symbols.Add(symbol);
            SetSymbolsForGroup(group, symbols);
            Debug.Log($"[{Tag}] Added '{symbol}' to PlayerSettings ({group})");
        }

        private static void RemoveSymbolFromGroup(BuildTargetGroup group, string symbol) {
            var symbols = GetSymbolsForGroup(group);
            if (!symbols.Remove(symbol)) return;
            SetSymbolsForGroup(group, symbols);
            Debug.Log($"[{Tag}] Removed '{symbol}' from PlayerSettings ({group})");
        }

        private static List<string> GetSymbolsForGroup(BuildTargetGroup group) {
#if UNITY_6000_0_OR_NEWER
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
            var raw = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
#else
            var raw = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#endif
            return raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s))
                       .ToList();
        }

        private static void SetSymbolsForGroup(BuildTargetGroup group, List<string> symbols) {
            var joined = string.Join(";", symbols);
#if UNITY_6000_0_OR_NEWER
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, joined);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, joined);
#endif
        }

        // ── Bulk Operations ──

        private static void ApplyBulkToActiveProfile(IReadOnlyList<SymbolChange> changes) {
#if UNITY_6000_0_OR_NEWER
            if (TryApplyBulkToActiveProfile(changes)) return;
#endif
            // Fallback to PlayerSettings
            ApplyBulkToGroup(GetPrimaryBuildTargetGroup(), changes);
        }

#if UNITY_6000_0_OR_NEWER
        private static bool TryApplyBulkToActiveProfile(IReadOnlyList<SymbolChange> changes) {
            var profileType = Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor");
            if (profileType == null) return false;

            var getActiveMethod = profileType.GetMethod("GetActiveBuildProfile",
                BindingFlags.Public | BindingFlags.Static);
            if (getActiveMethod == null) return false;

            var activeProfile = getActiveMethod.Invoke(null, null);
            if (activeProfile == null) return false;

            var definesProperty = profileType.GetProperty("scriptingDefines");
            if (definesProperty == null) return false;

            var defines = definesProperty.GetValue(activeProfile) as string[];
            var set = defines != null
                ? new HashSet<string>(defines.Where(s => !string.IsNullOrEmpty(s)))
                : new HashSet<string>();

            foreach (var change in changes) {
                if (change.ShouldAdd) set.Add(change.Symbol);
                else set.Remove(change.Symbol);
                Debug.Log($"[{Tag}] Bulk {(change.ShouldAdd ? "add" : "remove")} '{change.Symbol}' on Active Build Profile");
            }

            // Profile's scriptingDefines only take effect when this flag is on
            EnsureProfileOverrideEnabled(profileType, activeProfile);

            definesProperty.SetValue(activeProfile, set.ToArray());

            FinalizeProfileWrite(activeProfile as ScriptableObject);
            return true;
        }

        /// <summary>
        /// Set BuildProfile.overrideGlobalScriptingDefines = true so Unity actually uses
        /// the profile's scriptingDefines instead of falling back to PlayerSettings globals.
        /// No-op if the field/property doesn't exist in the current Unity version.
        /// </summary>
        private static void EnsureProfileOverrideEnabled(Type profileType, object profile) {
            if (profileType == null || profile == null) return;

            const string memberName = "overrideGlobalScriptingDefines";
            var prop = profileType.GetProperty(memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite) {
                var current = prop.GetValue(profile);
                if (current is bool b && !b) {
                    prop.SetValue(profile, true);
                    Debug.Log($"[{Tag}] Enabled {memberName} on Active Build Profile");
                }
                return;
            }

            var field = profileType.GetField(memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) {
                var current = field.GetValue(profile);
                if (current is bool b && !b) {
                    field.SetValue(profile, true);
                    Debug.Log($"[{Tag}] Enabled {memberName} on Active Build Profile");
                }
            }
        }

        /// <summary>
        /// Persist the changes to disk and trigger a script recompile so #if HAS_* updates.
        /// Without these, Unity only marks the SO dirty but never re-evaluates scripting defines.
        /// </summary>
        private static void FinalizeProfileWrite(ScriptableObject so) {
            if (so == null) return;
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssetIfDirty(so);
            CompilationPipeline.RequestScriptCompilation();
        }
#endif

        private static void ApplyBulkToGroup(BuildTargetGroup group, IReadOnlyList<SymbolChange> changes) {
            var symbols = GetSymbolsForGroup(group);
            var set = new HashSet<string>(symbols);

            foreach (var change in changes) {
                if (change.ShouldAdd) set.Add(change.Symbol);
                else set.Remove(change.Symbol);
                Debug.Log($"[{Tag}] Bulk {(change.ShouldAdd ? "add" : "remove")} '{change.Symbol}' on {group}");
            }

            SetSymbolsForGroup(group, set.ToList());
        }

        // ── Helpers ──

        private static BuildTargetGroup GetPrimaryBuildTargetGroup() {
            return BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        }

        private static IEnumerable<BuildTargetGroup> GetMobileBuildTargetGroups() {
            yield return BuildTargetGroup.Android;
            yield return BuildTargetGroup.iOS;
        }

        // ═══════════════════════════════════════════════════════
        //  DATA STRUCTURES
        // ═══════════════════════════════════════════════════════

        public readonly struct SDKSymbolEntry {
            public readonly string Symbol;
            public readonly string DetectionType;
            public readonly string DisplayName;

            public SDKSymbolEntry(string symbol, string detectionType, string displayName) {
                Symbol = symbol;
                DetectionType = detectionType;
                DisplayName = displayName;
            }
        }

        public readonly struct SymbolChange {
            public readonly string Symbol;
            public readonly bool ShouldAdd;
            public readonly string DisplayName;

            public SymbolChange(string symbol, bool shouldAdd, string displayName) {
                Symbol = symbol;
                ShouldAdd = shouldAdd;
                DisplayName = displayName;
            }
        }
    }
}
