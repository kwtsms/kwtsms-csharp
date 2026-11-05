// OTP flow example for KwtSMS C# client
//
// Demonstrates sending OTP codes with proper error handling.

using KwtSMS;

var sms = KwtSmsClient.FromEnv();

// Verify credentials first
var (ok, balance, error) = sms.Verify();
if (!ok)
{
    Console.WriteLine($"Setup error: {error}");
    return;
}

// Validate the phone number locally before sending
var validation = PhoneUtils.ValidatePhoneInput("+965XXXXXXXX");
if (!validation.IsValid)
{
    Console.WriteLine($"Invalid phone: {validation.Error}");
    return;
}

// Generate OTP (use a cryptographic random generator in production)
var otp = new Random().Next(100000, 999999).ToString();

// Always include app name in OTP messages (telecom compliance requirement)
var message = $"Your OTP for MyApp is: {otp}";

// Send OTP to a single number (never batch OTP sends)
var result = sms.Send(validation.Normalized, message);

if (result.Result == "OK")
{
    Console.WriteLine($"OTP sent. Message ID: {result.MsgId}");
    // Store msg-id and OTP in your database with a 5-minute expiry
    // Set a 3-4 minute resend timer
}
else
{
    // Map API errors to user-facing messages
    switch (result.Code)
    {
        case "ERR006":
        case "ERR025":
            Console.WriteLine("Please enter a valid phone number in international format (e.g., +965 XXXX XXXX).");
            break;
        case "ERR028":
            Console.WriteLine("Please wait a moment before requesting another code.");
            break;
        case "ERR003":
        case "ERR010":
        case "ERR011":
            // System-level errors: show generic message to user, alert admin
            Console.WriteLine("SMS service is temporarily unavailable. Please try again later.");
            // Log the real error for admin review
            Console.Error.WriteLine($"[ADMIN ALERT] SMS error: {result.Code} - {result.Description}");
            break;
        default:
            Console.WriteLine($"Could not send OTP: {result.Description}");
            break;
    }
}
