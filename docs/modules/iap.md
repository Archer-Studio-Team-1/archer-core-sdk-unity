# Module: com.archerstudio.sdk.iap

In-App Purchase manager bọc Unity IAP v5 (StoreController API). Hỗ trợ consumable, non-consumable, **subscription** (v1.0.1+), receipt validation, revenue tracking.

**Version**: `1.0.1` · **Deps**: `core`, `consent`, `tracking` · **Priority**: 50.

---

## 1. Public API

```csharp
IAPManager.Instance.Purchase(productId, source, reason, onComplete);
IAPManager.Instance.RestorePurchases(onComplete);                 // iOS
IAPManager.Instance.GetProducts();                                 // IReadOnlyList<ProductInfo>
IAPManager.Instance.GetProduct(productId);                         // ProductInfo?
IAPManager.Instance.GetSubscriptionInfo(productId);                // SubscriptionInfo?
IAPManager.Instance.IsSubscribed(productId);                       // bool
IAPManager.Instance.OpenSubscriptionManagement();                  // deeplink store
IAPManager.Instance.SetReceiptValidator(customValidator);

IAPManager.OnPurchaseCompleted += result => { /* PurchaseResult */ };
```

## 2. Provider pattern

```csharp
public interface IIAPProvider {
    void Initialize(IAPConfig config, Action<bool> onComplete);
    void Purchase(string productId, Action<PurchaseResult> onComplete);
    void RestorePurchases(Action<bool> onComplete);
    ProductInfo? GetProduct(string productId);
    SubscriptionInfo? GetSubscriptionInfo(string productId);
}
```

| Provider | Symbol |
|---|---|
| `UnityIAPProvider` | `HAS_UNITY_IAP` |
| `StubIAPProvider` | (fallback) |

`UnityIAPProvider` dùng Unity IAP v5 event-based StoreController. Subscription data lấy từ `FetchPurchases` Orders + `OnPurchasePending`/`OnPurchaseConfirmed`; map vào `SubscriptionInfo` (7 bool flags + 4 DateTime + `SubscriptionPeriod` ISO 8601).

## 3. Module class

`IAPManager : ISDKModule`
- `ModuleId = "iap"`, priority 50, deps `["consent", "tracking"]`.
- Graceful degradation: nếu `HAS_UNITY_IAP` không set hoặc config missing → return `Ready` với `StubIAPProvider` (không throw).
- Publish `PurchaseCompletedEvent` qua `SDKEventBus`.
- Gọi `TrackingManager.Track(new IapRevenueEvent(...))` + `TrackIAPRevenue(...)`.

## 4. Events emitted

- **SDKEventBus**: `PurchaseCompletedEvent`.
- **TrackingManager**: `IapRevenueEvent` (productId, revenueMicro, status, failReason, resultCode, reason).
- Map `PurchaseFailureReason` → BillingResponseCode string (`USER_CANCELED`, `ITEM_UNAVAILABLE`, `SERVICE_UNAVAILABLE`, …).

## 5. Config: `IAPConfig`

| Field | Ý nghĩa |
|---|---|
| `Products` | `List<IAPProductDefinition>`. |
| `EnableReceiptValidation` | bật validator. |
| `ValidationServerUrl` | endpoint custom validator. |

### `IAPProductDefinition`
| Field | Ý nghĩa |
|---|---|
| `ProductId` | canonical id (game code dùng). |
| `Type` | `Consumable` / `NonConsumable` / `Subscription`. |
| `GooglePlayStoreId`, `AppleAppStoreId` | id vendor (platform-specific). |
| `StoreSpecificId` | property tính toán theo platform runtime. |

Menu: `Assets > Create > ArcherStudio > SDK > IAP Config`.

## 6. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/Core/IAPManager.cs` | Lifecycle, purchase flow, subscription query, revenue dispatch. |
| `Runtime/Core/IAPCoroutineRunner.cs` | Timeout + retry helper. |
| `Runtime/Interfaces/IIAPProvider.cs` | Contract. |
| `Runtime/Interfaces/IReceiptValidator.cs` | Custom validation hook. |
| `Runtime/Providers/UnityIAPProvider.cs` | Unity IAP v5 wrapper (subscription-aware, generation callback protection, deferred purchase). |
| `Runtime/Providers/StubIAPProvider.cs` | Fallback. |
| `Runtime/Models/IAPModels.cs` | ProductType, PurchaseFailureReason, ProductInfo, PurchaseResult, ReceiptValidationResult, **SubscriptionInfo**. |
| `Runtime/Config/IAPConfig.cs` | SO + IAPProductDefinition. |
| `Runtime/IAPModuleRegistrar.cs` | Auto-register. |

## 7. Revenue flow

```
Unity IAP native: OnPurchaseConfirmed(order)
       │
       ▼ (main thread via dispatcher)
UnityIAPProvider → IAPManager.OnPurchaseSuccess
       │
       ├─ Extract revenue = ProductInfo.PriceDecimal + currency
       ├─ Publish PurchaseCompletedEvent
       ├─ TrackingManager.Track(new IapRevenueEvent(...))
       └─ TrackingManager.TrackIAPRevenue(
               productId, revenue, currency, txnId, receipt, source)
                 ├── Firebase:  in_app_purchase
                 └── Adjust:    verify + track revenue (VerifyAndTrack*Purchase)
```

## 8. Subscription flow (v1.0.1)

- `FetchPurchases` trả về orders; provider maintain `_activeSubscriptions` HashSet.
- `OnPurchasePending` / `OnPurchaseConfirmed` refresh entry.
- `SubscriptionInfo` expose: `IsSubscribed`, `ExpirationDate`, `RemainingTime`, `SubscriptionPeriod`, `IsAutoRenewing`, `IsCancelled`, `IsFreeTrial`, `IntroductoryPrice`, `OriginalTransactionId`.
- `OpenSubscriptionManagement` → deeplink:
  - iOS: `itms-apps://apps.apple.com/account/subscriptions`
  - Android: `https://play.google.com/store/account/subscriptions?sku={productId}&package={pkg}`

## 9. Editor tooling

- Config SO creation menu.
- (Editor/ trống.)

## 10. Chú ý khi sửa

- **Provider callback generation**: UnityIAPProvider có generation ID — nếu user re-init, callback cũ bị ignore. Đừng bỏ guard này.
- **Deferred purchase** (parental approval Android): provider giữ `OnPurchasePending` state, fire `OnPurchaseConfirmed` khi parent approve.
- **Receipt validator**: nếu set `EnableReceiptValidation=true` mà không `SetReceiptValidator(...)` → mặc định local receipt validator của Unity IAP (đủ cho basic, không chống replay).
- **Subscription period format**: ISO 8601 (`P1W`, `P1M`, `P1Y`) — parse từ Google/Apple khác nhau, util đã chuẩn hóa.
