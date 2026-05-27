# Changelog

## [Unreleased]

### Removed (Phase 5.5)
- Dependency on `com.google.firebase.functions` Unity package. Game projects no
  longer need to install the tarball. Switching to plain HTTPS removes the
  Firebase Unity SDK 13.x packaging defect (missing `Firebase.App.Internal`
  asmdef) and trims ~2-3 MB of native gRPC libs from APK size.
- `FirebaseFunctionsBridge` (reflection wrapper around Firebase.Functions SDK).

### Added (Phase 5.5)
- `CallableHttpClient` — invokes Cloud Functions over HTTPS using the documented
  callable wire protocol (`POST /{region}-{projectId}.cloudfunctions.net/{name}`,
  `Authorization: Bearer <idToken>`, `{ "data": {...} }` envelope).
- `MiniJson` — public-domain JSON parser, used only by `CallableHttpClient` to
  decode callable responses. Serialization stays with `PolymorphicJsonConverter`.
- `MiniJsonTests` (6 tests): result/error envelope shapes, nested objects/arrays,
  escape sequences, empty + null input.

### Changed (Phase 5.5)
- `FirestoreServiceProvider` constructor signature dropped the
  `functionsInstance` parameter — Cloud Functions now go through
  `CallableHttpClient`.
- `FirestoreModule.ProvisionProviders` no longer reflects into
  `Firebase.Functions.FirebaseFunctions.GetInstance`.
- asmdef removed reference to `Firebase.Functions` and the
  `HAS_FIREBASE_FUNCTIONS` versionDefine.

### Added
- `ISaveRepository` + `SaveRepository` — per-feature read/write
- `IBackupUploader` + `BackupUploader` — wraps `uploadMigrationBackup` Function
- `PolymorphicJsonConverter` — Dict ↔ Firestore w/ sorted-key normalize + `_kind` discriminator
- `ChecksumHelper` — SHA256 hex + BOM/CRLF normalize
- `FeatureRegistry` — reads `_config/save_features_registry` (game-agnostic)
- `DictExtensions.TryGet<T>` — typed lookup with numeric widening
- 24 NUnit tests
- Editor menu: `ArcherStudio / SDK / Firestore / Validate Setup`
- Quickstart sample (`Samples~/QuickStart`)

### Changed
- `FirestoreModule.InitializeAsync` always attempts Firebase Auth (anonymous-first);
  upgrades to social via `LinkWithCredentialAsync` on `LoginSucceededEvent`
- `EnsureFirebaseAuth` calls `Firebase.FirebaseApp.CheckAndFixDependenciesAsync` first
- `SubscribeToLogin` actually subscribes now (previously a stub)
- `DictExtensions` `internal` → `public`

### Fixed
- `OnSignInComplete` accepts both `Task<AuthResult>` and `Task<FirebaseUser>` (cross-SDK-version)
- `FirebaseFirestore.Settings` mutated in place (older SDK API)
- `FirestoreModule.OnConsentChanged` implemented (interface compliance)
- `MigrationRunner` verify gate reads via `SaveRepository` directly (bypass mode gate)
- `MigrationRunner` branches on `IServerDrivenMigrator` for T0 features

## [0.1.0] - 2026-05-25
- Initial Phase 4 MVP
