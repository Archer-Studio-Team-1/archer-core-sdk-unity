# Module: com.archerstudio.sdk.ads

Ad mediation thống nhất (AppLovin MAX / IronSource / AdMob). Banner, Interstitial, Rewarded, App Open với frequency cap và revenue tracking.

**Version**: `1.0.0` · **Deps**: `core`, `consent`, `tracking` · **Priority**: 50.

---

## 1. Public API

```csharp
AdManager.Instance.ShowBanner(adsId, BannerPosition.Bottom);
AdManager.Instance.IsInterstitialReady(adsId);
AdManager.Instance.ShowInterstitial(adsId, placementId, result => { /* AdResult */ });
AdManager.Instance.IsRewardedReady(adsId);
AdManager.Instance.ShowRewarded(adsId, placementId, onRewarded, onFailed, onClosed, onComplete);
AdManager.Instance.ShowAppOpen(placementId, onComplete);  // cold-start gate
AdManager.Instance.LoadAd(adsId);                          // manual preload

AdManager.OnAdRevenuePaid += data => { /* AdRevenueData */ };
```

## 2. Provider pattern

```csharp
public interface IAdProvider {
    void Initialize(AdConfig config, Action<bool> onComplete);
    void OnConsentChanged(ConsentStatus status);
    void LoadInterstitial(string unitId);
    bool IsInterstitialReady(string unitId);
    void ShowInterstitial(string unitId, Action<AdResult> onComplete);
    void LoadRewarded(string unitId);
    bool IsRewardedReady(string unitId);
    void ShowRewarded(...);
    void ShowBanner(string unitId, BannerPosition position);
    void HideBanner();
    void ShowAppOpen(...);
    event Action<AdRevenueData> OnAdRevenuePaid;
}
```

| Provider | Symbol |
|---|---|
| `AppLovinMaxProvider` | `HAS_APPLOVIN_MAX_SDK` |
| `IronSourceProvider` | `HAS_IRONSOURCE_SDK` |
| `AdMobProvider` | `HAS_ADMOB_SDK` |

Chọn qua `AdConfig.MediationPlatform` enum → `AdManager.CreateProvider()` switch.

## 3. Module class

`AdManager : ISDKModule`
- `ModuleId = "ads"`, priority 50, deps `["consent", "tracking"]`.
- Pre-read `ConsentManager.CurrentStatus` trước khi gọi `provider.Initialize`.
- Bridge `provider.OnAdRevenuePaid` → `AdRevenueTracker` → `TrackingManager`.
- Quản lý:
  - `AdPlacement` lookup (placementId → unitId theo platform)
  - `FrequencyCapper` (cooldown + session cap)
  - `AdRevenueTracker` (dispatcher revenue)

## 4. Events emitted

- Local: `AdManager.OnAdRevenuePaid(AdRevenueData)`.
- Bridge vào tracking: `TrackingManager.TrackAdRevenue(...)` — Firebase `ad_impression` + Adjust `AdjustAdRevenue` + custom event `ad_revenue` (BigQuery export).

## 5. Config: `AdConfig`

| Field | Ý nghĩa |
|---|---|
| `MediationPlatform` | enum: AppLovinMax / IronSource / AdMob. |
| `SdkKey` | (MAX) key từ dashboard. |
| `Placements` | `List<AdPlacement>` — mỗi placement gồm PlacementId, Format, AndroidUnitId, IosUnitId, AutoLoad. |
| `InterstitialCooldownSeconds` | thời gian cách tối thiểu 2 lần interstitial. |
| `MaxInterstitialsPerSession` | cap session. |
| `MaxRewardedPerSession` | cap session. |
| `EnableAppOpenAd`, `AppOpenColdStartDelay` | app open gate. |
| `ShowMediationDebugger` | MAX mediation debugger. |

Menu: `Assets > Create > ArcherStudio > SDK > Ad Config`.

## 6. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/Core/AdManager.cs` | Lifecycle, placement resolve, cap dispatch. |
| `Runtime/Core/AdRevenueTracker.cs` | Bridge revenue → TrackingManager. |
| `Runtime/Core/FrequencyCapper.cs` | Cooldown + session limit theo format. |
| `Runtime/Interfaces/IAdProvider.cs` | Contract 11 phương thức. |
| `Runtime/Providers/AppLovinMaxProvider.cs` | MAX SDK v5+ (ưu tiên SDK consent flow). |
| `Runtime/Providers/IronSourceProvider.cs` | LevelPlay. |
| `Runtime/Providers/AdMobProvider.cs` | Google Mobile Ads. |
| `Runtime/Models/AdModels.cs` | AdFormat, AdRevenueData, RewardData, AdResult, AdPlacement. |
| `Runtime/Config/AdConfig.cs` | SO config. |
| `Runtime/AdsModuleRegistrar.cs` | Auto-register. |

## 7. Revenue flow

```
Provider native callback (onAdRevenuePaid)
       │
       ▼ (main thread via dispatcher)
IAdProvider.OnAdRevenuePaid (event)
       │
       ▼
AdRevenueTracker.OnRevenuePaid(AdRevenueData)
       │
       ├──► TrackingManager.TrackAdRevenue(...)
       │        ├── Firebase: ad_impression event
       │        └── Adjust:   AdjustAdRevenue
       └──► TrackingManager.TrackAdRevenueCustomEvent(ad_revenue)
                  └── BigQuery export
```

## 8. Editor tooling

- Config SO creation menu.
- (Không có Editor script riêng — Editor/ trống.)

## 9. Chú ý khi sửa

- **Frequency cap** áp ở manager, không ở provider — tránh drift giữa networks.
- **Placement vs unit**: game code dùng `placementId` (stable), provider dùng `unitId` (platform-specific).
- **Consent**: provider phải respect `ConsentStatus.AdPersonalization` (non-personalized ads khi false).
- **App Open delay** tránh show ngay lúc cold start (gây flicker splash).
