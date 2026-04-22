# Kiến trúc tổng thể — Archer Studio SDK

Tài liệu này mô tả cách 10 package trong repo hoạt động cùng nhau. Chi tiết từng module xem [`modules/`](modules/).

---

## 1. Nguyên tắc thiết kế

| Nguyên tắc | Lý do |
|---|---|
| **Modular UPM** | Mỗi chức năng là một package độc lập → game chọn lắp module nào cần, tránh phình APK. |
| **Provider pattern** | Module chỉ biết `IXxxProvider`. Vendor SDK (Firebase/Adjust/AppLovin/GPGS…) plug vào qua `#if HAS_*`. |
| **Dependency graph explicit** | `ISDKModule.Dependencies` khai báo module id → core sắp topo sort → init không race. |
| **Consent‑first** | Consent là module priority 0. Mọi module ads/analytics phải được init SAU khi consent resolved, và phải react `ConsentChangedEvent`. |
| **Zero‑GC event bus** | `SDKEventBus` dùng static generic + readonly struct → không allocate khi publish hot path. |
| **Main‑thread dispatcher** | Callback native (Android JNI, iOS Objective‑C) luôn marshal về main thread trước khi vào game code. |
| **Stub fallback** | Mỗi provider có `Stub*Provider` khi vendor SDK không có → SDK không bao giờ fail bootstrap chỉ vì thiếu integration. |

## 2. Dependency graph giữa các package

```
                    ┌────────────────────────────┐
                    │  com.archerstudio.sdk.core │
                    │  (foundation)              │
                    └──────────────┬─────────────┘
                                   │
       ┌────────────┬──────────────┼──────────────┬────────────┐
       │            │              │              │            │
       ▼            ▼              ▼              ▼            ▼
   consent     deeplink         push        remoteconfig   testlab
       │
       ├──► tracking ──┬──► ads
       │               └──► iap
       │
       └──► login
```

Gốc là **core**. Module chỉ depend xuôi — không có cycle. `consent` là module downstream đầu tiên vì hầu như mọi thứ còn lại cần consent trước khi gọi vendor SDK.

## 3. Cấu trúc Core package

Core là package nền. Nó KHÔNG biết business logic của ads/iap/login — chỉ cung cấp khung.

```
com.archerstudio.sdk.core/Runtime/
├── Bootstrap/
│   ├── SDKBootstrap.cs          # MonoBehaviour entry, đặt ở scene boot
│   ├── SDKBootstrapConfig.cs    # ScriptableObject (AutoDiscoverModules, timeout…)
│   └── SDKModuleFactory.cs      # Static: giữ Dictionary<string, ModuleCreator>
├── Lifecycle/
│   ├── SDKInitializer.cs        # Batch init runner, raises OnSDKReady
│   ├── DependencyGraph.cs       # Kahn topo sort → List<List<ISDKModule>>
│   ├── ModuleRegistry.cs        # Dictionary<string, ISDKModule>
│   ├── FirebaseModule.cs        # Cross-cutting: Firebase init + Consent Mode v2
│   └── FacebookModule.cs        # Cross-cutting: FB.Init + consent apply
├── Interfaces/
│   ├── ISDKModule.cs            # Contract mọi module phải implement
│   ├── IConsentAware.cs         # Optional: module muốn nhận ConsentChanged
│   └── ConsentStatus.cs         # readonly struct: AdStorage, AnalyticsStorage, AdUserData, AdPersonalization
├── Events/
│   ├── SDKEventBus.cs           # Publish/Subscribe<T> (static generic)
│   └── SDKEvents.cs             # ConsentChangedEvent, SDKReadyEvent, BootstrapCompleteEvent, ModuleInitializedEvent
├── Config/
│   ├── SDKCoreConfig.cs         # ScriptableObject: AppId, feature toggles, log level
│   └── ModuleConfigBase.cs      # abstract: Enabled flag
├── Logging/
│   └── SDKLogger.cs             # Level-gated logger với ring buffer 200 entries
└── Utils/
    ├── UnityMainThreadDispatcher.cs
    ├── SingletonMono.cs / SingletonMonoDontDestroy.cs
    └── SDKInitCoordinator.cs, FirebaseInitializer.cs, SDKDebugDumper.cs
```

### 3.1. ISDKModule contract

```csharp
public interface ISDKModule : IDisposable {
    string ModuleId { get; }                      // ví dụ "consent", "ads"
    int InitializationPriority { get; }           // lower = earlier; Consent=0, Tracking=20, Ads=50
    IReadOnlyList<string> Dependencies { get; }   // module ids phải ready trước
    ModuleState State { get; }                    // NotInitialized/Initializing/Ready/Failed/Disposed
    void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete);
    void OnConsentChanged(ConsentStatus consent);
}
```

Module init có thể gọi `onComplete(false)` khi fail, SDK sẽ tiếp tục nếu `SDKBootstrapConfig.ContinueOnModuleFailure = true`.

### 3.2. ConsentStatus

`readonly struct` với 4 field ánh xạ Google Consent Mode v2:

- `AdStorage`
- `AnalyticsStorage`
- `AdUserData`
- `AdPersonalization`

Kèm flags: `IsEeaUser`, `IsCcpaUser`, `TcfString`, `AdditionalConsentString`. Factory methods `Default()`, `FromLegacy()` để bảo đảm không có path tạo inconsistent state.

### 3.3. SDKEventBus

```csharp
SDKEventBus.Subscribe<ConsentChangedEvent>(OnConsent);
SDKEventBus.Publish(new ConsentChangedEvent(status));
SDKEventBus.Unsubscribe<ConsentChangedEvent>(OnConsent);
```

Mỗi event type có static `List<Action<T>>` riêng → không phải lookup theo type. Subscribe đi kèm lifetime quản lý ở callsite (Unsubscribe trong `Dispose`).

## 4. Vòng đời khởi tạo chi tiết

```
[BeforeSceneLoad]
  └─ <Module>ModuleRegistrar.Register()
        → SDKModuleFactory.RegisterCreator("ads", () => new AdManager())
        (làm cho mọi module, static, chạy trước scene)

[Scene boot load]
  └─ SDKBootstrap.Start()
        ├─ LoadConfig()
        │     ├─ Resources.Load<SDKBootstrapConfig>("SDKBootstrapConfig")
        │     └─ Resources.Load<SDKCoreConfig>("SDKCoreConfig")
        ├─ State = InitializingServices
        ├─ FirebaseInitializer.CheckAndFixDependencies() (async)
        ├─ State = AwaitingConsent
        ├─ ConsentManager.RequestConsent()
        │     └─ provider.RequestConsent() → Publish(ConsentChangedEvent)
        ├─ State = InitializingModules
        ├─ SDKModuleFactory.CreateAll() → List<ISDKModule>
        ├─ ModuleRegistry.Register(all)
        ├─ DependencyGraph.TopologicalSort() → batches
        ├─ SDKInitializer.InitializeAsync(batches)
        │     └─ foreach batch: foreach module:
        │          module.InitializeAsync(coreConfig, onComplete)
        │          Publish(ModuleInitializedEvent)
        ├─ State = Ready
        └─ Publish(SDKReadyEvent) + Publish(BootstrapCompleteEvent)
```

**Mỗi batch** chạy song song (các module trong batch không phụ thuộc nhau). Batch tiếp theo chờ tất cả callback của batch trước. Timeout chung: `SDKBootstrapConfig.MaxInitTimeout` (default 15s).

## 5. Flow consent → downstream

```
ConsentManager.RequestConsent()
      │
      ▼
provider.RequestConsent()   (UMP / MAX CMP / Manual)
      │
      ▼
ConsentStatus được cache (PlayerPrefs) + TCF string dump
      │
      ▼
SDKEventBus.Publish(ConsentChangedEvent)
      │
      ├──► FirebaseModule.OnConsentChanged()   → Firebase Consent Mode v2 setConsent()
      ├──► FacebookModule.OnConsentChanged()   → FB.Mobile.SetAdvertiserIDCollectionEnabled / LDU
      ├──► TrackingManager.OnConsentChanged()  → Adjust DMA + Firebase Analytics consent
      ├──► AdManager.OnConsentChanged()        → provider.OnConsentChanged (MAX / IronSource / AdMob)
      └──► IAPManager, Login, Push, ...         (chỉ nếu implement IConsentAware)
```

**Rule**: module ads/analytics phải chờ `ConsentChangedEvent` đầu tiên trước khi gọi vendor init. Nếu dev cần consent update runtime (ví dụ player đổi setting), gọi `ConsentManager.SetConsent(...)` → cùng event sẽ được re‑publish.

## 6. Provider pattern điển hình

```
┌─────────────────┐         ┌─────────────────────┐
│  <Name>Manager  │─────────▶│  I<Name>Provider    │
│  (ISDKModule)   │          └──────────┬──────────┘
└─────────────────┘                     │
     ▲ register                ┌────────┼─────────┐
     │                          ▼        ▼         ▼
┌─────────────┐          FirebaseXxx  AdjustXxx  StubXxx
│ Registrar   │           #if HAS_   #if HAS_    (fallback)
│ (static)    │           FIREBASE   ADJUST_SDK
└─────────────┘
```

- `<Name>Manager` chứa business logic chung (frequency cap, profile sync, event routing).
- Provider chỉ chịu trách nhiệm gọi vendor API.
- Switch provider = đổi symbol + config, không đổi code manager.

## 7. Cross‑cutting modules ở Core

Core chứa 2 module đặc biệt:

### FirebaseModule (priority 10, deps: `["consent"]`)
- Chạy `Firebase.CheckAndFixDependencies()` một lần duy nhất.
- Map `ConsentStatus` sang Google Consent Mode v2 native calls.
- Cho phép các downstream module (tracking/remoteconfig/push) assume Firebase đã ready.

### FacebookModule (priority 30, deps: `["consent"]`)
- `FB.Init()`, apply LDU/AdvertiserIDCollection theo CCPA.
- Hook `OnFacebookInitialized` để ứng dụng pending consent.

Hai module này nằm ở Core vì cả hai đều là "foundation for multiple downstream features" (Consent Mode v2 dùng cho ads + analytics; Facebook cần cho tracking + attribution).

## 8. Versioning & phân phối

- Mỗi package có `version` riêng trong `package.json`.
- Tag release: `vX.Y.Z` (chung cho mọi package của cùng release).
- Consumer pin phiên bản qua `#vX.Y.Z` trong URL UPM.
- **Breaking change**: bump MAJOR của package đó, cập nhật `dependencies` ở các package phụ thuộc.

## 9. Editor tooling tổng quan

| Công cụ | Package | Mục đích |
|---|---|---|
| `SDKSetupWizard` | core | One‑click tạo ConfigSO + kiểm tra symbol + validate scene. |
| `SDKSymbolDetector` | core | Reflection tìm vendor SDK → set/clear `HAS_*` symbol. |
| `SDKLibraryImporter` | core | Import UPM helper. |
| `SDKSourceSwitcher` | core | Toggle source vs binary distribution. |
| `AndroidManifestVerifier` | core | Validate AndroidManifest cho module. |
| `ConsentBuildProcessor` | consent | Inject Info.plist (ATT, Consent Mode defaults). |
| `TestLabBuildProcessor` | testlab | Inject intent-filter Game Loop + Info.plist URL scheme. |
| `TrackingEventGeneratorWindow` | tracking | Generate event class từ schema. |
| `AdjustDependencyManagerWindow` | tracking | Quản lý plugin Adjust (OAID, Meta referrer). |
| `LoginConfigEditor` | login | Inspector validate Android Client ID. |

## 10. Đọc thêm

- [`modules/core.md`](modules/core.md)
- [`modules/consent.md`](modules/consent.md)
- [`modules/tracking.md`](modules/tracking.md)
- [`modules/ads.md`](modules/ads.md)
- [`modules/iap.md`](modules/iap.md)
- [`modules/login.md`](modules/login.md)
- [`modules/deeplink.md`](modules/deeplink.md)
- [`modules/push.md`](modules/push.md)
- [`modules/remoteconfig.md`](modules/remoteconfig.md)
- [`modules/testlab.md`](modules/testlab.md)
