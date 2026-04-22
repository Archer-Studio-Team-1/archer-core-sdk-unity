# Module: com.archerstudio.sdk.remoteconfig

Firebase Remote Config wrapper + local defaults + feature flag service.

**Version**: `1.0.0` · **Deps**: `core` · **Priority**: 40.

---

## 1. Public API

```csharp
RemoteConfigManager.Instance.FetchAndActivate(onComplete);       // bool
RemoteConfigManager.Instance.GetString(key, defaultValue);
RemoteConfigManager.Instance.GetBool(key, defaultValue);
RemoteConfigManager.Instance.GetInt(key, defaultValue);
RemoteConfigManager.Instance.GetFloat(key, defaultValue);
RemoteConfigManager.Instance.GetLong(key, defaultValue);

FeatureFlagService.IsEnabled("new_shop");
FeatureFlagService.GetVariant("onboarding_abtest");
```

## 2. Provider pattern

```csharp
public interface IRemoteConfigProvider {
    void Initialize(RemoteConfigConfig config, Action<bool> onComplete);
    void FetchAndActivate(Action<bool> onComplete);
    string GetString(string key, string def);
    bool   GetBool(string key, bool def);
    long   GetLong(string key, long def);
    double GetDouble(string key, double def);
}
```

| Provider | Symbol |
|---|---|
| `FirebaseRemoteConfigProvider` | `HAS_FIREBASE_REMOTE_CONFIG` |
| `StubRemoteConfigProvider` | (fallback — luôn trả default) |

## 3. Module class

`RemoteConfigManager : ISDKModule`
- `ModuleId = "remoteconfig"`, priority 40.
- Init: load `RemoteConfigConfig` + local defaults (TextAsset/JSON) → instantiate provider → auto-fetch nếu bật.
- Gate accessor: trước khi ready → luôn return default.

## 4. Events

- Không formal event system. Callback-based `FetchAndActivate(onComplete)`.
- Feature flag consumer tự gọi API (pull model).

## 5. Config: `RemoteConfigConfig`

| Field | Ý nghĩa |
|---|---|
| `MinimumFetchIntervalSeconds` | Firebase throttle (debug có thể 0). |
| `AutoFetchOnInit` | fetch ngay lúc init. |
| `EnableLocalDefaults` | load TextAsset `remote_config_defaults.json` từ Resources. |

## 6. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/Core/RemoteConfigManager.cs` | Lifecycle, gate, fetch dispatch. |
| `Runtime/Interfaces/IRemoteConfigProvider.cs` | Contract. |
| `Runtime/Providers/FirebaseRemoteConfigProvider.cs` | Firebase wrapper. |
| `Runtime/Providers/StubRemoteConfigProvider.cs` | Fallback. |
| `Runtime/FeatureFlags/FeatureFlagService.cs` | Typed helper trên RemoteConfig. |
| `Runtime/Config/RemoteConfigConfig.cs` | SO config. |

## 7. Platform hooks

- Firebase Remote Config tự handle iOS/Android differences.
- Local defaults fallback khi fetch fail (thiết bị offline hoặc throttled).

## 8. Chú ý khi sửa

- **Cache TTL**: `MinimumFetchIntervalSeconds` nên > 3600 ở production (tránh bị Firebase throttle).
- **Flag naming**: chuẩn snake_case để match Firebase console.
- **A/B test**: Firebase Remote Config Conditions quyết định variant — client chỉ đọc key.
- **Fetch vs activate**: Firebase tách 2 bước; manager wrap thành `FetchAndActivate` một call.
