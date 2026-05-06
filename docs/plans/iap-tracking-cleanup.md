# Plan: IAP Tracking Cleanup

> **Status**: DONE
> **Priority**: HIGH
> **Effort**: ~1.5 giờ

---

## Vấn đề

1. Revenue tracking gửi lên cả khi build dev/test → sai lệch dữ liệu Firebase/Adjust
2. Test account (license tester) mua IAP vẫn gửi revenue → pha tạp data production
3. Event `iap_revenue` thiếu currency gốc và giá trị gốc theo currency đó

## Kiểm tra đã thực hiện

- **User property "ltv"**: KHÔNG tồn tại ở cả SDK lẫn game layer. Không cần sửa.
- **`purchaseType`**: Chỉ có trong Google Play Developer API response (server-side), KHÔNG có trong client receipt payload.
- **Apple `environment`**: Chỉ có trong server response, không có trong client receipt.

---

## Giải pháp

### Rule 1: Không phải PRODUCTION → không tracking revenue

Không gửi bất kỳ revenue event nào (cả `iap_revenue` custom event lẫn `TrackIAPRevenue` cho Firebase/Adjust) khi build không có symbol PRODUCTION.

**Chỗ sửa**: `IAPManager.Purchase()` — wrap tracking calls bằng `#if PRODUCTION`

```
#if PRODUCTION
trackingManager?.Track(new IapRevenueEvent(...));
#endif
```

Và `CompletePurchaseSuccess()` → `TrackIAPRevenue()`:
```
#if PRODUCTION
TrackIAPRevenue(result, source);
#endif
```

### Rule 2: Server validate xác nhận không phải test → mới tracking

Khi PRODUCTION build + server validation enabled:
1. Server validate receipt với Google/Apple API
2. Server kiểm tra `purchaseType` (Google) / `environment` (Apple)
3. Server trả thêm field `isTestPurchase: true/false` trong response
4. Client nhận response → nếu `isTestPurchase = false` → mới gửi revenue tracking

**Flow mới**:
```
Store confirm → Server validate
  → isTestPurchase = true  → grant reward, SKIP tracking
  → isTestPurchase = false → grant reward, SEND tracking
  → Server validation off  → dùng Rule 1 (#if PRODUCTION)
```

**Chỗ sửa**:

Server (`firebase-functions/functions/validators/google-play.js`):
- Đọc `purchaseType` từ Google API response
- Return thêm `isTestPurchase: purchaseType !== undefined` (field tồn tại = test)

Server (`firebase-functions/functions/validators/apple.js`):
- Đọc `environment` từ Apple API response
- Return thêm `isTestPurchase: environment === "Sandbox"`

Server (`firebase-functions/functions/index.js`):
- Pass `isTestPurchase` vào response JSON

Client (`ServerReceiptValidator.cs`):
- Thêm `isTestPurchase` vào `ValidationResponse`

Client (`IAPManager.cs`):
- `ReceiptValidationResult` thêm field `IsTestPurchase`
- Trong purchase flow: chỉ gọi tracking khi `!validation.IsTestPurchase`

### Rule 3: Thêm params cho `iap_revenue` event

Thêm 2 fields:
- `purchase_currency` (string) — currency gốc từ store (e.g. "VND", "THB", "USD")
- `iap_revenue_origin_micro` (int) — giá trị revenue theo currency gốc * 1,000,000

**Chỗ sửa**:

`TrackingConstants.cs`:
```csharp
public const string PAR_PURCHASE_CURRENCY = "purchase_currency";
public const string PAR_IAP_REVENUE_ORIGIN_MICRO = "iap_revenue_origin_micro";
```

`IapRevenueEvent` — thêm 2 constructor params + BuildParams:
```csharp
public IapRevenueEvent(string productId, int iapRevenueMicro,
    string purchaseCurrency, int iapRevenueOriginMicro,
    string purchaseStatus, ...)
```

`IAPManager.Purchase()` — truyền currency + origin micro:
```csharp
string currency = productInfo.Value.CurrencyCode ?? "USD";
int originMicro = (int)(productInfo.Value.PriceDecimal * 1_000_000);

trackingManager?.Track(new IapRevenueEvent(
    productId, revenueMicro, currency, originMicro,
    status, failReason, resultCode, reason));
```

Lưu ý: `iap_revenue_micro` hiện tại đang gửi giá trị theo currency gốc (PriceDecimal từ store).
Cần xác nhận: `iap_revenue_micro` nên giữ nguyên là giá gốc hay convert sang USD?
Đề xuất: `iap_revenue_micro` = giá gốc (giữ nguyên), `iap_revenue_origin_micro` = cùng giá trị nhưng tên rõ ràng hơn.

---

## Test matrix

| Build | Server validation | Account | Revenue tracking |
|-------|------------------|---------|-----------------|
| Editor | Off | N/A | SKIP (not PRODUCTION) |
| Dev | Off | N/A | SKIP (not PRODUCTION) |
| Dev | On | Test | SKIP (not PRODUCTION) |
| **Production** | Off | Any | **SKIP** (no server info, fail-safe) |
| **Production** | On | Test (purchaseType=0) | SKIP (isTestPurchase=true) |
| **Production** | On | Promo (purchaseType=1) | SKIP (isTestPurchase=true) |
| **Production** | On | Rewarded (purchaseType=2) | SKIP (isTestPurchase=true) |
| **Production** | On | Real (purchaseType absent) | **SEND** |

---

## Files cần sửa

| File | Thay đổi |
|------|---------|
| `com.archerstudio.sdk.tracking/Runtime/Core/TrackingConstants.cs` | Thêm 2 param constants |
| `com.archerstudio.sdk.tracking/Runtime/Events/PurchaseEvents.cs` | Thêm 2 fields vào IapRevenueEvent |
| `com.archerstudio.sdk.iap/Runtime/Core/IAPManager.cs` | #if PRODUCTION guard + isTestPurchase check |
| `com.archerstudio.sdk.iap/Runtime/Providers/ServerReceiptValidator.cs` | ValidationResponse thêm isTestPurchase |
| `com.archerstudio.sdk.iap/Runtime/Models/ReceiptValidationResult.cs` | Thêm IsTestPurchase field |
| `firebase-functions/functions/validators/google-play.js` | Return purchaseType + isTestPurchase |
| `firebase-functions/functions/validators/apple.js` | Return environment + isTestPurchase |
| `firebase-functions/functions/index.js` | Pass isTestPurchase trong response |
