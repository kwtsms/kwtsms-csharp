// ASP.NET Core minimal API endpoint example for KwtSMS
//
// Demonstrates integrating kwtSMS into an ASP.NET Core web application.
// Requires: dotnet add package KwtSMS

using KwtSMS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register KwtSmsClient as a singleton (thread-safe, reuses HttpClient)
builder.Services.AddSingleton<KwtSmsClient>(sp =>
{
    return KwtSmsClient.FromEnv();
});

var app = builder.Build();

// POST /api/send-otp
// Body: { "phone": "+965XXXXXXXX" }
app.MapPost("/api/send-otp", async (HttpContext context, KwtSmsClient sms) =>
{
    var body = await context.Request.ReadFromJsonAsync<OtpRequest>();
    if (body == null || string.IsNullOrEmpty(body.Phone))
    {
        return Results.BadRequest(new { error = "Phone number is required" });
    }

    // Validate phone locally
    var validation = PhoneUtils.ValidatePhoneInput(body.Phone);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { error = validation.Error });
    }

    // TODO: Add rate limiting per phone number (max 3-5/hour)
    // TODO: Add rate limiting per IP (max 10-20/hour)
    // TODO: Verify CAPTCHA token before sending

    // Generate OTP
    var otp = Random.Shared.Next(100000, 999999).ToString();

    // Send
    var result = sms.Send(validation.Normalized, $"Your OTP for MyApp is: {otp}");

    if (result.Result == "OK")
    {
        // TODO: Store OTP with 5-minute expiry in your database
        return Results.Ok(new
        {
            success = true,
            message = "OTP sent successfully",
            // Never return the OTP in the response!
            resendAfterSeconds = 240 // 4 minutes
        });
    }

    // Map errors to user-facing messages
    return result.Code switch
    {
        "ERR006" or "ERR025" => Results.BadRequest(new
        {
            error = "Please enter a valid phone number in international format."
        }),
        "ERR028" => Results.StatusCode(429, new
        {
            error = "Please wait a moment before requesting another code."
        }),
        _ => Results.StatusCode(503, new
        {
            error = "SMS service is temporarily unavailable. Please try again later."
        })
    };
});

app.Run();

record OtpRequest(string Phone);
