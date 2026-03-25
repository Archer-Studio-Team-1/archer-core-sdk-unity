// ===================================================================
//  SDKTestPanel.cs -- Runtime Debug & Test UI
//
//  Full-screen overlay with:
//    - On-screen log viewer (scrollable, color-coded)
//    - Toggle log on/off, change log level
//    - Test buttons for ALL SDK modules
//
//  Usage:
//    1. Add this component to any GameObject in your test scene
//    2. UI auto-creates at runtime (no prefab needed)
//    3. Use 3-finger tap (mobile) or F12 (desktop) to toggle panel
//
//  Each section is guarded by #if HAS_SDK_* symbols (auto-defined via
//  versionDefines in the asmdef when the corresponding package is installed).
// ===================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArcherStudio.SDK.Core;

#if HAS_SDK_TRACKING
using ArcherStudio.SDK.Tracking;
using ArcherStudio.SDK.Tracking.Events;
#endif

#if HAS_SDK_ADS
using ArcherStudio.SDK.Ads;
#endif

#if HAS_SDK_CONSENT
using ArcherStudio.SDK.Consent;
#endif

#if HAS_SDK_IAP
using ArcherStudio.SDK.IAP;
#endif

#if HAS_SDK_REMOTECONFIG
using ArcherStudio.SDK.RemoteConfig;
#endif

#if HAS_SDK_CRASHREPORTING
using ArcherStudio.SDK.CrashReporting;
#endif

#if HAS_SDK_PUSH
using ArcherStudio.SDK.Push;
#endif

#if HAS_SDK_DEEPLINK
using ArcherStudio.SDK.DeepLink;
#endif

namespace ArcherStudio.SDK.Examples {

    /// <summary>
    /// Runtime test panel for SDK debugging on device.
    /// Auto-creates Canvas + UI. Toggle with F12 or 3-finger tap.
    /// </summary>
    public class SDKTestPanel : MonoBehaviour {
        private const string Tag = "TestPanel";

        // --- Configurable Test IDs (edit in Inspector) ---

        #if HAS_SDK_ADS
        [Header("Ad Placements")]
        [SerializeField] private string _bannerPlacement = "main_banner";
        [SerializeField] private string _interstitialPlacement = "level_complete";
        [SerializeField] private string _rewardedPlacement = "double_coins";
        [SerializeField] private string _appOpenPlacement = "app_open";
        #endif

        #if HAS_SDK_IAP
        [Header("In-App Purchase")]
        [SerializeField] private string _testProductId = "com.archer.cat.kitchen.remove_ads";
        [SerializeField] private string _purchaseSource = "test_panel";
        [SerializeField] private string _purchasePlacement = "test";
        #endif

        #if HAS_SDK_TRACKING
        [Header("Tracking — Stage / Tutorial")]
        [SerializeField] private string _stageSource = "campaign";
        [SerializeField] private string _tutorialName = "main";

        [Header("Tracking — UI / Feature")]
        [SerializeField] private string _buttonScreen = "test_panel";
        [SerializeField] private string _buttonName = "test_button";
        [SerializeField] private string _featureName = "shop";

        [Header("Tracking — Resources")]
        [SerializeField] private string _resourceName = "gem";
        [SerializeField] private string _earnSource = "test_reward";
        [SerializeField] private string _spendSource = "shop";
        [SerializeField] private string _spendItem = "hero_upgrade";
        [SerializeField] private ulong _earnAmount = 100;
        [SerializeField] private ulong _spendAmount = 50;

        [Header("Tracking — Purchase")]
        [SerializeField] private string _purchaseItemId = "gem_pack_100";
        [SerializeField] private string _purchaseItemSource = "shop";
        [SerializeField] private string _purchaseItemTrigger = "tap_buy";

        [Header("Tracking — Spell / Ad")]
        [SerializeField] private string _spellName = "fireball";
        [SerializeField] private string _adMediationName = "AppLovin MAX";
        [SerializeField] private string _adNetworkName = "TestNetwork";
        [SerializeField] private string _adUnitId = "test_unit";
        [SerializeField] private string _adPlacementName = "test_placement";

        [Header("User Profile")]
        [SerializeField] private string _userPropertyKey = "test_property";
        #endif

        #if HAS_SDK_REMOTECONFIG
        [Header("Remote Config")]
        [SerializeField] private string _remoteStringKey = "test_string_key";
        [SerializeField] private string _remoteBoolKey = "test_bool_key";
        [SerializeField] private string _remoteIntKey = "test_int_key";
        [SerializeField] private string _remoteFloatKey = "test_float_key";
        [SerializeField] private string _remoteStringDefault = "default_value";
        #endif

        #if HAS_SDK_PUSH
        [Header("Push Notifications")]
        [SerializeField] private string _pushTopic = "test_topic";
        #endif

        #if HAS_SDK_CRASHREPORTING
        [Header("Crash Reporting")]
        [SerializeField] private string _crashCustomKey = "test_key";
        #endif

        #if HAS_SDK_TRACKING && HAS_ADJUST_SDK
        [Header("Adjust — Test Config")]
        [SerializeField] private string _adjustTestDeeplink = "your-scheme://test/path";
        [SerializeField] private string _adjustGlobalParamKey = "test_key";
        [SerializeField] private string _adjustGlobalParamValue = "test_value";
        [SerializeField] private double _adjustTestAdRevenue = 0.01;
        [SerializeField] private string _adjustTestAdRevenueSource = "applovin_max_sdk";
        #endif

        // --- UI References (auto-created) ---
        private Canvas _canvas;
        private GameObject _panelRoot;
        private Text _logText;
        private ScrollRect _logScroll;
        private Text _statusText;
        private GameObject _buttonsPanel;
        private GameObject _logPanel;

        // --- State ---
        private bool _isVisible = true;
        private bool _autoScroll = true;
        private readonly List<string> _logLines = new List<string>(256);
        private int _testCounter;

        private void Awake() {
            CreateUI();
            SDKLogger.OnLogReceived += OnLogEntry;
        }

        private void OnDestroy() {
            SDKLogger.OnLogReceived -= OnLogEntry;
        }

        private void Update() {
            if (Input.GetKeyDown(KeyCode.F12)) {
                TogglePanel();
            }

            if (Input.touchCount == 3) {
                foreach (var touch in Input.touches) {
                    if (touch.phase == TouchPhase.Began) {
                        TogglePanel();
                        break;
                    }
                }
            }
        }

        private void TogglePanel() {
            _isVisible = !_isVisible;
            _panelRoot.SetActive(_isVisible);
        }

        // ==========================================================
        //  LOG VIEWER
        // ==========================================================

        private void OnLogEntry(SDKLogger.LogEntry entry) {
            string color = GetLogColor(entry.Level);
            string line = $"<color={color}>[{entry.Timestamp:F1}][{entry.Module}] {entry.Message}</color>";

            _logLines.Add(line);
            if (_logLines.Count > SDKLogger.MaxLogEntries) {
                _logLines.RemoveAt(0);
            }

            RefreshLogText();
        }

        private void RefreshLogText() {
            if (_logText == null) return;
            _logText.text = string.Join("\n", _logLines);

            if (_autoScroll && _logScroll != null) {
                // Defer scroll to end of frame so layout has time to update
                StartCoroutine(ScrollToBottomNextFrame());
            }
        }

        private System.Collections.IEnumerator ScrollToBottomNextFrame() {
            yield return null; // wait one frame for layout rebuild
            if (_logScroll != null) {
                _logScroll.verticalNormalizedPosition = 0f;
            }
        }

        private static string GetLogColor(LogLevel level) {
            switch (level) {
                case LogLevel.Verbose: return "#AAAAAA";
                case LogLevel.Debug:   return "#88CCFF";
                case LogLevel.Info:    return "#88FF88";
                case LogLevel.Warning: return "#FFCC00";
                case LogLevel.Error:   return "#FF6666";
                default:               return "#FFFFFF";
            }
        }

        // ==========================================================
        //  UI CREATION (all programmatic)
        // ==========================================================

        private void CreateUI() {
            // Canvas
            var canvasGo = new GameObject("SDKTestPanelCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists (required for UI interaction)
            if (UnityEngine.EventSystems.EventSystem.current == null) {
                var esGo = new GameObject("EventSystem");
                esGo.transform.SetParent(transform, false);
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Panel root
            _panelRoot = CreatePanel(canvasGo.transform, "PanelRoot",
                new Color(0, 0, 0, 0.92f));
            StretchFull(_panelRoot.GetComponent<RectTransform>());

            // Status bar (top)
            var statusBar = CreatePanel(_panelRoot.transform, "StatusBar",
                new Color(0.15f, 0.15f, 0.15f, 1f));
            var statusRect = statusBar.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(1, 1);
            statusRect.pivot = new Vector2(0.5f, 1);
            statusRect.sizeDelta = new Vector2(0, 80);

            _statusText = CreateText(statusBar.transform, "StatusText",
                "ArcherStudio SDK Test Panel | F12 / 3-finger to toggle", 22);
            StretchFull(_statusText.rectTransform);
            _statusText.rectTransform.offsetMin = new Vector2(20, 0);
            _statusText.alignment = TextAnchor.MiddleLeft;
            _statusText.color = Color.white;

            // Log panel (top 55%)
            _logPanel = CreatePanel(_panelRoot.transform, "LogPanel",
                new Color(0.05f, 0.05f, 0.05f, 1f));
            var logRect = _logPanel.GetComponent<RectTransform>();
            logRect.anchorMin = new Vector2(0, 0.42f);
            logRect.anchorMax = new Vector2(1, 1);
            logRect.offsetMin = new Vector2(0, 0);
            logRect.offsetMax = new Vector2(0, -80);

            CreateLogViewer(_logPanel.transform);

            // Log control bar
            var logControlBar = CreatePanel(_logPanel.transform, "LogControls",
                new Color(0.12f, 0.12f, 0.12f, 1f));
            var lcRect = logControlBar.GetComponent<RectTransform>();
            lcRect.anchorMin = new Vector2(0, 0);
            lcRect.anchorMax = new Vector2(1, 0);
            lcRect.pivot = new Vector2(0.5f, 0);
            lcRect.sizeDelta = new Vector2(0, 60);
            var lcLayout = logControlBar.AddComponent<HorizontalLayoutGroup>();
            lcLayout.spacing = 8;
            lcLayout.padding = new RectOffset(10, 10, 5, 5);
            lcLayout.childForceExpandWidth = true;
            lcLayout.childForceExpandHeight = true;

            CreateButton(logControlBar.transform, "Clear Log", OnClearLog);
            CreateButton(logControlBar.transform, "Auto-Scroll: ON", OnToggleAutoScroll);
            CreateButton(logControlBar.transform, "Log: ON", OnToggleLog);
            CreateButton(logControlBar.transform, "Level: " + SDKLogger.CurrentMinLevel, OnCycleLogLevel);

            // Buttons panel (bottom 42%)
            _buttonsPanel = CreatePanel(_panelRoot.transform, "ButtonsPanel",
                new Color(0.08f, 0.08f, 0.08f, 1f));
            var btnRect = _buttonsPanel.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0, 0);
            btnRect.anchorMax = new Vector2(1, 0.42f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            CreateButtonScrollView();

            // Load existing log buffer
            foreach (var entry in SDKLogger.LogBuffer) {
                OnLogEntry(entry);
            }
        }

        private void CreateLogViewer(Transform parent) {
            var scrollGo = new GameObject("LogScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(10, 60);
            scrollRect.offsetMax = new Vector2(-10, 0);

            // ScrollRect needs an Image for raycasting (drag detection)
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.01f); // nearly transparent

            _logScroll = scrollGo.AddComponent<ScrollRect>();
            _logScroll.horizontal = false;
            _logScroll.vertical = true;
            _logScroll.movementType = ScrollRect.MovementType.Elastic;
            _logScroll.elasticity = 0.1f;
            _logScroll.inertia = true;
            _logScroll.decelerationRate = 0.135f;
            _logScroll.scrollSensitivity = 30f;

            // Viewport — use RectMask2D (no extra Image needed, better perf)
            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            viewportGo.AddComponent<RectMask2D>();
            StretchFull(viewportGo.GetComponent<RectTransform>());
            _logScroll.viewport = viewportGo.GetComponent<RectTransform>();

            // Content — grows vertically as log text expands
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.padding = new RectOffset(8, 8, 4, 4);

            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _logScroll.content = contentRect;

            // Log text
            var textGo = new GameObject("LogText", typeof(RectTransform));
            textGo.transform.SetParent(contentGo.transform, false);
            _logText = textGo.AddComponent<Text>();
            _logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _logText.fontSize = 20;
            _logText.color = Color.white;
            _logText.supportRichText = true;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Overflow;
            _logText.raycastTarget = false; // let drag events pass through to ScrollRect

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.pivot = new Vector2(0, 1);
        }

        private void CreateButtonScrollView() {
            var scrollGo = new GameObject("BtnScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(_buttonsPanel.transform, false);
            StretchFull(scrollGo.GetComponent<RectTransform>());
            scrollGo.GetComponent<RectTransform>().offsetMin = new Vector2(10, 10);
            scrollGo.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -10);

            // Image for drag raycast
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.01f);

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;

            // Viewport — RectMask2D instead of Mask+Image
            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(scrollGo.transform, false);
            vpGo.AddComponent<RectMask2D>();
            StretchFull(vpGo.GetComponent<RectTransform>());
            scroll.viewport = vpGo.GetComponent<RectTransform>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(vpGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);

            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRect;

            var grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(320, 70);
            grid.spacing = new Vector2(10, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;

            var c = contentGo.transform; // shorthand

            // --- Section: SDK Lifecycle ---
            CreateSectionHeader(c, "SDK LIFECYCLE");
            CreateButton(c, "SDK Status", OnCheckSDKStatus, CLR_LIFECYCLE);
            CreateButton(c, "List Modules", OnListModules, CLR_LIFECYCLE);

            #if HAS_SDK_CONSENT
            // --- Section: Consent ---
            CreateSectionHeader(c, "CONSENT");
            CreateButton(c, "Show Consent", OnShowConsent, CLR_CONSENT);
            CreateButton(c, "Grant All", OnGrantConsent, CLR_CONSENT);
            CreateButton(c, "Deny All", OnDenyConsent, CLR_CONSENT);
            CreateButton(c, "Reset Consent", OnResetConsent, CLR_CONSENT);
            CreateButton(c, "Show CMP (MAX)", OnShowCmpForExistingUser, CLR_CONSENT);
            #endif

            #if HAS_SDK_ADS
            // --- Section: Ads ---
            CreateSectionHeader(c, "ADS");
            CreateButton(c, "Show Banner (Bot)", OnShowBannerBottom, CLR_ADS);
            CreateButton(c, "Show Banner (Top)", OnShowBannerTop, CLR_ADS);
            CreateButton(c, "Hide Banner", OnHideBanner, CLR_ADS);
            CreateButton(c, "Destroy Banner", OnDestroyBanner, CLR_ADS);
            CreateButton(c, "Load Interstitial", OnLoadInterstitial, CLR_ADS);
            CreateButton(c, "Is Inter Ready?", OnCheckInterstitial, CLR_ADS);
            CreateButton(c, "Show Interstitial", OnShowInterstitial, CLR_ADS);
            CreateButton(c, "Load Rewarded", OnLoadRewarded, CLR_ADS);
            CreateButton(c, "Is Rewarded Ready?", OnCheckRewarded, CLR_ADS);
            CreateButton(c, "Show Rewarded", OnShowRewarded, CLR_ADS);
            CreateButton(c, "Load App Open", OnLoadAppOpen, CLR_ADS);
            CreateButton(c, "Show App Open", OnShowAppOpen, CLR_ADS);
            CreateButton(c, "Load All Ads", OnLoadAllAds, CLR_ADS);
            #endif

            #if HAS_SDK_IAP
            // --- Section: IAP ---
            CreateSectionHeader(c, "IN-APP PURCHASE");
            CreateButton(c, "List Products", OnListProducts, CLR_IAP);
            CreateButton(c, "Purchase Test", OnPurchaseTest, CLR_IAP);
            CreateButton(c, "Restore Purchases", OnRestorePurchases, CLR_IAP);
            #endif

            #if HAS_SDK_TRACKING
            // --- Section: Tracking ---
            CreateSectionHeader(c, "TRACKING");
            CreateButton(c, "Stage Start", OnTrackStageStart, CLR_TRACKING);
            CreateButton(c, "Stage End", OnTrackStageEnd, CLR_TRACKING);
            CreateButton(c, "Tutorial Step", OnTrackTutorial, CLR_TRACKING);
            CreateButton(c, "Tutorial Complete", OnTrackTutorialComplete, CLR_TRACKING);
            CreateButton(c, "Button Click", OnTrackButtonClick, CLR_TRACKING);
            CreateButton(c, "Feature Unlock", OnTrackFeatureUnlock, CLR_TRACKING);
            CreateButton(c, "Feature Open", OnTrackFeatureOpen, CLR_TRACKING);
            CreateButton(c, "Feature Close", OnTrackFeatureClose, CLR_TRACKING);
            CreateButton(c, "Earn Resource", OnTrackEarnResource, CLR_TRACKING);
            CreateButton(c, "Spend Resource", OnTrackSpendResource, CLR_TRACKING);
            CreateButton(c, "IAP Revenue (Success)", OnTrackIapRevenueSuccess, CLR_TRACKING);
            CreateButton(c, "IAP Revenue (Fail)", OnTrackIapRevenueFail, CLR_TRACKING);
            CreateButton(c, "Loading Start", OnTrackLoadingStart, CLR_TRACKING);
            CreateButton(c, "Loading Result", OnTrackLoadingResult, CLR_TRACKING);
            CreateButton(c, "Exploration Start", OnTrackExplorationStart, CLR_TRACKING);
            CreateButton(c, "Exploration End", OnTrackExplorationEnd, CLR_TRACKING);
            CreateButton(c, "Exploration RankUp", OnTrackExplorationRankUp, CLR_TRACKING);
            CreateButton(c, "Forge Upgrade", OnTrackForgeUpgrade, CLR_TRACKING);
            CreateButton(c, "Character LvlUp", OnTrackCharacterLevelUp, CLR_TRACKING);
            CreateButton(c, "Spell Upgrade", OnTrackSpellUpgrade, CLR_TRACKING);
            CreateButton(c, "Ad Revenue (Custom)", OnTrackAdRevenueCustom, CLR_TRACKING);
            CreateButton(c, "Ad Impression", OnTrackAdImpression, CLR_TRACKING);
            CreateButton(c, "Custom Event", OnTrackCustomEvent, CLR_TRACKING);

            // --- Section: User Profile ---
            CreateSectionHeader(c, "USER PROFILE");
            CreateButton(c, "Update Profile", OnUpdateProfile, CLR_PROFILE);
            CreateButton(c, "Set User Prop", OnSetUserProperty, CLR_PROFILE);
            CreateButton(c, "Show Profile", OnShowProfile, CLR_PROFILE);
            #endif

            #if HAS_SDK_REMOTECONFIG
            // --- Section: Remote Config ---
            CreateSectionHeader(c, "REMOTE CONFIG");
            CreateButton(c, "Fetch & Activate", OnFetchRemoteConfig, CLR_REMOTECONFIG);
            CreateButton(c, "Get String", OnGetRemoteString, CLR_REMOTECONFIG);
            CreateButton(c, "Get Bool", OnGetRemoteBool, CLR_REMOTECONFIG);
            CreateButton(c, "Get Int", OnGetRemoteInt, CLR_REMOTECONFIG);
            CreateButton(c, "Get Float", OnGetRemoteFloat, CLR_REMOTECONFIG);
            #endif

            #if HAS_SDK_CRASHREPORTING
            // --- Section: Crash Reporting ---
            CreateSectionHeader(c, "CRASH REPORTING");
            CreateButton(c, "Log Message", OnCrashLog, CLR_CRASH);
            CreateButton(c, "Log Exception", OnCrashLogException, CLR_CRASH);
            CreateButton(c, "Set Custom Key", OnCrashSetKey, CLR_CRASH);
            CreateButton(c, "Set UserId", OnCrashSetUserId, CLR_CRASH);
            CreateButton(c, "Test Crash!", OnTestCrash, CLR_CRASH_DANGER);
            #endif

            #if HAS_SDK_PUSH
            // --- Section: Push Notifications ---
            CreateSectionHeader(c, "PUSH NOTIFICATIONS");
            CreateButton(c, "Request Permission", OnPushRequestPermission, CLR_PUSH);
            CreateButton(c, "Get Token", OnPushGetToken, CLR_PUSH);
            CreateButton(c, $"Sub: {_pushTopic}", OnPushSubscribe, CLR_PUSH);
            CreateButton(c, $"Unsub: {_pushTopic}", OnPushUnsubscribe, CLR_PUSH);
            #endif

            #if HAS_SDK_DEEPLINK
            // --- Section: Deep Link ---
            CreateSectionHeader(c, "DEEP LINK");
            CreateButton(c, "Show Last Link", OnShowLastDeepLink, CLR_DEEPLINK);
            #endif

            #if HAS_SDK_TRACKING && HAS_ADJUST_SDK
            // --- Section: Adjust — Device Info ---
            CreateSectionHeader(c, "ADJUST — DEVICE INFO");
            #if UNITY_ANDROID
            CreateButton(c, "OAID Diagnostics", OnAdjustOaidDiagnostics, CLR_ADJUST);
            CreateButton(c, "Get OAID", OnAdjustGetOaid, CLR_ADJUST);
            CreateButton(c, "OAID Status", OnAdjustOaidStatus, CLR_ADJUST);
            #endif
            CreateButton(c, "Get ADID", OnAdjustGetAdid, CLR_ADJUST);
            CreateButton(c, "Get Attribution", OnAdjustGetAttribution, CLR_ADJUST);
            CreateButton(c, "Get SDK Version", OnAdjustGetSdkVersion, CLR_ADJUST);
            CreateButton(c, "Get Google AdId", OnAdjustGetGoogleAdId, CLR_ADJUST);
            CreateButton(c, "Get IDFA (iOS)", OnAdjustGetIdfa, CLR_ADJUST);
            CreateButton(c, "Get IDFV (iOS)", OnAdjustGetIdfv, CLR_ADJUST);
            CreateButton(c, "Get Amazon AdId", OnAdjustGetAmazonAdId, CLR_ADJUST);
            CreateButton(c, "Get Last Deeplink", OnAdjustGetLastDeeplink, CLR_ADJUST);

            // --- Section: Adjust — Privacy & Control ---
            CreateSectionHeader(c, "ADJUST — PRIVACY & CONTROL");
            CreateButton(c, "Is Enabled?", OnAdjustIsEnabled, CLR_ADJUST);
            CreateButton(c, "Disable SDK", OnAdjustDisable, CLR_ADJUST);
            CreateButton(c, "Enable SDK", OnAdjustEnable, CLR_ADJUST);
            CreateButton(c, "Offline Mode", OnAdjustOffline, CLR_ADJUST);
            CreateButton(c, "Online Mode", OnAdjustOnline, CLR_ADJUST);
            CreateButton(c, "Consent: ON", OnAdjustMeasurementConsentOn, CLR_ADJUST);
            CreateButton(c, "Consent: OFF", OnAdjustMeasurementConsentOff, CLR_ADJUST);
            CreateButton(c, "GDPR Forget Me", OnAdjustGdprForgetMe, CLR_ADJUST_DANGER);

            // --- Section: Adjust — Global Params ---
            CreateSectionHeader(c, "ADJUST — GLOBAL PARAMS");
            CreateButton(c, "Add Callback Param", OnAdjustAddCallbackParam, CLR_ADJUST);
            CreateButton(c, "Rm Callback Param", OnAdjustRemoveCallbackParam, CLR_ADJUST);
            CreateButton(c, "Clear Callback Params", OnAdjustClearCallbackParams, CLR_ADJUST);
            CreateButton(c, "Add Partner Param", OnAdjustAddPartnerParam, CLR_ADJUST);
            CreateButton(c, "Rm Partner Param", OnAdjustRemovePartnerParam, CLR_ADJUST);
            CreateButton(c, "Clear Partner Params", OnAdjustClearPartnerParams, CLR_ADJUST);

            // --- Section: Adjust — Ad Revenue & Deeplink ---
            CreateSectionHeader(c, "ADJUST — AD REVENUE & DEEPLINK");
            CreateButton(c, "Track Ad Revenue", OnAdjustTrackAdRevenue, CLR_ADJUST);
            CreateButton(c, "Process Deeplink", OnAdjustProcessDeeplink, CLR_ADJUST);
            CreateButton(c, "Resolve Deeplink", OnAdjustResolveDeeplink, CLR_ADJUST);
            CreateButton(c, "Set Push Token", OnAdjustSetPushToken, CLR_ADJUST);
            #endif

            // --- Section: Logger Controls ---
            CreateSectionHeader(c, "LOGGER");
            CreateButton(c, "Log Verbose", () =>
                SDKLogger.Verbose(Tag, "Test verbose message"), new Color(0.4f, 0.4f, 0.4f));
            CreateButton(c, "Log Debug", () =>
                SDKLogger.Debug(Tag, "Test debug message"), new Color(0.3f, 0.5f, 0.7f));
            CreateButton(c, "Log Info", () =>
                SDKLogger.Info(Tag, "Test info message"), new Color(0.3f, 0.7f, 0.3f));
            CreateButton(c, "Log Warning", () =>
                SDKLogger.Warning(Tag, "Test warning message"), new Color(0.7f, 0.6f, 0.1f));
            CreateButton(c, "Log Error", () =>
                SDKLogger.Error(Tag, "Test error message"), new Color(0.7f, 0.2f, 0.2f));
        }

        // ==========================================================
        //  COLOR CONSTANTS
        // ==========================================================

        private static readonly Color CLR_LIFECYCLE    = new Color(0.2f, 0.5f, 0.8f);
        private static readonly Color CLR_CONSENT      = new Color(0.6f, 0.3f, 0.6f);
        private static readonly Color CLR_ADS          = new Color(0.7f, 0.5f, 0.1f);
        private static readonly Color CLR_IAP          = new Color(0.8f, 0.4f, 0.2f);
        private static readonly Color CLR_TRACKING     = new Color(0.2f, 0.6f, 0.3f);
        private static readonly Color CLR_PROFILE      = new Color(0.5f, 0.4f, 0.7f);
        private static readonly Color CLR_REMOTECONFIG = new Color(0.3f, 0.6f, 0.6f);
        private static readonly Color CLR_CRASH        = new Color(0.6f, 0.3f, 0.3f);
        private static readonly Color CLR_CRASH_DANGER = new Color(0.8f, 0.1f, 0.1f);
        private static readonly Color CLR_PUSH         = new Color(0.4f, 0.5f, 0.6f);
        private static readonly Color CLR_DEEPLINK     = new Color(0.5f, 0.6f, 0.4f);
        private static readonly Color CLR_ADJUST       = new Color(0.2f, 0.45f, 0.65f);
        private static readonly Color CLR_ADJUST_DANGER = new Color(0.7f, 0.15f, 0.15f);

        // ==========================================================
        //  HANDLERS -- SDK Lifecycle
        // ==========================================================

        private void OnCheckSDKStatus() {
            var init = SDKInitializer.Instance;
            if (init == null) {
                SDKLogger.Warning(Tag, "SDKInitializer.Instance is null.");
                return;
            }
            SDKLogger.Info(Tag,
                $"SDK Status: Initialized={init.IsInitialized}, " +
                $"AppId={init.Config?.AppId ?? "N/A"}, " +
                $"DebugMode={init.Config?.DebugMode}");
        }

        private void OnListModules() {
            var init = SDKInitializer.Instance;
            if (init == null) {
                SDKLogger.Warning(Tag, "SDKInitializer not available.");
                return;
            }
            string[] moduleIds = { "consent", "tracking", "ads", "iap",
                "remoteconfig", "crashreporting", "push", "deeplink" };
            foreach (var id in moduleIds) {
                var module = init.GetModule(id);
                string state = module != null ? module.State.ToString() : "NOT FOUND";
                SDKLogger.Info(Tag, $"  [{state}] {id}");
            }
        }

        // ==========================================================
        //  HANDLERS -- Consent
        // ==========================================================

        #if HAS_SDK_CONSENT
        private ConsentManager GetConsentManager() {
            var init = SDKInitializer.Instance;
            return init?.GetModule("consent") as ConsentManager;
        }

        private void OnShowConsent() {
            var cm = GetConsentManager();
            if (cm == null) {
                SDKLogger.Warning(Tag, "ConsentManager not available.");
                return;
            }
            var s = cm.CurrentStatus;
            SDKLogger.Info(Tag,
                $"Consent: PersonalizedAds={s.CanShowPersonalizedAds}, " +
                $"Analytics={s.CanCollectAnalytics}, " +
                $"Attribution={s.CanTrackAttribution}, " +
                $"EEA={s.IsEeaUser}, ATT={s.HasAttConsent}");
        }

        private void OnGrantConsent() {
            var cm = GetConsentManager();
            if (cm == null) { SDKLogger.Warning(Tag, "ConsentManager not available."); return; }
            SDKLogger.Info(Tag, "Consent: SetConsent(granted=true, eea=false)");
            cm.SetConsent(true, false);
        }

        private void OnDenyConsent() {
            var cm = GetConsentManager();
            if (cm == null) { SDKLogger.Warning(Tag, "ConsentManager not available."); return; }
            SDKLogger.Info(Tag, "Consent: SetConsent(granted=false, eea=true)");
            cm.SetConsent(false, true);
        }

        private void OnResetConsent() {
            var cm = GetConsentManager();
            if (cm == null) { SDKLogger.Warning(Tag, "ConsentManager not available."); return; }
            SDKLogger.Info(Tag, "Consent: ResetConsent()");
            cm.ResetConsent();
        }

        private void OnShowCmpForExistingUser() {
            var cm = GetConsentManager();
            if (cm == null) { SDKLogger.Warning(Tag, "ConsentManager not available."); return; }
            SDKLogger.Info(Tag, "Consent: ShowCmpForExistingUser()");
            cm.ShowCmpForExistingUser(error => {
                if (error == null)
                    SDKLogger.Info(Tag, "CMP flow completed successfully.");
                else
                    SDKLogger.Warning(Tag, $"CMP flow result: {error}");
            });
        }
        #endif

        // ==========================================================
        //  HANDLERS -- Ads
        // ==========================================================

        #if HAS_SDK_ADS
        private void OnShowBannerBottom() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            SDKLogger.Info(Tag, $"Ads: ShowBanner ({_bannerPlacement}, Bottom)");
            AdManager.Instance.ShowBanner(_bannerPlacement, BannerPosition.Bottom);
        }

        private void OnShowBannerTop() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            SDKLogger.Info(Tag, $"Ads: ShowBanner ({_bannerPlacement}, Top)");
            AdManager.Instance.ShowBanner(_bannerPlacement, BannerPosition.Top);
        }

        private void OnHideBanner() {
            if (AdManager.Instance == null) return;
            SDKLogger.Info(Tag, $"Ads: HideBanner ({_bannerPlacement})");
            AdManager.Instance.HideBanner(_bannerPlacement);
        }

        private void OnDestroyBanner() {
            if (AdManager.Instance == null) return;
            SDKLogger.Info(Tag, $"Ads: DestroyBanner ({_bannerPlacement})");
            AdManager.Instance.DestroyBanner(_bannerPlacement);
        }

        private void OnLoadInterstitial() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            SDKLogger.Info(Tag, $"Ads: LoadAd ({_interstitialPlacement})");
            AdManager.Instance.LoadAd(_interstitialPlacement);
        }

        private void OnCheckInterstitial() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            bool ready = AdManager.Instance.IsInterstitialReady(_interstitialPlacement);
            SDKLogger.Info(Tag, $"Ads: IsInterstitialReady('{_interstitialPlacement}') = {ready}");
        }

        private void OnShowInterstitial() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            SDKLogger.Info(Tag, $"Ads: ShowInterstitial ({_interstitialPlacement})");
            AdManager.Instance.ShowInterstitial(_interstitialPlacement, result => {
                SDKLogger.Info(Tag, result.Success
                    ? "Interstitial shown OK."
                    : $"Interstitial failed: {result.Error}");
            });
        }

        private void OnLoadRewarded() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            SDKLogger.Info(Tag, $"Ads: LoadAd ({_rewardedPlacement})");
            AdManager.Instance.LoadAd(_rewardedPlacement);
        }

        private void OnCheckRewarded() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            bool ready = AdManager.Instance.IsRewardedReady(_rewardedPlacement);
            SDKLogger.Info(Tag, $"Ads: IsRewardedReady('{_rewardedPlacement}') = {ready}");
        }

        private void OnShowRewarded() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            SDKLogger.Info(Tag, $"Ads: ShowRewarded ({_rewardedPlacement})");
            AdManager.Instance.ShowRewarded(_rewardedPlacement, result => {
                if (result.Success && result.WasRewarded) {
                    SDKLogger.Info(Tag, $"Rewarded OK! Type={result.Reward.Type}, Amount={result.Reward.Amount}");
                } else if (result.Success) {
                    SDKLogger.Info(Tag, "Rewarded closed early (no reward).");
                } else {
                    SDKLogger.Warning(Tag, $"Rewarded failed: {result.Error}");
                }
            });
        }

        private void OnLoadAppOpen() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            SDKLogger.Info(Tag, $"Ads: LoadAd ({_appOpenPlacement}) -- loading app open placement");
            AdManager.Instance.LoadAd(_appOpenPlacement);
        }

        private void OnShowAppOpen() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            SDKLogger.Info(Tag, "Ads: ShowAppOpen");
            AdManager.Instance.ShowAppOpen(onComplete: result => {
                SDKLogger.Info(Tag, result.Success
                    ? "AppOpen shown OK."
                    : $"AppOpen failed: {result.Error}");
            });
        }

        private void OnLoadAllAds() {
            if (AdManager.Instance == null) { SDKLogger.Warning(Tag, "AdManager not available."); return; }
            SDKLogger.Info(Tag, "Ads: Loading all placements...");
            AdManager.Instance.LoadAd(_interstitialPlacement);
            AdManager.Instance.LoadAd(_rewardedPlacement);
            AdManager.Instance.LoadAd(_appOpenPlacement);
        }
        #endif

        // ==========================================================
        //  HANDLERS -- IAP
        // ==========================================================

        #if HAS_SDK_IAP
        private void OnListProducts() {
            if (IAPManager.Instance == null) { SDKLogger.Warning(Tag, "IAPManager not available."); return; }
            var products = IAPManager.Instance.GetProducts();
            SDKLogger.Info(Tag, $"IAP: {products.Count} products available:");
            foreach (var p in products) {
                SDKLogger.Info(Tag, $"  {p.ProductId} - {p.LocalizedTitle} ({p.LocalizedPrice})");
            }
            if (products.Count == 0) {
                SDKLogger.Info(Tag, "  (no products loaded)");
            }
        }

        private void OnPurchaseTest() {
            if (IAPManager.Instance == null) { SDKLogger.Warning(Tag, "IAPManager not available."); return; }
            SDKLogger.Info(Tag, $"IAP: Purchase({_testProductId})");
            IAPManager.Instance.Purchase(_testProductId, _purchaseSource, _purchasePlacement, result => {
                if (result.Success) {
                    SDKLogger.Info(Tag, $"IAP: Purchase OK! Product={result.ProductId}");
                } else {
                    SDKLogger.Warning(Tag, $"IAP: Purchase failed: {result.ErrorMessage} (reason={result.FailureReason})");
                }
            });
        }

        private void OnRestorePurchases() {
            if (IAPManager.Instance == null) { SDKLogger.Warning(Tag, "IAPManager not available."); return; }
            SDKLogger.Info(Tag, "IAP: RestorePurchases()");
            IAPManager.Instance.RestorePurchases(success => {
                SDKLogger.Info(Tag, $"IAP: RestorePurchases result={success}");
            });
        }
        #endif

        // ==========================================================
        //  HANDLERS -- Tracking
        // ==========================================================

        #if HAS_SDK_TRACKING
        private void OnTrackStageStart() {
            _testCounter++;
            SDKLogger.Info(Tag, $"Track: StageStartEvent ({_stageSource}, level_{_testCounter})");
            TrackingManager.Instance?.Track(new StageStartEvent(_stageSource, $"level_{_testCounter}"));
        }

        private void OnTrackStageEnd() {
            SDKLogger.Info(Tag, $"Track: StageEndEvent ({_stageSource}, level_{_testCounter}, 12500ms)");
            TrackingManager.Instance?.Track(new StageEndEvent(_stageSource, $"level_{_testCounter}", 12500));
        }

        private void OnTrackTutorial() {
            _testCounter++;
            SDKLogger.Info(Tag, $"Track: TutorialEvent ({_tutorialName}, step_{_testCounter}, {_testCounter})");
            TrackingManager.Instance?.Track(new TutorialEvent(_tutorialName, $"step_{_testCounter}", _testCounter));
        }

        private void OnTrackTutorialComplete() {
            SDKLogger.Info(Tag, "Track: TutorialCompleteEvent");
            TrackingManager.Instance?.Track(new TutorialCompleteEvent());
        }

        private void OnTrackButtonClick() {
            SDKLogger.Info(Tag, $"Track: ButtonClickEvent ({_buttonScreen}, {_buttonName})");
            TrackingManager.Instance?.Track(new ButtonClickEvent(_buttonScreen, _buttonName, "Test click"));
        }

        private void OnTrackFeatureUnlock() {
            _testCounter++;
            SDKLogger.Info(Tag, $"Track: FeatureUnlockEvent (feature_{_testCounter})");
            TrackingManager.Instance?.Track(new FeatureUnlockEvent($"feature_{_testCounter}"));
        }

        private void OnTrackFeatureOpen() {
            SDKLogger.Info(Tag, $"Track: FeatureOpenEvent ({_featureName})");
            TrackingManager.Instance?.Track(new FeatureOpenEvent(_featureName));
        }

        private void OnTrackFeatureClose() {
            SDKLogger.Info(Tag, $"Track: FeatureCloseEvent ({_featureName}, 5000ms)");
            TrackingManager.Instance?.Track(new FeatureCloseEvent(_featureName, 5000));
        }

        private void OnTrackEarnResource() {
            _testCounter++;
            var data = new ResourceTrackingData(
                ResourceCategory.Currency, _resourceName,
                new TrackingSource(ResourceEventType.Earn, _earnSource, "test"));
            ulong remaining = (ulong)(_testCounter * (int)_earnAmount);
            SDKLogger.Info(Tag, $"Track: EarnResourceEvent ({_resourceName}, +{_earnAmount}, remaining={remaining})");
            TrackingManager.Instance?.Track(new EarnResourceEvent(data, _earnAmount, remaining, remaining));
        }

        private void OnTrackSpendResource() {
            var data = new ResourceTrackingData(
                ResourceCategory.Currency, _resourceName,
                new TrackingSource(ResourceEventType.Spend, _spendSource, _spendItem));
            SDKLogger.Info(Tag, $"Track: SpendResourceEvent ({_resourceName}, -{_spendAmount})");
            TrackingManager.Instance?.Track(new SpendResourceEvent(data, _spendAmount, 500, _spendAmount));
        }

        private void OnTrackIapRevenueSuccess() {
            int revenueMicro = (int)(0.99 * 1_000_000);
            SDKLogger.Info(Tag, $"Track: IapRevenueEvent ({_purchaseItemId}, success, micro={revenueMicro})");
            TrackingManager.Instance?.Track(new IapRevenueEvent(
                _purchaseItemId, revenueMicro, "success", null, null, _purchaseItemTrigger));
        }

        private void OnTrackIapRevenueFail() {
            SDKLogger.Info(Tag, $"Track: IapRevenueEvent ({_purchaseItemId}, fail)");
            TrackingManager.Instance?.Track(new IapRevenueEvent(
                _purchaseItemId, 0, "fail", "User cancelled the purchase", "USER_CANCELED", _purchaseItemTrigger));
        }

        private void OnTrackLoadingStart() {
            SDKLogger.Info(Tag, "Track: LoadingStartEvent");
            TrackingManager.Instance?.Track(new LoadingStartEvent());
        }

        private void OnTrackLoadingResult() {
            int fps = (int)(1f / Time.deltaTime);
            SDKLogger.Info(Tag, $"Track: LoadingResultEvent (timeout=5000, fps={fps}, status=1)");
            TrackingManager.Instance?.Track(new LoadingResultEvent(5000, fps, 1));
        }

        private void OnTrackExplorationStart() {
            _testCounter++;
            SDKLogger.Info(Tag, $"Track: ExplorationStartEvent (boss_{_testCounter})");
            TrackingManager.Instance?.Track(new ExplorationStartEvent($"boss_{_testCounter}"));
        }

        private void OnTrackExplorationEnd() {
            SDKLogger.Info(Tag, $"Track: ExplorationEndEvent (boss_{_testCounter}, result=1)");
            TrackingManager.Instance?.Track(new ExplorationEndEvent($"boss_{_testCounter}", 1));
        }

        private void OnTrackExplorationRankUp() {
            SDKLogger.Info(Tag, $"Track: ExplorationRankUpEvent (boss_{_testCounter}, rank_gold)");
            TrackingManager.Instance?.Track(new ExplorationRankUpEvent($"boss_{_testCounter}", "rank_gold"));
        }

        private void OnTrackForgeUpgrade() {
            _testCounter++;
            SDKLogger.Info(Tag, $"Track: ForgeUpgradeEvent (level_{_testCounter})");
            TrackingManager.Instance?.Track(new ForgeUpgradeEvent($"level_{_testCounter}"));
        }

        private void OnTrackCharacterLevelUp() {
            _testCounter++;
            SDKLogger.Info(Tag, $"Track: CharacterLevelUpEvent (level_{_testCounter})");
            TrackingManager.Instance?.Track(new CharacterLevelUpEvent($"level_{_testCounter}"));
        }

        private void OnTrackSpellUpgrade() {
            _testCounter++;
            SDKLogger.Info(Tag, $"Track: SpellUpgradeEvent ({_spellName}, level_{_testCounter})");
            TrackingManager.Instance?.Track(new SpellUpgradeEvent(_spellName, $"level_{_testCounter}"));
        }

        private void OnTrackAdRevenueCustom() {
            int revenueMicro = (int)(0.001 * 1_000_000);
            SDKLogger.Info(Tag, $"Track: AdRevenueCustomEvent ({_adMediationName}, {_adNetworkName}, {_adUnitId}, micro={revenueMicro})");
            TrackingManager.Instance?.TrackAdRevenueCustomEvent(
                _adMediationName, _adNetworkName, _adUnitId, _adPlacementName, revenueMicro);
        }

        private void OnTrackAdImpression() {
            SDKLogger.Info(Tag, $"Track: AdImpressionEvent ({_adMediationName}, {_adNetworkName}, Banner, $0.001)");
            TrackingManager.Instance?.Track(new AdImpressionEvent(
                _adMediationName, _adNetworkName, "Banner", _adUnitId, "USD", 0.001, _adPlacementName));
        }

        private void OnTrackCustomEvent() {
            _testCounter++;
            SDKLogger.Info(Tag, $"Track: GenericGameTrackingEvent (test_event_{_testCounter})");
            TrackingManager.Instance?.Track(new GenericGameTrackingEvent(
                $"test_event_{_testCounter}",
                new Dictionary<string, object> {
                    { "source", "test_panel" },
                    { "counter", _testCounter },
                    { "timestamp", Time.realtimeSinceStartup }
                }));
        }

        // ==========================================================
        //  HANDLERS -- User Profile
        // ==========================================================

        private void OnUpdateProfile() {
            _testCounter++;
            SDKLogger.Info(Tag, $"UserProfile: level={_testCounter}, stage=level_{_testCounter}");
            TrackingManager.Instance?.UpdateUserProfile(p => {
                p.CurrentForgeShopLevel = _testCounter;
                p.CurrentStage = $"level_{_testCounter}";
                p.ProgressStage = _testCounter;
            });
        }

        private void OnSetUserProperty() {
            string value = $"value_{_testCounter}";
            SDKLogger.Info(Tag, $"SetUserProperty: {_userPropertyKey}={value}");
            TrackingManager.Instance?.SetUserProperty(_userPropertyKey, value);
        }

        private void OnShowProfile() {
            var profile = TrackingManager.Instance?.CurrentUserProfile;
            if (profile == null) {
                SDKLogger.Warning(Tag, "UserProfile is null.");
                return;
            }
            SDKLogger.Info(Tag,
                $"UserProfile:\n" +
                $"  DeviceId={profile.DeviceId}\n" +
                $"  AdjustId={profile.AdjustId}\n" +
                $"  ForgeShopLevel={profile.CurrentForgeShopLevel}, Stage={profile.CurrentStage}\n" +
                $"  IAP count={profile.IapCount}\n" +
                $"  IAA count={profile.IaaCount}\n" +
                $"  Gem={profile.CurrentGem}\n" +
                $"  DaySinceInstall={profile.DaySinceInstall}");
        }
        #endif

        // ==========================================================
        //  HANDLERS -- Remote Config
        // ==========================================================

        #if HAS_SDK_REMOTECONFIG
        private void OnFetchRemoteConfig() {
            if (RemoteConfigManager.Instance == null) {
                SDKLogger.Warning(Tag, "RemoteConfigManager not available.");
                return;
            }
            SDKLogger.Info(Tag, "RemoteConfig: FetchAndActivate()");
            RemoteConfigManager.Instance.FetchAndActivate(success => {
                SDKLogger.Info(Tag, $"RemoteConfig: FetchAndActivate result={success}");
            });
        }

        private void OnGetRemoteString() {
            if (RemoteConfigManager.Instance == null) {
                SDKLogger.Warning(Tag, "RemoteConfigManager not available.");
                return;
            }
            string val = RemoteConfigManager.Instance.GetString(_remoteStringKey, _remoteStringDefault);
            SDKLogger.Info(Tag, $"RemoteConfig: GetString('{_remoteStringKey}') = '{val}'");
        }

        private void OnGetRemoteBool() {
            if (RemoteConfigManager.Instance == null) {
                SDKLogger.Warning(Tag, "RemoteConfigManager not available.");
                return;
            }
            bool val = RemoteConfigManager.Instance.GetBool(_remoteBoolKey, false);
            SDKLogger.Info(Tag, $"RemoteConfig: GetBool('{_remoteBoolKey}') = {val}");
        }

        private void OnGetRemoteInt() {
            if (RemoteConfigManager.Instance == null) {
                SDKLogger.Warning(Tag, "RemoteConfigManager not available.");
                return;
            }
            int val = RemoteConfigManager.Instance.GetInt(_remoteIntKey, 0);
            SDKLogger.Info(Tag, $"RemoteConfig: GetInt('{_remoteIntKey}') = {val}");
        }

        private void OnGetRemoteFloat() {
            if (RemoteConfigManager.Instance == null) {
                SDKLogger.Warning(Tag, "RemoteConfigManager not available.");
                return;
            }
            float val = RemoteConfigManager.Instance.GetFloat(_remoteFloatKey, 0f);
            SDKLogger.Info(Tag, $"RemoteConfig: GetFloat('{_remoteFloatKey}') = {val}");
        }
        #endif

        // ==========================================================
        //  HANDLERS -- Crash Reporting
        // ==========================================================

        #if HAS_SDK_CRASHREPORTING
        private void OnCrashLog() {
            if (CrashReportingManager.Instance == null) {
                SDKLogger.Warning(Tag, "CrashReportingManager not available.");
                return;
            }
            string msg = $"Test crash log message #{_testCounter}";
            SDKLogger.Info(Tag, $"CrashReporting: Log('{msg}')");
            CrashReportingManager.Instance.Log(msg);
        }

        private void OnCrashLogException() {
            if (CrashReportingManager.Instance == null) {
                SDKLogger.Warning(Tag, "CrashReportingManager not available.");
                return;
            }
            var ex = new Exception($"Test non-fatal exception from SDKTestPanel #{_testCounter}");
            SDKLogger.Info(Tag, "CrashReporting: LogException (non-fatal)");
            CrashReportingManager.Instance.LogException(ex);
        }

        private void OnCrashSetKey() {
            if (CrashReportingManager.Instance == null) {
                SDKLogger.Warning(Tag, "CrashReportingManager not available.");
                return;
            }
            _testCounter++;
            string val = $"test_value_{_testCounter}";
            SDKLogger.Info(Tag, $"CrashReporting: SetCustomKey('{_crashCustomKey}', '{val}')");
            CrashReportingManager.Instance.SetCustomKey(_crashCustomKey, val);
        }

        private void OnCrashSetUserId() {
            if (CrashReportingManager.Instance == null) {
                SDKLogger.Warning(Tag, "CrashReportingManager not available.");
                return;
            }
            string userId = $"test_user_{_testCounter}";
            SDKLogger.Info(Tag, $"CrashReporting: SetUserId('{userId}')");
            CrashReportingManager.Instance.SetUserId(userId);
        }

        private void OnTestCrash() {
            SDKLogger.Error(Tag, "TEST CRASH: Throwing unhandled exception in 1 second...");
            Invoke(nameof(ThrowTestCrash), 1f);
        }

        private void ThrowTestCrash() {
            throw new Exception("SDKTestPanel: Intentional test crash!");
        }
        #endif

        // ==========================================================
        //  HANDLERS -- Push Notifications
        // ==========================================================

        #if HAS_SDK_PUSH
        private void OnPushRequestPermission() {
            if (PushManager.Instance == null) {
                SDKLogger.Warning(Tag, "PushManager not available.");
                return;
            }
            SDKLogger.Info(Tag, "Push: RequestPermission()");
            PushManager.Instance.RequestPermission(granted => {
                SDKLogger.Info(Tag, $"Push: Permission result={granted}");
            });
        }

        private void OnPushGetToken() {
            if (PushManager.Instance == null) {
                SDKLogger.Warning(Tag, "PushManager not available.");
                return;
            }
            SDKLogger.Info(Tag, "Push: GetToken()");
            PushManager.Instance.GetToken(token => {
                SDKLogger.Info(Tag, $"Push: Token={token ?? "(null)"}");
            });
        }

        private void OnPushSubscribe() {
            if (PushManager.Instance == null) {
                SDKLogger.Warning(Tag, "PushManager not available.");
                return;
            }
            SDKLogger.Info(Tag, $"Push: SubscribeToTopic('{_pushTopic}')");
            PushManager.Instance.SubscribeToTopic(_pushTopic);
        }

        private void OnPushUnsubscribe() {
            if (PushManager.Instance == null) {
                SDKLogger.Warning(Tag, "PushManager not available.");
                return;
            }
            SDKLogger.Info(Tag, $"Push: UnsubscribeFromTopic('{_pushTopic}')");
            PushManager.Instance.UnsubscribeFromTopic(_pushTopic);
        }
        #endif

        // ==========================================================
        //  HANDLERS -- Deep Link
        // ==========================================================

        #if HAS_SDK_DEEPLINK
        private void OnShowLastDeepLink() {
            if (DeepLinkManager.Instance == null) {
                SDKLogger.Warning(Tag, "DeepLinkManager not available.");
                return;
            }
            var last = DeepLinkManager.Instance.LastDeepLink;
            if (last.HasValue) {
                SDKLogger.Info(Tag, $"DeepLink: Last={last.Value}");
            } else {
                SDKLogger.Info(Tag, "DeepLink: No deep link received yet.");
            }
        }
        #endif

        // ==========================================================
        //  HANDLERS -- Adjust Device Info
        // ==========================================================

        #if HAS_SDK_TRACKING && HAS_ADJUST_SDK
        private AdjustTrackingProvider GetAdjustProvider() {
            var init = SDKInitializer.Instance;
            var tm = init?.GetModule("tracking") as TrackingManager;
            return tm?.GetAdjustProvider();
        }

        #if UNITY_ANDROID
        private void OnAdjustOaidDiagnostics() {
            SDKLogger.Info(Tag, "Adjust: OAID Dependency Diagnostics");
            AdjustOaidPlugin.DiagnoseDependencies();
        }

        private void OnAdjustGetOaid() {
            SDKLogger.Info(Tag, "Adjust: GetOaid()");
            AdjustOaidPlugin.GetOaid(oaid => {
                if (oaid != null)
                    SDKLogger.Info(Tag, $"OAID: {oaid}");
                else
                    SDKLogger.Warning(Tag, "OAID: (null) — HMS/MSA SDK not available or device has no OAID.");
            });
        }

        private void OnAdjustOaidStatus() {
            SDKLogger.Info(Tag, "Adjust: OAID Status");
            SDKLogger.Info(Tag, $"  OAID Plugin Initialized: {AdjustOaidPlugin.IsInitialized}");
            SDKLogger.Info(Tag, $"  Platform: {Application.platform}");
            AdjustOaidPlugin.GetOaid(oaid =>
                SDKLogger.Info(Tag, $"  Current OAID Value: {oaid ?? "(not available)"}"));
        }
        #endif

        private void OnAdjustGetAdid() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: GetAdid()");
            adj.GetAdid(adid => SDKLogger.Info(Tag, $"Adjust ADID: {adid ?? "(null)"}"));
        }

        private void OnAdjustGetAttribution() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: GetAttribution()");
            adj.GetAttribution(attr => {
                if (attr == null) {
                    SDKLogger.Info(Tag, "Adjust Attribution: (null)");
                    return;
                }
                SDKLogger.Info(Tag, $"Adjust Attribution: {attr}");
            });
        }

        private void OnAdjustGetSdkVersion() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: GetSdkVersion()");
            adj.GetSdkVersion(ver => SDKLogger.Info(Tag, $"Adjust SDK Version: {ver ?? "(null)"}"));
        }

        private void OnAdjustGetGoogleAdId() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: GetGoogleAdId()");
            adj.GetGoogleAdId(id => SDKLogger.Info(Tag, $"Google Ad ID: {id ?? "(null)"}"));
        }

        private void OnAdjustGetIdfa() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: GetIdfa()");
            adj.GetIdfa(idfa => SDKLogger.Info(Tag, $"IDFA: {idfa ?? "(null)"}"));
        }

        private void OnAdjustGetIdfv() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: GetIdfv()");
            adj.GetIdfv(idfv => SDKLogger.Info(Tag, $"IDFV: {idfv ?? "(null)"}"));
        }

        private void OnAdjustGetAmazonAdId() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: GetAmazonAdId()");
            adj.GetAmazonAdId(id => SDKLogger.Info(Tag, $"Amazon Ad ID: {id ?? "(null)"}"));
        }

        private void OnAdjustGetLastDeeplink() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: GetLastDeeplink()");
            adj.GetLastDeeplink(url => SDKLogger.Info(Tag, $"Last Deeplink: {url ?? "(null)"}"));
        }

        // ==========================================================
        //  HANDLERS -- Adjust Privacy & Control
        // ==========================================================

        private void OnAdjustIsEnabled() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: IsEnabled()");
            adj.IsEnabled(enabled => SDKLogger.Info(Tag, $"Adjust IsEnabled: {enabled}"));
        }

        private void OnAdjustDisable() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: Disable()");
            adj.Disable();
        }

        private void OnAdjustEnable() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: Enable()");
            adj.Enable();
        }

        private void OnAdjustOffline() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: SwitchToOfflineMode()");
            adj.SwitchToOfflineMode();
        }

        private void OnAdjustOnline() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: SwitchBackToOnlineMode()");
            adj.SwitchBackToOnlineMode();
        }

        private void OnAdjustMeasurementConsentOn() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: TrackMeasurementConsent(true)");
            adj.TrackMeasurementConsent(true);
        }

        private void OnAdjustMeasurementConsentOff() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: TrackMeasurementConsent(false)");
            adj.TrackMeasurementConsent(false);
        }

        private void OnAdjustGdprForgetMe() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Warning(Tag, "Adjust: GdprForgetMe() — THIS IS IRREVERSIBLE!");
            adj.GdprForgetMe();
        }

        // ==========================================================
        //  HANDLERS -- Adjust Global Params
        // ==========================================================

        private void OnAdjustAddCallbackParam() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, $"Adjust: AddGlobalCallbackParameter('{_adjustGlobalParamKey}', '{_adjustGlobalParamValue}')");
            adj.AddGlobalCallbackParameter(_adjustGlobalParamKey, _adjustGlobalParamValue);
        }

        private void OnAdjustRemoveCallbackParam() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, $"Adjust: RemoveGlobalCallbackParameter('{_adjustGlobalParamKey}')");
            adj.RemoveGlobalCallbackParameter(_adjustGlobalParamKey);
        }

        private void OnAdjustClearCallbackParams() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: RemoveGlobalCallbackParameters()");
            adj.RemoveGlobalCallbackParameters();
        }

        private void OnAdjustAddPartnerParam() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, $"Adjust: AddGlobalPartnerParameter('{_adjustGlobalParamKey}', '{_adjustGlobalParamValue}')");
            adj.AddGlobalPartnerParameter(_adjustGlobalParamKey, _adjustGlobalParamValue);
        }

        private void OnAdjustRemovePartnerParam() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, $"Adjust: RemoveGlobalPartnerParameter('{_adjustGlobalParamKey}')");
            adj.RemoveGlobalPartnerParameter(_adjustGlobalParamKey);
        }

        private void OnAdjustClearPartnerParams() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, "Adjust: RemoveGlobalPartnerParameters()");
            adj.RemoveGlobalPartnerParameters();
        }

        // ==========================================================
        //  HANDLERS -- Adjust Ad Revenue & Deeplink
        // ==========================================================

        private void OnAdjustTrackAdRevenue() {
            var init = SDKInitializer.Instance;
            var tm = init?.GetModule("tracking") as TrackingManager;
            if (tm == null) { SDKLogger.Warning(Tag, "TrackingManager not available."); return; }
            SDKLogger.Info(Tag,
                $"TrackAdRevenue(platform='{_adjustTestAdRevenueSource}', " +
                $"revenue={_adjustTestAdRevenue}, currency='USD', network='{_adNetworkName}', " +
                $"unit='{_adUnitId}', placement='{_adPlacementName}')");
            tm.TrackAdRevenue(
                _adjustTestAdRevenueSource,
                _adNetworkName,
                "interstitial",
                _adUnitId,
                "USD",
                _adjustTestAdRevenue,
                _adPlacementName);
        }

        private void OnAdjustProcessDeeplink() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, $"Adjust: ProcessDeeplink('{_adjustTestDeeplink}')");
            adj.ProcessDeeplink(_adjustTestDeeplink);
        }

        private void OnAdjustResolveDeeplink() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            SDKLogger.Info(Tag, $"Adjust: ProcessAndResolveDeeplink('{_adjustTestDeeplink}')");
            adj.ProcessAndResolveDeeplink(_adjustTestDeeplink, resolved =>
                SDKLogger.Info(Tag, $"Adjust Resolved Deeplink: {resolved ?? "(null)"}"));
        }

        private void OnAdjustSetPushToken() {
            var adj = GetAdjustProvider();
            if (adj == null) { SDKLogger.Warning(Tag, "AdjustProvider not available."); return; }
            string testToken = "test_push_token_" + _testCounter++;
            SDKLogger.Info(Tag, $"Adjust: SetPushToken('{testToken}')");
            adj.SetPushToken(testToken);
        }
        #endif

        // ==========================================================
        //  LOG CONTROL HANDLERS
        // ==========================================================

        private void OnClearLog() {
            _logLines.Clear();
            SDKLogger.ClearBuffer();
            if (_logText != null) _logText.text = "";
            SDKLogger.Info(Tag, "Log cleared.");
        }

        private void OnToggleAutoScroll() {
            _autoScroll = !_autoScroll;
            var btn = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (btn != null) {
                var text = btn.GetComponentInChildren<Text>();
                if (text != null) text.text = $"Auto-Scroll: {(_autoScroll ? "ON" : "OFF")}";
            }
            SDKLogger.Info(Tag, $"Auto-scroll: {_autoScroll}");
        }

        private void OnToggleLog() {
            bool newState = !SDKLogger.IsEnabled;
            SDKLogger.SetEnabled(newState);
            var btn = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (btn != null) {
                var text = btn.GetComponentInChildren<Text>();
                if (text != null) text.text = $"Log: {(newState ? "ON" : "OFF")}";
            }
            if (newState) SDKLogger.Info(Tag, "Logging enabled.");
        }

        private void OnCycleLogLevel() {
            var current = SDKLogger.CurrentMinLevel;
            LogLevel next;
            switch (current) {
                case LogLevel.Verbose: next = LogLevel.Debug; break;
                case LogLevel.Debug:   next = LogLevel.Info; break;
                case LogLevel.Info:    next = LogLevel.Warning; break;
                case LogLevel.Warning: next = LogLevel.Error; break;
                case LogLevel.Error:   next = LogLevel.None; break;
                default:               next = LogLevel.Verbose; break;
            }
            SDKLogger.SetMinLevel(next);
            var btn = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (btn != null) {
                var text = btn.GetComponentInChildren<Text>();
                if (text != null) text.text = $"Level: {next}";
            }
            SDKLogger.Info(Tag, $"Log level changed to: {next}");
        }

        // ==========================================================
        //  UI HELPERS
        // ==========================================================

        private static GameObject CreatePanel(Transform parent, string name, Color color) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.text = content;
            text.color = Color.white;
            text.supportRichText = true;
            return text;
        }

        private static Button CreateButton(Transform parent, string label,
            UnityEngine.Events.UnityAction onClick, Color? bgColor = null) {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = bgColor ?? new Color(0.25f, 0.25f, 0.25f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.text = label;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            StretchFull(textGo.GetComponent<RectTransform>());

            return btn;
        }

        private static void CreateSectionHeader(Transform parent, string title) {
            var go = new GameObject($"Header_{title}", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 40;

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.text = $"-- {title} --";
            text.color = new Color(1f, 1f, 1f, 0.6f);
            text.alignment = TextAnchor.MiddleLeft;
            text.fontStyle = FontStyle.Bold;
        }

        private static void StretchFull(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
