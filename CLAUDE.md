# CLAUDE.md — Archer Studio SDK for Unity

Hướng dẫn cho Claude (và dev) khi làm việc trong repo này. Đọc cùng với [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) và [`docs/modules/`](docs/modules/).

---

## 0. Workspace paths

| Repo | Path | Mô tả |
|------|------|-------|
| **IDK** (game project) | `/Volumes/WORKSPACE/Team01/IDK` | Unity game project tiêu thụ SDK |
| **SDK** (mono-repo) | `/Volumes/WORKSPACE/Team01/archer-core-sdk-unity` | Archer Studio SDK packages |
| **Functions** (backend) | `/Volumes/WORKSPACE/Team01/firebase-functions` | Firebase Cloud Functions (IAP validation, subscription) |

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
| `HAS_GPGS` | Google Play Games Services |
| `HAS_FIREBASE_APP_CHECK` | Firebase App Check |
| `HAS_SDK_APPCHECK` | SDK appcheck package installed (auto via asmdef versionDefines) |

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
       │             │              └── iap ←── appcheck (soft dep, #if HAS_SDK_APPCHECK)
       │             └── login
       ├── appcheck
       ├── deeplink
       ├── push
       ├── remoteconfig
       ├── badgesystem
       └── testlab
```

Version pinning ở `package.json` dùng semver; bump tag theo pattern `<name>/vX.Y.Z`. Đừng downgrade `com.archerstudio.sdk.core` dependency của module khi bump version.

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

- `main` = nhánh phát hành.
- **Tag per-package**: `<name>/v<X.Y.Z>` (e.g. `core/v1.2.0`, `iap/v1.1.0`, `appcheck/v1.0.0`).
- Consumer manifest trỏ tag cụ thể: `...git?path=com.archerstudio.sdk.iap#iap/v1.1.0`.
- Mỗi thay đổi module → bump `version` trong `package.json` của package đó (SemVer).
- Khi bump version → tạo tag → push tag: `git tag <name>/v<X.Y.Z> && git push origin <name>/v<X.Y.Z>`.
- Commit style: `feat(<scope>): …`, `fix(<scope>): …`, scope là tên module (`login`, `iap`, `ads`…).
- Không tạo tag cho package chưa thay đổi. Chỉ tag package có diff so với tag trước.

## 9. Security config per-environment

`SDKCoreConfig` chứa `SDKSecurityConfig` riêng cho 3 môi trường: **Editor**, **Dev**, **Production**.

| Setting | Editor | Dev | Production |
|---------|--------|-----|------------|
| `EnableAppCheck` | `false` | `false` | `true` |
| `EnableIAPServerValidation` | `false` | `false` | `true` |

Runtime chọn config qua `SDKCoreConfig.GetActiveSecurityConfig()`:
- `UNITY_EDITOR` → Editor config
- `PRODUCTION` symbol → Production config
- Còn lại → Dev config

**App Check** (`com.archerstudio.sdk.appcheck`):
- Production: Play Integrity (Android) / DeviceCheck (iOS) — real attestation.
- Dev + `AppCheckConfig.UseDebugProviderInDev=true`: Firebase Debug Provider.
- Dev + `UseDebugProviderInDev=false` hoặc Editor: Stub (null token, IAP vẫn hoạt động).

**IAP Server Validation** (`ServerReceiptValidator`):
- Khi `EnableIAPServerValidation=false` → không tạo validator, không gọi API, purchase grant ngay.
- Khi `true` → blocking validation: store confirm → server verify → cấp reward. Fail-close policy.
- Loading overlay hiện trong lúc chờ server (config: `ShowLoadingOverlay`, `LoadingOverlayTimeout`).

**Quy tắc**: Khi tắt security cho môi trường nào → **không tốn tài nguyên** (không gọi network, không tạo object). IAP mua hàng hoạt động bình thường ở mọi môi trường.

## 10. Testing

- Unit tests chạy qua Unity Test Runner (NUnit). Chỉ test Runtime assembly.
- Core tests (đầy đủ): `DependencyGraph`, `ModuleRegistry`, `SDKEventBus`, `SDKModuleFactory`, `ConsentStatus`. Dùng làm reference khi viết test cho module mới.
- Không mock `UnityEngine.Debug`. Dùng `SDKLogger` để inject test buffer.

## 11. Những file Claude phải đọc trước khi edit

| Khi bạn sửa… | Đọc trước… |
|---|---|
| Lifecycle / init order | `com.archerstudio.sdk.core/Runtime/Bootstrap/SDKBootstrap.cs`, `SDKInitializer.cs`, `DependencyGraph.cs` |
| Event bus | `Core/Runtime/Events/SDKEventBus.cs`, `SDKEvents.cs` |
| Consent propagation | `Core/Runtime/Interfaces/IConsentAware.cs`, `Consent/Runtime/ConsentManager.cs` |
| Thêm symbol | `Core/Editor/SDKSymbolDetector.cs` |
| Config wizard | `Core/Editor/SDKSetupWizard.cs` |
| Security / App Check | `Core/Runtime/Config/SDKCoreConfig.cs` (SDKSecurityConfig), `AppCheck/Runtime/AppCheckManager.cs` |
| IAP server validation | `IAP/Runtime/Core/IAPManager.cs`, `IAP/Runtime/Providers/ServerReceiptValidator.cs` |
| Loading overlay | `Core/Runtime/UI/SDKLoadingOverlay.cs`, `Core/Editor/SDKLoadingOverlayPrefabCreator.cs` |
| Module mới | `docs/ARCHITECTURE.md` và module gần giống nhất trong `docs/modules/` |

## 12. Không được làm

- Không gọi `new TrackingManager()` thủ công. Module tự register.
- Không thêm dependency vendor SDK thẳng vào `package.json` (để consumer tự chọn phiên bản). Chỉ dùng symbol.
- Không block main thread trong `InitializeAsync`. Async qua callback hoặc coroutine.
- Không hardcode secret (ad unit id, app token) trong code. Luôn qua ConfigSO.
- Không đổi `ModuleId` sau khi package đã release — nó là contract.

## 13. Tài liệu liên quan

- [`README.md`](README.md) — cài đặt & bảng packages.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — kiến trúc tổng thể.
- [`docs/modules/`](docs/modules/) — chi tiết từng module.
- [`TODOS.md`](TODOS.md) — scope defer.

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **archer-core-sdk-unity** (4475 symbols, 4491 relationships, 0 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## When Debugging

1. `gitnexus_query({query: "<error or symptom>"})` — find execution flows related to the issue
2. `gitnexus_context({name: "<suspect function>"})` — see all callers, callees, and process participation
3. `READ gitnexus://repo/archer-core-sdk-unity/process/{processName}` — trace the full execution flow step by step
4. For regressions: `gitnexus_detect_changes({scope: "compare", base_ref: "main"})` — see what your branch changed

## When Refactoring

- **Renaming**: MUST use `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` first. Review the preview — graph edits are safe, text_search edits need manual review. Then run with `dry_run: false`.
- **Extracting/Splitting**: MUST run `gitnexus_context({name: "target"})` to see all incoming/outgoing refs, then `gitnexus_impact({target: "target", direction: "upstream"})` to find all external callers before moving code.
- After any refactor: run `gitnexus_detect_changes({scope: "all"})` to verify only expected files changed.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Tools Quick Reference

| Tool | When to use | Command |
|------|-------------|---------|
| `query` | Find code by concept | `gitnexus_query({query: "auth validation"})` |
| `context` | 360-degree view of one symbol | `gitnexus_context({name: "validateUser"})` |
| `impact` | Blast radius before editing | `gitnexus_impact({target: "X", direction: "upstream"})` |
| `detect_changes` | Pre-commit scope check | `gitnexus_detect_changes({scope: "staged"})` |
| `rename` | Safe multi-file rename | `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` |
| `cypher` | Custom graph queries | `gitnexus_cypher({query: "MATCH ..."})` |

## Impact Risk Levels

| Depth | Meaning | Action |
|-------|---------|--------|
| d=1 | WILL BREAK — direct callers/importers | MUST update these |
| d=2 | LIKELY AFFECTED — indirect deps | Should test |
| d=3 | MAY NEED TESTING — transitive | Test if critical path |

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/archer-core-sdk-unity/context` | Codebase overview, check index freshness |
| `gitnexus://repo/archer-core-sdk-unity/clusters` | All functional areas |
| `gitnexus://repo/archer-core-sdk-unity/processes` | All execution flows |
| `gitnexus://repo/archer-core-sdk-unity/process/{name}` | Step-by-step execution trace |

## Self-Check Before Finishing

Before completing any code modification task, verify:
1. `gitnexus_impact` was run for all modified symbols
2. No HIGH/CRITICAL risk warnings were ignored
3. `gitnexus_detect_changes()` confirms changes match expected scope
4. All d=1 (WILL BREAK) dependents were updated

## Keeping the Index Fresh

After committing code changes, the GitNexus index becomes stale. Re-run analyze to update it:

```bash
npx gitnexus analyze
```

If the index previously included embeddings, preserve them by adding `--embeddings`:

```bash
npx gitnexus analyze --embeddings
```

To check whether embeddings exist, inspect `.gitnexus/meta.json` — the `stats.embeddings` field shows the count (0 means no embeddings). **Running analyze without `--embeddings` will delete any previously generated embeddings.**

> Claude Code users: A PostToolUse hook handles this automatically after `git commit` and `git merge`.

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
