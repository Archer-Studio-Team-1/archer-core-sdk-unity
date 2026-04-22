# Module: com.archerstudio.sdk.login

Authentication. GPGS v2+ silent sign-in trên Android, guest fallback ở platform khác. Expose `PlayerId` cho cloud save sau này.

**Version**: `1.1.0` · **Deps**: `core 1.1.0`, `consent 1.0.0` · **Priority**: 40.

---

## 1. Public API

```csharp
LoginModule.Instance.ReAuthenticate(onComplete);        // silent, gọi lúc boot
LoginModule.Instance.ManualAuthenticate(onComplete);    // UI prompt ("Kết nối tài khoản")
LoginModule.Instance.SignOut(onComplete);               // logout (app-side)
LoginModule.Instance.CurrentPlayer;                     // PlayerInfo? (PlayerId, DisplayName)
LoginModule.Instance.IsAuthenticated;
```

## 2. Provider pattern

```csharp
public interface ILoginProvider {
    bool IsAvailable { get; }
    void AuthenticateAsync(bool silent, Action<LoginResult> onComplete);
    void SignOut(Action onComplete);
}
```

| Provider | Symbol | Platform |
|---|---|---|
| `GPGSLoginProvider` | `HAS_GPGS_V2` + Android | Google Play Games Services v2 API. |
| `StubLoginProvider` | luôn có | iOS, Editor, platform không có GPGS. |

## 3. Module class

`LoginModule : ISDKModule`
- `ModuleId = "login"`, priority 40, deps `["consent"]`.
- Init: chọn provider theo platform + symbol → `AuthenticateAsync(silent: true)`.
- Success: cache `PlayerInfo`, publish `LoginSucceededEvent`.
- Failure: publish `LoginFailedEvent(LoginErrorCode)`.

## 4. Events (SDKEventBus)

| Event | Payload |
|---|---|
| `LoginSucceededEvent` | `PlayerId`, `DisplayName`, `IsGuest`. |
| `LoginFailedEvent` | `LoginErrorCode` (`NotInstalled`, `NotSignedIn`, `ConfigError`, `Cancelled`, `Unknown`). |
| `LoggedOutEvent` | — |

## 5. Config: `LoginConfig` (ScriptableObject)

| Field | Ý nghĩa |
|---|---|
| `AndroidClientId` | OAuth client ID (Web) từ Google Play Console. |
| `AutoSignInOnStart` | gọi `ReAuthenticate` trong `InitializeAsync`. |
| `RequestIdToken`, `RequestServerAuthCode` | tùy backend integration. |

Menu: `Assets > Create > ArcherStudio > SDK > Login Config`. Cũng có menu `ArcherStudio > SDK > Login Config` bật asset nếu có.

## 6. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/Core/LoginModule.cs` | Lifecycle, provider dispatch, event publish. |
| `Runtime/Interfaces/ILoginProvider.cs` | Contract. |
| `Runtime/Providers/GPGSLoginProvider.cs` | GPGS v2+ API (`PlayGamesPlatform.Instance.Authenticate`). |
| `Runtime/Providers/StubLoginProvider.cs` | Returns guest `LoginResult`. |
| `Runtime/Models/LoginModels.cs` | `PlayerInfo`, `LoginResult`, `LoginErrorCode`. |
| `Runtime/Events/LoginEvents.cs` | Event structs. |
| `Runtime/Config/LoginConfig.cs` | SO config. |
| `Runtime/LoginModuleRegistrar.cs` | Auto-register. |

## 7. Platform integration

### Android (GPGS v2+)
- Dựa trên `play-games-plugin-for-unity` (v2+). Cần `GooglePlayGamesManifest.androidlib` nằm trong `Plugins/Android/`.
- `PlayGamesPlatform.Instance.Authenticate(callback)` — trả `SignInStatus.Success` / `InternalError` / `Canceled` / `NotAuthenticated`.
- **Không có native sign-out**: `SignOut` chỉ clear SDK state; player vẫn đăng nhập ở Google Play Games app.
- Symbol: `HAS_GPGS_V2`.

### iOS / Editor / Desktop
- `StubLoginProvider` returns guest player (`IsGuest=true`, `PlayerId="guest_<deviceId>"`).

## 8. Editor tooling

| File | Mục đích |
|---|---|
| `Editor/LoginConfigEditor.cs` | Inspector: validate Android Client ID, help box hướng dẫn lấy id. |
| `Editor/ArcherStudio.SDK.Login.Editor.asmdef` | Editor assembly. |

## 9. Autologin flow (game side)

```
SDKBootstrap.WaitUntilInitialized
   → LoginModule đã init (silent sign-in)
       ├─ Success: LoginSucceededEvent fire → game chuyển tới home
       └─ Fail:    LoginFailedEvent  → game show nút "Kết nối tài khoản"
                                              → ManualAuthenticate() → retry
```

Tham khảo `IDK/Assets/_Game/Scripts/Services/Login/LoginService.cs` (game layer subscriber).

## 10. Tests (`Tests/Runtime/`)

- `LoginModuleTests.cs` — contract test với stub provider: dependency resolve, init ok, consent propagation, manual auth flow.

## 11. Chú ý khi sửa

- **GPGS v2 API khác v1**: `AuthScope` + `PlayGamesClientConfiguration` đã bỏ. Đọc `UPGRADING.txt` của plugin trước khi đổi init code.
- **Không bỏ guest fallback** — game cần PlayerId luôn có.
- **Tracking adoption rate**: deferred (xem `TODOS.md`) — khi làm cần subscribe `LoginSucceededEvent`/`LoginFailedEvent` và gửi qua `TrackingManager`.
- **LoginErrorCode → user message mapping**: không nằm trong SDK, thuộc game UI layer (Phase 2).
