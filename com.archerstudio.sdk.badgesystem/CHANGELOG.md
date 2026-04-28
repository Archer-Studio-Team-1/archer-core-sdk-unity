# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
