# Module: com.archerstudio.sdk.cloudsave

Backup và sync game progress lên Firestore, gắn với Google account qua GPGS login. Khi user cài lại app hoặc đổi máy, data được khôi phục từ cloud.

**Version**: `1.0.0` · **Deps**: `core 1.1.0`, `login 1.1.0` · **Priority**: 50.

---

## 1. Public API

```csharp
CloudSaveModule.Instance.SaveAsync("slot_main", jsonString, onComplete);
CloudSaveModule.Instance.LoadAsync("slot_main", onComplete);
CloudSaveModule.Instance.DeleteAsync("slot_main", onComplete);
// onComplete: Action<CloudSaveResult>
```

`CloudSaveResult` fields:

| Field | Kiểu | Ý nghĩa |
|---|---|---|
| `Success` | `bool` | true nếu op thành công |
| `Data` | `string` | cloud data (hoặc local data nếu LocalOnly) |
| `LocalData` | `string` | local dirty data, chỉ set khi `HasConflict=true` |
| `HasConflict` | `bool` | cloud mới hơn local AND local có unsaved changes |
| `ServerTimestamp` | `DateTime` | timestamp của cloud write |
| `ErrorCode` | `CloudSaveErrorCode` | None / NotAuthenticated / NetworkError / DataTooLarge / NotFound / ProviderError |

## 2. Provider pattern

```csharp
public interface ICloudSaveProvider {
    void InitAsync(Action<bool> onComplete);
    void SaveAsync(string slotKey, string jsonData, Action<CloudSaveResult> onComplete);
    void LoadAsync(string slotKey, Action<CloudSaveResult> onComplete);
    void DeleteAsync(string slotKey, Action<CloudSaveResult> onComplete);
}
```

| Provider | Symbol | Mô tả |
|---|---|---|
| `FirestoreCloudSaveProvider` | `HAS_FIREBASE_FIRESTORE` + `HAS_GPGS` + Android/Editor | Firebase Firestore với GPGS auth. |
| `StubCloudSaveProvider` | luôn có | In-memory dictionary, không persist — fallback khi Firestore không khả dụng. |

## 3. Module class

`CloudSaveModule : ISDKModule`
- `ModuleId = "cloudsave"`, priority 50, deps `["login"]`.
- Init: lấy `serverAuthCode` từ `LoginModule.Provider.GetServerSideAccessCode(webClientId)` → `FirestoreCloudSaveProvider(serverAuthCode).InitAsync(...)`.
- Fallback chain: `EnableCloudSave=false` → stub; `CloudSaveConfig null` → stub; user chưa login GPGS → stub; `GetServerSideAccessCode` fail → stub; Firestore init fail → stub.
- Subscribe `AppPauseEvent` để flush pending writes (v1.1).

## 4. Events (SDKEventBus)

| Event | Payload |
|---|---|
| `CloudSaveSyncedEvent` | `SlotKey`, `HasConflict` — fire sau SaveAsync thành công. |
| `CloudSaveFailedEvent` | `SlotKey`, `CloudSaveErrorCode` — fire khi op thất bại. |

## 5. Config: `CloudSaveConfig` (ScriptableObject)

| Field | Ý nghĩa |
|---|---|
| `WebClientId` | Web Client ID từ Firebase Console > Authentication > Google Play Games. |

Menu: `Assets > Create > ArcherStudio > SDK > Cloud Save Config`. Đặt file tại `Resources/CloudSaveConfig.asset`.

## 6. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/Core/CloudSaveModule.cs` | Lifecycle, fallback chain, event publish. |
| `Runtime/Interfaces/ICloudSaveProvider.cs` | Contract. |
| `Runtime/Providers/FirestoreCloudSaveProvider.cs` | Firestore + GPGS auth, conflict detection. |
| `Runtime/Providers/StubCloudSaveProvider.cs` | In-memory fallback. |
| `Runtime/Models/CloudSaveResult.cs` | `CloudSaveResult` readonly struct, 4 factory methods. |
| `Runtime/Events/CloudSaveEvents.cs` | `CloudSaveSyncedEvent`, `CloudSaveFailedEvent`. |
| `Runtime/Config/CloudSaveConfig.cs` | SO config. |
| `Runtime/CloudSaveModuleRegistrar.cs` | Auto-register. |
| `Samples~/FirebaseRules.txt` | Firestore security rules — deploy với `firebase deploy --only firestore:rules`. |

## 7. Auth flow

```
InitializeAsync
  └─ LoginModule.Provider.GetServerSideAccessCode(webClientId)
       └─ PlayGamesPlatform.Instance.RequestServerSideAccess(requestOfflineAccess: false)
            └─ serverAuthCode → PlayGamesAuthProvider.GetCredential(serverAuthCode)
                 └─ FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential)
                      └─ firebaseUid → _db = FirebaseFirestore.DefaultInstance
                           → path: /saves/{firebaseUid}/slots/{slotKey}
```

**Không dùng Anonymous Auth** — GPGS credential → Firebase UID stable per Google account, không tạo orphan UID khi reinstall.

## 8. Conflict detection

Mỗi slot lưu metadata trong PlayerPrefs:

| Key | Loại | Ý nghĩa |
|---|---|---|
| `cloudsave_{slot}_dirty` | int (0/1) | local có unsaved changes? |
| `cloudsave_{slot}_ts` | string (binary long) | UTC ticks của lần sync cuối |
| `cloudsave_{slot}_data` | string | local data cache |

Logic `LoadAsync`:
- `cloud > local && isDirty` → `CloudSaveResult.WithConflict(cloudData, localData)` — game tự resolve.
- `cloud > local && !isDirty` → `CloudSaveResult.Succeeded(cloudData)` — overwrite local.
- Else → `CloudSaveResult.LocalOnly(localData)` — cloud không có data hoặc local mới hơn.

`SaveAsync` validate `json.Length < 900_000` (Firestore doc limit 1 MiB). Sau write, read-after-write để lấy `ServerTimestamp` chính xác từ server.

## 9. Firestore schema

```
/saves/{firebaseUid}/slots/{slotKey}
  → data:       string     (game JSON)
  → updatedAt:  Timestamp  (ServerTimestamp)
  → appVersion: string
```

Security rule (xem `Samples~/FirebaseRules.txt`):
```js
match /saves/{userId}/slots/{slotKey} {
  allow read, write: if request.auth != null && request.auth.uid == userId;
}
```

## 10. Scripting define symbols

| Symbol | Nguồn |
|---|---|
| `HAS_FIREBASE_FIRESTORE` | Auto-detect qua `SDKSymbolDetector` (type `Firebase.Firestore.FirebaseFirestore`). |
| `HAS_GPGS` | Từ asmdef `versionDefines` khi package `com.google.play.games` installed. |

## 11. Tests (`Tests/Runtime/`)

- `CloudSaveModuleTests.cs` — identity, init (disabled/ready/never-blocks), SaveAsync guards (not-ready/too-large/stub), LoadAsync, DeleteAsync, Dispose.
- `CloudSaveResultTests.cs` — 4 factory methods.
- `StubCloudSaveProviderTests.cs` — init, save/load roundtrip, not-found, delete.

## 12. Chú ý khi sửa

- **`PlayGamesAuthProvider` namespace**: `Firebase.Auth` (không phải GPGS namespace). Verify khi upgrade Firebase SDK.
- **`RequestServerSideAccess` API**: GPGS plugin v2+ — `requestOfflineAccess: false` cho short-lived token. Xem `GPGS/UPGRADING.txt` trước khi đổi.
- **Read-after-write**: `SaveAsync` gọi `GetSnapshotAsync` sau `SetAsync` để lấy `ServerTimestamp` thực — cần 2 round trip.
- **Stub không persist**: `StubCloudSaveProvider` chỉ tồn tại trong session — dùng để dev/test, không replace local save.
- **Write debouncing**: deferred v1.1 (xem `TODOS.md`). `AppPauseEvent` relay đã có sẵn trong `SDKBootstrap` + `SDKEvents`.
- **GDPR delete**: xóa toàn bộ data user bằng `firebase firestore:delete /saves/{uid} --recursive`.
