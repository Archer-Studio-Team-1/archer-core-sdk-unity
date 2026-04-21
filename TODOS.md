# TODOS — archer-core-sdk-unity

Items được defer khỏi scope hiện tại. Mỗi item có context đủ để pick up trong 3 tháng.

---

## LoginTrackingEvent — analytics GPGS adoption rate

**What:** Subscribe `LoginSucceededEvent` / `LoginFailedEvent` từ SDKEventBus, track event vào analytics (Firebase/Adjust).

**Why:** Sau khi login Phase 1 ship, cần biết % player có GPGS và login thành công để quyết định khi nào invest vào cloud save. Nếu adoption rate thấp (<20%), cloud save sẽ không ROI.

**Context:** Deferred từ CEO plan (2026-04-21, branch feat/login). `LoginModule` Phase 1 phải ship trước. `TrackingManager` và `SDKEventBus` đều có sẵn — chỉ cần subscribe events và gọi `TrackingManager.Instance.Track(new LoginEvent(...))`. Tương tự `IapRevenueEvent` trong `IAPManager.cs`.

**Pros:** Visibility ngay vào GPGS adoption, không cần rework `LoginModule`.
**Cons:** Thêm tracking dependency vào login package (hoặc cần game layer tự subscribe).

**Depends on:** `com.archerstudio.sdk.login` Phase 1 done và stable.

---

## LoginResult error mapping — user-facing messages

**What:** Phase 1 dùng `LoginErrorCode` enum trong `LoginResult`. Phase 2 khi có màn hình "Kết nối tài khoản" sẽ cần map error code ra user-facing message.

**Why:** Player tap "Kết nối tài khoản", GPGS fail vì Google Play Games app chưa cài — player nên thấy "Vui lòng cài Google Play Games" thay vì silent fail.

**Context:** `LoginErrorCode` đã định nghĩa (`NotInstalled`, `NotSignedIn`, `ConfigError`). Cần thêm mapping layer ở game layer (không phải SDK). Không block Phase 1.

**Depends on:** `com.archerstudio.sdk.login` Phase 1, UI login screen (Phase 2).
