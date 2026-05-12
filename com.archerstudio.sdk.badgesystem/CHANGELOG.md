# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-05-12

### Changed
- **ZString is now an optional dependency.** The SDK compiles and runs with or without `com.cysharp.zstring` in the project.
- Introduced internal `BadgeStringBuilder` / `BadgeStringHelper` abstraction (`Runtime/Internal/BadgeStringBuilder.cs`). When the `ARCHER_BADGE_USE_ZSTRING` symbol is defined it wraps `Cysharp.Text.Utf16ValueStringBuilder` for allocation-free string building; otherwise it falls back to `System.Text.StringBuilder`.
- `ArcherStudio.SDK.BadgeSystem.asmdef`: removed hard reference to `ZString`. Added `versionDefines` so the symbol `ARCHER_BADGE_USE_ZSTRING` is injected automatically when `com.cysharp.zstring >= 1.0.0` is present.

### Migration
- No source changes required for consumers. Projects that already include ZString keep the same zero-alloc performance. Projects that remove ZString will use the .NET StringBuilder fallback and continue to work.

## [1.0.0] - 2026-01-28

### Changed
- **Breaking Change:** Renamed package from `com.voidex.badgenotification` to `com.archerstudio.badgenotification`.
- **Breaking Change:** Renamed root namespace from `Voidex` to `ArcherStudio`.
- Updated assembly definitions to match the new namespace `ArcherStudio.*`.
- Refactored `BadgeNotificationBase` to use `ArcherStudio.Trie` namespace.

### Added
- Added `Documentation~` folder for Unity Package Manager support.
- Added `CHANGELOG.md`.

### Fixed
- Fixed namespace consistency across Editor and Runtime assemblies.
