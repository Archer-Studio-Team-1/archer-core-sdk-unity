# Module: com.archerstudio.sdk.deeplink

Aggregate deep link từ nhiều nguồn (Unity built-in, Firebase Dynamic Links, Adjust deferred). Expose API thống nhất cho game.

**Version**: `1.0.0` · **Deps**: `core` · **Priority**: 70.

---

## 1. Public API

```csharp
DeepLinkManager.OnDeepLinkReceived += data => { /* DeepLinkData */ };
DeepLinkManager.LastDeepLink;   // cache cho subscriber late-bind
```

## 2. Provider pattern

```csharp
public interface IDeepLinkProvider {
    bool IsInitialized { get; }
    void Initialize(DeepLinkConfig config, Action<bool> onComplete);
}
```

| Provider | Symbol | Nguồn link |
|---|---|---|
| `UnityDeepLinkProvider` | (luôn) | `Application.absoluteURL` + `Application.deepLinkActivated`. |
| `FirebaseDeepLinkProvider` | `HAS_FIREBASE_DYNAMIC_LINKS` | Firebase Dynamic Links (iOS Universal Links, Android App Links). |
| `AdjustDeepLinkProvider` | `HAS_ADJUST_SDK` | Adjust deep link + **deferred** (từ attribution). |

Tất cả provider init song song; event emit được aggregate về `DeepLinkManager.OnDeepLinkReceived`.

## 3. Module class

`DeepLinkManager : ISDKModule`
- `ModuleId = "deeplink"`, priority 70.
- Init: instantiate provider theo symbol → `Initialize` song song.
- Subscribe mỗi provider event → aggregate → raise `OnDeepLinkReceived`.
- Cache `LastDeepLink` cho subscriber bind sau thời điểm link đến.

## 4. Events

- `DeepLinkManager.OnDeepLinkReceived(DeepLinkData)` — local event.
- `DeepLinkData` struct: `Url`, `Source` (Unity/Firebase/Adjust), `Parameters` (Dictionary), `Timestamp`.

## 5. Config: `DeepLinkConfig`

| Field | Ý nghĩa |
|---|---|
| `UriScheme` | ví dụ `mygame://` |
| `DynamicLinksDomain` | Firebase domain. |
| `EnableDeferredDeepLinks` | bật Adjust deferred. |

Nằm trong `Resources/DeepLinkConfig.asset`.

## 6. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/Core/DeepLinkManager.cs` | Lifecycle, aggregator, cache last link. |
| `Runtime/Interfaces/IDeepLinkProvider.cs` | Contract. |
| `Runtime/Providers/UnityDeepLinkProvider.cs` | `Application.deepLinkActivated`. |
| `Runtime/Providers/FirebaseDeepLinkProvider.cs` | Firebase Dynamic Links. |
| `Runtime/Providers/AdjustDeepLinkProvider.cs` | Adjust deep link + deferred callback. |
| `Runtime/Models/DeepLinkModels.cs` | `DeepLinkData`. |
| `Runtime/Utils/DeepLinkParser.cs` | Parse URL query → Dictionary. |
| `Runtime/DeepLinkModuleRegistrar.cs` | Auto-register. |

## 7. Platform hooks

- **iOS**: Universal Links via Firebase; Adjust deep link via `ADJAttribution`.
- **Android**: App Links via intent-filter (AndroidManifest); Adjust installed referrer.
- **Deferred link**: Adjust expose sau attribution (first session) — Firebase không support.

## 8. Chú ý khi sửa

- **Late subscriber**: game UI subscribe sau khi link đến → dùng `LastDeepLink` (không null) để process.
- **URI scheme** phải match AndroidManifest + Info.plist. Không tự động inject (manual setup).
- **Dedup**: cùng link có thể đến từ 2 provider (Unity + Firebase). Manager dedup theo `Url + Timestamp` trong 1s window.
