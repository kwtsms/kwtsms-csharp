# kwtSMS C# Client

[![CI](https://github.com/kwtsms/kwtsms-csharp/actions/workflows/ci.yml/badge.svg)](https://github.com/kwtsms/kwtsms-csharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/KwtSMS.svg)](https://www.nuget.org/packages/KwtSMS)
[![NuGet Downloads](https://img.shields.io/nuget/dt/KwtSMS.svg)](https://www.nuget.org/packages/KwtSMS)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![CodeQL](https://github.com/kwtsms/kwtsms-csharp/actions/workflows/codeql.yml/badge.svg)](https://github.com/kwtsms/kwtsms-csharp/actions/workflows/codeql.yml)

C# client for the [kwtSMS API](https://www.kwtsms.com). Send SMS, check balance, validate numbers, list sender IDs, check coverage, get delivery reports. Zero dependencies on .NET 6+.

## About kwtSMS

kwtSMS is a Kuwaiti SMS gateway trusted by top businesses to deliver messages anywhere in the world, with private Sender ID, free API testing, non-expiring credits, and competitive flat-rate pricing. Secure, simple to integrate, built to last. Open a free account in under 1 minute, no paperwork or payment required. [Click here to get started](https://www.kwtsms.com/signup/)

## Prerequisites

You need the **.NET SDK** installed.

### Step 1: Check if .NET is installed

```bash
dotnet --version
```

If you see a version number (6.0+), you're ready. If not, install .NET:

- **All platforms:** Download from https://dotnet.microsoft.com/download
- **macOS (Homebrew):** `brew install dotnet`
- **Ubuntu/Debian:** `sudo apt install dotnet-sdk-8.0`
- **Windows:** Download the installer from the link above

### Step 2: Create a project (if you don't have one)

```bash
dotnet new console -n my-project && cd my-project
```

### Step 3: Install KwtSMS

```bash
dotnet add package KwtSMS
```

Or add to your `.csproj`:

```xml
<PackageReference Include="KwtSMS" Version="0.6.0" />
```

## Install

```bash
dotnet add package KwtSMS
```

## Quick Start

```csharp
using KwtSMS;

// Load credentials from environment variables or .env file
var sms = KwtSmsClient.FromEnv();

// Verify credentials and check balance
var (ok, balance, error) = sms.Verify();
Console.WriteLine($"Balance: {balance} credits");

// Send SMS
var result = sms.Send("96598765432", "Your OTP for MyApp is: 123456");
if (result.Result == "OK")
{
    Console.WriteLine($"Sent! Message ID: {result.MsgId}");
    Console.WriteLine($"Balance after: {result.BalanceAfter}");
}
```

## Setup

Create a `.env` file in your project root:

```ini
KWTSMS_USERNAME=csharp_username
KWTSMS_PASSWORD=csharp_password
KWTSMS_SENDER_ID=YOUR-SENDERID   # Use KWT-SMS for testing only
KWTSMS_TEST_MODE=1                # 1=test (safe), 0=live
KWTSMS_LOG_FILE=kwtsms.log        # or "" to disable
```

Add `.env` to your `.gitignore`:
```
.env
```

Or pass credentials directly:

```csharp
var sms = new KwtSmsClient("csharp_username", "csharp_password", "YOUR-SENDERID", testMode: true);
```

## API Methods

### Verify Credentials

```csharp
var (ok, balance, error) = sms.Verify();
// ok: true if credentials are valid
// balance: available credits
// error: error message if failed
```

### Send SMS

```csharp
// Single number
var result = sms.Send("96598765432", "Hello from C#");

// Multiple numbers (comma-separated)
var result = sms.Send("96598765432,96512345678", "Bulk message");

// Multiple numbers (array)
var result = sms.Send(new[] { "96598765432", "96512345678" }, "Bulk message");

// Override sender ID
var result = sms.Send("96598765432", "Hello", sender: "MY-APP");
```

The `Send` method automatically:
- Normalizes phone numbers (strips `+`, `00`, spaces, dashes, converts Arabic digits)
- Deduplicates numbers before sending
- Cleans message text (strips emojis, HTML, hidden characters, converts Arabic digits)
- Validates numbers locally before calling the API
- For >200 numbers: auto-batches with 0.5s delay, retries ERR013 with exponential backoff

### Send with Retry (ERR028)

```csharp
// Automatically retries on ERR028 (rate limit), sleeping 16 seconds between attempts
var result = sms.SendWithRetry("96598765432", "OTP: 123456", maxRetries: 3);
```

### Check Balance

```csharp
double? balance = sms.Balance();
// Returns cached value on API failure
```

### Validate Numbers

```csharp
var result = sms.Validate("96598765432", "+96512345678", "invalid");
// result.Ok   = valid and routable numbers
// result.Er   = numbers with format errors
// result.Nr   = numbers with no route (country not activated)
// result.Rejected = numbers that failed local validation
```

### List Sender IDs

```csharp
var result = sms.SenderIds();
// result.SenderIds = ["KWT-SMS", "MY-APP", ...]
```

### Coverage (Active Countries)

```csharp
var result = sms.Coverage();
// result.Prefixes = ["965", "966", "971", ...]
```

### Message Status

```csharp
var result = sms.Status("msg-id-from-send-response");
// result.Status = "sent"
// result.Description = "Message successfully sent"
```

### Delivery Report (International Only)

```csharp
// DLR is only available for international (non-Kuwait) numbers.
// Wait at least 5 minutes after sending before checking.
var result = sms.Dlr("msg-id-from-send-response");
// result.Report = [{ Number: "971504496677", Status: "Received by recipient" }]
```

## Utility Functions

```csharp
using KwtSMS;

// Normalize phone number (digits only, no leading zeros)
string normalized = PhoneUtils.NormalizePhone("+965 9876-5432");
// "96598765432"

// Validate phone number
var validation = PhoneUtils.ValidatePhoneInput("user@email.com");
// validation.IsValid = false
// validation.Error = "'user@email.com' is an email address, not a phone number"

// Clean message text
string cleaned = MessageUtils.CleanMessage("Hello \U0001F600 World <b>bold</b>");
// "Hello  World bold"

// Access all error codes
foreach (var kvp in ApiErrors.Errors)
    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
```

## Phone Number Formats

All formats are accepted and normalized automatically:

| Input | Normalized | Valid? |
|-------|-----------|--------|
| `96598765432` | `96598765432` | Yes |
| `+96598765432` | `96598765432` | Yes |
| `0096598765432` | `96598765432` | Yes |
| `965 9876 5432` | `96598765432` | Yes |
| `965-9876-5432` | `96598765432` | Yes |
| `(965) 98765432` | `96598765432` | Yes |
| `\u0669\u0666\u0665\u0669\u0668\u0667\u0666\u0665\u0664\u0663\u0662` (Arabic-Indic) | `96598765432` | Yes |
| `\u06F9\u06F6\u06F5\u06F9\u06F8\u06F7\u06F6\u06F5\u06F4\u06F3\u06F2` (Extended Arabic-Indic) | `96598765432` | Yes |
| `123456` (too short) | rejected | No |
| `user@gmail.com` | rejected | No |

## Input Sanitization

`MessageUtils.CleanMessage()` is called automatically by `Send()` before every API call. It prevents the #1 cause of "message sent but not received" support tickets:

| Content | Effect without cleaning | What CleanMessage() does |
|---------|------------------------|--------------------------|
| Emojis | Stuck in queue, credits wasted, no error | Stripped |
| Hidden control characters (BOM, zero-width space, soft hyphen) | Spam filter rejection or queue stuck | Stripped |
| Arabic/Hindi numerals in body | OTP codes render inconsistently | Converted to Latin digits |
| HTML tags | ERR027, message rejected | Stripped |
| Directional marks (LTR, RTL) | May cause display issues | Stripped |

Arabic letters and Arabic text are fully supported and never stripped.

## Error Handling

Every error response includes a developer-friendly `Action` field:

```csharp
var result = sms.Send("96598765432", "Test");
if (result.Result == "ERROR")
{
    Console.WriteLine($"Code: {result.Code}");           // "ERR003"
    Console.WriteLine($"Description: {result.Description}"); // "Authentication error..."
    Console.WriteLine($"Action: {result.Action}");       // "Wrong API username or password..."
}
```

The library never throws exceptions. All methods return structured results.

## Cached Balance

The client caches balance from `Verify()` and `Send()` responses:

```csharp
sms.Verify();
Console.WriteLine($"Balance: {sms.CachedBalance}");
Console.WriteLine($"Purchased: {sms.CachedPurchased}");
```

Never call `Balance()` after `Send()`. The send response already includes your updated balance in `BalanceAfter`. Save it to your database.

## ASP.NET Core Integration

Register `KwtSmsClient` as a singleton (thread-safe, reuses HttpClient):

```csharp
builder.Services.AddSingleton<KwtSmsClient>(sp => KwtSmsClient.FromEnv());
```

See [examples/AspNetEndpoint](examples/AspNetEndpoint/) for a complete endpoint example.

## Credential Management

API credentials must NEVER be hardcoded. Recommended approaches:

1. **Environment variables / .env file** (default): Use `KwtSmsClient.FromEnv()`. The `.env` file is gitignored and editable without redeployment.

2. **Admin settings UI** (web apps): Provide a settings page where an admin can update credentials and test the connection with `Verify()`.

3. **Secrets manager** (production): Load from Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault. Pass to the constructor.

4. **Constructor injection** (custom config): Pass credentials directly from your DI container.

```csharp
// Azure Key Vault example
var username = await secretClient.GetSecretAsync("kwtsms-username");
var password = await secretClient.GetSecretAsync("kwtsms-password");
var sms = new KwtSmsClient(username.Value.Value, password.Value.Value, "MY-APP");
```

## Server Timezone

The `unix-timestamp` values in API responses are GMT+3 (Asia/Kuwait server time), NOT UTC. Convert when storing or displaying:

```csharp
var serverTime = DateTimeOffset.FromUnixTimeSeconds(result.UnixTimestamp!.Value);
// This timestamp is in GMT+3. Adjust if you need UTC:
var utcTime = serverTime.AddHours(-3);
```

## Best Practices

### Validate Before Calling the API

```csharp
// Validate phone locally first
var validation = PhoneUtils.ValidatePhoneInput(userInput);
if (!validation.IsValid)
{
    return new { error = validation.Error };
}

// Clean message
var message = MessageUtils.CleanMessage(userMessage);
if (string.IsNullOrWhiteSpace(message))
{
    return new { error = "Message is empty after cleaning." };
}

// Only valid, clean input reaches the API
var result = sms.Send(validation.Normalized, message);
```

### User-Facing Error Messages

Never expose raw API errors to end users:

| Situation | API Error | User Message |
|-----------|-----------|-------------|
| Invalid phone | ERR006, ERR025 | "Please enter a valid phone number in international format." |
| Wrong credentials | ERR003 | "SMS service is temporarily unavailable." (log + alert admin) |
| No balance | ERR010, ERR011 | "SMS service is temporarily unavailable." (alert admin to top up) |
| Rate limited | ERR028 | "Please wait a moment before requesting another code." |
| Message rejected | ERR031, ERR032 | "Your message could not be sent. Please try with different content." |

### OTP Requirements

- Always include app name: `"Your OTP for APPNAME is: 123456"`
- Use Transactional Sender ID (not Promotional, not `KWT-SMS`)
- Minimum 3-4 minute resend timer (KNET standard: 4 minutes)
- OTP expiry: 3-5 minutes
- Generate a new code on every resend, invalidate previous codes
- Send to a single number per request (never batch OTP sends)

### Sender ID

| | Promotional | Transactional |
|--|-------------|---------------|
| Use for | Bulk SMS, marketing, offers | OTP, alerts, notifications |
| DND delivery | Blocked (credits lost) | Bypasses DND |
| Speed | May have delays | Priority delivery |
| Cost | 10 KD one-time | 15 KD one-time |

`KWT-SMS` is a shared test sender: delays, blocked on Virgin Kuwait. Never use in production. Register your own at [kwtsms.com](https://www.kwtsms.com). Sender ID is case-sensitive.

## Security Checklist

```
BEFORE GOING LIVE:
[ ] Bot protection enabled (CAPTCHA for web, Device Attestation for mobile)
[ ] Rate limit per phone number (max 3-5/hour)
[ ] Rate limit per IP address (max 10-20/hour)
[ ] Rate limit per user/session if authenticated
[ ] Monitoring/alerting on abuse patterns
[ ] Admin notification on low balance
[ ] Test mode OFF (KWTSMS_TEST_MODE=0)
[ ] Private Sender ID registered (not KWT-SMS)
[ ] Transactional Sender ID for OTP (not promotional)
```

## Compatibility

| Target | Dependencies |
|--------|-------------|
| .NET 8.0+ | Zero |
| .NET 6.0+ | Zero |
| .NET Standard 2.0 (.NET Framework 4.6.1+) | System.Text.Json |

## Examples

| Example | Description |
|---------|-------------|
| [RawApi](examples/RawApi/) | Call every endpoint directly with HttpClient (no library needed) |
| [BasicUsage](examples/BasicUsage/) | Connect, verify, send SMS |
| [OtpFlow](examples/OtpFlow/) | OTP with proper error handling |
| [BulkSms](examples/BulkSms/) | Auto-batching for >200 numbers |
| [AspNetEndpoint](examples/AspNetEndpoint/) | ASP.NET Core minimal API |
| [ErrorHandling](examples/ErrorHandling/) | Error codes, validation, cleaning |
| [OtpProduction](examples/OtpProduction/) | Production OTP with rate limiting |

## Testing

```bash
# Unit + mock tests (no credentials needed)
dotnet test --filter "Category!=Integration"

# Integration tests (real API, test mode, no credits consumed)
export CSHARP_USERNAME=csharp_username
export CSHARP_PASSWORD=csharp_password
dotnet test --filter "Category=Integration"
```

## What's Handled Automatically

- **Phone normalization**: `+`, `00`, spaces, dashes, dots, parentheses stripped. Arabic-Indic digits converted. Leading zeros removed.
- **Duplicate phone removal**: If the same number appears multiple times (in different formats), it is sent only once.
- **Message cleaning**: Emojis removed (surrogate-pair safe). Hidden control characters (BOM, zero-width spaces, directional marks) removed. HTML tags stripped. Arabic-Indic digits in message body converted to Latin.
- **Batch splitting**: More than 200 numbers are automatically split into batches of 200 with 0.5s delay between batches.
- **ERR013 retry**: Queue-full errors are automatically retried up to 3 times with exponential backoff (30s / 60s / 120s).
- **Error enrichment**: Every API error response includes an `Action` field with a developer-friendly fix hint.
- **Credential masking**: Passwords are always masked as `***` in log files. Never exposed.
- **No exceptions**: All public methods return structured results. They never throw on API errors.

## FAQ

**1. My message was sent successfully (result: OK) but the recipient didn't receive it. What happened?**

Check the **Sending Queue** at [kwtsms.com](https://www.kwtsms.com/login/). If your message is stuck there, it was accepted by the API but not dispatched. Common causes are emoji in the message, hidden characters from copy-pasting, or spam filter triggers. Delete it from the queue to recover your credits. Also verify that `test` mode is off (`KWTSMS_TEST_MODE=0`). Test messages are queued but never delivered.

**2. What is the difference between Test mode and Live mode?**

**Test mode** (`KWTSMS_TEST_MODE=1`) sends your message to the kwtSMS queue but does NOT deliver it to the handset. No SMS credits are consumed. Use this during development. **Live mode** (`KWTSMS_TEST_MODE=0`) delivers the message for real and deducts credits. Always develop in test mode and switch to live only when ready for production.

**3. What is a Sender ID and why should I not use "KWT-SMS" in production?**

A **Sender ID** is the name that appears as the sender on the recipient's phone (e.g., "MY-APP" instead of a random number). `KWT-SMS` is a shared test sender. It causes delivery delays, is blocked on Virgin Kuwait, and should never be used in production. Register your own private Sender ID through your kwtSMS account. For OTP/authentication messages, you need a **Transactional** Sender ID to bypass DND (Do Not Disturb) filtering.

**4. I'm getting ERR003 "Authentication error". What's wrong?**

You are using the wrong credentials. The API requires your **API username and API password**, NOT your account mobile number. Log in to [kwtsms.com](https://www.kwtsms.com/login/), go to Account, and check your API credentials. Also make sure you are using POST (not GET) and `Content-Type: application/json`.

**5. Can I send to international numbers (outside Kuwait)?**

International sending is **disabled by default** on kwtSMS accounts. Log in to your [kwtSMS dashboard](https://www.kwtsms.com/login/) and add coverage for the country prefixes you need. Use `Coverage()` to check which countries are currently active on your account. Be aware that activating international coverage increases exposure to automated abuse. Implement rate limiting and CAPTCHA before enabling.

## Help & Support

- **[kwtSMS FAQ](https://www.kwtsms.com/faq/)**: Answers to common questions about credits, sender IDs, OTP, and delivery
- **[kwtSMS Support](https://www.kwtsms.com/support.html)**: Open a support ticket or browse help articles
- **[Contact kwtSMS](https://www.kwtsms.com/#contact)**: Reach the kwtSMS team directly for Sender ID registration and account issues
- **[API Documentation (PDF)](https://www.kwtsms.com/doc/KwtSMS.com_API_Documentation_v41.pdf)**: kwtSMS REST API v4.1 full reference
- **[Implementation Best Practices](https://www.kwtsms.com/articles/sms-api-implementation-best-practices.html)**: Official guide for production-ready SMS integrations
- **[Integration Test Checklist](https://www.kwtsms.com/articles/sms-api-integration-test-checklist.html)**: Pre-launch testing checklist from kwtSMS
- **[Sender ID Help](https://www.kwtsms.com/sender-id-help.html)**: How to register, whitelist, and troubleshoot sender IDs
- **[kwtSMS Dashboard](https://www.kwtsms.com/login/)**: Recharge credits, buy Sender IDs, view message logs, manage coverage
- **[Other Integrations](https://www.kwtsms.com/integrations.html)**: Plugins and integrations for other platforms and languages

## License

MIT
