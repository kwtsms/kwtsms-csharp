// Basic usage example for KwtSMS C# client
//
// Setup:
//   1. Create a .env file with your credentials (see README)
//   2. dotnet add package KwtSMS
//   3. Run this example

using KwtSMS;

// Option 1: Load from environment variables or .env file
var sms = KwtSmsClient.FromEnv();

// Option 2: Pass credentials directly
// var sms = new KwtSmsClient("csharp_username", "csharp_password", "YOUR-SENDERID", testMode: true);

// Verify credentials and check balance
var (ok, balance, error) = sms.Verify();
if (!ok)
{
    Console.WriteLine($"Verification failed: {error}");
    return;
}
Console.WriteLine($"Connected. Balance: {balance} credits");

// Send a single SMS
var result = sms.Send("965XXXXXXXX", "Your verification code is: 123456");
if (result.Result == "OK")
{
    Console.WriteLine($"Sent. Message ID: {result.MsgId}");
    Console.WriteLine($"Balance after: {result.BalanceAfter}");
    // Always save msg-id for status checks and delivery reports
}
else
{
    Console.WriteLine($"Send failed: {result.Description}");
    if (result.Action != null)
        Console.WriteLine($"Action: {result.Action}");
}
