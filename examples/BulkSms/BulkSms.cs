// Bulk SMS example for KwtSMS C# client
//
// Demonstrates sending to many numbers with auto-batching.

using KwtSMS;

var sms = KwtSmsClient.FromEnv();

// Verify connection
var (ok, balance, error) = sms.Verify();
if (!ok)
{
    Console.WriteLine($"Connection failed: {error}");
    return;
}
Console.WriteLine($"Balance: {balance} credits");

// For >200 numbers, the client automatically splits into batches of 200
// with a 0.5s delay between batches to respect rate limits.
// ERR013 (queue full) is retried automatically with exponential backoff.

var numbers = new[]
{
    "965XXXXXXXX",
    "96512345678",
    "+96587654321",
    "00965XXXXXXXX"
};

var message = "Special offer from MyStore: 20% off all items this weekend. Reply STOP to unsubscribe.";

var result = sms.Send(numbers, message);

Console.WriteLine($"Result: {result.Result}");
Console.WriteLine($"Numbers sent: {result.Numbers}");
Console.WriteLine($"Credits used: {result.PointsCharged}");
Console.WriteLine($"Balance after: {result.BalanceAfter}");

if (result.Invalid != null && result.Invalid.Count > 0)
{
    Console.WriteLine("\nInvalid numbers (not sent):");
    foreach (var invalid in result.Invalid)
    {
        Console.WriteLine($"  {invalid.Input}: {invalid.Error}");
    }
}
