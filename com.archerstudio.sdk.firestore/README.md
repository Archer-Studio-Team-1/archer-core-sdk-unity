# Archer Studio SDK — Firestore

Game-agnostic Firestore data plane. Provides server-authoritative currency,
structured user save documents, IAP catalog reads, and one-shot RPC bundles
for cold launches. Pairs with the server-side `firestore-core` Functions
codebase.

> **Status:** v0.1.0 — DEV soak in progress.
> **Replaces:** `com.archerstudio.sdk.cloudsave` (deprecated — see that package's `DEPRECATED.md`).

## Capabilities

- **Anonymous-first Firebase Auth** with automatic upgrade to social credential when GPGS / Apple Sign-In completes
- **Per-feature save read/write** via `ISaveRepository` (`users/{uid}/saves/{name}`)
- **T0 currency mutations** server-authoritative via `mutateResource` Function with idempotency + audit
- **Single-RPC user bundle** for cold launches (`PrepareUserBundleAsync`)
- **IAP catalog** cached client-side with TTL
- **Real-time listeners** on `private/state` for currency UI sync
- **Cloud Storage backup uploads** for the migration runner (immutable archive)
- **Polymorphism-safe JSON** via discriminator field (no Newtonsoft `$type` leakage)
- **Stable checksums** via sorted-key normalized JSON
- **Stub provider fallback** when Firebase backend or sign-in is unavailable

## Dependencies

- `com.archerstudio.sdk.core` ≥ 1.1.0
- `com.archerstudio.sdk.login` ≥ 1.1.0
- `com.archerstudio.sdk.appcheck` ≥ 1.0.0
- Firebase Unity SDK packages required at runtime:
  - `com.google.firebase.app`
  - `com.google.firebase.auth`
  - `com.google.firebase.firestore`

> Cloud Functions are invoked via plain HTTPS through `CallableHttpClient` —
> `com.google.firebase.functions` Unity package is **not** required.

## Quick start

1. Add to `Packages/manifest.json` (replace `<org>` + `<ref>` with your fork/tag):
   ```json
   "com.archerstudio.sdk.firestore": "https://github.com/<org>/archer-core-sdk-unity.git?path=com.archerstudio.sdk.firestore#<ref>"
   ```
2. Create `Assets/Resources/FirestoreConfig.asset` via
   `Create / ArcherStudio / SDK / Firestore Config`.
3. Set `WebClientId` (same value game uses for Login / CloudSave), keep
   `FunctionsRegion = asia-southeast1` (or your project region).
4. Server-side: seed `_config/save_features_registry` + `iap_catalog/*` for your
   Firebase project. See `Documentation~/INTEGRATION.md`.
5. Validate via menu `ArcherStudio / SDK / Firestore / Validate Setup`.
6. From your boot flow, after Login + SaveLoad are ready:
   ```csharp
   var module = FirestoreModule.Instance;
   module.UserRepository.CreateUserProfileAsync(_ => { });
   ```

> **Editor limitation:** Firebase Auth Unity SDK does not complete sign-in in
> Unity Editor Play Mode. Build an Android device APK for end-to-end testing.
> See `Documentation~/TROUBLESHOOTING.md`.

## Common operations

```csharp
using ArcherStudio.SDK.Firestore;

// Read all save subdocs in one RPC (cold launch)
module.UserRepository.PrepareUserBundleAsync(result => {
    if (!result.Success) { /* handle */ return; }
    var stage = result.Data.Saves["stage"];        // or null if never written
    var currencies = result.Data.Private?["currencies"];
});

// Spend currency server-authoritatively
module.UserRepository.MutateResourceAsync(new ResourceMutationRequest {
    Deltas = new Dictionary<string, string> { { "gem", "-50" } },
    Reason = "upgrade_blacksmith",
    Type   = "gameplay_spend",
    ClientTxnId = "ulid-here",         // optional idempotency key
}, result => {
    if (result.Success) {
        var newGem = result.Data.NewBalance["gem"];   // string big-int
    }
});

// Subscribe to real-time currency UI
var sub = module.UserRepository.ListenPrivateState(snap => {
    foreach (var kv in snap.Currencies) { /* update HUD */ }
});
// IMPORTANT: dispose when leaving the scene
sub.Dispose();

// Direct per-feature save (T1/T2)
module.SaveRepository.SaveFeatureAsync(
    featureName: "stage",
    data: dictionary,
    schemaVersion: 1,
    onComplete: r => { /* r.Success */ });
```

## Documentation

- `Documentation~/INTEGRATION.md` — full integration guide (server seeding, boot wiring, IAP path)
- `Documentation~/TROUBLESHOOTING.md` — common errors + fixes
- `Documentation~/API.md` — per-type API reference
- `CHANGELOG.md` — version history

## Tests

NUnit EditMode tests under `Tests/`:
- `FirestoreModuleTests` — lifecycle + stub provider
- `FirestoreResultTests` — result envelope
- `StubProviderTests` — no-backend behaviour
- `ChecksumHelperTests` — SHA256 stability
- `PolymorphicJsonConverterTests` — discriminator + key sort + numeric coercion
- `SaveRepositoryTests` — path shape + parse via in-memory FakeService

24 tests total. Run via `Window / General / Test Runner` → EditMode.

## License

Internal — Archer Studio.
