# 00 — Raw API

Call every kwtSMS REST endpoint directly with `HttpClient` and `System.Text.Json`. No external packages required. This shows exactly what happens over the wire so you can integrate the API into any C# project without using the KwtSMS client library.

## What This Example Covers

| # | Endpoint | URL | What It Does |
|---|----------|-----|--------------|
| 1 | Balance | `POST /API/balance/` | Check available and purchased credits |
| 2 | Sender IDs | `POST /API/senderid/` | List registered sender IDs on your account |
| 3 | Coverage | `POST /API/coverage/` | List active country prefixes |
| 4 | Validate | `POST /API/validate/` | Check if phone numbers are valid and routable |
| 5 | Send | `POST /API/send/` | Send an SMS (test mode: queued but not delivered) |
| 6 | Status | `POST /API/status/` | Check message queue status using msg-id |
| 7 | DLR | `POST /API/dlr/` | Get delivery report (international numbers only) |

## Prerequisites

- .NET 6.0 or later
- kwtSMS API credentials ([sign up at kwtsms.com](https://www.kwtsms.com))

## Step-by-Step Setup

### 1. Create a new console project

```bash
mkdir kwtsms-raw-example && cd kwtsms-raw-example
dotnet new console
```

### 2. Replace `Program.cs` with `RawApi.cs`

Copy the contents of [`RawApi.cs`](RawApi.cs) into your `Program.cs`.

### 3. Set your credentials

Open `Program.cs` and replace the configuration section at the top:

```csharp
string username = "csharp_username";   // ← your API username
string password = "csharp_password";   // ← your API password
string senderId = "KWT-SMS";          // ← your registered sender ID
string testMode = "1";                // ← "1" for test, "0" for live
```

### 4. Run

```bash
dotnet run
```

### 5. Expected output (test mode)

```
── 1. Balance ───────────────────────────────────────────
  Available: 150
  Purchased: 1000

── 2. Sender IDs ────────────────────────────────────────
  IDs: KWT-SMS  MY-APP

── 3. Coverage ──────────────────────────────────────────
  Prefixes: +965  +966  +971

── 4. Validate ──────────────────────────────────────────
  OK: 96598765432
  Format errors (ER):
  No route (NR): 966551234567

── 5. Send SMS ──────────────────────────────────────────
  Result:         OK
  Message ID:     f4c841adee210f31307633ceaebff2ec
  Numbers:        1
  Points charged: 1
  Balance after:  149
  Timestamp:      1684763355  (GMT+3, not UTC)

── 6. Status ────────────────────────────────────────────
  Error: ERR030 — Message stuck in queue with error

── 7. Delivery Report (DLR) ─────────────────────────────
  Error: ERR019 — No reports found

── Done ─────────────────────────────────────────────────
```

## How It Works

### Authentication

Every request includes `username` and `password` in the JSON body:

```csharp
var payload = new Dictionary<string, object>
{
    ["username"] = "csharp_username",
    ["password"] = "csharp_password"
};
```

### Request format

All endpoints use the same pattern:

```
POST https://www.kwtsms.com/API/{endpoint}/
Content-Type: application/json
Accept: application/json

{"username":"...","password":"...","other":"fields"}
```

- Always `POST`, never `GET` (GET exposes credentials in server logs)
- Always set `Content-Type: application/json` (omitting it switches to the legacy text API)
- Always set `Accept: application/json`

### Response format

Every response follows the same structure:

```json
// Success
{"result": "OK", "available": 150, "purchased": 1000}

// Error
{"result": "ERROR", "code": "ERR003", "description": "Authentication error"}
```

Check `result` first, then read the relevant fields.

### The helper function

The example uses one helper that handles all the HTTP plumbing:

```csharp
async Task<Dictionary<string, JsonElement>> CallApi(string endpoint, Dictionary<string, object> payload)
```

It serializes the payload to JSON, POSTs it to `https://www.kwtsms.com/API/{endpoint}/`, and returns the parsed JSON response. Every endpoint section then just builds a payload dictionary and calls this helper.

## Key Points

- **Test mode** (`"test": "1"`): messages are queued but not delivered. No credits consumed. Delete from queue on your dashboard to recover tentatively held credits.
- **Status in test mode**: returns `ERR030` (message stuck in queue) — this is normal.
- **DLR for Kuwait numbers**: returns `ERR019` or `ERR021` — delivery reports are only available for international numbers.
- **Phone numbers**: digits only, international format, no `+` or `00` prefix.
- **Timestamp**: all `unix-timestamp` values are GMT+3 (Asia/Kuwait), not UTC.
- **Rate limit**: max 10 requests/second per IP. The example calls endpoints sequentially so this is not an issue.

## Error Codes

| Code | Meaning |
|------|---------|
| ERR003 | Wrong username or password |
| ERR006 | No valid numbers submitted |
| ERR009 | Empty message |
| ERR010 | Zero balance |
| ERR019 | No DLR reports found |
| ERR025 | Invalid number format |
| ERR028 | Must wait 15s before sending to same number again |
| ERR030 | Message stuck in queue (normal for test mode) |

See the [full error code reference](https://www.kwtsms.com/doc/KwtSMS.com_API_Documentation_v41.pdf) for all codes.

## Next Steps

- Switch to `"test": "0"` and use your registered sender ID for live sends
- Use the [KwtSMS NuGet package](https://www.nuget.org/packages/KwtSMS) for automatic phone normalization, batching, retry, and error enrichment
- See [BasicUsage](../BasicUsage/) for the same operations using the client library
