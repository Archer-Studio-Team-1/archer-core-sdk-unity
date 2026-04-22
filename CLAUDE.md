# CLAUDE.md — Archer Studio SDK for Unity

Hướng dẫn cho Claude (và dev) khi làm việc trong repo này. Đọc cùng với [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) và [`docs/modules/`](docs/modules/).

---

## 1. Repo là gì

Mono‑repo chứa **các UPM packages** phân phối qua git URL (xem `README.md`). Mỗi folder `com.archerstudio.sdk.*` là một package Unity độc lập có `package.json`, `Runtime/`, `Editor/`, `Tests/` riêng.

- Unity target: **6000.0+** (Unity 6).
- Ngôn ngữ: **C#** (.NET Standard 2.1).
- Phân phối: **UPM git URL** với `?path=…#vX.Y.Z`.
- Game tiêu thụ: repo `Archer-Studio-Team-1-IDK` (và các project game khác).

## 2. Cấu trúc package

```
com.archerstudio.sdk.<name>/
├── package.json              # UPM manifest (name, version, dependencies)
├── Runtime/
│   ├── ArcherStudio.SDK.<Name>.asmdef
│   ├── Core/ hoặc <Name>Manager.cs     # manager/module chính
│   ├── Interfaces/           # contracts (provider abstractions)
│   ├── Providers/            # concrete implementations (vendor-gated)
│   ├── Models/               # DTO/struct (immutable)
│   ├── Events/               # event structs publish qua SDKEventBus
│   ├── Config/               # ScriptableObject config
│   └── <Name>ModuleRegistrar.cs        # [RuntimeInitializeOnLoadMethod] auto-register
├── Editor/
│   └── ArcherStudio.SDK.<Name>.Editor.asmdef
├── Tests/Runtime/
│   └── ArcherStudio.SDK.<Name>.Tests.asmdef
└── Samples~/                 # optional
```

**Quy tắc bất dịch**: mỗi package KHÔNG import trực tiếp code vendor ở public API. Vendor SDK (Firebase, Adjust, AppLovin, GPGS, …) luôn được gate bằng **scripting define symbol** và gọi thông qua `IXxxProvider`.

## 3. Scripting define symbols

Core tự phát hiện SDK đã cài qua `SDKSymbolDetector` và set symbol tương ứng. Provider code được `#if HAS_*` bao bọc.

| Symbol | Gate provider |
|---|---|
| `HAS_FIREBASE_SDK`, `HAS_FIREBASE_MESSAGING`, `HAS_FIREBASE_DYNAMIC_LINKS`, `HAS_FIREBASE_REMOTE_CONFIG` | Firebase bundle |
| `HAS_ADJUST_SDK` | Adjust attribution / deep link |
| `HAS_APPLOVIN_MAX_SDK` | AppLovin MAX ads + CMP |
| `HAS_IRONSOURCE_SDK` | IronSource/LevelPlay |
| `HAS_ADMOB_SDK` | Google Mobile Ads |
| `HAS_GOOGLE_UMP` | Google User Messaging Platform |
| `HAS_UNITY_IAP` | Unity IAP v5 |
| `HAS_FACEBOOK_SDK` | Facebook SDK |
| `HAS_GPGS_V2` | Google Play Games Services v2 |

Khi thêm provider mới → thêm symbol, update `SDKSymbolDetector`, bọc code bằng `#if`.

## 4. Vòng đời SDK (điều tối thiểu phải nhớ)

```
Game scene khởi động
  └─ SDKBootstrap (MonoBehaviour)
       ├─ load SDKBootstrapConfig + SDKCoreConfig từ Resources/
       ├─ SDKModuleFactory → gom module qua:
       │     - scene (MonoBehaviour đã attach)
       │     - registrar (static creator được ModuleRegistrar đăng ký trước scene load)
       ├─ ConsentManager.RequestConsent → broadcast ConsentChangedEvent
       ├─ DependencyGraph (Kahn topo sort) → batch init order
       ├─ SDKInitializer.InitializeAsync mỗi module (callback về main thread)
       └─ Publish BootstrapCompleteEvent + SDKReadyEvent
```

**Module không bao giờ tự lấy dependency**. Dependency khai báo qua `ISDKModule.Dependencies` (List<string> module id) để DependencyGraph sắp xếp.

## 5. Dependency graph giữa các package

```
core ──┬── consent ──┬── tracking ──┬── ads
       │             │              └── iap
       │             └── login
       ├── deeplink
       ├── push
       ├── remoteconfig
       └── testlab
```

Version pinning ở `package.json` dùng semver; bump tag theo pattern `vX.Y.Z`. Đừng downgrade `com.archerstudio.sdk.core` dependency của module khi bump version.

## 6. Quy ước code

- **Immutability**: models/events là `readonly struct` hoặc immutable class. Publish event không gây GC.
- **Main thread**: callback vendor native phải đi qua `UnityMainThreadDispatcher.Enqueue(...)`.
- **Logging**: không dùng `Debug.Log` trực tiếp. Dùng `SDKLogger.Info/Warning/Error` với tag module.
- **Null‑safety**: khi vendor SDK chưa cài, provider trả về `Stub*Provider` thay vì throw.
- **Config**: tất cả config là `ScriptableObject` nằm trong `Resources/<Name>Config.asset`. Tạo qua menu `Assets > Create > ArcherStudio > SDK > …`.
- **Event bus**: đừng expose public `event Action` ở manager trừ khi cần. Ưu tiên `SDKEventBus.Publish(new XxxEvent(...))`.
- **Dependencies khai báo ở `package.json`** theo semver. Assembly reference trong `.asmdef` phải match.

## 7. Thêm module mới — checklist

1. Tạo folder `com.archerstudio.sdk.<name>/` với layout chuẩn (§2).
2. Viết `package.json` + khai báo deps tới `com.archerstudio.sdk.core`.
3. Định nghĩa `I<Name>Provider` + ít nhất một `Stub<Name>Provider`.
4. Viết `<Name>Manager` implement `ISDKModule`:
   - `ModuleId`, `Dependencies`, `InitializationPriority`.
   - `InitializeAsync(SDKCoreConfig, Action<bool>)` gọi `onComplete(true)` khi sẵn sàng.
   - `OnConsentChanged(ConsentStatus)` nếu module consent‑aware (implement `IConsentAware`).
5. Viết `<Name>ModuleRegistrar` với `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` gọi `SDKModuleFactory.RegisterCreator(...)`.
6. Tạo `<Name>Config : ModuleConfigBase`, menu `CreateAssetMenu`.
7. Update `SDKCoreConfig.Enable<Name>` flag + ô symbol trong `SDKSymbolDetector`.
8. Viết Tests cho contract (factory, dependency, consent propagation).
9. Cập nhật `README.md` bảng packages và update `docs/modules/<name>.md`.

## 8. Branching & versioning

- `main` = nhánh phát hành, tag theo package: `<package>/vX.Y.Z` hoặc tag chung `vX.Y.Z` (đang dùng chung).
- Mỗi thay đổi module → bump `version` trong `package.json` của package đó (SemVer). Consumer repo chọn tag chính xác.
- Commit style: `feat(<scope>): …`, `fix(<scope>): …`, scope là tên module (`login`, `iap`, `ads`…).

## 9. Testing

- Unit tests chạy qua Unity Test Runner (NUnit). Chỉ test Runtime assembly.
- Core tests (đầy đủ): `DependencyGraph`, `ModuleRegistry`, `SDKEventBus`, `SDKModuleFactory`, `ConsentStatus`. Dùng làm reference khi viết test cho module mới.
- Không mock `UnityEngine.Debug`. Dùng `SDKLogger` để inject test buffer.

## 10. Những file Claude phải đọc trước khi edit

| Khi bạn sửa… | Đọc trước… |
|---|---|
| Lifecycle / init order | `com.archerstudio.sdk.core/Runtime/Bootstrap/SDKBootstrap.cs`, `SDKInitializer.cs`, `DependencyGraph.cs` |
| Event bus | `Core/Runtime/Events/SDKEventBus.cs`, `SDKEvents.cs` |
| Consent propagation | `Core/Runtime/Interfaces/IConsentAware.cs`, `Consent/Runtime/ConsentManager.cs` |
| Thêm symbol | `Core/Editor/SDKSymbolDetector.cs` |
| Config wizard | `Core/Editor/SDKSetupWizard.cs` |
| Module mới | `docs/ARCHITECTURE.md` và module gần giống nhất trong `docs/modules/` |

## 11. Không được làm

- Không gọi `new TrackingManager()` thủ công. Module tự register.
- Không thêm dependency vendor SDK thẳng vào `package.json` (để consumer tự chọn phiên bản). Chỉ dùng symbol.
- Không block main thread trong `InitializeAsync`. Async qua callback hoặc coroutine.
- Không hardcode secret (ad unit id, app token) trong code. Luôn qua ConfigSO.
- Không đổi `ModuleId` sau khi package đã release — nó là contract.

## 12. Tài liệu liên quan

- [`README.md`](README.md) — cài đặt & bảng packages.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — kiến trúc tổng thể.
- [`docs/modules/`](docs/modules/) — chi tiết từng module.
- [`TODOS.md`](TODOS.md) — scope defer.
