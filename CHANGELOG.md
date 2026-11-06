# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.7.0] - 2026-11-06

Versions 0.4.0, 0.5.0 and 0.6.0 below were prepared but never published to NuGet (the latest NuGet version was 0.3.0). Their changes reach NuGet with 0.7.0: per-country phone validation, trunk prefix stripping and the security audit fixes.

### Added
- The client sends a User-Agent `kwtsms-csharp/<version>` (name and version only, no personal data) so kwtSMS can see which integrations are in use. A new constructor overload with a sixth `appName` argument puts your app's token in front, e.g. `my-shop/2.1.0 kwtsms-csharp/0.7.0`. New `KwtSmsClient.Version` (from the assembly), `UserAgent` and `BuildUserAgent()`.

### Changed

- `SendWithRetry()`: retry on ERR028 is now off by default (`maxRetries` defaults to 0) and the wait is a `waitSeconds` parameter (default 10) instead of a hardcoded 16 seconds. ERR028 is opt-in per account (Duplicate SMS protection) and its window is set per account.
- ERR028 action text no longer states a fixed 15-second wait.
- README, examples and the ERR006/ERR025 action text use the placeholder `965XXXXXXXX` instead of a sample mobile number. Tests that need a phone number read it from the `KWTSMS_TEST_NUMBER` environment variable and are skipped when it is unset.
- The live 250-number bulk integration test is replaced by an offline mocked test of the same 200 + 50 batching.
- Package author and license holder are now kwtSMS.

### Docs

- Test mode: test messages are queued but not delivered. Their credits are held until you delete them from the Sending Queue in your kwtsms.com account (previously documented as "no credits consumed").

## [0.6.0] - 2026-03-13

### Fixed

- All public method catch blocks now return generic error messages instead of leaking `ex.Message` internals. Real exception details logged to file only.
- `InvariantCulture` for all `TryParse` calls in `KwtSmsClient`, preventing null balance on non-English locales.
- Thread-safe JSONL logging with write lock in `Logger`.
- Added `[CollectionDefinition("HttpClient", DisableParallelization = true)]` backing class for test isolation.
- PRD updated to match actual code: project structure, utility functions, bulk send result type, class names, validation rules.

### Removed

- Unused `BulkSendResult` class (bulk sends use `SendResult`).
- Unused `Moq` dependency from test project.

---

## [0.5.0] - 2026-03-13

### Added

- Per-country phone validation: `PHONE_RULES` table covering 70+ countries with local number length and mobile prefix rules.
- `FindCountryCode()`: longest-match country code detection (3-digit, 2-digit, 1-digit).
- `ValidatePhoneFormat()`: per-country length and mobile prefix validation.
- `PhoneRule` public class.
- Domestic trunk prefix stripping in `NormalizePhone()`: e.g. `9660551234567` → `966551234567` (Saudi), `9710501234567` → `971501234567` (UAE).
- 15 new unit tests covering trunk stripping, country format, `FindCountryCode`, and `ValidatePhoneFormat`. Total: 115 tests.

---

## [0.4.0] - 2026-03-13

### Removed

- CLI tool (`tools/KwtSMS.Cli`): replaced by the dedicated cross-platform [kwtsms-cli](https://github.com/boxlinknet/kwtsms-cli) standalone binary.

---

## [0.3.0] - 2026-03-07

### Added

- Raw API example (`examples/RawApi/`): call every kwtSMS endpoint directly with HttpClient, no library needed.
- Live bulk send integration tests: 250 numbers (200+50 batches) for the client library.
- Pre-flight balance check and exact credit verification in bulk tests.
- `[Collection("HttpClient")]` test isolation for shared static state.

### Changed

- Use `csharp_wrong_user` / `csharp_wrong_pass` credential placeholders in tests.

## [0.2.0] - 2026-03-06

### Added

- About kwtSMS section with signup link in README.
- Prerequisites section with .NET install instructions.
- Phone Number Formats table with Arabic-Indic examples.
- Input Sanitization, What's Handled Automatically sections.
- FAQ section (5 common questions) and Help & Support section.
- 15 new test cases for Arabic-Indic phone numbers (total: 100 tests).

### Changed

- README title: "kwtSMS C# Client" (lowercase kwtSMS).
- Use `csharp_username` / `csharp_password` as credential placeholders across all docs, examples, and tests for multi-library disambiguation.

## [0.1.1] - 2026-03-06

### Fixed

- Fix NuGet pack failure caused by missing icon.png reference.
- Updated dependencies via Dependabot (actions/checkout v6, actions/setup-dotnet v5, codeql-action v4, System.Text.Json 10.0.3, Microsoft.NET.Test.Sdk 18.3.0, xunit.runner.visualstudio 3.1.5).

## [0.1.0] - 2026-03-06

### Added

- Initial release of KwtSMS C# client library.
- `KwtSmsClient` class with all API methods: `Send`, `Balance`, `Verify`, `Validate`, `SenderIds`, `Coverage`, `Status`, `Dlr`.
- `FromEnv()` factory method for loading credentials from environment variables or `.env` files.
- `PhoneUtils.NormalizePhone()` for phone number normalization (Arabic digits, stripping non-digits, removing leading zeros).
- `PhoneUtils.ValidatePhoneInput()` for local phone validation (empty, email, too short, too long, no digits).
- `MessageUtils.CleanMessage()` for SMS content sanitization (emojis, HTML, hidden characters, Arabic digit conversion).
- `ApiErrors.Errors` dictionary with all 29+ kwtSMS error codes mapped to developer-friendly action messages.
- `ApiErrors.EnrichError()` for adding action guidance to error responses.
- Auto-batching for bulk sends (>200 numbers) with 0.5s delay between batches.
- ERR013 (queue full) automatic retry with exponential backoff (30s, 60s, 120s).
- `SendWithRetry()` for automatic ERR028 (rate limit) retry with 16s delay.
- Phone number deduplication before sending.
- JSONL logging with password masking.
- Thread-safe cached balance tracking.
- Multi-target: netstandard2.0, net6.0, net8.0.
- Comprehensive test suite: unit tests, mocked API tests, real integration tests.
- CI/CD with GitHub Actions (test on .NET 6/8/9, publish to NuGet on tag).
- CodeQL security analysis.
- Dependabot for automated dependency updates.
