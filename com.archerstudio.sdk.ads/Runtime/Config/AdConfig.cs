using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.Ads {

    [CreateAssetMenu(fileName = "AdConfig", menuName = "ArcherStudio/SDK/Ad Config")]
    public class AdConfig : ModuleConfigBase {

        [Header("Mediation")]
        [Tooltip("Which ad mediation platform to use.")]
        public AdMediationPlatform MediationPlatform = AdMediationPlatform.AppLovinMax;

        [Tooltip("SDK key for the mediation platform (e.g., AppLovin SDK key).")]
        public string SdkKey;

        [Header("Ad Units")]
        public List<AdPlacement> Placements = new List<AdPlacement>();

        [Header("Frequency Capping")]
        [Tooltip("Minimum seconds between interstitial ads.")]
        public int InterstitialCooldownSeconds = 30;

        [Tooltip("Maximum interstitials per session.")]
        public int MaxInterstitialsPerSession = 10;

        [Tooltip("Maximum rewarded ads per session.")]
        public int MaxRewardedPerSession = 20;

        [Header("App Open")]
        [Tooltip("Enable app open ads.")]
        public bool EnableAppOpenAd = true;

        [Tooltip("Delay before first app open ad (seconds after cold start).")]
        public float AppOpenColdStartDelay = 3f;

        [Header("Debug")]
        [Tooltip("Show mediation debugger on init (AppLovin MAX).")]
        public bool ShowMediationDebugger = false;
    }
}
