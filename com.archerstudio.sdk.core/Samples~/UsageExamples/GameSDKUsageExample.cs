// ═══════════════════════════════════════════════════════════════════
//  GameSDKUsageExample.cs
//  ArcherStudioSDK — Complete Usage Examples
//
//  File này chứa code mẫu thực tế để sử dụng ArcherStudioSDK.
//  Copy từng class cần thiết vào game project của bạn.
//
//  Code KHÔNG bị comment — compile và chạy được ngay.
//  Chỉ cần:
//    1. Cài SDK packages (sdk.core, sdk.tracking, sdk.ads)
//    2. Tạo config assets (Setup Wizard hoặc thủ công)
//    3. Kéo script vào GameObject trong scene
//
//  Modules covered:
//    1. SDK Bootstrap & Lifecycle
//    2. Consent (GDPR/ATT)
//    3. Tracking (Firebase + Adjust) — ALL event types
//    4. Ads (Banner, Interstitial, Rewarded, App Open)
//    5. Advanced: UserProfile, EventBus, Custom Events
// ═══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using UnityEngine;
using ArcherStudio.SDK.Core;
using ArcherStudio.SDK.Tracking;
using ArcherStudio.SDK.Tracking.Events;
using ArcherStudio.SDK.Ads;

namespace ArcherStudio.SDK.Examples {

    // ═════════════════════════════════════════════════════════════
    //  1. SDK LIFECYCLE — Lắng nghe SDK Ready & Module Init
    // ═════════════════════════════════════════════════════════════
    //
    //  Bootstrap Scene (Scene 0) tự xử lý init flow.
    //  Script này đặt ở game scene — chỉ lắng nghe kết quả.
    //
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Đặt trong game scene (MainMenu, Gameplay).
    /// Lắng nghe SDK init xong rồi bắt đầu game logic.
    /// </summary>
    public class SDKLifecycleExample : MonoBehaviour {

        private void OnEnable() {
            SDKEventBus.Subscribe<SDKReadyEvent>(OnSDKReady);
            SDKEventBus.Subscribe<ModuleInitializedEvent>(OnModuleInitialized);
            SDKEventBus.Subscribe<ConsentChangedEvent>(OnConsentChanged);
        }

        private void OnDisable() {
            SDKEventBus.Unsubscribe<SDKReadyEvent>(OnSDKReady);
            SDKEventBus.Unsubscribe<ModuleInitializedEvent>(OnModuleInitialized);
            SDKEventBus.Unsubscribe<ConsentChangedEvent>(OnConsentChanged);
        }

        private void OnSDKReady(SDKReadyEvent e) {
            if (e.Success) {
                SDKLogger.Info("Game", "SDK ready! All modules initialized.");
            } else {
                SDKLogger.Warning("Game",
                    $"SDK ready with {e.FailedModuleCount} failed modules.");
            }
        }

        private void OnModuleInitialized(ModuleInitializedEvent e) {
            SDKLogger.Info("Game",
                $"Module '{e.ModuleId}' → {(e.Success ? "OK" : "FAILED")}");
        }

        private void OnConsentChanged(ConsentChangedEvent e) {
            var s = e.Status;
            SDKLogger.Info("Game",
                $"Consent: ads={s.CanShowPersonalizedAds}, " +
                $"analytics={s.CanCollectAnalytics}, " +
                $"attribution={s.CanTrackAttribution}");
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  2. TRACKING — Toàn bộ event types
    // ═════════════════════════════════════════════════════════════
    //
    //  TrackingManager gửi events đến cả Firebase và Adjust.
    //  Mỗi event class đã define sẵn EventName và params.
    //  Một số event có AdjustToken → tự gửi cho Adjust attribution.
    //
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Ví dụ tracking toàn bộ event types.
    /// Kéo vào GameObject, gọi các method từ game logic.
    /// </summary>
    public class TrackingExample : MonoBehaviour {

        // ─── Stage / Level ───

        /// <summary>
        /// Gọi khi bắt đầu level.
        /// </summary>
        public void TrackStageStart(string category, string stageId) {
            TrackingManager.Instance.Track(
                new StageStartEvent(category, stageId));
        }

        /// <summary>
        /// Gọi khi kết thúc level.
        /// </summary>
        /// <param name="durationMs">Thời gian chơi (milliseconds)</param>
        public void TrackStageEnd(string category, string stageId, int durationMs) {
            TrackingManager.Instance.Track(
                new StageEndEvent(category, stageId, durationMs));
        }

        // ─── Tutorial ───

        /// <summary>
        /// Gọi ở mỗi bước tutorial.
        /// </summary>
        public void TrackTutorialStep(string category, string stepName, int stepIndex) {
            TrackingManager.Instance.Track(
                new TutorialEvent(category, stepName, stepIndex));
        }

        /// <summary>
        /// Gọi khi hoàn thành tutorial.
        /// Tự gửi Adjust event (token "98yxoq") cho attribution.
        /// </summary>
        public void TrackTutorialComplete() {
            TrackingManager.Instance.Track(new TutorialCompleteEvent());
        }

        // ─── Feature Tracking ───

        public void TrackFeatureUnlock(string featureId) {
            TrackingManager.Instance.Track(new FeatureUnlockEvent(featureId));
        }

        public void TrackFeatureOpen(string featureId) {
            TrackingManager.Instance.Track(new FeatureOpenEvent(featureId));
        }

        public void TrackFeatureClose(string featureId, int durationMs) {
            TrackingManager.Instance.Track(
                new FeatureCloseEvent(featureId, durationMs));
        }

        // ─── Resource Economy ───

        /// <summary>
        /// Track khi user kiếm được resource (reward, loot, ...).
        /// </summary>
        public void TrackEarnResource(
            string itemId, string source, string sourceId,
            double amount, double remaining, double totalEarned) {

            var data = new ResourceTrackingData(
                ResourceCategory.Currency, itemId,
                new TrackingSource(ResourceEventType.Earn, source, sourceId));

            TrackingManager.Instance.Track(
                new EarnResourceEvent(data, amount, remaining, totalEarned));
        }

        /// <summary>
        /// Track khi user tiêu resource (upgrade, mua item, ...).
        /// </summary>
        public void TrackSpendResource(
            string itemId, string source, string sourceId,
            double amount, double remaining, double totalSpent) {

            var data = new ResourceTrackingData(
                ResourceCategory.Currency, itemId,
                new TrackingSource(ResourceEventType.Spend, source, sourceId));

            TrackingManager.Instance.Track(
                new SpendResourceEvent(data, amount, remaining, totalSpent));
        }

        /// <summary>
        /// Track khi user mua resource bằng tiền thật (IAP).
        /// </summary>
        public void TrackBuyResource(
            string itemId, string source, string sourceId,
            double amount, double remaining, double totalBought) {

            var data = new ResourceTrackingData(
                ResourceCategory.Currency, itemId,
                new TrackingSource(ResourceEventType.Buy, source, sourceId));

            TrackingManager.Instance.Track(
                new BuyResourceEvent(data, amount, remaining, totalBought));
        }

        // ─── IAP Revenue Tracking (v2) ───

        /// <summary>
        /// Track khi player mua IAP (thành công hoặc thất bại).
        /// </summary>
        public void TrackIapRevenue(string productId, int revenueMicro,
            string purchaseStatus, string failReason = null, string placement = null) {
            TrackingManager.Instance.Track(
                new IapRevenueEvent(productId, revenueMicro, purchaseStatus, failReason, placement));
        }

        // ─── Button Click ───

        public void TrackButtonClick(string category, string buttonName) {
            TrackingManager.Instance.Track(
                new ButtonClickEvent(category, buttonName));
        }

        public void TrackButtonClickWithDesc(
            string category, string buttonName, string description) {
            TrackingManager.Instance.Track(
                new ButtonClickEvent(category, buttonName, description));
        }

        // ─── Custom Event ───

        /// <summary>
        /// Gửi event tùy ý với tên và params bất kỳ.
        /// Dùng khi không có event class có sẵn phù hợp.
        /// </summary>
        public void TrackCustomEvent(
            string eventName, Dictionary<string, object> parameters) {
            TrackingManager.Instance.Track(
                new GenericGameTrackingEvent(eventName, parameters));
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  3. USER PROFILE — Auto-sync to Firebase + Adjust
    // ═════════════════════════════════════════════════════════════
    //
    //  UserProfile lưu persistent data (level, IAP status, ...).
    //  Khi set property → tự sync lên Firebase User Properties
    //  + Adjust session parameters.
    //
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Ví dụ quản lý User Profile cho tracking attribution.
    /// </summary>
    public class UserProfileExample : MonoBehaviour {

        /// <summary>
        /// Gọi sau khi SDK ready để setup user profile ban đầu.
        /// </summary>
        public void SetupUserProfile(int currentLevel, bool isPayer) {
            TrackingManager.Instance.UpdateUserProfile(p => {
                p.CurrentForgeShopLevel = currentLevel;
                p.IapCount = isPayer ? 1 : 0;
            });
        }

        /// <summary>
        /// Gọi khi user lên level.
        /// </summary>
        public void OnPlayerLevelUp(int newLevel, string stageId) {
            TrackingManager.Instance.UpdateUserProfile(p => {
                p.CurrentForgeShopLevel = newLevel;
                p.CurrentStage = stageId;
                p.ProgressStage = newLevel;
            });
        }

        /// <summary>
        /// Gọi khi user mua IAP lần đầu (chuyển thành payer).
        /// </summary>
        public void OnFirstPurchase() {
            TrackingManager.Instance.UpdateUserProfile(p => {
                p.IapCount = p.IapCount + 1;
            });
        }

        /// <summary>
        /// Gọi khi user xem rewarded ad.
        /// </summary>
        public void OnRewardedAdWatched() {
            TrackingManager.Instance.UpdateUserProfile(p => {
                p.IaaCount = p.IaaCount + 1;
            });
        }

        /// <summary>
        /// Track resource economy qua UserProfile.
        /// </summary>
        public void TrackResourceChange(string resourceId, ulong earnAmount) {
            var profile = TrackingManager.Instance.CurrentUserProfile;
            double totalEarned = profile.AddEarned(resourceId, earnAmount);
            SDKLogger.Info("Game", $"{resourceId}: total earned = {totalEarned}");
        }

        /// <summary>
        /// Set custom property (gửi đến tất cả tracking providers).
        /// </summary>
        public void SetCustomProperty(string key, string value) {
            TrackingManager.Instance.SetUserProperty(key, value);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  4. ADS — Banner, Interstitial, Rewarded, App Open
    // ═════════════════════════════════════════════════════════════
    //
    //  Cấu hình placements trong AdConfig ScriptableObject:
    //    PlacementId → tên gọi trong code
    //    Format → Banner / Interstitial / Rewarded / AppOpen
    //    AndroidUnitId / IosUnitId → ad unit IDs
    //    AutoLoad → true = tự load khi SDK init
    //
    //  Ad revenue tự bridge sang Firebase + Adjust
    //  qua AdRevenueTracker — không cần gọi thủ công.
    //
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Ví dụ hiển thị ads đầy đủ.
    /// Kéo vào GameObject, gọi từ UI buttons.
    /// </summary>
    public class AdsExample : MonoBehaviour {

        // ─── Banner ───

        /// <summary>
        /// Hiện banner ở dưới màn hình.
        /// PlacementId phải khớp với config trong AdConfig.
        /// </summary>
        public void ShowBanner() {
            AdManager.Instance.ShowBanner("main_banner", BannerPosition.Bottom);
            SDKLogger.Info("Ads", "Banner shown: main_banner");
        }

        /// <summary>
        /// Hiện banner ở trên màn hình.
        /// </summary>
        public void ShowBannerTop() {
            AdManager.Instance.ShowBanner("main_banner", BannerPosition.Top);
        }

        /// <summary>
        /// Ẩn banner (vẫn giữ instance, có thể show lại).
        /// </summary>
        public void HideBanner() {
            AdManager.Instance.HideBanner("main_banner");
        }

        /// <summary>
        /// Destroy banner (giải phóng bộ nhớ, cần load lại nếu muốn show).
        /// Gọi khi chuyển scene hoặc không cần banner nữa.
        /// </summary>
        public void DestroyBanner() {
            AdManager.Instance.DestroyBanner("main_banner");
        }

        // ─── Interstitial ───

        /// <summary>
        /// Show interstitial sau khi hoàn thành level.
        /// Tự kiểm tra: ad ready, cooldown, session cap.
        /// </summary>
        public void ShowInterstitialAfterLevel(Action onComplete) {
            if (!AdManager.Instance.IsInterstitialReady("level_complete")) {
                SDKLogger.Info("Ads", "Interstitial not ready, skipping.");
                onComplete?.Invoke();
                return;
            }

            AdManager.Instance.ShowInterstitial("level_complete", result => {
                if (result.Success) {
                    SDKLogger.Info("Ads", "Interstitial shown OK.");
                } else {
                    SDKLogger.Info("Ads", $"Interstitial: {result.Error}");
                }
                // Tiếp tục game flow bất kể ad thành công hay không
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Show interstitial với callback đơn giản.
        /// </summary>
        public void ShowInterstitial(string placementId) {
            AdManager.Instance.ShowInterstitial(placementId, result => {
                SDKLogger.Info("Ads",
                    result.Success
                        ? $"Interstitial '{placementId}' shown."
                        : $"Interstitial '{placementId}' failed: {result.Error}");
            });
        }

        // ─── Rewarded Ad ───

        /// <summary>
        /// Show rewarded ad để nhận thưởng (x2 coins, extra life, ...).
        /// CHỈ phát thưởng khi result.WasRewarded == true.
        /// </summary>
        public void ShowRewardedAd(string placementId, Action<bool, int> onResult) {
            if (!AdManager.Instance.IsRewardedReady(placementId)) {
                SDKLogger.Info("Ads", $"Rewarded '{placementId}' not ready.");
                onResult?.Invoke(false, 0);
                return;
            }

            AdManager.Instance.ShowRewarded(placementId, result => {
                if (result.Success && result.WasRewarded) {
                    // User xem hết ad → phát thưởng
                    SDKLogger.Info("Ads",
                        $"Rewarded OK! Type={result.Reward.Type}, " +
                        $"Amount={result.Reward.Amount}");
                    onResult?.Invoke(true, result.Reward.Amount);
                } else {
                    // Ad lỗi hoặc user đóng sớm → KHÔNG phát thưởng
                    string reason = !result.Success
                        ? $"Error: {result.Error}"
                        : "User closed early";
                    SDKLogger.Info("Ads", $"Rewarded not completed: {reason}");
                    onResult?.Invoke(false, 0);
                }
            });
        }

        /// <summary>
        /// Ví dụ: Double coins sau khi clear level.
        /// </summary>
        public void DoubleCoinsReward(int baseCoins, Action<int> onCoinsGranted) {
            ShowRewardedAd("double_coins", (rewarded, amount) => {
                int finalCoins = rewarded ? baseCoins * 2 : baseCoins;
                onCoinsGranted?.Invoke(finalCoins);
            });
        }

        /// <summary>
        /// Ví dụ: Extra life khi game over.
        /// </summary>
        public void ExtraLifeReward(Action<bool> onResult) {
            ShowRewardedAd("extra_life", (rewarded, _) => {
                onResult?.Invoke(rewarded);
            });
        }

        // ─── App Open Ad ───

        /// <summary>
        /// Show app open ad khi resume từ background.
        /// Gọi trong OnApplicationPause(false).
        /// </summary>
        public void ShowAppOpenAd() {
            AdManager.Instance.ShowAppOpen(onComplete: result => {
                SDKLogger.Info("Ads",
                    result.Success
                        ? "App open ad shown."
                        : $"App open ad: {result.Error}");
            });
        }

        /// <summary>
        /// Show app open ad với placement cụ thể.
        /// </summary>
        public void ShowAppOpenAd(string placementId) {
            AdManager.Instance.ShowAppOpen(placementId, result => {
                SDKLogger.Info("Ads",
                    result.Success
                        ? $"App open '{placementId}' shown."
                        : $"App open '{placementId}': {result.Error}");
            });
        }

        // ─── Manual Load ───

        /// <summary>
        /// Load ad thủ công (khi AutoLoad = false trong config).
        /// </summary>
        public void PreloadAd(string placementId) {
            AdManager.Instance.LoadAd(placementId);
            SDKLogger.Info("Ads", $"Preloading '{placementId}'...");
        }

        // ─── Ad Revenue Listener ───

        private void OnEnable() {
            if (AdManager.Instance != null) {
                AdManager.Instance.OnAdRevenuePaid += OnAdRevenuePaid;
            }
        }

        private void OnDisable() {
            if (AdManager.Instance != null) {
                AdManager.Instance.OnAdRevenuePaid -= OnAdRevenuePaid;
            }
        }

        /// <summary>
        /// Lắng nghe mọi ad revenue event.
        /// Revenue tự bridge sang Firebase + Adjust, nhưng bạn có thể
        /// dùng callback này cho UI hoặc game analytics riêng.
        /// </summary>
        private void OnAdRevenuePaid(AdRevenueData data) {
            SDKLogger.Info("Ads",
                $"Revenue: {data.Value:F6} {data.Currency} | " +
                $"Platform={data.AdPlatform}, Source={data.AdSource}, " +
                $"Format={data.AdFormat}, Placement={data.Placement}");
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  5. COMPLETE GAME INTEGRATION
    // ═════════════════════════════════════════════════════════════
    //
    //  Ví dụ tổng hợp: 1 script dùng cả Tracking + Ads
    //  trong game loop thực tế.
    //
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Script game hoàn chỉnh tích hợp Tracking + Ads.
    /// Đặt trong MainMenu hoặc GameplayManager.
    /// </summary>
    public class CompleteGameExample : MonoBehaviour {

        private float _levelStartTime;
        private int _currentLevel = 1;
        private double _totalGems;
        private double _totalGemsEarned;
        private double _totalGemsSpent;

        private void OnEnable() {
            SDKEventBus.Subscribe<SDKReadyEvent>(OnSDKReady);
        }

        private void OnDisable() {
            SDKEventBus.Unsubscribe<SDKReadyEvent>(OnSDKReady);
        }

        // ─── SDK Ready ───

        private void OnSDKReady(SDKReadyEvent e) {
            SDKLogger.Info("Game", $"SDK Ready! Success={e.Success}");

            // Setup user profile
            TrackingManager.Instance.UpdateUserProfile(p => {
                p.CurrentForgeShopLevel = _currentLevel;
            });

            // Show banner
            AdManager.Instance.ShowBanner("main_banner", BannerPosition.Bottom);
        }

        // ─── Game Flow: Level Start ───

        public void StartLevel(int levelNumber) {
            _currentLevel = levelNumber;
            _levelStartTime = Time.realtimeSinceStartup;

            // Track stage start
            TrackingManager.Instance.Track(
                new StageStartEvent("campaign", $"level_{levelNumber}"));

            // Track button click
            TrackingManager.Instance.Track(
                new ButtonClickEvent("main_menu", "play",
                    $"Started level {levelNumber}"));

            // Update profile
            TrackingManager.Instance.UpdateUserProfile(p => {
                p.CurrentForgeShopLevel = levelNumber;
                p.CurrentStage = $"level_{levelNumber}";
            });

            // Hide banner during gameplay
            AdManager.Instance.HideBanner("main_banner");
        }

        // ─── Game Flow: Level Complete ───

        public void OnLevelComplete(int gemsEarned) {
            int durationMs = (int)((Time.realtimeSinceStartup - _levelStartTime) * 1000);

            // Track stage end
            TrackingManager.Instance.Track(
                new StageEndEvent("campaign", $"level_{_currentLevel}", durationMs));

            // Track gems earned
            _totalGems += (double)gemsEarned;
            _totalGemsEarned += (double)gemsEarned;
            TrackEarnGems(gemsEarned);

            // Update profile
            TrackingManager.Instance.UpdateUserProfile(p => {
                p.ProgressStage = _currentLevel;
                p.CurrentGem = _totalGems;
            });

            // Show banner again
            AdManager.Instance.ShowBanner("main_banner", BannerPosition.Bottom);

            // Show interstitial between levels
            if (AdManager.Instance.IsInterstitialReady("level_complete")) {
                AdManager.Instance.ShowInterstitial("level_complete", result => {
                    SDKLogger.Info("Game",
                        result.Success
                            ? "Interstitial after level shown."
                            : $"Interstitial skipped: {result.Error}");
                });
            }
        }

        // ─── Game Flow: Double Reward ───

        /// <summary>
        /// User chọn xem rewarded ad để nhân đôi reward.
        /// </summary>
        public void OnDoubleRewardClicked(int baseReward) {
            TrackingManager.Instance.Track(
                new ButtonClickEvent("level_complete", "double_reward"));

            if (!AdManager.Instance.IsRewardedReady("double_coins")) {
                SDKLogger.Info("Game", "Rewarded ad not ready.");
                GrantReward(baseReward);
                return;
            }

            AdManager.Instance.ShowRewarded("double_coins", result => {
                if (result.Success && result.WasRewarded) {
                    GrantReward(baseReward * 2);

                    // Update IAA count
                    TrackingManager.Instance.UpdateUserProfile(p => {
                        p.IaaCount = p.IaaCount + 1;
                    });
                } else {
                    GrantReward(baseReward);
                }
            });
        }

        // ─── Game Flow: Tutorial ───

        public void OnTutorialStep(string stepName, int stepIndex) {
            TrackingManager.Instance.Track(
                new TutorialEvent("main_tutorial", stepName, stepIndex));
        }

        public void OnTutorialFinished() {
            TrackingManager.Instance.Track(new TutorialCompleteEvent());
        }

        // ─── Game Flow: Feature Tracking ───

        public void OnShopOpened() {
            TrackingManager.Instance.Track(new FeatureOpenEvent("shop"));
        }

        public void OnShopClosed(int durationMs) {
            TrackingManager.Instance.Track(
                new FeatureCloseEvent("shop", durationMs));
        }

        public void OnNewFeatureUnlocked(string featureId) {
            TrackingManager.Instance.Track(new FeatureUnlockEvent(featureId));
        }

        // ─── Game Flow: IAP ───

        public void OnPurchaseSuccess(string productId, int gemAmount, double revenueUsd) {
            // Track iap_revenue (v2)
            int revenueMicro = (int)(revenueUsd * 1_000_000);
            TrackingManager.Instance.Track(
                new IapRevenueEvent(productId, revenueMicro, "success", null, "click"));

            // Track resource bought
            _totalGems += (double)gemAmount;
            var data = new ResourceTrackingData(
                ResourceCategory.Currency, "gem",
                new TrackingSource(ResourceEventType.Buy, "iap", productId));
            double totalBought = TrackingManager.Instance.CurrentUserProfile
                .AddBought("gem", (double)gemAmount);
            TrackingManager.Instance.Track(
                new BuyResourceEvent(data, (double)gemAmount, _totalGems, totalBought));

            // Update profile
            TrackingManager.Instance.UpdateUserProfile(p => {
                p.IapCount = p.IapCount + 1;
                p.CurrentGem = _totalGems;
            });
        }

        public void OnPurchaseFailed(string productId, string errorMessage) {
            TrackingManager.Instance.Track(
                new IapRevenueEvent(productId, 0, "fail", errorMessage, "click"));
        }

        // ─── Game Flow: App Resume → App Open Ad ───

        private void OnApplicationPause(bool pauseStatus) {
            if (!pauseStatus && AdManager.Instance != null) {
                // App resumed from background → show app open ad
                AdManager.Instance.ShowAppOpen();
            }
        }

        // ─── Game Flow: Custom Events ───

        /// <summary>
        /// Track khi user chia sẻ achievement.
        /// </summary>
        public void OnShareClicked(string platform, string content) {
            TrackingManager.Instance.Track(new GenericGameTrackingEvent(
                "share",
                new Dictionary<string, object> {
                    { "platform", platform },
                    { "content_type", content },
                    { "level", _currentLevel }
                }));
        }

        /// <summary>
        /// Track khi user rate app.
        /// </summary>
        public void OnRateApp(int stars) {
            TrackingManager.Instance.Track(new GenericGameTrackingEvent(
                "rate_app",
                new Dictionary<string, object> {
                    { "stars", stars },
                    { "level", _currentLevel }
                }));
        }

        // ─── Helpers ───

        private void TrackEarnGems(int amount) {
            var data = new ResourceTrackingData(
                ResourceCategory.Currency, "gem",
                new TrackingSource(
                    ResourceEventType.Earn, "level_reward",
                    $"level_{_currentLevel}"));

            TrackingManager.Instance.Track(
                new EarnResourceEvent(
                    data, (double)amount, _totalGems, _totalGemsEarned));
        }

        private void GrantReward(int gems) {
            _totalGems += (double)gems;
            _totalGemsEarned += (double)gems;
            TrackEarnGems(gems);
            SDKLogger.Info("Game", $"Granted {gems} gems. Total: {_totalGems}");
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  6. CUSTOM REVENUE EVENT — Adjust attribution
    // ═════════════════════════════════════════════════════════════
    //
    //  Khi cần gửi revenue event riêng cho Adjust
    //  (không dùng IapRevenueEvent), tạo class kế thừa
    //  GameTrackingEvent và override AdjustToken + Revenue fields.
    //
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Ví dụ sử dụng purchase tracking.
    /// Gọi TrackIAPRevenue() — mỗi provider tự xử lý bên trong:
    /// - Firebase: logs "in_app_purchase" event
    /// - Adjust: verifies receipt + tracks revenue via VerifyAndTrack*Purchase
    /// </summary>
    public class RevenueTrackingExample : MonoBehaviour {

        /// <summary>
        /// Gọi sau khi purchase thành công — chỉ 1 function duy nhất.
        /// </summary>
        public void TrackPurchaseRevenue(
            string productId, double price, string currency,
            string transactionId, string receipt, string source) {

            TrackingManager.Instance.TrackIAPRevenue(
                productId, price, currency,
                transactionId, receipt, source);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  7. EVENTBUS — Custom Game Events
    // ═════════════════════════════════════════════════════════════
    //
    //  SDKEventBus dùng cho inter-module communication.
    //  Bạn có thể tạo custom events cho game logic.
    //  Event phải là readonly struct implement ISDKEvent.
    //
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Custom event: player died.
    /// </summary>
    public readonly struct PlayerDiedEvent : ISDKEvent {
        public string CauseOfDeath { get; }
        public int LevelNumber { get; }

        public PlayerDiedEvent(string cause, int level) {
            CauseOfDeath = cause;
            LevelNumber = level;
        }
    }

    /// <summary>
    /// Custom event: currency changed.
    /// </summary>
    public readonly struct CurrencyChangedEvent : ISDKEvent {
        public string CurrencyType { get; }
        public long OldAmount { get; }
        public long NewAmount { get; }

        public CurrencyChangedEvent(string type, long oldAmount, long newAmount) {
            CurrencyType = type;
            OldAmount = oldAmount;
            NewAmount = newAmount;
        }
    }

    /// <summary>
    /// Ví dụ publish/subscribe custom events.
    /// </summary>
    public class EventBusExample : MonoBehaviour {

        private void OnEnable() {
            SDKEventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            SDKEventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        private void OnDisable() {
            SDKEventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            SDKEventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        /// <summary>
        /// Gọi khi player chết — publish event cho các system khác xử lý.
        /// </summary>
        public void PublishPlayerDied(string cause, int level) {
            SDKEventBus.Publish(new PlayerDiedEvent(cause, level));
        }

        /// <summary>
        /// Gọi khi gems thay đổi — UI, tracking, analytics đều nhận.
        /// </summary>
        public void PublishCurrencyChanged(long oldAmount, long newAmount) {
            SDKEventBus.Publish(
                new CurrencyChangedEvent("gem", oldAmount, newAmount));
        }

        private void OnPlayerDied(PlayerDiedEvent e) {
            SDKLogger.Info("Game",
                $"Player died at level {e.LevelNumber}: {e.CauseOfDeath}");

            // Auto-track death event
            TrackingManager.Instance.Track(new GenericGameTrackingEvent(
                "player_death",
                new Dictionary<string, object> {
                    { "cause", e.CauseOfDeath },
                    { "level", e.LevelNumber }
                }));
        }

        private void OnCurrencyChanged(CurrencyChangedEvent e) {
            SDKLogger.Info("Game",
                $"Currency '{e.CurrencyType}': {e.OldAmount} → {e.NewAmount}");
        }
    }
}
