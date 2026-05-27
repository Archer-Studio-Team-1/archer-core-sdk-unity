# Troubleshooting

Common errors when integrating the Firestore SDK. Each entry follows the
pattern **symptom → root cause → fix**.

## Auth & sign-in

### `"An internal error has occurred."` from `SignInAnonymouslyAsync`

**Symptom (logs):**
```
[Firestore] Firebase Auth sign-in failed: One or more errors occurred. (An internal error has occurred.)
```

**Root cause:** You're testing in **Unity Editor Play Mode**. Firebase Auth Unity
SDK does not actually call the backend in Editor — it returns a generic
"internal error". Confirmed via REST: anonymous sign-up works perfectly through
curl against the same project's API key.

**Fix:** Build an Android device APK and test on the device. The Editor is only
useful for compile + NUnit tests + bootstrap inspection.

If you must test in Editor: spin up Firebase Auth Emulator (`firebase emulators:start --only auth`)
and point Unity to localhost. Not officially supported by this SDK.

---

### `Service available: False` + `Firebase UID: (null)` even on device

**Possible causes (logs to look for):**

| Log | Cause | Fix |
|-----|-------|-----|
| `Firebase dependencies unavailable: ...` | Firebase plugin not initialized | Make sure `com.google.firebase.app` is in manifest and Plugin Importer has Android target enabled |
| `No GPGS server auth code — signing into Firebase Auth anonymously.` followed by error | Anonymous Auth not enabled in Firebase Console | Console → Authentication → Sign-in method → enable Anonymous |
| `FirestoreConfig not found in Resources/. Using stub provider.` | Missing config asset | Create `Assets/Resources/FirestoreConfig.asset` via menu |
| No `[Firestore]` logs at all | Module not registered | Verify package is in `manifest.json` and Unity recompiled |

---

### Anonymous user UID changes on every launch

**Root cause:** Firebase Auth persistence not enabled, or device cleared app data.

**Fix:** Anonymous accounts persist by default. If they don't:
- Verify `Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser` is checked on
  app start (the SDK does this).
- On Android: app uninstall + reinstall always assigns a new anonymous UID. To
  preserve the account across reinstalls, link to Google/Facebook/Apple.

---

## Firestore reads / writes

### `PermissionDenied` from `SaveFeatureAsync`

**Cause:** Firestore rules rejected the write. Common reasons:
1. Field count or list size exceeds caps declared in the rules.
2. Field types don't match (e.g. sending `int` where rules expect `string`).
3. Schema version downgrade attempt.

**Fix:** Check Firebase Console → Firestore → Rules → Playground. Paste the
exact doc shape your client sent. Rules will tell you which clause denied it.

---

### `NotFound` after a successful write

**Cause:** Firestore is eventually consistent. A read-immediately-after-write
may miss the new doc.

**Fix:** Wait ~1s before re-reading, or use a listener which receives the local
echo of the write before the server confirms.

---

### `Unavailable` / `NetworkError` on `CallFunctionAsync`

Callable functions are invoked through `CallableHttpClient` over plain HTTPS;
no `com.google.firebase.functions` Unity package is involved. Common causes
when the call fails:

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `Unavailable: Firebase project id unavailable` | `FirebaseApp.DefaultInstance.Options.ProjectId` is null | Ensure `google-services.json` is bundled and `FirebaseApp.CheckAndFixDependenciesAsync` ran |
| `NotAuthenticated: no firebase user` | `FirebaseAuth.DefaultInstance.CurrentUser` is null | Confirm Auth completed (Editor cannot complete Auth — test on device) |
| `NetworkError: connection error: ...` | Device offline or DNS failure | Retry; SDK does not auto-retry transport errors |
| `NotAuthenticated: id token fetch failed` | Token refresh failed (e.g. clock skew) | Reboot device / re-sign-in |
| HTTP 404 from `{region}-{projectId}.cloudfunctions.net/{name}` | Function not deployed in that region | Match `FirestoreConfig.FunctionsRegion` to the region the function was deployed to |

---

### Function returns "FAILED_PRECONDITION: App Check required"

**Cause:** App Check enforcement is ON for the Function but the device didn't
produce a valid attestation token.

**Fix:** During development, switch the service to UNENFORCED in Firebase Console
→ App Check → APIs. For real users on PROD, ensure:
- The release keystore SHA-256 is registered in Firebase Console → Android app → SHA fingerprints
- Play Integrity API is enabled in GCP Console for the project
- Device has Google Play Services + clock not skewed

---

## Migration layer (game-specific, e.g. IDK CloudSync)

### `MigrationRunner: mode=Off, skipping all features.`

**Cause:** Remote Config `cloud_sync_mode = off` (default). Expected behaviour
until you flip the mode.

**Fix:** Firebase Console → Remote Config → set `cloud_sync_mode = shadow` →
Publish. Restart the app or wait for `OnApplicationFocus` refresh.

---

### Migration fires but cloud doc never appears

**Cause:** Mode is `shadow` (cloud writes fire-and-forget) AND the write silently
errored. The runner records `cloud_write_failed` in the local state file.

**Fix:** Read `Application.persistentDataPath/cloud_sync_state.json` and inspect
`LastError`. Common values:
- `bridge_write_failed` — Firestore returned an error (check rules)
- `verify_checksum_mismatch` — wrote OK but read-back differs (polymorphism bug?)
- `backup_upload_failed` — Storage Function rejected (cap, auth)

---

### `Tools / IDK / Cloud Sync / Show Status` shows `Registry count: 0`

**Cause:** `RegisterPilotMigrators` was never called.

**Fix:** Verify your boot flow calls
`CloudSyncBootstrap.Instance.RegisterPilotMigrators(saveLoadManager)` AFTER
`saveLoadManager` is available but BEFORE `StartMigration()`.

---

## Polymorphism & JSON

### Stage / Forge subclass info lost after round-trip

**Symptom:** Local `AbilityDurationData` becomes generic `AbilityBaseData` after
load.

**Root cause:** The base class doesn't carry a discriminator field that the
PolymorphicJsonConverter can preserve. Newtonsoft `$type` is not used here
(security risk — leaks .NET type names).

**Fix:** Add an explicit string discriminator field on the polymorphic base:

```csharp
public abstract class AbilityBaseData {
    public string _kind;  // "duration", "permanent", etc.
}
```

Then write your local deserializer to switch on `_kind`. Marked as action item
MA2 in the IDK migration map.

---

### `verify_checksum_mismatch` for one feature only

**Cause:** That feature's local→cloud round-trip is non-deterministic
(unsorted Dict iteration, polymorphic subclass loss, timestamp drift).

**Fix:** Check the migrator's `WriteLocal` path — it should produce identical
JSON when called twice. Run the SDK `PolymorphicJsonConverterTests` against
the suspect shape.

---

## Cost & quota

### Daily Firestore cost spikes overnight

**Likely cause:** A snapshot listener was left attached when the user logged out
or scene unloaded. Listeners cost reads continuously even when offline.

**Fix:** Audit every `Listen(...)` call site. Ensure `IDisposable` returned by
listener is `Dispose()`d in `OnDestroy` / `OnDisable`. Use the SDK debug menu
to inspect active listeners (Phase 6 enhancement).

---

### Function timeout (`60s default` exceeded)

**Cause:** A migration / batch operation is too large.

**Fix:** Server-side, increase timeout to 5 min via `runWith({ timeoutSeconds: 300 })`.
Client-side, paginate the work — e.g. migrate IAP history in batches of 500
transactions instead of all at once.

---

## SDK module lifecycle

### `FirestoreModule.Instance == null` in scene code

**Cause:** Module hasn't initialized yet (still in `Initializing` state).

**Fix:** Wait for `SDKEvents.ModuleInitializedEvent` or check `module.State == ModuleState.Ready`.

```csharp
ArcherStudio.SDK.Core.SDKEventBus.Subscribe<ArcherStudio.SDK.Core.ModuleInitializedEvent>(evt => {
    if (evt.ModuleId == "firestore") {
        // module is ready
    }
});
```

---

### Module shows `Ready` but reports `Service.IsAvailable = False` forever

**Cause:** Auth never completed (see Auth section above).

**Fix:** Check device logs for `[Firestore] Firebase Auth ready.` log line. If
absent, debug the auth handshake. If present but `IsAvailable` still false,
the SDK reset `_auth` reference somehow — file a bug with the trace.

---

## Reporting bugs

If you see a symptom not covered here:

1. Capture `adb logcat -s Unity` for the boot flow (or Editor Console for §4.1-§4.3 only)
2. Note Firebase Unity SDK version (`Packages/com.google.firebase.app-*.tgz`)
3. Note `FirestoreConfig` values (no secrets — just flags)
4. Open an issue with reproducer steps. Do NOT paste API keys, project IDs,
   or user UIDs in public issues.
