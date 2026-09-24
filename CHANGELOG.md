# Changelog

One version covers every package: this is a monorepo and the packages move together.

## [1.3.0] — 2026-09-24

Everything here exists to let a host drive the modules directly, without
`SDKBootstrap`. Nothing changes for a project that keeps using it.

Lets Google UMP serve as the one consent flow for every mediation: a game can then swap MAX for
AdMob or LevelPlay without touching consent.

### Breaking

- **`com.archerstudio.sdk.ads` no longer ships any mediation.** AppLovin MAX,
  AdMob and IronSource moved to `com.archerstudio.sdk.ads.max`,
  `.ads.admob` and `.ads.levelplay`. A project must add the one package for the
  mediation it ships; without it, `AdManager` fails to initialise and logs the
  package name to install.

  Migration is one line in `Packages/manifest.json`:

  ```json
  "com.archerstudio.sdk.ads.max": "git+https://github.com/Archer-Studio-Team-1/archer-core-sdk-unity.git?path=com.archerstudio.sdk.ads.max#v1.3.0"
  ```

  No code change: `AdConfig.MediationPlatform` still selects the provider, and
  ad unit ids, placements and call sites are untouched.

### Added

- `AdProviderRegistry` — each mediation package registers itself from a
  `RuntimeInitializeOnLoadMethod`, so `com.archerstudio.sdk.ads` references no
  vendor SDK and a project carries only the mediation it uses.
- `AdMobProvider` is implemented (banner, interstitial, rewarded, app open,
  `OnAdPaid` revenue, npa on denied personalization). It had been a stub that
  reported failure since the first release, with no way to switch it on.
- `AdManager.SetInitialConsent(ConsentStatus)` — consent handed to the provider
  before mediation starts. Its only other route was `SDKInitializer`, which does
  not exist without `SDKBootstrap`, so mediation would otherwise start on its own
  default. In an EEA session that is a compliance problem, not a missing feature.
- `InitializeAsync` overloads taking a config directly, on `AdManager`,
  `IAPManager` and `ConsentManager`. A host with its own settings asset no longer
  needs a matching `Resources` folder.
- `SDKCoreConfig.ResolveOrDefault` — falls back to `Resources`, then to built-in
  defaults.
- `TrackingManager.InitializeAsync(SDKCoreConfig, TrackingConfig, Action<bool>)` —
  the same config-taking overload for tracking, so a host no longer needs
  `Resources/TrackingConfig`.
- Retry with exponential backoff on ad load failure (MAX and AdMob): 2ⁿ seconds,
  capped at 64s, six attempts, reset on a successful load.
- `ArcherStudio/SDK/Auto-Detect Symbols` — a per-machine toggle for the symbol
  detector, which writes scripting defines into `ProjectSettings` on every domain
  reload.

- `IPrivacyOptionsProvider` — a provider that can reopen its form for a player who already answered.
  Separate from `IConsentProvider`, so providers written against it keep compiling.
- `GoogleUmpProvider` implements it with UMP's `PrivacyOptionsRequirementStatus` and
  `ConsentForm.ShowPrivacyOptionsForm`. A UMP-only game had no way to let a player change their
  answer, which GDPR expects.
- `MaxConsentProvider` implements it too, over its existing `ShowCmpForExistingUser`. It reports privacy
  options as required only for a player in a GDPR region with a supported CMP: elsewhere there is no
  answer to change.
- `ConsentManager.IsPrivacyOptionsRequired` and `ConsentManager.ShowPrivacyOptions`, the same path for
  every provider.
- `AdManager.LeaveConsentToMediation()` — for a host with no consent module. The provider then gets no
  consent at all and the mediation applies its own regional defaults, without the "no consent
  supplied" warning meant for a host that forgot.

### Changed

- `AppLovinMaxProvider` sets `SetHasUserConsent` / `SetDoNotSell` only once it has been given consent.
  Before, a provider nobody told anything still called `SetHasUserConsent(true)` from
  `ConsentStatus.Default` — an explicit "consented" rather than MAX's own default.

- `com.archerstudio.sdk.ads`, `.tracking` and `.iap` no longer depend on `com.archerstudio.sdk.consent`.
  Their only use of it — reading `ConsentManager` through `SDKInitializer` when no consent was handed
  over — sits behind `HAS_SDK_CONSENT`, set when the package is installed. A project without consent
  drops the package; adding it back needs no change here.

### Fixed

- **Tracking no longer declares consent nobody gave.** Without a consent module,
  `TrackingManager` pushed `ConsentStatus.Default` — all granted, non-EEA — to
  every provider: Firebase set consent mode to granted and enabled collection,
  and Adjust sent third-party sharing flags before `InitSdk`, marking every
  player a consenting non-EEA user. A status whose `Source` is `Default` now
  reaches no provider, and each vendor keeps its own regional defaults. Adjust's
  pre-init `TrackMeasurementConsent(true)` is unchanged. Adjust also skipped the
  first post-init `SetConsent` as an echo of the pre-init call; it now skips it
  only when that call actually sent sharing flags, so the first real answer is
  not lost.

- **One failed ad load at boot no longer disables that format for the session.**
  MAX only logged the failure, and nothing retried, so a cold start without fill
  or without network left rewarded ads unavailable until the app restarted.
- `com.archerstudio.sdk.tracking` compiles without `com.archerstudio.sdk.login`.
  It referenced `LoginModule` and `LoginSucceededEvent` with no guard, and
  `package.json` never declared the dependency, so installing tracking alone did
  not build. Both paths now log which one they took: a project that ships login
  and still sees "Login integration: disabled" has a broken versionDefine, and
  `login_id` would otherwise disappear from every event in silence.
- `IAPManager.InitializeAsync` no longer throws when handed a null
  `SDKCoreConfig`.

- Reopening the form never updated `ConsentManager.CurrentStatus`, the cache or subscribers. A player
  who withdrew consent from a privacy-settings screen kept being served personalized ads until the next
  launch. `ShowPrivacyOptions` now re-reads the provider, caches the result and publishes
  `ConsentChangedEvent` before it calls back. `MaxConsentProvider` re-reads MAX's flags after its CMP
  closes instead of keeping the value from initialization.

### Deprecated

- `ConsentManager.ShowCmpForExistingUser` — forwards to `ShowPrivacyOptions`.

### Notes

- `com.archerstudio.sdk.ads.levelplay` still contains the IronSource stub. It was
  moved, not written.
- `AdMobProvider` reports `adSource` as `"admob"`. With AdMob mediation, the
  serving adapter's name is available through `ResponseInfo` and is not read yet.

## [1.2.2] and earlier

Not recorded here. See `git log` and the per-package changelogs in
`com.archerstudio.sdk.firestore` and `com.archerstudio.sdk.badgesystem`.
