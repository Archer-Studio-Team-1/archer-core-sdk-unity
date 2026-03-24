using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Consent {

    [CreateAssetMenu(fileName = "ConsentConfig", menuName = "ArcherStudio/SDK/Consent Config")]
    public class ConsentConfig : ModuleConfigBase {

        [Header("Provider")]
        [Tooltip("Which consent provider to use.")]
        public ConsentProviderType ProviderType = ConsentProviderType.GoogleUMP;

        [Header("AppLovin MAX Consent")]
        [Tooltip("MAX SDK Key (required when ProviderType = AppLovinMax). " +
                 "MAX handles GDPR/UMP + iOS ATT automatically during SDK init.")]
        public string MaxSdkKey;

        [Tooltip("Show MAX mediation debugger after initialization.")]
        public bool MaxShowMediationDebugger = false;

        [Header("Testing")]
        [Tooltip("Force consent dialog in editor for testing.")]
        public bool ForceShowInEditor = false;

        [Tooltip("Debug geography for testing (EEA, NOT_EEA).")]
        public DebugGeography TestGeography = DebugGeography.Disabled;

        [Header("iOS ATT")]
        [Tooltip("Request App Tracking Transparency on iOS after consent dialog. " +
                 "Ignored when ProviderType = AppLovinMax (MAX handles ATT).")]
        public bool RequestATT = true;

        [Tooltip("Delay before showing ATT dialog (seconds).")]
        public float AttDelay = 0.5f;
    }

    public enum ConsentProviderType {
        GoogleUMP,
        Manual,
        AppLovinMax
    }

    public enum DebugGeography {
        Disabled,
        EEA,
        NotEEA
    }
}
