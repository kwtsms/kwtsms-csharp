// Production-grade OTP service for KwtSMS C# client
//
// This example demonstrates a complete OTP flow with:
// - Rate limiting per phone and per IP
// - OTP generation, storage, and verification
// - Resend timer enforcement
// - Proper error mapping for end users
//
// Adapt the IOtpStore interface for your storage backend (Redis, SQL, in-memory).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using KwtSMS;

namespace OtpProduction
{
    /// <summary>
    /// Configuration for the OTP service.
    /// </summary>
    public class OtpConfig
    {
        /// <summary>App name included in OTP messages (telecom compliance).</summary>
        public string AppName { get; set; } = "MyApp";

        /// <summary>OTP length (digits).</summary>
        public int OtpLength { get; set; } = 6;

        /// <summary>OTP expiry in seconds.</summary>
        public int ExpirySeconds { get; set; } = 300; // 5 minutes

        /// <summary>Minimum seconds between resends to the same number.</summary>
        public int ResendCooldownSeconds { get; set; } = 240; // 4 minutes (KNET standard)

        /// <summary>Max OTP requests per phone per hour.</summary>
        public int MaxPerPhonePerHour { get; set; } = 5;

        /// <summary>Max OTP requests per IP per hour.</summary>
        public int MaxPerIpPerHour { get; set; } = 20;
    }

    /// <summary>
    /// Stored OTP entry.
    /// </summary>
    public class OtpEntry
    {
        public string Phone { get; set; } = "";
        public string Code { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public DateTime SentAt { get; set; }
        public int Attempts { get; set; }
    }

    /// <summary>
    /// OTP storage interface. Implement with Redis, SQL, or in-memory for your use case.
    /// </summary>
    public interface IOtpStore
    {
        OtpEntry? Get(string phone);
        void Set(string phone, OtpEntry entry);
        void Delete(string phone);
        int GetRequestCount(string key, TimeSpan window);
        void IncrementRequestCount(string key);
    }

    /// <summary>
    /// In-memory OTP store for development. Use Redis or SQL in production.
    /// </summary>
    public class InMemoryOtpStore : IOtpStore
    {
        private readonly ConcurrentDictionary<string, OtpEntry> _entries = new();
        private readonly ConcurrentDictionary<string, List<DateTime>> _rateLimits = new();

        public OtpEntry? Get(string phone) =>
            _entries.TryGetValue(phone, out var entry) ? entry : null;

        public void Set(string phone, OtpEntry entry) =>
            _entries[phone] = entry;

        public void Delete(string phone) =>
            _entries.TryRemove(phone, out _);

        public int GetRequestCount(string key, TimeSpan window)
        {
            if (!_rateLimits.TryGetValue(key, out var timestamps))
                return 0;

            var cutoff = DateTime.UtcNow - window;
            timestamps.RemoveAll(t => t < cutoff);
            return timestamps.Count;
        }

        public void IncrementRequestCount(string key)
        {
            _rateLimits.AddOrUpdate(key,
                _ => new List<DateTime> { DateTime.UtcNow },
                (_, list) => { list.Add(DateTime.UtcNow); return list; });
        }
    }

    /// <summary>
    /// Result of an OTP operation.
    /// </summary>
    public class OtpResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? ResendAfterSeconds { get; set; }
        public string? InternalError { get; set; } // For admin logging only
    }

    /// <summary>
    /// Production OTP service with rate limiting and proper error handling.
    /// </summary>
    public class OtpService
    {
        private readonly KwtSmsClient _sms;
        private readonly IOtpStore _store;
        private readonly OtpConfig _config;
        private readonly Random _random = new();

        public OtpService(KwtSmsClient sms, IOtpStore store, OtpConfig? config = null)
        {
            _sms = sms;
            _store = store;
            _config = config ?? new OtpConfig();
        }

        /// <summary>
        /// Request an OTP for a phone number.
        /// </summary>
        /// <param name="rawPhone">Phone number in any format.</param>
        /// <param name="clientIp">Client IP address for rate limiting.</param>
        /// <returns>User-safe result. Log InternalError for admin alerts.</returns>
        public OtpResult RequestOtp(string rawPhone, string clientIp)
        {
            // 1. Validate phone
            var validation = PhoneUtils.ValidatePhoneInput(rawPhone);
            if (!validation.IsValid)
            {
                return new OtpResult
                {
                    Success = false,
                    Message = "Please enter a valid phone number in international format (e.g., +965 XXXX XXXX)."
                };
            }

            var phone = validation.Normalized;

            // 2. Rate limit per phone
            var phoneCount = _store.GetRequestCount($"phone:{phone}", TimeSpan.FromHours(1));
            if (phoneCount >= _config.MaxPerPhonePerHour)
            {
                return new OtpResult
                {
                    Success = false,
                    Message = $"Too many requests to this number. Please try again in {60 - (DateTime.UtcNow.Minute % 60)} minutes."
                };
            }

            // 3. Rate limit per IP
            var ipCount = _store.GetRequestCount($"ip:{clientIp}", TimeSpan.FromHours(1));
            if (ipCount >= _config.MaxPerIpPerHour)
            {
                return new OtpResult
                {
                    Success = false,
                    Message = "Too many requests. Please try again later."
                };
            }

            // 4. Check resend cooldown
            var existing = _store.Get(phone);
            if (existing != null)
            {
                var elapsed = (DateTime.UtcNow - existing.SentAt).TotalSeconds;
                if (elapsed < _config.ResendCooldownSeconds)
                {
                    var remaining = (int)(_config.ResendCooldownSeconds - elapsed);
                    return new OtpResult
                    {
                        Success = false,
                        Message = $"Please wait {remaining} seconds before requesting a new code.",
                        ResendAfterSeconds = remaining
                    };
                }
            }

            // 5. Generate new OTP (invalidates previous)
            var code = GenerateOtp();
            var message = $"Your OTP for {_config.AppName} is: {code}";

            // 6. Send via kwtSMS
            var result = _sms.Send(phone, message);

            if (result.Result == "OK")
            {
                // 7. Store OTP with expiry
                _store.Set(phone, new OtpEntry
                {
                    Phone = phone,
                    Code = code,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(_config.ExpirySeconds),
                    SentAt = DateTime.UtcNow,
                    Attempts = 0
                });

                // 8. Increment rate limit counters
                _store.IncrementRequestCount($"phone:{phone}");
                _store.IncrementRequestCount($"ip:{clientIp}");

                return new OtpResult
                {
                    Success = true,
                    Message = "Verification code sent.",
                    ResendAfterSeconds = _config.ResendCooldownSeconds
                };
            }

            // Map API errors to user messages
            return result.Code switch
            {
                "ERR028" => new OtpResult
                {
                    Success = false,
                    Message = "Please wait a moment before requesting another code.",
                    ResendAfterSeconds = 15
                },
                "ERR006" or "ERR025" => new OtpResult
                {
                    Success = false,
                    Message = "Please enter a valid phone number in international format."
                },
                _ => new OtpResult
                {
                    Success = false,
                    Message = "SMS service is temporarily unavailable. Please try again later.",
                    InternalError = $"kwtSMS error: {result.Code} - {result.Description}"
                }
            };
        }

        /// <summary>
        /// Verify an OTP code.
        /// </summary>
        /// <param name="rawPhone">Phone number the OTP was sent to.</param>
        /// <param name="code">OTP code entered by the user.</param>
        /// <returns>User-safe result.</returns>
        public OtpResult VerifyOtp(string rawPhone, string code)
        {
            var validation = PhoneUtils.ValidatePhoneInput(rawPhone);
            if (!validation.IsValid)
            {
                return new OtpResult { Success = false, Message = "Invalid phone number." };
            }

            var phone = validation.Normalized;
            var entry = _store.Get(phone);

            if (entry == null)
            {
                return new OtpResult { Success = false, Message = "No verification code found. Please request a new one." };
            }

            if (DateTime.UtcNow > entry.ExpiresAt)
            {
                _store.Delete(phone);
                return new OtpResult { Success = false, Message = "Verification code has expired. Please request a new one." };
            }

            entry.Attempts++;
            if (entry.Attempts > 5)
            {
                _store.Delete(phone);
                return new OtpResult { Success = false, Message = "Too many incorrect attempts. Please request a new code." };
            }

            if (entry.Code != code)
            {
                _store.Set(phone, entry);
                return new OtpResult { Success = false, Message = "Invalid verification code." };
            }

            // Success: delete the OTP
            _store.Delete(phone);
            return new OtpResult { Success = true, Message = "Phone number verified." };
        }

        private string GenerateOtp()
        {
            var max = (int)Math.Pow(10, _config.OtpLength);
            var min = (int)Math.Pow(10, _config.OtpLength - 1);
            return _random.Next(min, max).ToString();
        }
    }
}
