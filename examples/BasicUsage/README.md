# Basic Usage

Demonstrates connecting to the kwtSMS API, verifying credentials, and sending a single SMS.

## Prerequisites

- .NET 6.0 or later
- kwtSMS API credentials ([sign up at kwtsms.com](https://www.kwtsms.com))

## Setup

Create a `.env` file:

```ini
KWTSMS_USERNAME=csharp_username
KWTSMS_PASSWORD=csharp_password
KWTSMS_SENDER_ID=KWT-SMS
KWTSMS_TEST_MODE=1
```

## Run

```bash
dotnet run
```

## Key Points

- Use `KwtSmsClient.FromEnv()` to load credentials from environment variables or `.env` files.
- Always save the `MsgId` from the send response for status checks and delivery reports.
- Save `BalanceAfter` to avoid extra API calls.
- Set `KWTSMS_TEST_MODE=1` during development (messages are queued, not delivered; credits held until you delete them from the Sending Queue).
