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

---

## CloudSave — Write debouncing / rate limiting

**What:** Thêm dirty flag + debounce timer (30s minimum interval per slot) vào `CloudSaveModule`. Flush ngay khi app pause (`AppPauseEvent`) hoặc `Dispose()`.

**Why:** Firestore Spark plan = 20K writes/day. Với 10K DAU x 3 saves/phiên = 30K writes/day — vượt quota. Debouncing giảm 10x+. Không có debouncing cũng tạo unnecessary Firestore cost ở Blaze plan.

**Context:** Deferred từ Eng Review (2026-04-22, branch main). `CloudSaveModule` hiện dùng `_isSaving` queue để serialize saves — debouncing là layer trên: thay vì write ngay, set dirty flag + start timer, flush sau 30s hoặc khi app pause. Requires `AppPauseEvent` trong `SDKEvents.cs` + relay trong `SDKBootstrap.OnApplicationPause`. `CloudSaveModule` subscribe `AppPauseEvent`, flush pending dirty write trước khi app background.

**Pros:** Giảm Firestore cost ~10x. Player không mất data khi app bị kill (flush on pause). Scalable cho auto-save pattern.
**Cons:** Save delay tối đa 30s — nếu app crash trong window đó, pending write bị mất. Acceptable vì game đã lưu local song song.

**Depends on:** `com.archerstudio.sdk.cloudsave` v1.0.0 ship, IDK game save structure decision (để biết dirty tracking granularity cần thiết).
