// Raw API example - call every kwtSMS endpoint directly with HttpClient.
// No external libraries required, just System.Net.Http and System.Text.Json.
//
// This shows exactly what the KwtSMS client library does under the hood.
// Copy any section into your own code to call the API directly.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

// ── Configuration ──────────────────────────────────────────────────────────
// Replace these with your API credentials from kwtsms.com → Account → API.

string username = "csharp_username";
string password = "csharp_password";
string senderId = "KWT-SMS";     // Use your registered sender ID in production
string testMode = "1";           // "1" = test (queued, not delivered), "0" = live

// ── Helper ─────────────────────────────────────────────────────────────────
// Single helper: POST JSON to a kwtSMS endpoint, return parsed response.

var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

async Task<Dictionary<string, JsonElement>> CallApi(string endpoint, Dictionary<string, object> payload)
{
    var url = $"https://www.kwtsms.com/API/{endpoint}/";
    var json = JsonSerializer.Serialize(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    using var request = new HttpRequestMessage(HttpMethod.Post, url);
    request.Content = content;
    request.Headers.Accept.Clear();
    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

    var response = await http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body)!;
}

Dictionary<string, object> Auth() => new()
{
    ["username"] = username,
    ["password"] = password
};


// ═══════════════════════════════════════════════════════════════════════════
// 1. BALANCE - Check available and purchased credits
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── 1. Balance ───────────────────────────────────────────");

var balanceResult = await CallApi("balance", Auth());

if (balanceResult["result"].GetString() == "OK")
{
    Console.WriteLine($"  Available: {balanceResult["available"]}");
    Console.WriteLine($"  Purchased: {balanceResult["purchased"]}");
}
else
{
    Console.WriteLine($"  Error: {balanceResult["code"]} - {balanceResult["description"]}");
}
Console.WriteLine();


// ═══════════════════════════════════════════════════════════════════════════
// 2. SENDER IDS - List registered sender IDs on this account
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── 2. Sender IDs ────────────────────────────────────────");

var senderResult = await CallApi("senderid", Auth());

if (senderResult["result"].GetString() == "OK")
{
    Console.Write("  IDs: ");
    foreach (var id in senderResult["senderid"].EnumerateArray())
        Console.Write($"{id.GetString()}  ");
    Console.WriteLine();
}
else
{
    Console.WriteLine($"  Error: {senderResult["code"]} - {senderResult["description"]}");
}
Console.WriteLine();


// ═══════════════════════════════════════════════════════════════════════════
// 3. COVERAGE - List active country prefixes
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── 3. Coverage ──────────────────────────────────────────");

var coverageResult = await CallApi("coverage", Auth());

if (coverageResult["result"].GetString() == "OK")
{
    Console.Write("  Prefixes: ");
    foreach (var prefix in coverageResult["prefixes"].EnumerateArray())
        Console.Write($"+{prefix.GetString()}  ");
    Console.WriteLine();
}
else
{
    Console.WriteLine($"  Error: {coverageResult["code"]} - {coverageResult["description"]}");
}
Console.WriteLine();


// ═══════════════════════════════════════════════════════════════════════════
// 4. VALIDATE - Check if phone numbers are valid and routable
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── 4. Validate ──────────────────────────────────────────");

var validatePayload = Auth();
validatePayload["mobile"] = "965XXXXXXXX,966551234567";   // Kuwait + Saudi

var validateResult = await CallApi("validate", validatePayload);

if (validateResult["result"].GetString() == "OK")
{
    var mobile = validateResult["mobile"];
    Console.Write("  OK: ");
    foreach (var n in mobile.GetProperty("OK").EnumerateArray())
        Console.Write($"{n.GetString()}  ");
    Console.WriteLine();

    Console.Write("  Format errors (ER): ");
    foreach (var n in mobile.GetProperty("ER").EnumerateArray())
        Console.Write($"{n.GetString()}  ");
    Console.WriteLine();

    Console.Write("  No route (NR): ");
    foreach (var n in mobile.GetProperty("NR").EnumerateArray())
        Console.Write($"{n.GetString()}  ");
    Console.WriteLine();
}
else
{
    Console.WriteLine($"  Error: {validateResult["code"]} - {validateResult["description"]}");
}
Console.WriteLine();


// ═══════════════════════════════════════════════════════════════════════════
// 5. SEND - Send an SMS (test mode: queued but not delivered)
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── 5. Send SMS ──────────────────────────────────────────");

var sendPayload = Auth();
sendPayload["sender"]  = senderId;
sendPayload["mobile"]  = "965XXXXXXXX";
sendPayload["message"] = "Your verification code is: 123456";
sendPayload["test"]    = testMode;

var sendResult = await CallApi("send", sendPayload);

string? msgId = null;

if (sendResult["result"].GetString() == "OK")
{
    msgId = sendResult["msg-id"].GetString();
    Console.WriteLine($"  Result:         OK");
    Console.WriteLine($"  Message ID:     {msgId}");
    Console.WriteLine($"  Numbers:        {sendResult["numbers"]}");
    Console.WriteLine($"  Points charged: {sendResult["points-charged"]}");
    Console.WriteLine($"  Balance after:  {sendResult["balance-after"]}");
    Console.WriteLine($"  Timestamp:      {sendResult["unix-timestamp"]}  (GMT+3, not UTC)");
}
else
{
    Console.WriteLine($"  Error: {sendResult["code"]} - {sendResult["description"]}");
}
Console.WriteLine();


// ═══════════════════════════════════════════════════════════════════════════
// 6. STATUS - Check message queue status (requires msg-id from send)
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── 6. Status ────────────────────────────────────────────");

if (msgId != null)
{
    var statusPayload = Auth();
    statusPayload["msgid"] = msgId;

    var statusResult = await CallApi("status", statusPayload);

    if (statusResult["result"].GetString() == "OK")
    {
        Console.WriteLine($"  Status:      {statusResult["status"]}");
        Console.WriteLine($"  Description: {statusResult["description"]}");
    }
    else
    {
        // ERR030 is normal for test mode messages (stuck in queue)
        Console.WriteLine($"  Error: {statusResult["code"]} - {statusResult["description"]}");
    }
}
else
{
    Console.WriteLine("  Skipped (no msg-id from send)");
}
Console.WriteLine();


// ═══════════════════════════════════════════════════════════════════════════
// 7. DLR - Delivery report (international numbers only, not Kuwait)
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── 7. Delivery Report (DLR) ─────────────────────────────");

if (msgId != null)
{
    var dlrPayload = Auth();
    dlrPayload["msgid"] = msgId;

    var dlrResult = await CallApi("dlr", dlrPayload);

    if (dlrResult["result"].GetString() == "OK")
    {
        Console.WriteLine("  Reports:");
        foreach (var entry in dlrResult["report"].EnumerateArray())
            Console.WriteLine($"    {entry.GetProperty("Number")}: {entry.GetProperty("Status")}");
    }
    else
    {
        // ERR019/ERR021 is normal for Kuwait numbers (DLR not available)
        Console.WriteLine($"  Error: {dlrResult["code"]} - {dlrResult["description"]}");
    }
}
else
{
    Console.WriteLine("  Skipped (no msg-id from send)");
}
Console.WriteLine();


Console.WriteLine("── Done ─────────────────────────────────────────────────");
