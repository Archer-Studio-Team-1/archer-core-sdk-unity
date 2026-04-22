# Module: com.archerstudio.sdk.tracking

Đa‑provider analytics (Firebase Analytics + Adjust attribution). Tracking game progression, purchase, ad revenue, user properties.

**Version**: `1.0.0` · **Deps**: `core`, `consent`, `com.unity.nuget.newtonsoft-json` · **Priority**: 20.

---

## 1. Public API

```csharp
TrackingManager.Instance.TrackEvent(new FeatureUsedEvent("shop_open"));
TrackingManager.Instance.TrackAdRevenue(platform, source, format, unitName, currency, value, placement);
TrackingManager.Instance.TrackIAPRevenue(productId, revenue, currency, txnId, receipt, source);
TrackingManager.Instance.SetUserId("player_123");
TrackingManager.Instance.SetUserProperty("vip_tier", "gold");
TrackingManager.Instance.CurrentUserProfile;  // UserProfile
```

MonoBehaviour singleton, lazy-init qua `TrackingModuleRegistrar`.

## 2. Provider pattern

```csharp
public interface ITrackingProvider {
    void Initialize(TrackingConfig config, Action<bool> onComplete);
    void TrackEvent(GameTrackingEvent evt);
    void TrackAdRevenue(AdRevenueData data);
    void TrackIAPRevenue(IAPRevenueData data);
    void SetUserId(string id);
    void SetUserProperty(string key, string value);
    void SetConsent(ConsentStatus status);
}
```

| Provider | Symbol | Ghi chú |
|---|---|---|
| `FirebaseTrackingProvider` | `HAS_FIREBASE_SDK` | `ad_impression`, `in_app_purchase`, custom events. |
| `AdjustTrackingProvider` | `HAS_ADJUST_SDK` | Token-based events, DMA mapping, revenue verify. |

Init song song qua countdown latch; tất cả provider xong mới fire `onComplete(true)`.

## 3. Module class

`TrackingManager : ISDKModule`
- `ModuleId = "tracking"`, priority 20, deps `["consent", "firebase"]`.
- Load `TrackingConfig` từ Resources.
- Instantiate enabled providers, init song song.
- Subscribe `ConsentChangedEvent` → propagate `SetConsent(status)` đến provider.
- Listen `UserProfile.OnPropertyChanged` → sync qua `SetUserProperty`.
- Reusable `SharedParams` dictionary tránh GC khi fire event hot path.

## 4. Events

- Consumer: `ConsentChangedEvent`.
- Produced: không publish lên EventBus (tracking là sink). Manager raise `UserProfile.OnPropertyChanged` nội bộ.
- Event hierarchy (base `GameTrackingEvent`):
  - `FeatureEvents`, `PurchaseEvents`, `StageEvents`, `LoadingEvents`, `ExplorationEvents`, `TaskEvents`, `TutorialEvent`, `UIEvents`, `AdEvents`, `AdImpressionEvent`, `VaultEvent`, `ResourceEvents`, `ForgeEvents`.

## 5. Config: `TrackingConfig`

**Adjust**:
- `AdjustAppToken`, `UseSandboxInDebug`, `AdjustLogLevel`
- Token map: `AdjustTokenPurchase`
- `EnableCoppaCompliance`, `EnableSendInBackground`
- `ExternalDeviceId`, `DefaultTracker`
- `EnableLinkMe`, `StoreName`, `StoreAppId`, `MetaAppId`, `EnableOaid`

**Global params**:
- `GlobalCallbackParams`, `GlobalPartnerParams` — `List<StringPair>`.

**Providers**:
- `EnabledProviders` — List (Firebase, Adjust).

**Debug**:
- `VerboseLogging`.

Nằm trong `Resources/TrackingConfig.asset`.

## 6. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/TrackingManager.cs` | Lifecycle, dispatcher, consent listen, profile sync. |
| `Runtime/ITrackingProvider.cs` | Contract. |
| `Runtime/FirebaseTrackingProvider.cs` | Firebase Analytics integration. |
| `Runtime/AdjustTrackingProvider.cs` | Adjust SDK (tokens, DMA, revenue). |
| `Runtime/GameTrackingEvent.cs` | Base class (EventName, AdjustToken, DeduplicationId, BuildParams). |
| `Runtime/UserProfile.cs` | Persisted model + JSON serialization + `OnPropertyChanged`. |
| `Runtime/TrackingConfig.cs` | SO config. |
| `Runtime/TrackingConstants.cs` | Key strings (EventName, ParamName, UserProperty). |
| `Runtime/Events/*.cs` | 16 file event classes. |
| `Runtime/AdjustOaidPlugin.cs` | OAID reader (HMS). |
| `Runtime/TrackingModuleRegistrar.cs` | Auto-register. |

## 7. Platform hooks

- **Android**: Adjust gradle plugin auto-inject permission; OAID optional (HMS ads-identifier).
- **iOS**: Revenue receipt verify qua Adjust.
- **Consent Mode v2**: Firebase respect `AD_STORAGE`, `ANALYTICS_STORAGE` signals mà consent module đã set.
- **Adjust DMA**: map `ConsentStatus` → `google_dma` params (`eea`, `ad_personalization`, `ad_user_data`, `ad_storage`, `npa`) + Facebook `data_processing` (CCPA).

## 8. Editor tooling

| File | Mục đích |
|---|---|
| `Editor/TrackingEventGeneratorWindow.cs` | Generate event class từ schema. |
| `Editor/AdjustDependencyManagerWindow.cs` | Quản lý plugin Adjust (OAID, Meta referrer). |

## 9. Tích hợp với các module khác

- **Consent → Tracking**: pre-read `ConsentManager.CurrentStatus` lúc init, subscribe `ConsentChangedEvent`.
- **Ads → Tracking**: `AdManager.OnAdRevenuePaid` bridge → `TrackingManager.TrackAdRevenue`.
- **IAP → Tracking**: `IAPManager` gọi `TrackIAPRevenue` + publish `IapRevenueEvent`.

## 10. Chú ý khi sửa

- **Đừng bỏ consent check** — provider phải gated `SetConsent` trước khi track sensitive data.
- **Deduplication**: `GameTrackingEvent.DeduplicationId` → Firebase/Adjust tránh double-count.
- **Custom event**: implement subclass `GameTrackingEvent`, override `BuildParams`.
- **Adjust token**: event cần token riêng trên dashboard Adjust — không hardcode, lấy từ config.
