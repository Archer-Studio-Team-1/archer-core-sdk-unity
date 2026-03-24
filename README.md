# Archer Studio SDK for Unity

Internal SDK for mobile game projects. Modular architecture with UPM package distribution.

## Requirements

- Unity 6000.0+ (Unity 6)

## Packages

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `com.archerstudio.sdk.core` | Foundation: init orchestration, dependency graph, event bus, logging, config | — |
| `com.archerstudio.sdk.consent` | GDPR/CCPA consent management, Google UMP, iOS ATT | core |
| `com.archerstudio.sdk.tracking` | Event tracking: Firebase Analytics, Adjust attribution | core, consent |
| `com.archerstudio.sdk.ads` | Ad mediation: AppLovin MAX, IronSource, AdMob | core, consent, tracking |
| `com.archerstudio.sdk.iap` | In-App Purchase: Unity IAP wrapper, receipt validation | core, consent, tracking |
| `com.archerstudio.sdk.deeplink` | Deep linking: Unity, Firebase Dynamic Links, Adjust | core |
| `com.archerstudio.sdk.push` | Push notifications: Firebase Cloud Messaging | core |
| `com.archerstudio.sdk.remoteconfig` | Remote Config: Firebase Remote Config, feature flags | core |

## Dependency Graph

```
Core (foundation)
├── Consent → Core
├── Tracking → Core, Consent
├── Ads → Core, Consent, Tracking
├── IAP → Core, Consent, Tracking
├── DeepLink → Core
├── Push → Core
└── RemoteConfig → Core
```

## Installation (UPM Git URL)

Add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.archerstudio.sdk.core": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.core#v0.1.3",
    "com.archerstudio.sdk.consent": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.consent#v0.1.3",
    "com.archerstudio.sdk.tracking": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.tracking#v0.1.3",
    "com.archerstudio.sdk.ads": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.ads#v0.1.3",
    "com.archerstudio.sdk.iap": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.iap#v0.1.3",
    "com.archerstudio.sdk.deeplink": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.deeplink#v0.1.3",
    "com.archerstudio.sdk.push": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.push#v0.1.3",
    "com.archerstudio.sdk.remoteconfig": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.remoteconfig#v0.1.3"
  }
}
```

Or via SSH:
```
git+git@github.com:Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.core#v0.1.3
```

## Versioning

Uses git tags: `v0.1.3`, `v0.2.0`, etc.

To update SDK in your project, change the tag in manifest.json (e.g., `#v0.1.3` → `#v0.2.0`).
