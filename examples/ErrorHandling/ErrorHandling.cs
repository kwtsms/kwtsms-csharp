// Error handling example for KwtSMS C# client
//
// Demonstrates how to handle all error scenarios gracefully.

using KwtSMS;

var sms = KwtSmsClient.FromEnv();

// Verify first
var (ok, balance, error) = sms.Verify();
if (!ok)
{
    Console.WriteLine($"Cannot connect to SMS gateway: {error}");
    // In production: log the error, alert admin, show generic message to user
    return;
}

// Example 1: Phone validation before send
Console.WriteLine("=== Phone Validation ===");
var testNumbers = new[] { "+965XXXXXXXX", "user@email.com", "123", "abc", "" };
foreach (var num in testNumbers)
{
    var validation = PhoneUtils.ValidatePhoneInput(num);
    if (validation.IsValid)
        Console.WriteLine($"  {num} -> OK ({validation.Normalized})");
    else
        Console.WriteLine($"  {num} -> REJECTED: {validation.Error}");
}

// Example 2: Message cleaning
Console.WriteLine("\n=== Message Cleaning ===");
var dirtyMessage = "Hello \U0001F600 World <b>bold</b> \u200B\u0661\u0662\u0663";
var cleanMessage = MessageUtils.CleanMessage(dirtyMessage);
Console.WriteLine($"  Before: {dirtyMessage}");
Console.WriteLine($"  After:  {cleanMessage}");

// Example 3: Send with error handling
Console.WriteLine("\n=== Send with Error Handling ===");
var result = sms.Send("965XXXXXXXX", "Test from C# error handling example");
Console.WriteLine($"  Result: {result.Result}");
if (result.Result == "OK")
{
    Console.WriteLine($"  Message ID: {result.MsgId}");
    Console.WriteLine($"  Balance after: {result.BalanceAfter}");
}
else
{
    Console.WriteLine($"  Error code: {result.Code}");
    Console.WriteLine($"  Description: {result.Description}");
    Console.WriteLine($"  Action: {result.Action}");
}

// Example 4: All available error codes
Console.WriteLine("\n=== All Error Codes ===");
foreach (var kvp in ApiErrors.Errors)
{
    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
}

// Example 5: Check message status
Console.WriteLine("\n=== Message Status ===");
if (result.MsgId != null)
{
    var status = sms.Status(result.MsgId);
    Console.WriteLine($"  Status: {status.Status}");
    Console.WriteLine($"  Description: {status.Description}");
}
