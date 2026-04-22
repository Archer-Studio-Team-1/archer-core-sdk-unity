# Module: com.archerstudio.sdk.push

Firebase Cloud Messaging wrapper cho push notification.

**Version**: `1.0.0` · **Deps**: `core` · **Priority**: 60.

---

## 1. Public API

```csharp
PushManager.Instance.RequestPermission(onResult);            // iOS 13+ prompt
PushManager.Instance.GetToken(onToken);                       // FCM token
PushManager.Instance.SubscribeToTopic("promo");
PushManager.Instance.UnsubscribeFromTopic("promo");

PushManager.OnMessageReceived += msg => { /* PushMessage */ };
PushManager.OnTokenRefreshed  += token => { /* string */ };
```

## 2. Provider pattern

```csharp
public interface IPushProvider {
    void Initialize(PushConfig config, Action<bool> onComplete);
    void RequestPermission(Action<bool> onResult);
    void GetToken(Action<string> onToken);
    void SubscribeToTopic(string topic);
    void UnsubscribeFromTopic(string topic);
    event Action<PushMessage> OnMessageReceived;
    event Action<string> OnTokenRefreshed;
}
```

| Provider | Symbol |
|---|---|
| `FirebasePushProvider` | `HAS_FIREBASE_MESSAGING` |
| `StubPushProvider` | (fallback) |

## 3. Module class

`PushManager : ISDKModule`
- `ModuleId = "push"`, priority 60.
- Init: instantiate provider → auto-subscribe topic mặc định (nếu config bật).
- Delegate 100% operation xuống provider; manager chỉ routing events.

## 4. Events

- Local delegate: `OnMessageReceived(PushMessage)`, `OnTokenRefreshed(string)`.
- Không publish qua SDKEventBus (dùng delegate vì push thường nhiều listener short-lived).

## 5. Config: `PushConfig`

| Field | Ý nghĩa |
|---|---|
| `AutoRequestPermission` | iOS: auto prompt lúc init. |
| `DefaultTopics` | `string[]` — auto subscribe sau init. |

## 6. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/Core/PushManager.cs` | Lifecycle, topic management. |
| `Runtime/Interfaces/IPushProvider.cs` | Contract. |
| `Runtime/Providers/FirebasePushProvider.cs` | FCM wrapper. |
| `Runtime/Providers/StubPushProvider.cs` | Fallback. |
| `Runtime/Models/PushModels.cs` | `PushMessage` (Title, Body, Data, NotificationId). |

## 7. Platform hooks

- **iOS**: APNs registration qua Firebase. `NSUserNotificationUsageDescription` (nếu cần) trong Info.plist.
- **Android**: FCM registration tự động qua Firebase gradle plugin.
- **Notification channel**: mặc định dùng default channel; tạo channel custom ở game code nếu cần category phân tầng.

## 8. Chú ý khi sửa

- **Token refresh**: FCM có thể refresh bất kỳ lúc nào → game cần resubscribe topic hoặc gửi token lên server trong handler.
- **Background message**: data-only message nhận qua `OnMessageReceived` khi app foreground. Background message xử lý bởi Firebase native service (không vào Unity).
- **iOS permission timing**: không request quá sớm (user chưa hiểu context). Game điều khiển thời điểm gọi `RequestPermission`.
