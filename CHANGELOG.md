# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.7.0] - 2026-11-06

Versions 0.4.0, 0.5.0 and 0.6.0 below were never published to NuGet (the latest NuGet version was 0.3.0). Their changes reach NuGet with 0.7.0.

### Added
- Client identifies itself with name and version; optional app name argument.

### Changed
- `SendWithRetry()` retry options updated.
- Package author and license holder are now kwtSMS.

## [0.6.0] - 2026-03-13

### Fixed
- Bug fixes.

### Removed
- Unused `BulkSendResult` class.

---

## [0.5.0] - 2026-03-13

### Added
- Per-country phone validation.
- Trunk prefix handling in phone normalization.

---

## [0.4.0] - 2026-03-13

### Removed
- Built-in CLI, replaced by the standalone [kwtsms-cli](https://github.com/boxlinknet/kwtsms-cli).

---

## [0.3.0] - 2026-03-07

### Added
- Raw API example.
- Live bulk send integration tests.

## [0.2.0] - 2026-03-06

### Added
- About kwtSMS, Prerequisites, Phone Number Formats, FAQ and Help & Support sections in README.

### Changed
- Credential placeholders updated across docs and examples.

## [0.1.1] - 2026-03-06

### Fixed
- Bug fixes.

## [0.1.0] - 2026-03-06

### Added
- Initial release of KwtSMS C# client library.
- `KwtSmsClient` with `Send`, `Balance`, `Verify`, `Validate`, `SenderIds`, `Coverage`, `Status` and `Dlr`.
- `FromEnv()` factory.
- Phone and message utilities.
- Error code map with developer-friendly messages.
- Automatic batching and retry for bulk sends.
- `SendWithRetry()`.
- JSONL logging.
- Multi-target: netstandard2.0, net6.0, net8.0.
- Test suite and CI/CD.
