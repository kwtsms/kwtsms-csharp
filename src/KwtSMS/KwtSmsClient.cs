using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace KwtSMS
{
    /// <summary>
    /// C# client for the kwtSMS SMS gateway (kwtsms.com).
    /// Thread-safe. Reuse a single instance across your application.
    /// </summary>
    public class KwtSmsClient
    {
        private readonly string _username;
        private readonly string _password;
        private readonly string _senderId;
        private readonly bool _testMode;
        private readonly string _logFile;

        /// <summary>
        /// Library version, sent in the User-Agent. Read from the assembly, which the build stamps with the
        /// csproj &lt;Version&gt; (the "+commit" suffix the SDK may add is dropped).
        /// </summary>
        // 2026-11-05 14:15 +03: added so kwtSMS can count accounts per integration (#939).
        public static readonly string Version = ReadAssemblyVersion();

        // 2026-11-05 14:15 +03: User-Agent sent on every API call, "[app token ]kwtsms-csharp/<version>" (#939).
        private readonly string _userAgent;

        private readonly object _balanceLock = new object();
        private double? _cachedBalance;
        private double? _cachedPurchased;

        // 2026-10-22 15:36 +03: testMode doc corrected, test sends hold credits until deleted from the queue.
        /// <summary>
        /// Create a new kwtSMS client.
        /// </summary>
        /// <param name="username">API username (not your account mobile number).</param>
        /// <param name="password">API password.</param>
        /// <param name="senderId">Default sender ID shown on recipient's phone. Default: "KWT-SMS" (testing only).</param>
        /// <param name="testMode">When true, messages queue but are not delivered. Their credits are held until you delete them from the Sending Queue in your kwtsms.com account.</param>
        /// <param name="logFile">JSONL log file path. Empty string to disable logging.</param>
        public KwtSmsClient(
            string username,
            string password,
            string senderId = "KWT-SMS",
            bool testMode = false,
            string logFile = "kwtsms.log")
            : this(username, password, senderId, testMode, logFile, "")
        {
        }

        /// <summary>
        /// Create a new kwtSMS client that also names the calling app in the User-Agent.
        /// </summary>
        /// <param name="username">API username (not your account mobile number).</param>
        /// <param name="password">API password.</param>
        /// <param name="senderId">Default sender ID shown on recipient's phone.</param>
        /// <param name="testMode">When true, messages queue but are not delivered (credits held until deleted from the Sending Queue).</param>
        /// <param name="logFile">JSONL log file path. Empty string to disable logging.</param>
        /// <param name="appName">Product token of the calling app or plugin, e.g. "kwtsms-wordpress/3.5.2", sent before the library token.</param>
        // 2026-11-05 14:15 +03: new overload (the 5-argument constructor keeps its binary signature), #939.
        public KwtSmsClient(
            string username,
            string password,
            string senderId,
            bool testMode,
            string logFile,
            string appName)
        {
            _userAgent = BuildUserAgent(appName);
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _senderId = senderId ?? "KWT-SMS";
            _testMode = testMode;
            _logFile = logFile ?? "";
        }

        /// <summary>The User-Agent this client sends: "[app token ]kwtsms-csharp/&lt;Version&gt;".</summary>
        // 2026-11-05 14:15 +03: added for per-integration counts (#939).
        public string UserAgent => _userAgent;

        /// <summary>
        /// Build the User-Agent from an optional app token. The token is caller input, so control characters
        /// (CR/LF would split the header) are removed, whitespace runs become '-', leading and trailing '-' are
        /// trimmed, and it is cut to 64 characters. Non-ASCII characters are dropped too: .NET refuses to send a
        /// header with them, which would fail every request. Empty gives the library token only.
        /// </summary>
        // 2026-11-05 14:15 +03: added for per-integration counts (#939).
        public static string BuildUserAgent(string? appName = "")
        {
            var sb = new StringBuilder();
            foreach (var c in appName ?? "")
            {
                if (!char.IsControl(c) && (c < 0x80 || char.IsWhiteSpace(c))) sb.Append(c);
            }
            var app = Regex.Replace(sb.ToString(), @"\s+", "-").Trim('-');
            if (app.Length > 64) app = app.Substring(0, 64).TrimEnd('-');
            var lib = "kwtsms-csharp/" + Version;
            return app.Length == 0 ? lib : app + " " + lib;
        }

        private static string ReadAssemblyVersion()
        {
            var asm = typeof(KwtSmsClient).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(info)) info = asm.GetName().Version?.ToString(3) ?? "0.0.0";
            var plus = info!.IndexOf('+');
            return plus >= 0 ? info.Substring(0, plus) : info;
        }

        /// <summary>
        /// Create a client from environment variables or a .env file.
        /// Reads: KWTSMS_USERNAME, KWTSMS_PASSWORD, KWTSMS_SENDER_ID, KWTSMS_TEST_MODE, KWTSMS_LOG_FILE.
        /// </summary>
        /// <param name="envFile">Path to .env file. Defaults to ".env".</param>
        /// <returns>Configured KwtSmsClient instance.</returns>
        public static KwtSmsClient FromEnv(string envFile = ".env")
        {
            var env = EnvLoader.LoadEnvFile(envFile);

            string GetVar(string key, string defaultValue = "")
            {
                var val = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(val)) return val;
                if (env.TryGetValue(key, out var envVal) && !string.IsNullOrEmpty(envVal))
                    return envVal;
                return defaultValue;
            }

            var username = GetVar("KWTSMS_USERNAME");
            var password = GetVar("KWTSMS_PASSWORD");
            var senderId = GetVar("KWTSMS_SENDER_ID", "KWT-SMS");
            var testModeStr = GetVar("KWTSMS_TEST_MODE", "0");
            var logFile = GetVar("KWTSMS_LOG_FILE", "kwtsms.log");

            var testMode = testModeStr == "1" || testModeStr.Equals("true", StringComparison.OrdinalIgnoreCase);

            return new KwtSmsClient(username, password, senderId, testMode, logFile);
        }

        /// <summary>
        /// Whether test mode is enabled. When true, messages queue but are not delivered.
        /// </summary>
        public bool TestMode => _testMode;

        /// <summary>
        /// Cached available balance from the last Verify() or successful Send() call.
        /// Null if no API call has been made yet.
        /// </summary>
        public double? CachedBalance
        {
            get { lock (_balanceLock) { return _cachedBalance; } }
        }

        /// <summary>
        /// Cached total purchased credits from the last Verify() or Balance() call.
        /// Null if no call has been made yet.
        /// </summary>
        public double? CachedPurchased
        {
            get { lock (_balanceLock) { return _cachedPurchased; } }
        }

        /// <summary>
        /// Test credentials and check balance. Never throws exceptions.
        /// </summary>
        /// <returns>Tuple of (ok, balance, error). ok is true if credentials are valid.</returns>
        public (bool Ok, double? Balance, string? Error) Verify()
        {
            try
            {
                var payload = AuthPayload();
                var response = HttpRequest.Post("balance", payload, _logFile, _userAgent);

                var result = response.ContainsKey("result") ? response["result"]?.ToString() : null;

                if (result == "OK")
                {
                    var balance = GetDouble(response, "available");
                    var purchased = GetDouble(response, "purchased");

                    lock (_balanceLock)
                    {
                        _cachedBalance = balance;
                        _cachedPurchased = purchased;
                    }

                    return (true, balance, null);
                }

                var error = response.ContainsKey("description")
                    ? response["description"]?.ToString()
                    : "Unknown error";

                if (response.ContainsKey("action"))
                    error += " " + response["action"]?.ToString();

                return (false, null, error);
            }
            catch (Exception ex)
            {
                Logger.WriteLog(_logFile, "balance", null, null, false, $"Verify error: {ex.Message}");
                return (false, null, "Could not connect to the SMS service. Check your network connection.");
            }
        }

        /// <summary>
        /// Get current balance. Returns cached value on API failure. Null if no data available.
        /// </summary>
        public double? Balance()
        {
            try
            {
                var payload = AuthPayload();
                var response = HttpRequest.Post("balance", payload, _logFile, _userAgent);

                var result = response.ContainsKey("result") ? response["result"]?.ToString() : null;

                if (result == "OK")
                {
                    var balance = GetDouble(response, "available");
                    var purchased = GetDouble(response, "purchased");

                    lock (_balanceLock)
                    {
                        _cachedBalance = balance;
                        _cachedPurchased = purchased;
                    }

                    return balance;
                }

                // Return cached value on failure
                lock (_balanceLock)
                {
                    return _cachedBalance;
                }
            }
            catch
            {
                lock (_balanceLock)
                {
                    return _cachedBalance;
                }
            }
        }

        /// <summary>
        /// Send SMS to one or more phone numbers.
        /// Numbers are normalized and validated before sending.
        /// For more than 200 numbers, automatically batches into groups of 200.
        /// Never throws exceptions.
        /// </summary>
        /// <param name="mobile">Phone number(s). Can be a single number, comma-separated, or multiple strings.</param>
        /// <param name="message">SMS message text. Cleaned automatically (emojis stripped, Arabic digits converted).</param>
        /// <param name="sender">Optional sender ID override. Uses default if null.</param>
        /// <returns>SendResult with result, msg-id, balance, or error details.</returns>
        public SendResult Send(string mobile, string message, string? sender = null)
        {
            return Send(new[] { mobile }, message, sender);
        }

        /// <summary>
        /// Send SMS to multiple phone numbers.
        /// </summary>
        public SendResult Send(string[] mobiles, string message, string? sender = null)
        {
            try
            {
                // Clean message
                var cleanedMessage = MessageUtils.CleanMessage(message);
                if (string.IsNullOrWhiteSpace(cleanedMessage))
                {
                    return new SendResult
                    {
                        Result = "ERROR",
                        Code = "ERR009",
                        Description = "Message is empty after cleaning (emojis, HTML, and control characters were removed).",
                        Action = ApiErrors.Errors.ContainsKey("ERR009") ? ApiErrors.Errors["ERR009"] : null
                    };
                }

                // Parse and validate all numbers
                var allNumbers = new List<string>();
                foreach (var m in mobiles)
                {
                    if (string.IsNullOrEmpty(m)) continue;
                    foreach (var part in m.Split(','))
                    {
                        var trimmed = part.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            allNumbers.Add(trimmed);
                    }
                }

                var validNumbers = new List<string>();
                var invalidEntries = new List<InvalidEntry>();

                foreach (var num in allNumbers)
                {
                    var validation = PhoneUtils.ValidatePhoneInput(num);
                    if (validation.IsValid)
                    {
                        validNumbers.Add(validation.Normalized);
                    }
                    else
                    {
                        invalidEntries.Add(new InvalidEntry
                        {
                            Input = num,
                            Error = validation.Error ?? "Invalid phone number"
                        });
                    }
                }

                // Deduplicate normalized numbers
                validNumbers = validNumbers.Distinct().ToList();

                // All numbers invalid
                if (validNumbers.Count == 0)
                {
                    return new SendResult
                    {
                        Result = "ERROR",
                        Code = "ERR_INVALID_INPUT",
                        Description = "No valid phone numbers provided.",
                        Action = ApiErrors.Errors.ContainsKey("ERR_INVALID_INPUT") ? ApiErrors.Errors["ERR_INVALID_INPUT"] : null,
                        Invalid = invalidEntries.Count > 0 ? invalidEntries : null
                    };
                }

                // Bulk send for >200 numbers
                if (validNumbers.Count > 200)
                {
                    return SendBulk(validNumbers, cleanedMessage, sender ?? _senderId, invalidEntries);
                }

                // Single send
                var payload = AuthPayload();
                payload["sender"] = sender ?? _senderId;
                payload["mobile"] = string.Join(",", validNumbers);
                payload["message"] = cleanedMessage;
                payload["test"] = _testMode ? "1" : "0";

                var response = HttpRequest.Post("send", payload, _logFile, _userAgent);

                var sendResult = new SendResult
                {
                    Result = response.ContainsKey("result") ? response["result"]?.ToString() ?? "" : "",
                    Invalid = invalidEntries.Count > 0 ? invalidEntries : null
                };

                if (sendResult.Result == "OK")
                {
                    sendResult.MsgId = response.ContainsKey("msg-id") ? response["msg-id"]?.ToString() : null;
                    sendResult.Numbers = GetInt(response, "numbers");
                    sendResult.PointsCharged = GetInt(response, "points-charged");
                    sendResult.BalanceAfter = GetDouble(response, "balance-after");
                    sendResult.UnixTimestamp = GetLong(response, "unix-timestamp");

                    if (sendResult.BalanceAfter.HasValue)
                    {
                        lock (_balanceLock) { _cachedBalance = sendResult.BalanceAfter; }
                    }
                }
                else
                {
                    sendResult.Code = response.ContainsKey("code") ? response["code"]?.ToString() : null;
                    sendResult.Description = response.ContainsKey("description") ? response["description"]?.ToString() : null;
                    sendResult.Action = response.ContainsKey("action") ? response["action"]?.ToString() : null;
                }

                return sendResult;
            }
            catch (Exception ex)
            {
                Logger.WriteLog(_logFile, "send", null, null, false, $"Send error: {ex.Message}");
                return new SendResult
                {
                    Result = "ERROR",
                    Code = "CLIENT_ERROR",
                    Description = "An unexpected error occurred in the SMS client."
                };
            }
        }

        // 2026-10-22 15:36 +03: retry off by default and wait is a parameter (default 10 s), was a hardcoded 16 s; ERR028 is opt-in with a per-account window.
        /// <summary>
        /// Send SMS with optional retry on ERR028 (duplicate SMS protection).
        /// Retry is off by default (maxRetries = 0): ERR028 is returned like any other error.
        /// </summary>
        /// <param name="mobile">Phone number(s).</param>
        /// <param name="message">SMS message text.</param>
        /// <param name="sender">Optional sender ID override.</param>
        /// <param name="maxRetries">Maximum number of ERR028 retries (default 0, retry off).</param>
        /// <param name="waitSeconds">Seconds to wait before each retry (default 10, the API's default window).</param>
        /// <returns>SendResult.</returns>
        /// <remarks>
        /// ERR028 only happens when Duplicate SMS protection is enabled in the account's API settings.
        /// The window is set per account (10 seconds by default), so pass waitSeconds to match yours.
        /// The whole request is rejected and nothing is charged.
        /// WARNING: each retry sleeps the calling thread for waitSeconds.
        /// Do not call this method on an ASP.NET Core request thread. Use a background job instead.
        /// </remarks>
        public SendResult SendWithRetry(string mobile, string message, string? sender = null, int maxRetries = 0, int waitSeconds = 10)
        {
            var result = Send(mobile, message, sender);

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                if (result.Code != "ERR028") break;
                // 2026-10-22 15:36 +03: wait the caller's window, not a fixed 16 s.
                Thread.Sleep(waitSeconds * 1000);
                result = Send(mobile, message, sender);
            }

            return result;
        }

        /// <summary>
        /// Validate phone numbers via the kwtSMS API.
        /// Pre-rejects invalid numbers locally before calling the API.
        /// </summary>
        /// <param name="phones">Phone numbers to validate.</param>
        /// <returns>ValidateResult with ok, er, nr lists and rejected numbers.</returns>
        public ValidateResult Validate(params string[] phones)
        {
            try
            {
                var validNumbers = new List<string>();
                var rejected = new List<InvalidEntry>();

                foreach (var phone in phones)
                {
                    if (string.IsNullOrEmpty(phone)) continue;

                    foreach (var part in phone.Split(','))
                    {
                        var trimmed = part.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        var validation = PhoneUtils.ValidatePhoneInput(trimmed);
                        if (validation.IsValid)
                        {
                            validNumbers.Add(validation.Normalized);
                        }
                        else
                        {
                            rejected.Add(new InvalidEntry
                            {
                                Input = trimmed,
                                Error = validation.Error ?? "Invalid"
                            });
                        }
                    }
                }

                // Deduplicate
                validNumbers = validNumbers.Distinct().ToList();

                if (validNumbers.Count == 0)
                {
                    return new ValidateResult
                    {
                        Error = "No valid phone numbers to validate.",
                        Rejected = rejected
                    };
                }

                var payload = AuthPayload();
                payload["mobile"] = string.Join(",", validNumbers);

                var response = HttpRequest.Post("validate", payload, _logFile, _userAgent);

                var validateResult = new ValidateResult { Rejected = rejected };

                var result = response.ContainsKey("result") ? response["result"]?.ToString() : null;

                if (result == "OK" && response.ContainsKey("mobile"))
                {
                    var mobileData = response["mobile"];
                    if (mobileData is Dictionary<string, object?> mobileDict)
                    {
                        validateResult.Ok = GetStringList(mobileDict, "OK");
                        validateResult.Er = GetStringList(mobileDict, "ER");
                        validateResult.Nr = GetStringList(mobileDict, "NR");
                    }

                    validateResult.Raw = response;
                }
                else
                {
                    validateResult.Error = response.ContainsKey("description")
                        ? response["description"]?.ToString()
                        : "Validation failed";
                    validateResult.Raw = response;
                }

                return validateResult;
            }
            catch (Exception ex)
            {
                Logger.WriteLog(_logFile, "validate", null, null, false, $"Validate error: {ex.Message}");
                return new ValidateResult
                {
                    Error = "An unexpected error occurred in the SMS client."
                };
            }
        }

        /// <summary>
        /// List sender IDs registered on this account.
        /// </summary>
        public SenderIdResult SenderIds()
        {
            try
            {
                var payload = AuthPayload();
                var response = HttpRequest.Post("senderid", payload, _logFile, _userAgent);

                var result = response.ContainsKey("result") ? response["result"]?.ToString() : null;

                if (result == "OK")
                {
                    return new SenderIdResult
                    {
                        Result = "OK",
                        SenderIds = GetStringList(response, "senderid")
                    };
                }

                return new SenderIdResult
                {
                    Result = "ERROR",
                    Code = response.ContainsKey("code") ? response["code"]?.ToString() : null,
                    Description = response.ContainsKey("description") ? response["description"]?.ToString() : null,
                    Action = response.ContainsKey("action") ? response["action"]?.ToString() : null
                };
            }
            catch (Exception ex)
            {
                Logger.WriteLog(_logFile, "senderid", null, null, false, $"SenderIds error: {ex.Message}");
                return new SenderIdResult
                {
                    Result = "ERROR",
                    Description = "An unexpected error occurred in the SMS client."
                };
            }
        }

        /// <summary>
        /// List active country prefixes for SMS delivery.
        /// </summary>
        public CoverageResult Coverage()
        {
            try
            {
                var payload = AuthPayload();
                var response = HttpRequest.Post("coverage", payload, _logFile, _userAgent);

                var result = response.ContainsKey("result") ? response["result"]?.ToString() : null;

                if (result == "OK")
                {
                    return new CoverageResult
                    {
                        Result = "OK",
                        Prefixes = GetStringList(response, "prefixes")
                    };
                }

                return new CoverageResult
                {
                    Result = "ERROR",
                    Code = response.ContainsKey("code") ? response["code"]?.ToString() : null,
                    Description = response.ContainsKey("description") ? response["description"]?.ToString() : null,
                    Action = response.ContainsKey("action") ? response["action"]?.ToString() : null
                };
            }
            catch (Exception ex)
            {
                Logger.WriteLog(_logFile, "coverage", null, null, false, $"Coverage error: {ex.Message}");
                return new CoverageResult
                {
                    Result = "ERROR",
                    Description = "An unexpected error occurred in the SMS client."
                };
            }
        }

        /// <summary>
        /// Check message delivery status.
        /// </summary>
        /// <param name="msgId">Message ID from a previous Send() response.</param>
        public StatusResult Status(string msgId)
        {
            try
            {
                var payload = AuthPayload();
                payload["msgid"] = msgId;

                var response = HttpRequest.Post("status", payload, _logFile, _userAgent);

                var result = response.ContainsKey("result") ? response["result"]?.ToString() : null;

                if (result == "OK")
                {
                    return new StatusResult
                    {
                        Result = "OK",
                        Status = response.ContainsKey("status") ? response["status"]?.ToString() : null,
                        Description = response.ContainsKey("description") ? response["description"]?.ToString() : null
                    };
                }

                return new StatusResult
                {
                    Result = "ERROR",
                    Code = response.ContainsKey("code") ? response["code"]?.ToString() : null,
                    Description = response.ContainsKey("description") ? response["description"]?.ToString() : null,
                    Action = response.ContainsKey("action") ? response["action"]?.ToString() : null
                };
            }
            catch (Exception ex)
            {
                Logger.WriteLog(_logFile, "status", null, null, false, $"Status error: {ex.Message}");
                return new StatusResult
                {
                    Result = "ERROR",
                    Description = "An unexpected error occurred in the SMS client."
                };
            }
        }

        /// <summary>
        /// Get delivery report for a message (international numbers only, not available for Kuwait).
        /// Wait at least 5 minutes after sending before checking.
        /// </summary>
        /// <param name="msgId">Message ID from a previous Send() response.</param>
        public DlrResult Dlr(string msgId)
        {
            try
            {
                var payload = AuthPayload();
                payload["msgid"] = msgId;

                var response = HttpRequest.Post("dlr", payload, _logFile, _userAgent);

                var result = response.ContainsKey("result") ? response["result"]?.ToString() : null;

                if (result == "OK")
                {
                    var dlrResult = new DlrResult { Result = "OK" };

                    if (response.ContainsKey("report") && response["report"] is List<object?> reportList)
                    {
                        foreach (var item in reportList)
                        {
                            if (item is Dictionary<string, object?> entry)
                            {
                                dlrResult.Report.Add(new DlrEntry
                                {
                                    Number = entry.ContainsKey("Number") ? entry["Number"]?.ToString() ?? "" : "",
                                    Status = entry.ContainsKey("Status") ? entry["Status"]?.ToString() ?? "" : ""
                                });
                            }
                        }
                    }

                    return dlrResult;
                }

                return new DlrResult
                {
                    Result = "ERROR",
                    Code = response.ContainsKey("code") ? response["code"]?.ToString() : null,
                    Description = response.ContainsKey("description") ? response["description"]?.ToString() : null,
                    Action = response.ContainsKey("action") ? response["action"]?.ToString() : null
                };
            }
            catch (Exception ex)
            {
                Logger.WriteLog(_logFile, "dlr", null, null, false, $"Dlr error: {ex.Message}");
                return new DlrResult
                {
                    Result = "ERROR",
                    Description = "An unexpected error occurred in the SMS client."
                };
            }
        }

        #region Private helpers

        private SendResult SendBulk(List<string> numbers, string message, string sender, List<InvalidEntry> invalidEntries)
        {
            var batches = new List<List<string>>();
            for (int i = 0; i < numbers.Count; i += 200)
            {
                batches.Add(numbers.GetRange(i, Math.Min(200, numbers.Count - i)));
            }

            var msgIds = new List<string>();
            var errors = new List<Dictionary<string, object?>>();
            int totalNumbers = 0;
            int totalPoints = 0;
            double? lastBalance = null;

            for (int batchIdx = 0; batchIdx < batches.Count; batchIdx++)
            {
                // Rate limit: 0.5s between batches
                if (batchIdx > 0) Thread.Sleep(500);

                var batch = batches[batchIdx];
                var batchResult = SendBatchWithRetry(batch, message, sender);

                if (batchResult.ContainsKey("result") && batchResult["result"]?.ToString() == "OK")
                {
                    if (batchResult.ContainsKey("msg-id"))
                        msgIds.Add(batchResult["msg-id"]?.ToString() ?? "");

                    totalNumbers += GetIntFromDict(batchResult, "numbers");
                    totalPoints += GetIntFromDict(batchResult, "points-charged");

                    var bal = GetDoubleFromDict(batchResult, "balance-after");
                    if (bal.HasValue) lastBalance = bal;
                }
                else
                {
                    var errDict = new Dictionary<string, object?>
                    {
                        ["batch"] = batchIdx + 1,
                        ["numbers"] = string.Join(",", batch)
                    };
                    if (batchResult.ContainsKey("code"))
                        errDict["code"] = batchResult["code"]?.ToString() ?? "";
                    if (batchResult.ContainsKey("description"))
                        errDict["description"] = batchResult["description"]?.ToString() ?? "";
                    errors.Add(errDict);
                }
            }

            if (lastBalance.HasValue)
            {
                lock (_balanceLock) { _cachedBalance = lastBalance; }
            }

            string overallResult;
            if (errors.Count == 0)
                overallResult = "OK";
            else if (msgIds.Count > 0)
                overallResult = "PARTIAL";
            else
                overallResult = "ERROR";

            // Return as SendResult with bulk info in description
            return new SendResult
            {
                Result = overallResult,
                MsgId = msgIds.Count > 0 ? string.Join(",", msgIds) : null,
                Numbers = totalNumbers,
                PointsCharged = totalPoints,
                BalanceAfter = lastBalance,
                Invalid = invalidEntries.Count > 0 ? invalidEntries : null,
                Description = overallResult == "PARTIAL"
                    ? $"Bulk send completed with {errors.Count} failed batch(es) out of {batches.Count}."
                    : overallResult == "ERROR"
                        ? $"All {batches.Count} batches failed."
                        : null
            };
        }

        private Dictionary<string, object?> SendBatchWithRetry(List<string> numbers, string message, string sender)
        {
            var backoffMs = new[] { 30000, 60000, 120000 };

            var payload = AuthPayload();
            payload["sender"] = sender;
            payload["mobile"] = string.Join(",", numbers);
            payload["message"] = message;
            payload["test"] = _testMode ? "1" : "0";

            var response = HttpRequest.Post("send", payload, _logFile, _userAgent);

            for (int retry = 0; retry < 3; retry++)
            {
                if (!response.ContainsKey("code") || response["code"]?.ToString() != "ERR013")
                    break;

                Thread.Sleep(backoffMs[retry]);
                response = HttpRequest.Post("send", payload, _logFile, _userAgent);
            }

            return response;
        }

        private Dictionary<string, object?> AuthPayload()
        {
            return new Dictionary<string, object?>
            {
                ["username"] = _username,
                ["password"] = _password
            };
        }

        private static double? GetDouble(Dictionary<string, object?> dict, string key)
        {
            if (!dict.ContainsKey(key) || dict[key] == null) return null;

            var val = dict[key]!;
            if (val is double d) return d;
            if (val is long l) return l;
            if (val is int i) return i;
            if (double.TryParse(val.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return null;
        }

        private static int? GetInt(Dictionary<string, object?> dict, string key)
        {
            if (!dict.ContainsKey(key) || dict[key] == null) return null;

            var val = dict[key]!;
            if (val is int i) return i;
            if (val is long l) return (int)l;
            if (val is double d) return (int)d;
            if (int.TryParse(val.ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return null;
        }

        private static long? GetLong(Dictionary<string, object?> dict, string key)
        {
            if (!dict.ContainsKey(key) || dict[key] == null) return null;

            var val = dict[key]!;
            if (val is long l) return l;
            if (val is int i) return i;
            if (val is double d) return (long)d;
            if (long.TryParse(val.ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return null;
        }

        private static int GetIntFromDict(Dictionary<string, object?> dict, string key)
        {
            return GetInt(dict, key) ?? 0;
        }

        private static double? GetDoubleFromDict(Dictionary<string, object?> dict, string key)
        {
            return GetDouble(dict, key);
        }

        private static List<string> GetStringList(Dictionary<string, object?> dict, string key)
        {
            if (!dict.ContainsKey(key) || dict[key] == null)
                return new List<string>();

            if (dict[key] is List<object?> list)
            {
                return list
                    .Where(x => x != null)
                    .Select(x => x!.ToString()!)
                    .ToList();
            }

            return new List<string>();
        }

        #endregion
    }
}
