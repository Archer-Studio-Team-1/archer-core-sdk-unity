# IAP Server-Side Receipt Validation — Setup Guide

Hướng dẫn đầy đủ setup server-side IAP receipt validation cho Archer Studio SDK, sử dụng Firebase Cloud Functions để xác minh receipt mua hàng với Google Play API / Apple App Store Server API.

**Mục đích**: Chống gian lận IAP — client gửi receipt lên server, server xác minh trực tiếp với Google/Apple trước khi grant purchase.

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Yêu cầu trước khi bắt đầu](#2-yêu-cầu-trước-khi-bắt-đầu)
3. [Setup Google Cloud Console](#3-setup-google-cloud-console)
4. [Setup Google Play Console](#4-setup-google-play-console)
5. [Setup Firebase Functions](#5-setup-firebase-functions)
6. [Deploy Firebase Functions](#6-deploy-firebase-functions)
7. [Tích hợp Unity SDK](#7-tích-hợp-unity-sdk)
8. [Setup Apple (iOS)](#8-setup-apple-ios)
9. [Testing](#9-testing)
10. [Troubleshooting](#10-troubleshooting)
11. [Security Checklist](#11-security-checklist)

---

## 1. Tổng quan kiến trúc

```
┌──────────────┐     purchase      ┌────────────────┐
│  Unity Game  │ ──────────────>   │  Google Play / │
│  (Client)    │ <──────────────   │  App Store     │
│              │     receipt       └────────────────┘
│              │                           ▲
│              │     POST receipt           │ Server-to-Server
│              │ ──────────────>   ┌───────┴────────┐
│              │                   │ Firebase Cloud  │
│              │ <──────────────   │ Functions       │
│              │   valid/invalid   │ (validatePurchase)│
└──────────────┘                   └───────┬────────┘
                                           │
                                   ┌───────▼────────┐
                                   │   Firestore    │
                                   │ (dedup txns)   │
                                   └────────────────┘
```

**Flow**:
1. Client mua hàng qua Unity IAP → nhận receipt/purchaseToken
2. SDK gửi receipt lên Firebase Functions endpoint
3. Server xác minh trực tiếp với Google Play API / Apple Server API
4. Server kiểm tra transaction dedup trong Firestore (chống replay attack)
5. Server trả về `valid: true/false`
6. Client chỉ grant purchase khi server confirm `valid: true`

**Fail-open strategy**: Nếu network error (timeout, server down), client vẫn grant purchase để không block user. Chỉ reject khi server explicitly trả `valid: false`.

---

## 2. Yêu cầu trước khi bắt đầu

- Firebase project đã tạo và linked với GCP project
- Firebase CLI đã cài (`npm install -g firebase-tools`)
- Node.js 22+ (cho Firebase Functions)
- Tài khoản có quyền Owner hoặc Editor trên GCP project
- Google Play Console access (để link service account)
- (iOS) Apple Developer account với App Store Connect access

---

## 3. Setup Google Cloud Console

### 3.1. Enable Google Play Developer API

1. Vào [Google Cloud Console](https://console.cloud.google.com)
2. Chọn đúng project (ví dụ: `team1-game6-IdleDungeonKeeper`)
3. Vào **APIs & Services** → **Library**
4. Tìm **Google Play Android Developer API**
5. Click **Enable**

### 3.2. Tạo Service Account

1. Vào **IAM & Admin** → **Service Accounts**
2. Click **Create Service Account**
3. Đặt tên: `play-iap-validator`
4. Description: `Service account for IAP receipt validation`
5. Click **Create and Continue**
6. **KHÔNG cần** grant role nào ở bước này → Click **Done**

### 3.3. Tạo Key cho Service Account

1. Click vào service account vừa tạo
2. Tab **Keys** → **Add Key** → **Create New Key**
3. Chọn **JSON** → **Create**
4. File JSON sẽ tự download — **giữ bí mật, KHÔNG commit vào git**
5. Đổi tên thành `service-account.json`

### 3.4. Setup Cloud Build Permissions (cho deploy)

Đây là bước hay bị thiếu khi deploy Firebase Functions lần đầu.

1. Vào [Cloud Build → Permissions](https://console.cloud.google.com/cloud-build/settings/service-account)
2. Chọn service account: `<PROJECT_NUMBER>-compute@developer.gserviceaccount.com`
3. Enable các role sau:

| Role | Mục đích |
|------|----------|
| **Artifact Registry Writer** | Push container images |
| **Cloud Build Editor** | Tạo và cancel builds |
| **Cloud Build WorkerPool User** | Chạy builds trong worker pool |
| **Cloud Build Service Account** | Perform builds |
| **Cloud Functions Developer** | Deploy Cloud Functions |
| **Cloud Run Admin** | Quản lý Cloud Run services |
| **Storage Admin** | Access storage buckets |

4. Nếu có popup "Assign Service Account User Role" → Click **Grant permission** hoặc **Skip for now** nếu đã có

**⚠️ Lưu ý**: Cũng cần grant role **Cloud Build Service Agent** cho Legacy Cloud Build Service Account (`<PROJECT_NUMBER>@cloudbuild.gserviceaccount.com`):
- Vào **IAM & Admin** → **IAM** → **Grant Access**
- Principal: `<PROJECT_NUMBER>@cloudbuild.gserviceaccount.com`
- Role: **Cloud Build Service Agent**
- Save

---

## 4. Setup Google Play Console

### 4.1. Invite Service Account

1. Vào [Google Play Console](https://play.google.com/console)
2. **Settings** → **Users and permissions** → **Invite new users**
3. Email: `play-iap-validator@<PROJECT_ID>.iam.gserviceaccount.com`
4. Ở tab **App permissions**: chọn app cần validate
5. Ở tab **Account permissions**: bật các quyền:
   - **View financial data, orders, and cancellation survey responses**
   - **Manage orders and subscriptions**
6. Click **Invite user** → **Send invite**

**⚠️ Lưu ý quan trọng**: 
- Service account invitation có thể mất **24-48 giờ** để Google Play API fully recognize
- Trong thời gian chờ, API sẽ trả lỗi 401/403
- Người invite phải là **account owner** hoặc có quyền **Admin** trên Google Play Console

---

## 5. Setup Firebase Functions

### 5.1. Cấu trúc project

```
firebase-functions/
├── firebase.json
├── .firebaserc
├── .gitignore
└── functions/
    ├── package.json
    ├── index.js                    # Main endpoint: POST /validatePurchase
    ├── service-account.json        # ⚠️ KHÔNG commit — nằm trong .gitignore
    └── validators/
        ├── google-play.js          # Google Play Developer API v3
        └── apple.js                # Apple App Store Server API v2
```

### 5.2. Cài đặt dependencies

```bash
cd firebase-functions/functions
npm install
```

Dependencies chính:
- `firebase-admin` — Firestore access
- `firebase-functions` — Cloud Functions framework
- `googleapis` — Google Play Developer API
- `jose` — JWT signing cho Apple API

### 5.3. Đặt service-account.json

Copy file JSON key đã download ở bước 3.3 vào `functions/service-account.json`.

```bash
cp ~/Downloads/team1-game6-idledungeonkeeper-*.json functions/service-account.json
```

### 5.4. Set API Key (bảo mật endpoint)

```bash
# Tạo API key bất kỳ (ví dụ UUID)
firebase functions:secrets:set IAP_API_KEY --project <PROJECT_ID>
# Nhập giá trị key khi được hỏi
```

Key này phải match với `ValidationApiKey` trong Unity SDK config.

### 5.5. Cấu hình firebase.json

```json
{
  "functions": [
    {
      "source": "functions",
      "codebase": "iap-validation",
      "ignore": ["node_modules", ".git", "firebase-debug.log", "*.local"]
    }
  ]
}
```

### 5.6. Cấu hình .firebaserc

```json
{
  "projects": {
    "default": "<PROJECT_ID>"
  }
}
```

---

## 6. Deploy Firebase Functions

### 6.1. Login Firebase CLI

```bash
firebase login
```

Nếu project không hiện trong `firebase projects:list` (do không phải người tạo), vẫn có thể deploy bằng flag `--project`:

```bash
firebase deploy --only functions --project <PROJECT_ID>
```

### 6.2. Deploy

```bash
cd firebase-functions
firebase deploy --only functions --project <PROJECT_ID>
```

### 6.3. Lấy URL endpoint

Sau khi deploy thành công, URL sẽ có dạng:
```
https://asia-southeast1-<PROJECT_ID>.cloudfunctions.net/validatePurchase
```

URL này sẽ được điền vào `IAPConfig.ValidationServerUrl` trong Unity.

### 6.4. Các lỗi thường gặp khi deploy

| Lỗi | Nguyên nhân | Giải pháp |
|-----|-------------|-----------|
| `ERR_REQUIRE_ESM` | Package `jose` là ESM-only | Dùng dynamic `import()` thay `require()` — xem code `apple.js` |
| `missing permission on build service account` | Cloud Build thiếu quyền | Enable roles trong Cloud Build → Permissions (xem mục 3.4) |
| `Runtime Node.js 20 was deprecated` | Node runtime cũ | Đổi `engines.node` trong `package.json` thành `"22"` |
| `Couldn't find firebase-functions package` | Chưa install dependencies | Chạy `npm install` trong folder `functions/` |
| `Functions deploy had errors` | Nhiều nguyên nhân | Kiểm tra Cloud Build logs trong GCP Console |

---

## 7. Tích hợp Unity SDK

### 7.1. Các file liên quan

| File | Vai trò |
|------|---------|
| `IAPConfig.cs` | Config ScriptableObject — chứa URL, API key, toggle |
| `IAPManager.cs` | Manager — auto-setup validator, blocking validation flow |
| `ServerReceiptValidator.cs` | HTTP client — gửi receipt lên server |
| `IReceiptValidator.cs` | Interface cho receipt validation |

### 7.2. Cấu hình IAPConfig

1. Trong Unity: **Assets > Create > ArcherStudio > SDK > IAP Config**
2. Đặt vào folder `Resources/` (tên file: `IAPConfig`)
3. Điền các field:

| Field | Giá trị |
|-------|---------|
| `Enable Receipt Validation` | ✅ (checked) |
| `Validation Server Url` | `https://asia-southeast1-<PROJECT_ID>.cloudfunctions.net/validatePurchase` |
| `Validation Api Key` | Cùng giá trị đã set ở bước 5.4 |

### 7.3. Flow hoạt động trong SDK

```
IAPManager.Purchase(productId)
  └─ _provider.Purchase() → Store trả về PurchaseResult
       └─ result.Success?
            ├─ YES + Validation ON:
            │    └─ ServerReceiptValidator.Validate(receipt)
            │         ├─ Server valid=true  → CompletePurchaseSuccess() → grant
            │         ├─ Server valid=false → PurchaseResult.Failed() → reject
            │         └─ Network error      → grant (fail-open)
            ├─ YES + Validation OFF:
            │    └─ CompletePurchaseSuccess() → grant ngay
            └─ NO: → log warning, report failure
```

### 7.4. Auto-setup

`IAPManager.InitializeAsync()` tự tạo `ServerReceiptValidator` nếu:
- `IAPConfig.EnableReceiptValidation == true`
- `IAPConfig.ValidationServerUrl` không empty
- Chưa có validator nào được set thủ công

Không cần gọi `SetReceiptValidator()` thủ công trong game code.

### 7.5. Receipt format (Unity IAP → Server)

**Android (Google Play)**:
```
Unity IAP Receipt JSON:
{
  "Store": "GooglePlay",
  "Payload": "{\"json\":\"{\\\"purchaseToken\\\":\\\"...\\\",\\\"packageName\\\":\\\"...\\\"}\",\"signature\":\"...\"}"
}

SDK parse ra → gửi server:
{
  "platform": "google",
  "productId": "com.example.gems100",
  "purchaseToken": "<extracted-from-receipt>",
  "packageName": "com.archer.idle.dungeon.keeper.tycoon.rpg"
}
```

**iOS (Apple)**:
```
SDK gửi server:
{
  "platform": "apple",
  "productId": "com.example.gems100",
  "receipt": "<base64-app-receipt>"
}
```

---

## 8. Setup Apple (iOS)

### 8.1. App Store Server API v2 (Recommended)

1. Vào [App Store Connect](https://appstoreconnect.apple.com)
2. **Users and Access** → **Integrations** → **In-App Purchase**
3. Generate API Key → download file `.p8`
4. Ghi lại: **Key ID**, **Issuer ID**, **Bundle ID**

5. Set Firebase secrets:
```bash
firebase functions:secrets:set APPLE_KEY_ID --project <PROJECT_ID>
firebase functions:secrets:set APPLE_ISSUER_ID --project <PROJECT_ID>
firebase functions:secrets:set APPLE_BUNDLE_ID --project <PROJECT_ID>
firebase functions:secrets:set APPLE_PRIVATE_KEY --project <PROJECT_ID>
# Paste nội dung file .p8 khi được hỏi
```

### 8.2. Legacy verifyReceipt (Fallback)

Nếu chưa setup API v2, có thể dùng legacy (Apple deprecated nhưng vẫn hoạt động):

1. Vào App Store Connect → App → **App Information** → **Shared Secret**
2. Set secret:
```bash
firebase functions:secrets:set APPLE_SHARED_SECRET --project <PROJECT_ID>
```

### 8.3. Ưu tiên validation

Server tự chọn method theo thứ tự:
1. **App Store Server API v2** — nếu có KEY_ID + ISSUER_ID + PRIVATE_KEY
2. **Legacy verifyReceipt** — nếu có SHARED_SECRET + receipt
3. **Error** — nếu không cấu hình gì

---

## 9. Testing

### 9.1. Test trực tiếp với curl

```bash
# Test Google Play validation
curl -X POST \
  https://asia-southeast1-<PROJECT_ID>.cloudfunctions.net/validatePurchase \
  -H "Content-Type: application/json" \
  -H "x-api-key: <YOUR_API_KEY>" \
  -d '{
    "platform": "google",
    "productId": "com.example.gems100",
    "purchaseToken": "<real-purchase-token>",
    "packageName": "com.archer.idle.dungeon.keeper.tycoon.rpg"
  }'
```

### 9.2. Test với Firebase Emulator

```bash
cd firebase-functions
firebase emulators:start --only functions
```

Endpoint local: `http://127.0.0.1:5001/<PROJECT_ID>/asia-southeast1/validatePurchase`

### 9.3. Kiểm tra logs

```bash
firebase functions:log --project <PROJECT_ID>
```

Hoặc xem trong [GCP Cloud Logging](https://console.cloud.google.com/logs).

### 9.4. Test trong Unity

1. Build app lên device thật (IAP không hoạt động trên editor)
2. Dùng Google Play Internal Testing track hoặc Apple TestFlight
3. Kiểm tra log SDK: tìm tag `IAP.ServerValidator`
4. Kết quả expected:
   - `"Sending validation request for <productId>..."` — đang gửi
   - `"Server validated purchase: <productId>"` — thành công
   - `"Server rejected purchase: <productId>"` — bị reject

---

## 10. Troubleshooting

### Google Play API errors

| Error | Nguyên nhân | Giải pháp |
|-------|-------------|-----------|
| 401 Unauthorized | Service account chưa được authorize | Kiểm tra service-account.json đúng project |
| 403 Forbidden | Thiếu quyền trên Google Play Console | Invite service account vào Play Console (bước 4) |
| 404 Not Found | purchaseToken invalid hoặc product chưa publish | Kiểm tra productId và token |
| 410 Gone | Token đã expired hoặc consumed | Bình thường cho consumable đã consume |

### Firebase Functions deploy errors

| Error | Giải pháp |
|-------|-----------|
| Permission denied on Cloud Build | Enable roles trong Cloud Build Permissions (bước 3.4) |
| `service-account.json` not found | Copy file key vào `functions/` folder |
| `BILLING_NOT_ENABLED` | Enable billing cho GCP project |
| Function timeout | Tăng `timeoutSeconds` trong `index.js` |

### Unity SDK issues

| Vấn đề | Giải pháp |
|--------|-----------|
| Validation luôn skip | Kiểm tra `IAPConfig.EnableReceiptValidation` = true và URL không empty |
| Network timeout | Kiểm tra internet, tăng `timeoutSeconds` trong ServerReceiptValidator constructor |
| Receipt parse lỗi | Kiểm tra format Unity IAP receipt (nested JSON) |

---

## 11. Security Checklist

- [ ] `service-account.json` nằm trong `.gitignore` — **KHÔNG BAO GIỜ commit**
- [ ] `IAP_API_KEY` set qua Firebase Secrets — không hardcode trong source
- [ ] Service account chỉ có quyền tối thiểu cần thiết
- [ ] HTTPS endpoint (Firebase Functions tự handle)
- [ ] Transaction deduplication enabled (Firestore collection `iap_transactions`)
- [ ] Firestore Security Rules: chỉ cho phép server (admin SDK) access `iap_transactions`
- [ ] Rate limiting: `maxInstances: 10` trong function config
- [ ] Không log sensitive data (purchaseToken, receipt) trong production

### Firestore Security Rules (recommended)

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    // iap_transactions: chỉ server (admin SDK) mới access được
    // Client KHÔNG được đọc/ghi collection này
    match /iap_transactions/{transactionId} {
      allow read, write: if false;
    }
  }
}
```

---

## Changelog

| Ngày | Thay đổi |
|------|----------|
| 2026-05-04 | Initial setup: Firebase Functions, Google Play API, Service Account |
| 2026-05-05 | Fix jose ESM import, Cloud Build permissions, Node 22 upgrade |
| 2026-05-05 | Unity SDK integration: blocking validation, auto-setup, IAPConfig changes |
