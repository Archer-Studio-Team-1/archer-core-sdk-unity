# ⚠ DEPRECATED — com.archerstudio.sdk.cloudsave

**Status:** Maintenance-only. Use `com.archerstudio.sdk.firestore` for new games.

**Last reviewed:** 2026-05-26

## Why deprecated

CloudSave stores game data as a single opaque JSON blob per slot at
`/saves/{firebaseUid}/slots/{slotKey}`. This worked for the original use case
("backup my save file across devices") but is unsuitable for what Archer Studio
games now need:

| Capability | CloudSave | Firestore SDK |
|------------|-----------|---------------|
| Cross-device sync | ✓ | ✓ |
| Per-field anti-cheat (currency, IAP) | ✗ | ✓ (Cloud Functions T0) |
| Customer-support queries (find user, view balance) | ✗ (opaque blob) | ✓ (structured + indexed) |
| Real-time UI listeners | ✗ | ✓ |
| Audit trail for grants/refunds | ✗ | ✓ |
| Schema evolution per feature | ✗ | ✓ (per-feature `schemaVersion`) |
| Server-authoritative writes | ✗ | ✓ |

The Firestore SDK package is a superset.

## What stays the same

- Firebase Auth bootstrap (GPGS server auth code → Firebase Auth). Both packages
  use the same flow; the Firestore module reuses the auth state established by
  either CloudSave or its own bootstrap path.
- Underlying storage layer is still Firestore — only the schema and API change.

## Migration path for games still on CloudSave

1. Add `com.archerstudio.sdk.firestore` to `Packages/manifest.json`.
2. Create `Resources/FirestoreConfig.asset` with the same `WebClientId` value used by CloudSave.
3. Set `SDKCoreConfig.EnableCloudSave = false`.
4. Implement per-feature migration in your game's save layer:
   - For each blob slot you used to write, map it to one feature subdoc under
     `users/{uid}/saves/{feature}` (see `firebase-functions/games/idk/save-features.yaml`
     for an example).
   - Server-authoritative data (currencies, IAP) moves under
     `users/{uid}/private/state` and is mutated via `mutateResource` Cloud Function.
5. Once migration scripts have run for active users, remove `com.archerstudio.sdk.cloudsave`
   from `Packages/manifest.json`.

See `com.archerstudio.sdk.firestore/README.md` for full SDK usage.

## What this package still ships

- `ICloudSaveProvider` interface + `CloudSaveModule` lifecycle
- `FirestoreCloudSaveProvider` (blob-per-slot) + `StubCloudSaveProvider`
- Offline cache via PlayerPrefs with timestamp/dirty conflict detection

No bug fixes or new features will be added unless an existing game needs them.

## Removal timeline

- 2026-05-26: Marked deprecated. IDK no longer depends on it.
- 2026-Q4 (proposed): Stop accepting new dependencies. Existing games keep it.
- 2027 (proposed): Archive package after no game depends on it.
