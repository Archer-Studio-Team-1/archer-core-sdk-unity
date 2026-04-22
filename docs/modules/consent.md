# Module: com.archerstudio.sdk.consent

Quản lý consent GDPR / CCPA / iOS ATT. Publish `ConsentChangedEvent` để downstream tuân thủ.

**Version**: `1.0.1` · **Deps**: `core` · **Priority**: 0 (init đầu tiên).

---

## 1. Public API

```csharp
ConsentManager.CurrentStatus;                         // ConsentStatus
ConsentManager.SetConsent(bool granted, bool isEea);  // manual override
ConsentManager.ResetConsent();                         // clear cache + re-prompt
ConsentManager.ShowCmpForExistingUser(onComplete);    // MAX CMP reshow
ConsentManager.ApplyPendingFacebookConsent();         // gọi sau FB.Init
```

Singleton tự tạo qua `ConsentModuleRegistrar` (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`).

## 2. Provider pattern

```csharp
public interface IConsentProvider {
    ConsentStatus GetCurrentStatus();
    bool IsConsentRequired { get; }
    void RequestConsent(Action<ConsentStatus> onComplete);
    void ResetConsent();
}
```

| Provider | Symbol gate | Khi dùng |
|---|---|---|
| `GoogleUmpProvider` | `HAS_GOOGLE_UMP` | GDPR EEA, TCF v2.3. |
| `MaxConsentProvider` | `HAS_APPLOVIN_MAX_SDK` | Khi mediation là MAX (CMP gộp GDPR + ATT). |
| `ManualConsentProvider` | (luôn có) | Game có custom UI consent hoặc test. |

Chọn qua `ConsentConfig.ProviderType`.

## 3. Module class

`ConsentManager : ISDKModule`
- `ModuleId = "consent"`, priority 0.
- Init: tạo provider → `RequestConsent` → cache PlayerPrefs → publish `ConsentChangedEvent`.
- Listen callback native: marshal về main thread qua `UnityMainThreadDispatcher`.
- Apply Consent Mode v2 cho Firebase (qua FirebaseModule), FB LDU, Adjust DMA, MAX.

## 4. Events

- Publish: `ConsentChangedEvent(ConsentStatus)` qua `SDKEventBus`.
- Không expose local delegate — 100% event‑driven.

## 5. Config: `ConsentConfig` (ScriptableObject)

| Field | Ý nghĩa |
|---|---|
| `ProviderType` | `GoogleUMP` / `Manual` / `AppLovinMax`. |
| `MaxSdkKey` | cho MAX provider. |
| `RequestATT` | iOS: có prompt ATT hay không. |
| `AttDelay` | delay giây trước ATT (tránh bắn trùng UMP). |
| `ForceShowInEditor` | editor test. |
| `TestGeography` | giả lập EEA/Non‑EEA cho UMP. |

Menu: `Assets > Create > ArcherStudio > SDK > Consent Config`, nằm trong `Resources/ConsentConfig.asset`.

## 6. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/ConsentManager.cs` | Lifecycle, cache, TCF dump, consent re‑broadcast. |
| `Runtime/IConsentProvider.cs` | Contract. |
| `Runtime/GoogleUmpProvider.cs` | UMP SDK wrapper (`#if HAS_GOOGLE_UMP`). |
| `Runtime/MaxConsentProvider.cs` | MAX CMP. |
| `Runtime/ManualConsentProvider.cs` | Stub/test. |
| `Runtime/ConsentHelper.cs` | Parse IAB TCF v2.3 (Purpose/Vendor/LI queries). |
| `Runtime/ConsentConfig.cs` | SO config. |
| `Runtime/ConsentModuleRegistrar.cs` | Auto-register. |

## 7. Platform hooks

### iOS (`ConsentBuildProcessor`)
- Inject Info.plist:
  - `GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_STORAGE=false`
  - `GOOGLE_ANALYTICS_DEFAULT_ALLOW_ANALYTICS_STORAGE=false`
  - `GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA=false`
  - `GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS=false`
  - `NSUserTrackingUsageDescription` (từ config).
- ATT prompt gọi sau UMP (nếu bật `RequestATT`), qua `RequestATTWithDelay`.

### Android
- UMP gradle plugin tự inject permission.
- Consent Mode v2 mapping: `AdStorage` → `ad_storage`, etc.

## 8. Editor tooling

- Inspector ConfigSO.
- `ConsentBuildProcessor` (`IPostprocessBuildWithReport`) — chạy khi build iOS.

## 9. Chú ý khi sửa

- **Không gọi vendor SDK trực tiếp** — chỉ qua provider.
- **Main thread guard** cho UMP callback (Android native).
- **Test với `ForceShowInEditor=true` + `TestGeography=EEA`** để reproduce EU case.
- Khi thêm provider mới: extend enum + update `ConsentManager.CreateProvider` switch.
