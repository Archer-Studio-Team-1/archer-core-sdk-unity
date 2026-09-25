# Archer Studio SDK for Unity

Internal SDK for mobile game projects. Modular architecture with UPM package distribution.

## Requirements

- Unity 6000.0+ (Unity 6)

## Packages

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `com.archerstudio.sdk.core` | Foundation: init orchestration, dependency graph, event bus, logging, config | — |
| `com.archerstudio.sdk.consent` | GDPR/CCPA consent management, Google UMP, iOS ATT | core |
| `com.archerstudio.sdk.tracking` | Event tracking: Firebase Analytics, Adjust attribution | core (+ consent, login, optional) |
| `com.archerstudio.sdk.ads` | Ad facade: placements, frequency cap, revenue routing. Ships **no** mediation — install one of the provider packages below | core, tracking (+ consent, optional) |
| `com.archerstudio.sdk.ads.max` | AppLovin MAX provider | core, ads |
| `com.archerstudio.sdk.ads.admob` | Google AdMob provider | core, ads |
| `com.archerstudio.sdk.ads.levelplay` | IronSource / LevelPlay provider | core, ads |
| `com.archerstudio.sdk.iap` | In-App Purchase: Unity IAP wrapper, receipt validation | core, tracking (+ consent, optional) |
| `com.archerstudio.sdk.deeplink` | Deep linking: Unity, Firebase Dynamic Links, Adjust | core |
| `com.archerstudio.sdk.push` | Push notifications: Firebase Cloud Messaging | core |
| `com.archerstudio.sdk.remoteconfig` | Remote Config: Firebase Remote Config, feature flags | core |
| `com.archerstudio.sdk.cloudsave` | Cloud Save: Firestore-backed save/load with offline cache and conflict resolution | core, login |

## Dependency Graph

```
Core (foundation)
├── Consent → Core
├── Tracking → Core (+ Consent, Login when installed)
├── Ads → Core, Tracking (+ Consent when installed)
├── IAP → Core, Tracking (+ Consent when installed)
├── DeepLink → Core
├── Push → Core
├── RemoteConfig → Core
└── CloudSave → Core, Login
```

Consent is optional since 1.3.0: without `sdk.consent`, the code that reads `ConsentManager` is compiled out
(`HAS_SDK_CONSENT` from `versionDefines`), mediation keeps its own regional defaults, and tracking pushes no
consent at all - a `ConsentStatus` whose `Source` is `Default` reaches no provider.

## Without SDKBootstrap

A host with its own boot sequence (archer-core's `ads-sdk`, `iap-sdk`, `consent-sdk`, `analytics-sdk` do
this) drives the managers directly: `AdManager`, `IAPManager`, `ConsentManager` and `TrackingManager` each
have an `InitializeAsync` overload taking their config, so no `Resources/*Config` asset is needed. Two
things `SDKBootstrap` did that the host then owns:

| Step | Why |
|---|---|
| `FirebaseInitializer.EnsureInitialized` before tracking | `FirebaseTrackingProvider` checks `FirebaseInitializer.IsAvailable` once, at init |
| Start tracking before ads and IAP | `AdRevenueTracker` and `IAPManager` report revenue through `TrackingManager.Instance`; an uninitialized manager has no providers and drops it |

## Installation (UPM Git URL)

Add to your project's `Packages/manifest.json`. Install the one mediation the game ships -
`ads.max`, `ads.admob` or `ads.levelplay` - not all three:

```json
{
  "dependencies": {
    "com.archerstudio.sdk.core": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.core#v1.3.0",
    "com.archerstudio.sdk.consent": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.consent#v1.3.0",
    "com.archerstudio.sdk.tracking": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.tracking#v1.3.0",
    "com.archerstudio.sdk.ads": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.ads#v1.3.0",
    "com.archerstudio.sdk.ads.max": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.ads.max#v1.3.0",
    "com.archerstudio.sdk.iap": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.iap#v1.3.0",
    "com.archerstudio.sdk.deeplink": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.deeplink#v1.3.0",
    "com.archerstudio.sdk.push": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.push#v1.3.0",
    "com.archerstudio.sdk.remoteconfig": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.remoteconfig#v1.3.0"
  }
}
```

Or via SSH:
```
git+git@github.com:Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.core#v1.3.0
```

## Versioning

Uses git tags: `v1.2.2`, `v1.3.0`, etc. One tag covers every package - the repo is a monorepo and the packages move together.

To update SDK in your project, change the tag in manifest.json (e.g., `#v1.2.2` → `#v1.3.0`).
