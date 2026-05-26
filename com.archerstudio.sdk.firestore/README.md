# Archer Studio SDK — Firestore

Game-agnostic Firestore data plane for Archer Studio games. Pairs with the server-side
codebase at `firebase-functions/packages/firestore-core/`.

## Replaces `com.archerstudio.sdk.cloudsave`

This package supersedes the older CloudSave module. New games should depend
only on Firestore. See `com.archerstudio.sdk.cloudsave/DEPRECATED.md` for the
migration path. Both packages can coexist during transition — they share the
same Firebase Auth state.

## Features

- **User profile bootstrap** — idempotent `users/{uid}` doc with unique 6-char playerCode
- **Single-RPC user bundle** — fetch user + private state + all save features in one call
- **T0 currency mutations** — server-authoritative `mutateResource` with audit + idempotency
- **IAP catalog reads** — cached client-side, server-managed
- **Feature registry** — read tier metadata + cloud paths from Firestore (no client hardcode)
- **Real-time listeners** — currency UI updates via private/state snapshots
- **Stub provider fallback** — works in Editor / CI without Firebase backend

## Dependencies

- `com.archerstudio.sdk.core` ≥ 1.1.0
- `com.archerstudio.sdk.login` ≥ 1.1.0 (Firebase Auth via Play Games credential)
- `com.archerstudio.sdk.appcheck` ≥ 1.0.0
- Firebase Unity SDK: `Firebase.App`, `Firebase.Auth`, `Firebase.Firestore`, `Firebase.Functions`

## Setup

1. **Add to manifest.json:**
   ```json
   "com.archerstudio.sdk.firestore": "https://github.com/<org>/archer-core-sdk-unity.git?path=com.archerstudio.sdk.firestore#v0.1.0"
   ```

2. **Create config asset:** Right-click in `Assets/Resources/` →
   `Create / Archer Studio / SDK / Firestore Config`.

3. **Configure:** Set `WebClientId` (same value as `CloudSaveConfig`), keep
   `FunctionsRegion = asia-southeast1`.

4. **Validate:** Menu `Archer Studio / SDK / Firestore / Validate Setup`.

5. **Seed backend config** (server-side, one-time per Firebase project):
   ```bash
   cd firebase-functions
   ./games/<gamename>/scripts/seed-via-rest.sh <projectId>
   ```

## Usage

```csharp
using ArcherStudio.SDK.Firestore;

// After SDK core finished initialization (LoginSucceededEvent fired):
var module = FirestoreModule.Instance;

// 1) Ensure profile exists
module.UserRepository.CreateUserProfileAsync(_ => { });

// 2) Cold-launch fetch
module.UserRepository.PrepareUserBundleAsync(result => {
    if (result.Success) { /* result.Data.Saves["stage"], result.Data.Private["currencies"], etc. */ }
});

// 3) Spend currency (server-validated)
module.UserRepository.MutateResourceAsync(new ResourceMutationRequest {
    Deltas = new Dictionary<string, string> { { "gem", "-50" } },
    Reason = "upgrade_blacksmith",
    Type = "gameplay_spend",
}, result => { /* result.Data.NewBalance */ });

// 4) Real-time currency UI
var sub = module.UserRepository.ListenPrivateState(snap => {
    // snap.Currencies["gem"], snap.VipSubscription, snap.IapEntitlements
});
// sub.Dispose() when leaving the scene
```

## Architecture

```
FirestoreModule (lifecycle, dep: login)
   │
   ├─ IFirestoreService (low-level)
   │    └─ FirestoreServiceProvider (real, Firebase.Firestore)
   │    └─ StubFirestoreServiceProvider (no-op fallback)
   │
   ├─ IUserRepository → UserRepository (callable wrappers)
   ├─ IIapCatalogService → IapCatalogService (cached catalog reads)
   └─ FeatureRegistry (cached _config/save_features_registry reads)

FirebaseAuthBootstrap — bridges Login.GetServerSideAccessCode → PlayGamesAuthProvider
                       → FirebaseAuth.SignInWithCredentialAsync. Falls back to anonymous.
```

## Per-game configuration

The package is game-agnostic. Per-game data lives in Firestore and is seeded once per project:

- `_config/save_features_registry` — list of save features with tier + size caps
- `iap_catalog/{productId}` — IAP product entries with grants

See `firebase-functions/games/idk/scripts/seed-via-rest.sh` for the IDK seed.

## Tests

NUnit tests under `Tests/` cover module lifecycle, stub provider behaviour, and result envelope.

## Status

Phase 4 MVP — interfaces + module + repositories. Not yet integrated with IDK SaveLoad
system. Migration runner + cloud sync bridge land in Phase 5.
