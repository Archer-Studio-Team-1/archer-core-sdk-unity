# Changelog

## [Unreleased]

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
