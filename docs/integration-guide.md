# SDK Integration Guide

Hướng dẫn đầy đủ để cài đặt và sử dụng Archer Studio SDK trong một dự án Unity game. Đọc cùng với [`ARCHITECTURE.md`](ARCHITECTURE.md) và các tài liệu module trong [`modules/`](modules/).

---

## Mục lục

1. [Yêu cầu](#1-yêu-cầu)
2. [Cài đặt packages (UPM)](#2-cài-đặt-packages-upm)
3. [Tạo config assets](#3-tạo-config-assets)
4. [Setup scene boot](#4-setup-scene-boot)
5. [Chờ SDK sẵn sàng trong game code](#5-chờ-sdk-sẵn-sàng-trong-game-code)
6. [Sử dụng từng module](#6-sử-dụng-từng-module)
7. [CloudSave — hướng dẫn chi tiết](#7-cloudsave--hướng-dẫn-chi-tiết)
8. [Testing trên Editor và Device](#8-testing-trên-editor-và-device)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Yêu cầu

| Yêu cầu | Phiên bản tối thiểu |
|---|---|
| Unity | 6000.0+ (Unity 6) |
| C# | .NET Standard 2.1 |
| Firebase Unity SDK | 13.x (nếu dùng Firestore, Auth, Analytics, FCM) |
| Google Play Games Services (GPGS) | v2+ (nếu dùng Login trên Android) |

---

## 2. Cài đặt packages (UPM)

Thêm vào `Packages/manifest.json` của project:

```json
{
  "dependencies": {
    "com.archerstudio.sdk.core":         "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.core#main",
    "com.archerstudio.sdk.consent":      "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.consent#main",
    "com.archerstudio.sdk.login":        "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.login#main",
    "com.archerstudio.sdk.tracking":     "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.tracking#main",
    "com.archerstudio.sdk.ads":          "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.ads#main",
    "com.archerstudio.sdk.iap":          "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.iap#main",
    "com.archerstudio.sdk.remoteconfig": "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.remoteconfig#main",
    "com.archerstudio.sdk.push":         "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.push#main",
    "com.archerstudio.sdk.deeplink":     "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.deeplink#main",
    "com.archerstudio.sdk.cloudsave":    "https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.cloudsave#main"
  }
}
```

**Chú ý:**
- `#main` luôn lấy commit mới nhất. Để pin version ổn định, thay bằng tag cụ thể: `#com.archerstudio.sdk.cloudsave/v1.0.0`.
- `com.archerstudio.sdk.core` là bắt buộc; các module khác tùy theo feature cần dùng.
- Sau khi lưu `manifest.json`, Unity Package Manager tự resolve và download.

### Cài qua SSH (nếu dùng SSH key cho GitHub)

```
git+git@github.com:Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.core#main
```

---

## 3. Tạo config assets

SDK đọc config từ `ScriptableObject` nằm trong folder `Resources/`. Có 2 cách tạo:

### Cách 1 — Setup Wizard (khuyến nghị)

Menu **ArcherStudio > SDK > Setup Wizard** → tab **Quick Setup** → bật module cần dùng → nhấn **Run Setup**.

Wizard sẽ tự tạo tất cả `.asset` files cần thiết trong `Assets/Resources/`.

### Cách 2 — Tạo thủ công

Tạo từng file riêng lẻ qua menu **Assets > Create > ArcherStudio > SDK > …**:

| Asset name | Menu path | Bắt buộc? |
|---|---|---|
| `SDKCoreConfig` | SDK > Core Config | ✅ |
| `SDKBootstrapConfig` | SDK > Bootstrap Config | ✅ |
| `ConsentConfig` | SDK > Consent Config | Nếu dùng Consent |
| `LoginConfig` | SDK > Login Config | Nếu dùng Login |
| `TrackingConfig` | SDK > Tracking Config | Nếu dùng Tracking |
| `AdConfig` | SDK > Ad Config | Nếu dùng Ads |
| `IAPConfig` | SDK > IAP Config | Nếu dùng IAP |
| `RemoteConfigConfig` | SDK > Remote Config | Nếu dùng RemoteConfig |
| `PushConfig` | SDK > Push Config | Nếu dùng Push |
| `CloudSaveConfig` | SDK > Cloud Save Config | Nếu dùng CloudSave |

### Cấu hình SDKCoreConfig

Mở `Assets/Resources/SDKCoreConfig.asset` trong Inspector và bật các module cần dùng:

| Field | Mặc định | Ý nghĩa |
|---|---|---|
| `AppId` | — | Bundle ID của app (ví dụ `com.archer.idle.dungeon.keeper`) |
| `DebugMode` | false | Bật log chi tiết |
| `MinLogLevel` | Info | Log level tối thiểu ghi ra Console |
| `EnableConsent` | true | Bật GDPR/ATT consent |
| `EnableLogin` | false | Bật Login qua Google Play Games |
| `EnableTracking` | true | Bật Firebase Analytics + Adjust |
| `EnableAds` | true | Bật ad mediation |
| `EnableIAP` | true | Bật In-App Purchase |
| `EnableCloudSave` | false | Bật Cloud Save (Firestore) |
| `EnablePush` | false | Bật Firebase Cloud Messaging |
| `EnableDeepLink` | false | Bật deep linking |
| `EnableTestLab` | false | Bật Firebase Test Lab |

---

## 4. Setup scene boot

### 4.1. Thêm SDKBootstrap vào scene

Tạo một GameObject trong scene boot (scene đầu tiên load) và attach component **SDKBootstrap**:

```
SplashScene (Scene)
└── [GameObject] "SDK"
    └── SDKBootstrap (Component)
```

Hoặc dùng menu **ArcherStudio > SDK > Create Bootstrap GameObject**.

### 4.2. SDKBootstrapConfig

Gán `SDKBootstrapConfig.asset` vào field `Bootstrap Config` trên component. Các tùy chọn quan trọng:

| Field | Mặc định | Ý nghĩa |
|---|---|---|
| `AutoDiscoverModules` | true | Tự tìm module từ Registrar (không cần thủ công register) |
| `InitTimeoutSeconds` | 30 | Timeout tổng cho toàn bộ init sequence |
| `ContinueOnModuleFailure` | true | Tiếp tục nếu một module init fail (không crash app) |

### 4.3. Quy trình bootstrap

Khi scene load, `SDKBootstrap` chạy theo thứ tự:

```
1. Load SDKBootstrapConfig + SDKCoreConfig từ Resources/
2. Gom tất cả ISDKModule đã đăng ký (ModuleRegistrar + scene)
3. ConsentManager.RequestConsent()  →  publish ConsentChangedEvent
4. DependencyGraph topo sort  →  xếp batch init theo priority + dependency
5. SDKInitializer chạy từng batch (song song trong batch, tuần tự giữa batch)
6. Publish BootstrapCompleteEvent + SDKReadyEvent
```

---

## 5. Chờ SDK sẵn sàng trong game code

### Cách 1 — UniTask (khuyến nghị cho async flow)

```csharp
using ArcherStudio.SDK.Core;
using Cysharp.Threading.Tasks;

private async UniTask StartGameAsync()
{
    // Chờ song song: architecture + addressables + SDK
    await UniTask.WhenAll(
        GameArchitecture.WaitUntilInitialized,
        Addressables.InitializeAsync().ToUniTask(),
        SDKBootstrap.WaitUntilInitialized
    );

    // SDK ready — bắt đầu load data
}
```

### Cách 2 — Subscribe event

```csharp
using ArcherStudio.SDK.Core;

private void Start()
{
    SDKEventBus.Subscribe<SDKReadyEvent>(OnSDKReady);
}

private void OnSDKReady(SDKReadyEvent e)
{
    SDKEventBus.Unsubscribe<SDKReadyEvent>(OnSDKReady);
    // SDK ready
}
```

---

## 6. Sử dụng từng module

### Login

```csharp
using ArcherStudio.SDK.Core;
using ArcherStudio.SDK.Login;

// Lắng nghe kết quả login
SDKEventBus.Subscribe<LoginSucceededEvent>(e => {
    Debug.Log($"Logged in: {e.PlayerId}, name={e.DisplayName}");
});
SDKEventBus.Subscribe<LoginFailedEvent>(e => {
    Debug.LogWarning($"Login failed: {e.Reason}");
});

// Trigger login thủ công (gọi sau khi SDK ready)
LoginModule.Instance?.TriggerLogin();

// Kiểm tra trạng thái
bool isLoggedIn = LoginModule.Instance?.Provider?.IsSignedIn ?? false;
```

### Tracking

```csharp
using ArcherStudio.SDK.Tracking;

// Track event tùy chỉnh
TrackingModule.Instance?.TrackEvent("level_complete", new Dictionary<string, object> {
    { "level", 5 },
    { "score", 1200 }
});
```

### Ads

```csharp
using ArcherStudio.SDK.Ads;

// Show rewarded ad
AdsModule.Instance?.ShowRewardedAd("main_menu", result => {
    if (result.Rewarded) GivePlayerReward();
});

// Show interstitial
AdsModule.Instance?.ShowInterstitialAd("game_over");
```

### IAP

```csharp
using ArcherStudio.SDK.IAP;

// Mua sản phẩm
IAPModule.Instance?.BuyProduct("remove_ads", result => {
    if (result.Success) UnlockRemoveAds();
});

// Restore purchases
IAPModule.Instance?.RestorePurchases();
```

### Remote Config

```csharp
using ArcherStudio.SDK.RemoteConfig;

// Đọc giá trị
int multiplier = RemoteConfigModule.Instance?.GetInt("difficulty_multiplier", defaultValue: 1) ?? 1;
string promoText = RemoteConfigModule.Instance?.GetString("promo_banner_text", "") ?? "";
```

---

## 7. CloudSave — hướng dẫn chi tiết

### 7.1. Điều kiện để CloudSave dùng Firestore thật

Tất cả điều kiện sau phải thỏa mãn:

- `EnableCloudSave = true` trong `SDKCoreConfig`
- `CloudSaveConfig.asset` tồn tại trong `Resources/` với `WebClientId` đã điền
- Package `com.google.firebase.firestore` đã cài (symbol `HAS_FIREBASE_FIRESTORE` được set)
- User đã đăng nhập thành công qua GPGS (`LoginModule.Instance.Provider.IsSignedIn = true`)
- Build target là Android (hoặc Editor với Firestore Emulator)

Nếu bất kỳ điều kiện nào thiếu → module tự fall back về `StubCloudSaveProvider` (in-memory, không crash).

### 7.2. Cấu hình CloudSaveConfig

1. Vào **Assets > Create > ArcherStudio > SDK > Cloud Save Config**
2. Đặt tên file là `CloudSaveConfig` và lưu vào `Assets/Resources/`
3. Điền field **Web Client ID**:
   - Vào Firebase Console → Project Settings → Your Apps → Android app
   - Hoặc vào Google Cloud Console → Credentials → OAuth 2.0 Client IDs → Web client (auto created by Google Service)
   - Copy "Client ID" (dạng `xxxxx.apps.googleusercontent.com`)

### 7.3. API sử dụng

```csharp
using ArcherStudio.SDK.CloudSave;
using ArcherStudio.SDK.Core;

public class CloudSaveService : MonoBehaviour
{
    private const string SaveSlot = "main_save";

    // ── SAVE ──────────────────────────────────────────────────
    public void SaveToCloud(MainGameData data)
    {
        string json = JsonUtility.ToJson(data);

        CloudSaveModule.Instance?.SaveAsync(SaveSlot, json, result => {
            if (result.Success)
                Debug.Log("[CloudSave] Save OK");
            else
                Debug.LogWarning($"[CloudSave] Save failed: {result.ErrorCode}");
        });
    }

    // ── LOAD ──────────────────────────────────────────────────
    public void LoadFromCloud(Action<MainGameData> onLoaded)
    {
        CloudSaveModule.Instance?.LoadAsync(SaveSlot, result => {
            if (result.HasConflict)
            {
                // Cloud có data mới hơn VÀ local có unsaved changes
                // result.Data      = cloud version
                // result.LocalData = local dirty version
                HandleConflict(result.Data, result.LocalData, onLoaded);
                return;
            }

            if (result.Success && result.Data != null)
            {
                var data = JsonUtility.FromJson<MainGameData>(result.Data);
                onLoaded?.Invoke(data);
            }
            else if (result.ErrorCode == CloudSaveErrorCode.NotFound)
            {
                // Lần đầu chơi — không có cloud save
                onLoaded?.Invoke(new MainGameData());
            }
            else
            {
                Debug.LogWarning($"[CloudSave] Load failed: {result.ErrorCode}");
            }
        });
    }

    // ── CONFLICT RESOLUTION ───────────────────────────────────
    private void HandleConflict(string cloudJson, string localJson, Action<MainGameData> onResolved)
    {
        // Option A: Cloud wins (đơn giản, phù hợp đa thiết bị)
        onResolved?.Invoke(JsonUtility.FromJson<MainGameData>(cloudJson));

        // Option B: Local wins (giữ progress hiện tại, rồi sync lên)
        // onResolved?.Invoke(JsonUtility.FromJson<MainGameData>(localJson));

        // Option C: Show UI cho user chọn (nếu game muốn trao quyền cho user)
        // ConflictUI.Show(cloudJson, localJson, choice => {
        //     onResolved?.Invoke(JsonUtility.FromJson<MainGameData>(choice));
        // });
    }
}
```

### 7.4. Lắng nghe sự kiện CloudSave

```csharp
// Khi sync thành công
SDKEventBus.Subscribe<CloudSaveSyncedEvent>(e => {
    Debug.Log($"[CloudSave] Slot '{e.SlotKey}' synced. Conflict={e.HadConflict}");
});

// Khi có lỗi
SDKEventBus.Subscribe<CloudSaveFailedEvent>(e => {
    Debug.LogWarning($"[CloudSave] Slot '{e.SlotKey}' error: {e.ErrorCode}");
});
```

### 7.5. Giới hạn và lưu ý

| Giới hạn | Giá trị |
|---|---|
| Max size mỗi slot | 900 KB (bị block trước khi gửi Firestore) |
| Max document Firestore | 1 MB |
| Firestore free tier | 20K writes/ngày, 50K reads/ngày |
| Offline behavior | StubProvider cache in-memory; Firestore SDK tự cache local |
| Authentication | Bắt buộc login GPGS trước khi CloudSave init với Firestore |

### 7.6. Firestore security rules

Deploy rules sau lên Firebase Console (Firestore > Rules):

```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /saves/{userId}/{document=**} {
      allow read, write: if request.auth != null
                         && request.auth.uid == userId;
    }
  }
}
```

File mẫu rules cũng có trong: `com.archerstudio.sdk.cloudsave/Samples~/FirebaseRules.txt`

---

## 8. Testing trên Editor và Device

### 8.1. Testing trên Editor

Trên Editor, các provider vendor (Firestore, GPGS, AppLovin…) không có. SDK tự động dùng **Stub providers** — không cần config gì thêm.

| Module | Hành vi trên Editor |
|---|---|
| Consent | StubConsentProvider — return Granted ngay lập tức |
| Login | StubLoginProvider — IsSignedIn=false, không crash |
| Tracking | StubTrackingProvider — log event ra Console thay vì gửi Firebase |
| Ads | StubAdsProvider — fake ad loaded/shown |
| CloudSave | StubCloudSaveProvider — lưu in-memory (mất khi stop Play Mode) |

**Bật DebugMode** trong `SDKCoreConfig` để thấy log chi tiết từ mọi module trong Console.

Sau khi nhấn Play, Console sẽ in xác nhận:
```
[SDK] Bootstrap complete. 8 modules ready in 123ms
[CloudSave] HAS_FIREBASE_FIRESTORE not defined. Using stub provider.
[Login] Stub provider active — not signed in.
```

### 8.2. Testing trên Android Device

Checklist trước khi build Android:

- [ ] `google-services.json` có trong `Assets/` (download từ Firebase Console)
- [ ] GPGS App ID đã set trong **Window > Google Play Games > Setup > Android Setup**
- [ ] SHA-1 + SHA-256 fingerprint đã thêm vào Firebase Console (Project Settings > Your Apps)
- [ ] `SDKCoreConfig.EnableLogin = true` và `EnableCloudSave = true`
- [ ] `CloudSaveConfig.WebClientId` điền đúng Web Client ID
- [ ] Chạy **ArcherStudio > SDK > Setup Wizard > Symbols > Auto-Detect All SDKs** để set `HAS_FIREBASE_FIRESTORE`, `HAS_GPGS_V2`
- [ ] Firestore rules đã deploy lên Firebase Console
- [ ] Firebase Auth đã bật provider **Play Games** (Firebase Console > Authentication > Sign-in method)

### 8.3. Debug CloudSave trên Device

Bật `DebugMode = true` và `MinLogLevel = Debug` trong SDKCoreConfig. Xem log qua logcat:

```bash
# Lọc log SDK
adb logcat -s Unity | grep -E "CloudSave|SDK"
```

Luồng log kỳ vọng khi CloudSave init thành công:
```
[SDK] Module cloudsave: Initializing...
[CloudSave] GPGS signed in. Requesting server auth code...
[CloudSave] Got server auth code. Signing into Firebase Auth...
[CloudSave] Firebase Auth UID: abc123xyz
[CloudSave] Firestore provider ready.
[SDK] Module cloudsave: Ready (245ms)
```

### 8.4. Simulate conflict trên Editor

`StubCloudSaveProvider` không simulate conflict. Để test conflict handling, dùng unit test:

```csharp
[Test]
public void LoadAsync_WithConflict_CallsHandlerWithBothVersions()
{
    var cloudJson  = "{\"level\":10,\"gold\":5000}";
    var localJson  = "{\"level\":8,\"gold\":4000}";
    var result = CloudSaveResult.WithConflict(cloudJson, localJson, DateTime.UtcNow);

    // Test conflict handler của bạn
    Assert.IsTrue(result.HasConflict);
    Assert.AreEqual(cloudJson, result.Data);
    Assert.AreEqual(localJson, result.LocalData);
}
```

---

## 9. Troubleshooting

### SDK không init (timeout sau 30s)

1. Kiểm tra Console có log `[SDK] Bootstrap complete`? Nếu không → module nào đó hang.
2. Bật `DebugMode = true`, xem module nào đang `Initializing` mà không complete.
3. Đảm bảo `SDKBootstrapConfig.ContinueOnModuleFailure = true` để app không bị block khi một module fail.

### CloudSave luôn dùng Stub (không lên Firestore)

Kiểm tra log theo thứ tự:

```
[CloudSave] EnableCloudSave=false. Skipping.
→ Fix: set EnableCloudSave=true trong SDKCoreConfig

[CloudSave] CloudSaveConfig not found in Resources/.
→ Fix: tạo asset tại Assets/Resources/CloudSaveConfig.asset

[CloudSave] User not signed in via GPGS. Using stub provider.
→ Fix: đảm bảo Login module init xong và user đã sign in trước CloudSave

[CloudSave] HAS_FIREBASE_FIRESTORE not defined. Using stub provider.
→ Fix: chạy Setup Wizard > Symbols > Auto-Detect
```

### Login không hoạt động trên device

- GPGS App ID chưa config đúng trong **Window > Google Play Games > Setup**
- SHA-1 fingerprint của keystore chưa thêm vào Firebase Console
- `google-services.json` cũ — download lại sau khi thêm SHA-1

### Build fail sau khi thêm SDK package

Nếu báo lỗi assembly reference:
- Kiểm tra `.asmdef` của game có reference đúng `ArcherStudio.SDK.Core` không
- Chạy **ArcherStudio > SDK > Setup Wizard > Symbols** → Auto-Detect để sync symbols

### `SDKBootstrap.WaitUntilInitialized` không resolve

- Kiểm tra GameObject có `SDKBootstrap` component tồn tại trong scene hiện tại không
- Kiểm tra `SDKBootstrapConfig.asset` đã gán đúng chưa
- Nếu dùng `UniTask.WhenAll`, đảm bảo không có Task nào khác trong nhóm bị cancel gây throw exception bỏ qua phần còn lại
