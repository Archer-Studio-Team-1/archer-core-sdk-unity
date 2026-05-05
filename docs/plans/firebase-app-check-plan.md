# Plan: Firebase App Check Integration

> **Mục đích**: Tích hợp Firebase App Check vào Archer SDK (Unity client) và Firebase Functions (server) để chống giả mạo request.
> **Repo**: `archer-core-sdk-unity` (SDK) + `firebase-functions/` (server, local only)
> **Dùng với**: Claude Code CLI — copy plan này vào conversation context, chạy từng phase.

---

## Tổng quan vấn đề

Hiện tại endpoint `validatePurchase` chỉ được bảo vệ bởi **API key tĩnh** (`x-api-key` header). API key này nằm trong Unity build → có thể bị extract từ APK/IPA → attacker gọi thẳng endpoint với receipt giả.

**Firebase App Check** giải quyết bằng cách:
1. Client lấy **App Check token** từ Firebase SDK (token ngắn hạn, gắn với app attestation)
2. Server verify token trước khi xử lý request
3. Token chỉ valid nếu app thật sự chạy trên device thật (Play Integrity / DeviceCheck)

---

## Phase 1: Server-side — Thêm App Check verification vào Firebase Functions

### 1.1 Cài dependency

**File**: `firebase-functions/functions/package.json`

Thêm vào `dependencies`:
```json
"firebase-app-check": "^0.1.0"
```

> **Lưu ý**: `firebase-admin` v13 đã có built-in App Check verification. Không cần package riêng, chỉ cần dùng `admin.appCheck().verifyToken(token)`.

### 1.2 Thêm middleware verify App Check token

**File mới**: `firebase-functions/functions/middleware/app-check.js`

```javascript
const { getAppCheck } = require("firebase-admin/app-check");
const { logger } = require("firebase-functions/v2");

/**
 * Verify Firebase App Check token from request header.
 * Header: X-Firebase-AppCheck: <token>
 *
 * @param {Object} req - Express request
 * @returns {Object} - { valid: boolean, appId?: string, error?: string }
 */
async function verifyAppCheckToken(req) {
  const appCheckToken = req.header("X-Firebase-AppCheck");

  if (!appCheckToken) {
    return { valid: false, error: "Missing App Check token" };
  }

  try {
    const result = await getAppCheck().verifyToken(appCheckToken);
    logger.info("App Check verified", { appId: result.appId });
    return { valid: true, appId: result.appId };
  } catch (err) {
    logger.warn("App Check verification failed", { error: err.message });
    return { valid: false, error: `App Check failed: ${err.message}` };
  }
}

module.exports = { verifyAppCheckToken };
```

### 1.3 Tích hợp vào endpoint validatePurchase

**File**: `firebase-functions/functions/index.js`

Thêm vào đầu file:
```javascript
const { verifyAppCheckToken } = require("./middleware/app-check");
```

Trong handler `validatePurchase`, thêm SAU bước verify API key, TRƯỚC bước xử lý platform:

```javascript
// --- App Check verification (optional but recommended) ---
const appCheckEnforced = process.env.ENFORCE_APP_CHECK === "true";
const appCheck = await verifyAppCheckToken(req);

if (!appCheck.valid) {
  if (appCheckEnforced) {
    logger.warn("App Check rejected (enforced)", { error: appCheck.error });
    return res.status(403).json({ valid: false, error: "App attestation failed" });
  }
  // Soft mode: log warning but allow through (for gradual rollout)
  logger.warn("App Check failed (not enforced)", { error: appCheck.error });
}
```

### 1.4 Cấu hình environment variable

```bash
# Bật soft mode trước (log warning, không block)
firebase functions:config:set appcheck.enforce="false" --project team1-game6-idledungeonkeeper

# Sau khi verify ok, bật enforce
firebase functions:config:set appcheck.enforce="true" --project team1-game6-idledungeonkeeper
```

**Hoặc dùng .env** cho Functions v2:
```
# firebase-functions/functions/.env
ENFORCE_APP_CHECK=false
```

### 1.5 Deploy và test

```bash
cd firebase-functions/functions
npm install
cd ..
firebase deploy --only functions --project team1-game6-idledungeonkeeper
```

**Test không có token** (phải trả 403 khi enforced):
```bash
curl -X POST \
  https://asia-southeast1-team1-game6-idledungeonkeeper.cloudfunctions.net/validatePurchase \
  -H "Content-Type: application/json" \
  -H "x-api-key: <YOUR_API_KEY>" \
  -d '{"platform":"google","productId":"test","purchaseToken":"fake"}'
```

---

## Phase 2: Unity Client — Tạo package `com.archerstudio.sdk.appcheck`

### 2.1 Tạo cấu trúc package

```
com.archerstudio.sdk.appcheck/
├── package.json
├── Runtime/
│   ├── ArcherStudio.SDK.AppCheck.asmdef
│   ├── AppCheckManager.cs
│   ├── Interfaces/
│   │   └── IAppCheckProvider.cs
│   ├── Providers/
│   │   ├── FirebaseAppCheckProvider.cs    ← #if HAS_FIREBASE_APP_CHECK
│   │   └── StubAppCheckProvider.cs
│   ├── Config/
│   │   └── AppCheckConfig.cs
│   └── AppCheckModuleRegistrar.cs
├── Editor/
│   └── ArcherStudio.SDK.AppCheck.Editor.asmdef
└── Tests/
    └── Runtime/
        └── ArcherStudio.SDK.AppCheck.Tests.asmdef
```

### 2.2 package.json

**File**: `com.archerstudio.sdk.appcheck/package.json`

```json
{
  "name": "com.archerstudio.sdk.appcheck",
  "version": "1.0.0",
  "displayName": "Archer SDK - App Check",
  "description": "Firebase App Check integration for request attestation",
  "unity": "6000.0",
  "dependencies": {
    "com.archerstudio.sdk.core": "1.0.0"
  },
  "author": {
    "name": "Archer Studio",
    "url": "https://archergame.mobi"
  }
}
```

### 2.3 Interface — IAppCheckProvider

**File**: `com.archerstudio.sdk.appcheck/Runtime/Interfaces/IAppCheckProvider.cs`

```csharp
using System;

namespace ArcherStudio.SDK.AppCheck {

    public interface IAppCheckProvider {
        /// <summary>
        /// Initialize the App Check provider.
        /// </summary>
        void Initialize(AppCheckConfig config, Action<bool> onComplete);

        /// <summary>
        /// Get a valid App Check token. Caches and auto-refreshes.
        /// Returns null/empty if not available.
        /// </summary>
        void GetToken(Action<string> onToken);

        void Dispose();
    }
}
```

### 2.4 StubAppCheckProvider (fallback khi không có Firebase)

**File**: `com.archerstudio.sdk.appcheck/Runtime/Providers/StubAppCheckProvider.cs`

```csharp
using System;
using ArcherStudio.SDK.Core;

namespace ArcherStudio.SDK.AppCheck {

    public class StubAppCheckProvider : IAppCheckProvider {
        private const string Tag = "AppCheck.Stub";

        public void Initialize(AppCheckConfig config, Action<bool> onComplete) {
            SDKLogger.Warning(Tag,
                "Firebase App Check SDK not installed. " +
                "App Check disabled. Install Firebase Unity SDK and ensure HAS_FIREBASE_APP_CHECK is defined.");
            onComplete?.Invoke(true); // Don't block other modules
        }

        public void GetToken(Action<string> onToken) {
            onToken?.Invoke(null); // No token available
        }

        public void Dispose() { }
    }
}
```

### 2.5 FirebaseAppCheckProvider (real implementation)

**File**: `com.archerstudio.sdk.appcheck/Runtime/Providers/FirebaseAppCheckProvider.cs`

```csharp
#if HAS_FIREBASE_APP_CHECK
using System;
using ArcherStudio.SDK.Core;
using Firebase.AppCheck;
using Firebase.Extensions;

namespace ArcherStudio.SDK.AppCheck {

    public class FirebaseAppCheckProvider : IAppCheckProvider {
        private const string Tag = "AppCheck.Firebase";
        private FirebaseAppCheck _appCheck;

        public void Initialize(AppCheckConfig config, Action<bool> onComplete) {
            try {
                // Set attestation provider factory based on platform
                #if UNITY_ANDROID
                FirebaseAppCheck.SetAppCheckProviderFactory(
                    PlayIntegrityProviderFactory.Instance);
                SDKLogger.Info(Tag, "Using Play Integrity provider (Android).");
                #elif UNITY_IOS
                FirebaseAppCheck.SetAppCheckProviderFactory(
                    DeviceCheckProviderFactory.Instance);
                SDKLogger.Info(Tag, "Using DeviceCheck provider (iOS).");
                #else
                // Debug provider for Editor testing
                FirebaseAppCheck.SetAppCheckProviderFactory(
                    DebugProviderFactory.Instance);
                SDKLogger.Warning(Tag, "Using Debug provider (Editor/unsupported platform).");
                #endif

                _appCheck = FirebaseAppCheck.DefaultInstance;
                SDKLogger.Info(Tag, "Firebase App Check initialized.");
                onComplete?.Invoke(true);
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Failed to init App Check: {e.Message}");
                onComplete?.Invoke(false);
            }
        }

        public void GetToken(Action<string> onToken) {
            if (_appCheck == null) {
                SDKLogger.Warning(Tag, "App Check not initialized. Returning null token.");
                onToken?.Invoke(null);
                return;
            }

            _appCheck.GetAppCheckTokenAsync(forceRefresh: false)
                .ContinueWithOnMainThread(task => {
                    if (task.IsFaulted || task.IsCanceled) {
                        SDKLogger.Warning(Tag,
                            $"Failed to get App Check token: {task.Exception?.Message}");
                        onToken?.Invoke(null);
                        return;
                    }

                    var result = task.Result;
                    SDKLogger.Debug(Tag, "Got App Check token (valid).");
                    onToken?.Invoke(result.Token);
                });
        }

        public void Dispose() {
            _appCheck = null;
        }
    }
}
#endif
```

### 2.6 AppCheckConfig (ScriptableObject)

**File**: `com.archerstudio.sdk.appcheck/Runtime/Config/AppCheckConfig.cs`

```csharp
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.AppCheck {

    [CreateAssetMenu(fileName = "AppCheckConfig", menuName = "ArcherStudio/SDK/App Check Config")]
    public class AppCheckConfig : ModuleConfigBase {

        [Header("App Check Settings")]
        [Tooltip("Use Debug provider in Editor for testing.")]
        public bool UseDebugProviderInEditor = true;

        [Tooltip("Auto-refresh token interval in minutes (0 = use Firebase default).")]
        public int TokenRefreshIntervalMinutes = 0;
    }
}
```

### 2.7 AppCheckManager (ISDKModule)

**File**: `com.archerstudio.sdk.appcheck/Runtime/AppCheckManager.cs`

```csharp
using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.AppCheck {

    public class AppCheckManager : ISDKModule {
        private const string Tag = "AppCheck";

        // ISDKModule
        public string ModuleId => "appcheck";
        public int InitializationPriority => 15; // Init early, before IAP/tracking
        public IReadOnlyList<string> Dependencies => Array.Empty<string>(); // Only needs core
        public ModuleState State { get; private set; } = ModuleState.NotInitialized;

        public static AppCheckManager Instance { get; private set; }

        private IAppCheckProvider _provider;
        private AppCheckConfig _config;

        public void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete) {
            State = ModuleState.Initializing;
            Instance = this;

            _config = Resources.Load<AppCheckConfig>("AppCheckConfig");
            if (_config == null) {
                SDKLogger.Warning(Tag,
                    "AppCheckConfig not found in Resources/. " +
                    "Create via: Assets > Create > ArcherStudio > SDK > App Check Config. " +
                    "App Check module will be inactive.");
                State = ModuleState.Ready;
                onComplete?.Invoke(true);
                return;
            }

            if (!_config.Enabled) {
                SDKLogger.Info(Tag, "AppCheckConfig.Enabled=false. App Check inactive.");
                State = ModuleState.Ready;
                onComplete?.Invoke(true);
                return;
            }

            _provider = CreateProvider();
            _provider.Initialize(_config, success => {
                State = success ? ModuleState.Ready : ModuleState.Failed;
                if (success) {
                    SDKLogger.Info(Tag, "AppCheckManager initialized.");
                } else {
                    SDKLogger.Error(Tag, "AppCheckManager failed to initialize.");
                }
                onComplete?.Invoke(success);
            });
        }

        public void OnConsentChanged(ConsentStatus consent) {
            // App Check doesn't need consent
        }

        public void Dispose() {
            _provider?.Dispose();
            _provider = null;
            Instance = null;
            State = ModuleState.Disposed;
        }

        /// <summary>
        /// Get a valid App Check token. Returns null if not available.
        /// Used by ServerReceiptValidator and other modules that call server endpoints.
        /// </summary>
        public void GetToken(Action<string> onToken) {
            if (_provider == null || State != ModuleState.Ready) {
                onToken?.Invoke(null);
                return;
            }
            _provider.GetToken(onToken);
        }

        private IAppCheckProvider CreateProvider() {
            #if HAS_FIREBASE_APP_CHECK
            return new FirebaseAppCheckProvider();
            #else
            SDKLogger.Warning(Tag, "HAS_FIREBASE_APP_CHECK not defined. Using stub.");
            return new StubAppCheckProvider();
            #endif
        }
    }
}
```

### 2.8 AppCheckModuleRegistrar

**File**: `com.archerstudio.sdk.appcheck/Runtime/AppCheckModuleRegistrar.cs`

```csharp
using ArcherStudio.SDK.Core;
using UnityEngine;

namespace ArcherStudio.SDK.AppCheck {

    public static class AppCheckModuleRegistrar {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() {
            SDKModuleFactory.RegisterCreator("appcheck", () => new AppCheckManager());
        }
    }
}
```

### 2.9 Assembly definition

**File**: `com.archerstudio.sdk.appcheck/Runtime/ArcherStudio.SDK.AppCheck.asmdef`

```json
{
    "name": "ArcherStudio.SDK.AppCheck",
    "rootNamespace": "ArcherStudio.SDK.AppCheck",
    "references": [
        "ArcherStudio.SDK.Core"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

---

## Phase 3: Kết nối App Check token vào ServerReceiptValidator

### 3.1 Sửa ServerReceiptValidator để gửi App Check token

**File**: `com.archerstudio.sdk.iap/Runtime/Providers/ServerReceiptValidator.cs`

Trong method `SendValidationRequest`, thêm logic lấy token và attach vào header:

```csharp
private System.Collections.IEnumerator SendValidationRequest(
    string jsonPayload, string productId, Action<ReceiptValidationResult> onComplete) {

    // Try to get App Check token first
    string appCheckToken = null;
    bool tokenReady = false;

    var appCheck = AppCheck.AppCheckManager.Instance;
    if (appCheck != null) {
        appCheck.GetToken(token => {
            appCheckToken = token;
            tokenReady = true;
        });

        // Wait for token callback (max 5 seconds)
        float waited = 0f;
        while (!tokenReady && waited < 5f) {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }

        if (!tokenReady) {
            SDKLogger.Warning(Tag, "App Check token timeout. Proceeding without.");
            tokenReady = true;
        }
    } else {
        tokenReady = true;
    }

    // ... existing code: build UnityWebRequest ...

    var request = new UnityWebRequest(_serverUrl, "POST");
    // ... existing setup ...

    // Attach App Check token if available
    if (!string.IsNullOrEmpty(appCheckToken)) {
        request.SetRequestHeader("X-Firebase-AppCheck", appCheckToken);
    }

    // ... rest of existing code ...
}
```

### 3.2 Thêm assembly reference

**File**: `com.archerstudio.sdk.iap/Runtime/ArcherStudio.SDK.IAP.asmdef`

Thêm reference (optional, soft dependency):
```json
{
    "references": [
        "ArcherStudio.SDK.Core",
        "ArcherStudio.SDK.AppCheck"  // ← thêm
    ],
    "versionDefines": [
        {
            "name": "com.archerstudio.sdk.appcheck",
            "expression": "1.0.0",
            "define": "HAS_SDK_APPCHECK"
        }
    ]
}
```

Bọc code tham chiếu AppCheckManager bằng `#if HAS_SDK_APPCHECK` để IAP package vẫn build khi chưa cài appcheck package.

---

## Phase 4: Thêm symbol detection vào SDKSymbolDetector

### 4.1 Thêm entry cho Firebase App Check

**File**: `com.archerstudio.sdk.core/Editor/SDKSymbolDetector.cs`

Thêm vào danh sách symbols:

```csharp
// Trong list _symbolMappings hoặc tương đương:
{ "Firebase.AppCheck.FirebaseAppCheck", "HAS_FIREBASE_APP_CHECK" },
```

### 4.2 Thêm EnableAppCheck vào SDKCoreConfig

**File**: `com.archerstudio.sdk.core/Runtime/Config/SDKCoreConfig.cs`

```csharp
[Header("App Check")]
[Tooltip("Enable Firebase App Check for request attestation.")]
public bool EnableAppCheck = false;
```

---

## Phase 5: Firebase Console Setup

### 5.1 Bật App Check trong Firebase Console

1. Mở Firebase Console → project `team1-game6-idledungeonkeeper`
2. Vào **App Check** (menu bên trái, mục Build)
3. Đăng ký app:
   - **Android**: chọn **Play Integrity** provider
     - Cần liên kết Google Cloud project với Play Console
     - Nếu đã có service account liên kết → tự động
   - **iOS**: chọn **DeviceCheck** hoặc **App Attest** provider
     - Cần Apple Team ID và Key ID (tạo DeviceCheck key trong Apple Developer)
4. **QUAN TRỌNG**: Chưa enforce ngay! Để monitor mode trước

### 5.2 Enable App Check cho Cloud Functions

1. Trong Firebase Console → App Check → **APIs** tab
2. Tìm **Cloud Functions** → bật enforce
3. **Chỉ bật sau khi** đã verify client gửi token thành công (xem Phase 6)

### 5.3 Play Integrity setup (Android)

1. Google Cloud Console → APIs & Services → Enable **Play Integrity API**
2. Google Play Console → app → Setup → App Integrity → liên kết Firebase project
3. Nếu dùng internal testing track: App Check debug token có thể cần cho test devices

### 5.4 DeviceCheck/App Attest setup (iOS)

1. Apple Developer → Certificates, Identifiers & Profiles → Keys → Create DeviceCheck key
2. Download `.p8` file → lưu Key ID
3. Firebase Console → App Check → iOS app → nhập Key ID + Team ID + .p8 content

---

## Phase 6: Testing & Gradual Rollout

### 6.1 Test với Debug Provider (Editor/Dev build)

```
# Trong Unity Editor Console, sẽ thấy debug token:
# "AppCheck debug token: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
#
# Copy token này → Firebase Console → App Check → Manage Debug Tokens → thêm vào
```

### 6.2 Kiểm tra server logs

```bash
# Xem logs Firebase Functions
firebase functions:log --project team1-game6-idledungeonkeeper

# Tìm entries "App Check verified" hoặc "App Check failed"
```

### 6.3 Rollout checklist

```
Phase A: Soft mode (1-2 tuần)
  ☐ Deploy server với ENFORCE_APP_CHECK=false
  ☐ Deploy Unity build với App Check SDK
  ☐ Monitor logs: bao nhiêu % request có valid token?
  ☐ Fix issues nếu token rate < 95%

Phase B: Enforce mode
  ☐ Set ENFORCE_APP_CHECK=true
  ☐ Firebase Console → App Check → APIs → enforce Cloud Functions
  ☐ Monitor: purchase success rate không giảm?
  ☐ Có fallback: nếu issue → set lại false ngay

Phase C: Remove API key (optional)
  ☐ Sau khi App Check stable 2+ tuần
  ☐ Có thể giữ API key như layer phụ hoặc remove
```

---

## Lệnh Claude Code gợi ý

Khi dùng Claude Code trong terminal, bạn có thể paste từng block:

```bash
# Phase 1: Server middleware
claude "Đọc file firebase-functions/functions/index.js và thêm App Check middleware theo plan trong docs/plans/firebase-app-check-plan.md Phase 1. Tạo file middleware/app-check.js và sửa index.js. Dùng soft mode (ENFORCE_APP_CHECK env var)."

# Phase 2: Unity package
claude "Tạo package com.archerstudio.sdk.appcheck/ theo layout trong docs/plans/firebase-app-check-plan.md Phase 2. Follow convention từ CLAUDE.md. Đọc com.archerstudio.sdk.iap/ làm reference cho pattern."

# Phase 3: Kết nối IAP ↔ App Check
claude "Sửa ServerReceiptValidator.cs để gửi App Check token trong header X-Firebase-AppCheck. Đọc plan Phase 3. Dùng #if HAS_SDK_APPCHECK guard. Thêm asmdef reference với versionDefines."

# Phase 4: Symbol detector
claude "Thêm HAS_FIREBASE_APP_CHECK vào SDKSymbolDetector.cs. Thêm EnableAppCheck vào SDKCoreConfig.cs. Đọc plan Phase 4 và follow pattern existing symbols."
```

---

## Dependency graph sau khi thêm App Check

```
core ──┬── consent ──┬── tracking ──┬── ads
       │             │              └── iap ←── appcheck (soft dep, optional)
       │             └── login
       ├── appcheck  ← MỚI (init priority 15, trước iap=50)
       ├── deeplink
       ├── push
       ├── remoteconfig
       └── testlab
```

---

## Rủi ro & Mitigation

| Rủi ro | Mitigation |
|---|---|
| App Check token expire giữa chừng purchase flow | GetToken auto-refresh; timeout 5s rồi proceed without token |
| Firebase App Check SDK chưa support Unity 6 stable | Check release notes; fallback to Debug provider |
| Enforce quá sớm → block user thật | Soft mode 2 tuần; monitor % valid token |
| Play Integrity quota limit (10K/day free tier) | Monitor usage; nếu cần upgrade → Play Integrity API billing |
| iOS App Attest không work trên simulator | Dùng DeviceCheck provider (fallback tốt hơn) hoặc Debug provider |

---

## Tóm tắt thứ tự thực hiện

1. **Server**: Tạo middleware, sửa index.js, deploy (soft mode) → **~30 phút**
2. **Firebase Console**: Bật App Check, đăng ký providers → **~20 phút**
3. **Unity Package**: Tạo `com.archerstudio.sdk.appcheck` → **~1 giờ**
4. **Kết nối**: Sửa ServerReceiptValidator gửi token → **~30 phút**
5. **Symbol**: Update SDKSymbolDetector + SDKCoreConfig → **~15 phút**
6. **Test**: Debug provider trong Editor, test full flow → **~1 giờ**
7. **Monitor**: Soft mode 1-2 tuần → enforce → **ongoing**

**Tổng effort ước tính**: ~3-4 giờ code + 1-2 tuần monitor trước khi enforce.
