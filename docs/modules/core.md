# Module: com.archerstudio.sdk.core

Package nền. Cung cấp khung lifecycle, dependency graph, event bus, config và utilities. Không chứa business logic vendor cụ thể (ngoại trừ `FirebaseModule`/`FacebookModule` làm cross‑cutting foundation).

**Version hiện tại**: `1.1.0` · **Dependencies**: — · **Unity**: 6000.0+

---

## 1. Public API

### Entry point

| Symbol | Loại | Mục đích |
|---|---|---|
| `SDKBootstrap` | MonoBehaviour | Attach vào GameObject ở scene boot. Tự chạy toàn bộ pipeline init. |
| `SDKBootstrap.WaitUntilInitialized` | static UniTask | Game code await sẵn sàng. |
| `SDKBootstrap.State` | enum | `Idle` / `InitializingServices` / `AwaitingConsent` / `InitializingModules` / `Ready` / `Failed`. |
| `SDKInitializer.GetModule<T>()` | static | Lấy module theo type. |
| `SDKInitializer.GetModule(id)` | static | Lấy module theo id. |
| `SDKInitializer.OnSDKReady` | event | Fire khi tất cả module ready. |

### Event bus

```csharp
SDKEventBus.Subscribe<ConsentChangedEvent>(OnConsent);
SDKEventBus.Publish(new ConsentChangedEvent(status));
SDKEventBus.Unsubscribe<ConsentChangedEvent>(OnConsent);
SDKEventBus.Clear<ConsentChangedEvent>(); // test only
```

### Logging

```csharp
SDKLogger.SetMinLevel(LogLevel.Info);
SDKLogger.Info("[Ads]", "Interstitial loaded");
SDKLogger.Error("[Ads]", exception);
SDKLogger.OnLogReceived += entry => testPanel.Append(entry);
```

## 2. Lifecycle & orchestration

- **`SDKModuleFactory`** (static): module tự register qua `RegisterCreator(id, () => new Manager())` trong `<Module>ModuleRegistrar` gắn `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`.
- **`DependencyGraph`**: Kahn topo sort. Circular → hard error. Missing dep → warning, bỏ qua.
- **`ModuleRegistry`**: `Dictionary<string, ISDKModule>`, duplicate id bị reject.
- **`SDKInitializer`**: batch execution. Mỗi batch chạy song song, batch kế tiếp chờ tất cả callback. Timeout dùng `SDKBootstrapConfig.MaxInitTimeout`.

## 3. Contract: `ISDKModule`

```csharp
public interface ISDKModule : IDisposable {
    string ModuleId { get; }
    int InitializationPriority { get; }
    IReadOnlyList<string> Dependencies { get; }
    ModuleState State { get; }
    void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete);
    void OnConsentChanged(ConsentStatus consent);
}
```

Chuẩn priority trong codebase:

| Priority | Module |
|---:|---|
| 0 | Consent |
| 10 | Firebase (core cross‑cut) |
| 20 | Tracking |
| 30 | Facebook (core cross‑cut) |
| 40 | Login, RemoteConfig |
| 50 | Ads, IAP |
| 60 | Push |
| 70 | DeepLink |

## 4. Config

### `SDKCoreConfig` (ScriptableObject)
- `AppId`, `DebugMode`, `MinLogLevel`
- Feature toggles: `EnableConsent`, `EnableTracking`, `EnableAnalytics`, `EnableAds`, `EnableIAP`, `EnableRemoteConfig`, `EnablePush`, `EnableDeepLink`, `EnableTestLab`
- Tạo qua menu `Assets > Create > ArcherStudio > SDK > Core Config`, đặt trong `Resources/SDKCoreConfig.asset`.

### `SDKBootstrapConfig` (ScriptableObject)
- `AutoDiscoverModules` (bool)
- `ContinueOnModuleFailure` (bool)
- `MaxInitTimeout` (float, default 15s)
- Nằm trong `Resources/SDKBootstrapConfig.asset`.

### `ModuleConfigBase`
Abstract base cho tất cả `<Name>Config`, có `Enabled` flag.

## 5. Events (defined tại `SDKEvents.cs`)

| Event | Payload | Publisher |
|---|---|---|
| `ConsentChangedEvent` | `ConsentStatus` | ConsentManager |
| `SDKReadyEvent` | `bool Success, int FailedModuleCount` | SDKInitializer |
| `ModuleInitializedEvent` | `string ModuleId, bool Success` | SDKInitializer |
| `BootstrapCompleteEvent` | `float ElapsedSeconds, int TotalModules, int FailedModules` | SDKBootstrap |

## 6. Utilities

| Symbol | Mục đích |
|---|---|
| `UnityMainThreadDispatcher.Enqueue(Action)` | Marshal callback native về main thread. |
| `UnityMainThreadDispatcher.EnqueueDelayed(seconds, Action)` | Delay + main thread. |
| `UnityMainThreadDispatcher.IsMainThread()` | Check thread hiện tại. |
| `SingletonMono<T>` / `SingletonMonoDontDestroy<T>` | Base class cho MonoBehaviour singleton. |
| `FirebaseInitializer.CheckAndFixDependencies()` | Guard gọi `Firebase.DependencyStatus` một lần. |
| `SDKInitCoordinator` | Barrier helper cho multi‑module init. |
| `SDKDebugDumper`, `SDKDebugDraw` | Runtime debug overlays. |

## 7. Cross‑cutting modules ở Core

### FirebaseModule
- `ModuleId = "firebase"`, priority 10, deps `["consent"]`.
- Gọi `CheckAndFixDependencies()` đúng một lần → downstream có thể assume Firebase ready.
- Map `ConsentStatus` sang Consent Mode v2 (`AD_STORAGE`, `ANALYTICS_STORAGE`, `AD_USER_DATA`, `AD_PERSONALIZATION`).

### FacebookModule
- `ModuleId = "facebook"`, priority 30, deps `["consent"]`.
- `#if HAS_FACEBOOK_SDK`. Gọi `FB.Init()`, apply LDU + AdvertiserIDCollection theo CCPA.
- Pending consent được queue và apply sau khi `FB.Init` xong.

## 8. Editor tooling (`Editor/*.cs`)

| File | Mục đích |
|---|---|
| `SDKSetupWizard.cs` | EditorWindow 4 tab: Quick Setup / Configs / Symbols / Validate. Menu `ArcherStudio > SDK > Setup Wizard`. |
| `SDKSymbolDetector.cs` | Reflection tìm vendor SDK, toggle `HAS_*` symbol theo build profile. |
| `SDKSymbolManagerWindow.cs` | UI quản lý symbol thủ công. |
| `SDKSourceSwitcher.cs` | Toggle source vs binary SDK. |
| `SDKVersionManager.cs` | Tra phiên bản tất cả package. |
| `SDKLibraryImporter.cs` | Import UPM helper. |
| `AndroidManifestVerifier.cs` | Validate AndroidManifest (permission, activity). |
| `AndroidManifestSanitizer.cs` | Strip dup entries khi merge. |

## 9. Samples (`Samples~/`)

- **TestPanel** — runtime debug overlay (`F12` desktop / 3‑finger tap mobile). Show module state, log buffer, consent, trigger test calls.
- **UsageExamples** — snippet khởi tạo, consent, tracking, ads, IAP, EventBus custom event.

## 10. Tests (`Tests/Runtime/`)

| Test class | Coverage |
|---|---|
| `DependencyGraphTests` | Linear / parallel / circular detection. |
| `ModuleRegistryTests` | Register, GetModule, duplicate. |
| `SDKEventBusTests` | Publish/Subscribe/Unsubscribe, exception safety. |
| `SDKModuleFactoryTests` | Creator registration, CreateAll ordering. |
| `ConsentStatusTests` | Struct immutability, Default, FromLegacy. |

## 11. Key insights

- **Zero‑cost event bus**: static `List<Action<T>>` per type → không lookup dictionary.
- **Soft dependency**: module thiếu dep chỉ warn (cho phép game không cài đủ package).
- **Thread safety**: mọi vendor callback vào SDK phải đi qua `UnityMainThreadDispatcher`.
- **Consent struct immutable**: không có path mutate sau khi tạo → safe để publish qua EventBus.
