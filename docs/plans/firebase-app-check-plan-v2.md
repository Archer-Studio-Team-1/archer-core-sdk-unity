# Plan v2: Firebase App Check + IAP Hardening

> **Muc dich**: Hardening IAP validation pipeline + tich hop Firebase App Check
> **Repo**: `archer-core-sdk-unity` (SDK) + `firebase-functions/` (server)
> **Yeu cau**: 100% user that mua duoc + 100% chan hack/cheat

---

## Van de phat hien tu plan v1

| # | Van de | Muc do | Mo ta |
|---|--------|--------|-------|
| 1 | Fail-open trong ServerReceiptValidator | CRITICAL | Network error/parse error -> auto pass purchase. Hacker chi can block request toi server |
| 2 | API key trong APK | HIGH | Extract duoc tu APK, goi endpoint truc tiep |
| 3 | Transaction dedup tin userId tu client | HIGH | userId do client gui, khong verify |
| 4 | Subscription reward cap local, khong verify server | CRITICAL | Game tu quyet dinh cap VIP reward ma khong hoi server |
| 5 | Khong validate appId trong App Check response | MEDIUM | Attacker dung Firebase project khac |
| 6 | Play Integrity quota 10K/day | MEDIUM | DAU > 10K hoac flood -> het quota |
| 7 | Busy-wait loop cho App Check token | LOW | Performance waste |

---

## Phase 0: Harden Server - Fix fail-open (CRITICAL)

### 0.1 Fix ServerReceiptValidator.cs - Retry + fail-close

**File**: `com.archerstudio.sdk.iap/Runtime/Providers/ServerReceiptValidator.cs`

- Network error -> retry 2 lan (delay 1s, 3s) -> fail-CLOSE
- HTTP 401/403 -> fail-close ngay (unauthorized)
- HTTP 400 -> fail-close (bad request = receipt gia)
- HTTP 500 -> retry 1 lan -> fail-close
- Chi 200 + valid:true -> pass
- Xoa tat ca fail-open logic

### 0.2 Fix server dedup - Dung purchaseToken/orderId lam key

**File**: `firebase-functions/functions/index.js`

- Google: dung orderId tu Google API response lam dedup key
- Apple: dung originalTransactionId lam dedup key
- Khong tin userId tu client cho dedup logic

### 0.3 Them rate limiting per IP

**File**: `firebase-functions/functions/middleware/rate-limit.js` (moi)

- Max 30 validations per IP per hour
- Firestore-based counter
- Return 429 khi vuot qua

### 0.4 Them endpoint validateSubscription

**File**: `firebase-functions/functions/index.js` + `validators/subscription-status.js`

- POST /validateSubscription
- Goi Google subscriptionsv2.get / Apple Server API
- Tra: { valid, expirationDate, autoRenewing, cancelled }

---

## Phase 1: Server - App Check middleware

### 1.1 Them middleware verify App Check token

**File moi**: `firebase-functions/functions/middleware/app-check.js`

- Verify token bang admin.appCheck().verifyToken()
- Validate appId khop voi app that
- Soft/enforce mode qua env var ENFORCE_APP_CHECK

### 1.2 Tich hop vao endpoint validatePurchase + validateSubscription

**File**: `firebase-functions/functions/index.js`

- Goi verifyAppCheckToken sau API key check
- Soft mode: log warning nhung cho qua
- Enforce mode: reject 403

### 1.3 Cau hinh environment

```
ENFORCE_APP_CHECK=false  (bat dau soft mode)
```

---

## Phase 2: Unity - Package com.archerstudio.sdk.appcheck

### 2.1 Tao package structure

```
com.archerstudio.sdk.appcheck/
  package.json
  Runtime/
    ArcherStudio.SDK.AppCheck.asmdef
    AppCheckManager.cs
    Interfaces/IAppCheckProvider.cs
    Providers/FirebaseAppCheckProvider.cs
    Providers/StubAppCheckProvider.cs
    Config/AppCheckConfig.cs
    AppCheckModuleRegistrar.cs
```

### 2.2 Implementation

- IAppCheckProvider interface: Initialize + GetToken + Dispose
- FirebaseAppCheckProvider: Play Integrity (Android) + DeviceCheck (iOS)
- StubAppCheckProvider: fallback khi khong co Firebase
- AppCheckManager: ISDKModule, priority 10 (init som)
- AppCheckConfig: ScriptableObject, Enabled toggle

---

## Phase 3: Ket noi App Check -> ServerReceiptValidator

### 3.1 Refactor ServerReceiptValidator dung callback chain

- GetAppCheckToken -> callback -> attach X-Firebase-AppCheck header -> send request
- Khong dung coroutine busy-wait loop

### 3.2 Assembly reference voi #if HAS_SDK_APPCHECK guard

- Them reference trong asmdef voi versionDefines
- Code tham chieu AppCheckManager boc trong #if

---

## Phase 4: Symbol detection + Config

### 4.1 Them HAS_FIREBASE_APP_CHECK vao SDKSymbolDetector.cs
### 4.2 Them EnableAppCheck vao SDKCoreConfig.cs

---

## Phase 5: Firebase Console Setup

### 5.1 Bat App Check trong Firebase Console
### 5.2 Android: Play Integrity provider
### 5.3 iOS: DeviceCheck provider
### 5.4 Setup quota alert cho Play Integrity API

---

## Phase 6: Harden Game Logic - Subscription server verify

### 6.1 SubscriptionService them server-verify

```
WaitForSDKAndRefresh:
  1. Doi IAPManager ready
  2. Doi FetchPurchases
  3. Neu co subscription active (tu store):
     -> Goi server /validateSubscription
     -> Chi set IsReady=true neu server confirm
  4. Neu khong co subscription -> set IsReady=true ngay
```

### 6.2 VipSubscriptionSystem.PurchaseIAP doi server validation

```
PurchaseIAP:
  1. Trigger purchase flow
  2. SDK purchase callback + server validation
  3. CHI khi server pass -> cap reward + update runtime
  4. Neu server reject -> revert, show error
```

---

## Phase 7: Testing & Rollout

### Test matrix

```
User that, mang tot         -> mua thanh cong, server validate OK
User that, mang yeu         -> retry 2 lan, neu fail -> show loi, cho retry
User that, da mua           -> restore works, subscription status dung
Hacker, receipt gia          -> server reject -> purchase fail
Hacker, block server         -> retry fail -> purchase fail (fail-close)
Hacker, replay token         -> dedup catch -> reject
Hacker, modify APK           -> App Check fail -> reject (khi enforce)
Hacker, modify save data     -> server verify subscription -> reject
```

### Rollout stages

```
Week 1-2: Phase 0 (fix fail-open) + Phase 1 soft mode
  Monitor: purchase success rate, server validation rate
  KPI: >98% user that pass validation

Week 3: Phase 2-4 (App Check client)
  Monitor: % request co valid App Check token
  KPI: >95% co token

Week 4: Enable enforce mode
  Monitor: purchase fail rate khong tang
  Fallback: disable enforce trong 5 phut neu fail rate > 5%

Week 5+: Phase 6 (subscription server verify)
  Monitor: VIP status accuracy
```

---

## Thu tu uu tien

| # | Task | Priority | Effort |
|---|------|----------|--------|
| 0 | Fix fail-open ServerReceiptValidator | CRITICAL | 1h |
| 1 | Server App Check middleware (soft) | HIGH | 30m |
| 2 | Fix dedup + rate limit | HIGH | 30m |
| 3 | Unity App Check package | HIGH | 1h |
| 4 | Ket noi IAP -> App Check | MEDIUM | 30m |
| 5 | Symbol detection + Config | LOW | 15m |
| 6 | Subscription server verify | HIGH | 2h |
| 7 | Rate limiting server | MEDIUM | 30m |

**Tong effort**: ~6-7 gio code + 4-5 tuan rollout
