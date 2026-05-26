# Integration Guide

Detailed steps to integrate `com.archerstudio.sdk.firestore` into a game.
Assumes a new Firebase project + a Unity 6000.x game using the Archer SDK
core + login + appcheck modules.

> All examples use placeholders like `<projectId>`, `<package-name>`. Replace
> with your actual values from your team's secure documentation — do NOT
> commit real IDs into public-facing READMEs.

## 1. Backend setup (one-time per Firebase project)

Outside the scope of this SDK package, but the SDK won't work without it. See
the canonical doc in your team's `firebase-functions/` repo. High level:

| Step | What |
|------|------|
| Firebase project | Created with Blaze billing enabled |
| Firestore database | Native mode, single region matching `FirestoreConfig.FunctionsRegion` |
| Authentication providers | Anonymous required; Google/Facebook/Apple optional |
| App Check | Provider configured (Play Integrity Android / DeviceCheck or App Attest iOS); start in UNENFORCED mode |
| Cloud Storage bucket | For migration backup uploads |
| Cloud Functions deployed | `firestore-core` codebase from team monorepo |
| `_config/save_features_registry` doc | Seeded with the game's save feature list |
| `iap_catalog/{productId}` docs | Seeded with the game's IAP products |
| Remote Config keys | `cloud_sync_mode` etc. (used by IDK CloudSync layer, not this SDK directly) |

## 2. Unity package install

```json
// Packages/manifest.json
{
  "dependencies": {
    "com.archerstudio.sdk.core": "...",
    "com.archerstudio.sdk.login": "...",
    "com.archerstudio.sdk.appcheck": "...",
    "com.archerstudio.sdk.firestore": "https://github.com/<org>/archer-core-sdk-unity.git?path=com.archerstudio.sdk.firestore#<ref>"
  }
}
```

After Unity refresh, the Package Manager should show the package version.

## 3. FirestoreConfig asset

Create `Assets/Resources/FirestoreConfig.asset` via the Unity menu:
`Create / ArcherStudio / SDK / Firestore Config`.

| Field | Recommended |
|-------|-------------|
| `WebClientId` | Same value as `CloudSaveConfig.WebClientId` (Firebase Console → Project Settings → OAuth Web client ID) |
| `FunctionsRegion` | Match Firestore region. Default `asia-southeast1`. |
| `EnableOfflinePersistence` | `true` |
| `IapCatalogCacheTtlMs` | `300000` (5 min) |
| `FeatureRegistryCacheTtlMs` | `3600000` (1 h) |
| `VerboseLogging` | `false` in PROD |

Validate via menu: `ArcherStudio / SDK / Firestore / Validate Setup`.

## 4. Boot flow wiring

The SDK module auto-registers with the SDK core factory (no manual registration
needed). It initializes during `SDKBootstrap.InitializeSequence` after Login.

Auth happens in two stages:

1. **Anonymous immediately** (or reuses an existing Firebase Auth user from
   CloudSave). Firestore is usable right away.
2. **Upgrade to social credential** when `LoginSucceededEvent` fires (GPGS Google
   Sign-In or similar). The SDK calls `LinkWithCredentialAsync` automatically.

After boot, you can use the module from any scene:

```csharp
var module = ArcherStudio.SDK.Firestore.FirestoreModule.Instance;
if (module == null || module.Service == null || !module.Service.IsAvailable) {
    // Still initializing or in stub mode (no Auth). Either retry later or
    // gracefully degrade to local-only.
    return;
}
```

## 5. Profile bootstrap

Call once per first-launch after Auth completes:

```csharp
module.UserRepository.CreateUserProfileAsync(result => {
    // result.Data == true if a fresh profile was created
    // (false means existing profile loaded — idempotent)
});
```

Server allocates a unique 6-char player code (`AB12CD` style) and seeds
`users/{uid}.flags` with defaults.

## 6. Single-RPC bundle load

For cold launches, fetch user root + private state + all save subdocs in one RPC:

```csharp
module.UserRepository.PrepareUserBundleAsync(result => {
    if (!result.Success) { /* fall back to local */ return; }
    var bundle = result.Data;

    // bundle.User                              — users/{uid} root
    // bundle.Private["currencies"]             — Dict<string, string> big-int
    // bundle.Private["vipSubscription"]        — Dict<string, object>
    // bundle.Saves["stage"]                    — Dict<string, object> or null
    // bundle.Saves["forge"]                    — Dict<string, object> or null
});
```

The Function reads all 25 docs in parallel and returns within ~300-500ms on a
healthy network.

## 7. Currency mutations (T0 server-authoritative)

NEVER write `private/state.currencies` directly from the client — rules deny
client writes to that path. Always go through `MutateResourceAsync`:

```csharp
module.UserRepository.MutateResourceAsync(new ResourceMutationRequest {
    // Negative = spend, positive = earn. String-encoded big int.
    Deltas = new Dictionary<string, string> {
        { "gem", "-50" },
        { "gold", "+100" },
    },
    Reason = "stage_5_upgrade",
    Type   = "gameplay_spend",        // see ResourceMutationType for full set
    ClientTxnId = System.Guid.NewGuid().ToString("N"),  // recommended for idempotency
}, result => {
    if (result.Success) {
        // result.Data.NewBalance: Dict<string, string>
        // result.Data.TxnId
        // result.Data.Duplicate  (true if same ClientTxnId already processed)
    }
});
```

The Function:
- Validates the new balance is non-negative (rejects with `InvalidArgument`)
- Caps at per-currency upper bound
- Writes immutable `users/{uid}/transactions/{txnId}` for audit
- Returns the new balance

## 8. Per-feature save writes (T1/T2)

For game state that's safe to client-write (with rule validation):

```csharp
// Build a Firestore-friendly Dict from your local model.
// Use the SDK PolymorphicJsonConverter for stable shape:
var data = ArcherStudio.SDK.Firestore.PolymorphicJsonConverter.ToFirestoreDict(
    JsonConvert.DeserializeObject<Dictionary<string, object>>(
        JsonConvert.SerializeObject(yourSaveModel)));

module.SaveRepository.SaveFeatureAsync(
    featureName: "stage",
    data: data,
    schemaVersion: 1,
    onComplete: r => {
        if (!r.Success) {
            // Rule violation, network issue, or App Check rejection
        }
    });
```

Read back:

```csharp
module.SaveRepository.LoadFeatureAsync("stage", r => {
    if (r.Success) {
        var snap = r.Data;
        // snap.SchemaVersion, snap.Data (Dict<string, object>), snap.UpdatedBy
    } else if (r.ErrorCode == FirestoreErrorCode.NotFound) {
        // Never written before — use defaults
    }
});
```

## 9. Real-time listeners

For UI that needs to reflect server-side changes (currency UI, inbox badge):

```csharp
private IDisposable _currencySub;

void OnEnable() {
    _currencySub = module.UserRepository.ListenPrivateState(snap => {
        if (snap?.Currencies != null) {
            UpdateCurrencyHud(snap.Currencies);
        }
    });
}

void OnDisable() {
    _currencySub?.Dispose();   // mandatory — listeners survive scene unloads otherwise
}
```

## 10. IAP integration

The SDK does NOT directly call the IAP validator. Instead, the existing
SDK IAP package (`com.archerstudio.sdk.iap`) sends the receipt to
`validatePurchase` HTTP Function, which writes `iap_transactions/{txnId}`.
A Firestore trigger (`onIapTransactionCreated`) then grants to
`users/{uid}/private/state` server-side.

For this to work, the `userId` field in the IAP request body MUST be the
Firebase Auth UID. The SDK IAP reads `userId` from
`TrackingManager.CurrentUserProfile.FirebaseStorageId`, so wire that field
to the Firebase UID once Auth completes:

```csharp
using ArcherStudio.SDK.Firestore;
using ArcherStudio.SDK.Tracking;

void OnAuthReady() {
    var uid = FirestoreModule.Instance?.Service?.CurrentFirebaseUid;
    if (string.IsNullOrEmpty(uid)) return;
    TrackingManager.Instance.UpdateUserProfile(p => p.FirebaseStorageId = uid);
}
```

(Subscribe `OnAuthReady` to your post-Auth boot hook.)

## 11. Migration layer (IDK CloudSync — game-side)

Each game implements its own migration layer on top of this SDK. The SDK
provides primitives (`SaveRepository.SaveFeatureAsync`, `BackupUploader`,
`UserRepository.MutateResourceAsync`); game-side code reads from local saves
and feeds them in via `IFeatureMigrator` implementations.

Reference: see the IDK `Assets/_Game/Scripts/Core/CloudSync/` layer for a
working example. Pattern is documented in the IDK repo's
`Assets/docs/firestore/phase-5-summary.md`.

## 12. Configuration knobs

| Knob | Where | Default | Notes |
|------|-------|---------|-------|
| Functions region | `FirestoreConfig.FunctionsRegion` | `asia-southeast1` | Match Firestore database region |
| Offline persistence | `FirestoreConfig.EnableOfflinePersistence` | `true` | Firestore SDK caches reads locally |
| IAP catalog TTL | `FirestoreConfig.IapCatalogCacheTtlMs` | 5 min | Trade off freshness vs Function call cost |
| Feature registry TTL | `FirestoreConfig.FeatureRegistryCacheTtlMs` | 1 h | Registry rarely changes |
| Verbose logging | `FirestoreConfig.VerboseLogging` | `false` | Spammy in PROD — leave off |
| App Check enforcement | Firebase Console → App Check | UNENFORCED | Switch to ENFORCED post-soak |
| Per-currency caps | Server-side (`currencyCatalog.ts`) | `gold 1e15, gem 1e9, others 1e6` | Migrators clamp + flag at cap |
| Migration max bytes | Server-side `uploadMigrationBackup` | 5 MB | Most save features are < 50 KB |
| Functions rate limit | Server-side per-Function | varies | See team docs |

## 13. Stub provider behaviour

When the SDK can't reach Firebase (no `FirestoreConfig`, no Auth user, no
Firebase SDK installed, Editor without Auth), it provisions a stub provider:

- `Service.IsAvailable` returns `false`
- Every read/write returns `FirestoreErrorCode.NotAuthenticated`
- Listeners return a no-op `IDisposable`

The stub is the SDK's contract for "I'm not going to silently lose data" —
your migration layer should check `IsReady` and fall back to local-only.

## 14. Versioning policy

- `0.x` — DEV-only; expect breaking changes
- `1.0` — first PROD release; semver from then on
- Breaking changes ship with a migration note in `CHANGELOG.md`
