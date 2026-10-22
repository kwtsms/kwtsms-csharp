using System;
using System.Collections.Generic;

namespace KwtSMS
{
    /// <summary>
    /// Maps all 33 kwtSMS error codes to developer-friendly action messages.
    /// </summary>
    public static class ApiErrors
    {
        /// <summary>
        /// Complete error code to action message mapping. Read-only.
        /// Useful for building custom error UIs.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Errors = new Dictionary<string, string>
        {
            ["ERR001"] = "API is disabled on this account. Enable it at kwtsms.com -> Account -> API.",
            ["ERR002"] = "A required parameter is missing. Check that username, password, sender, mobile, and message are all provided.",
            ["ERR003"] = "Wrong API username or password. Check KWTSMS_USERNAME and KWTSMS_PASSWORD. These are your API credentials, not your account mobile number.",
            ["ERR004"] = "This account does not have API access. Contact kwtSMS support to enable it.",
            ["ERR005"] = "This account is blocked. Contact kwtSMS support.",
            ["ERR006"] = "No valid phone numbers. Make sure each number includes the country code (e.g., 96598765432 for Kuwait, not 98765432).",
            ["ERR007"] = "Too many numbers in a single request (maximum 200). Split into smaller batches.",
            ["ERR008"] = "This sender ID is banned. Use a different sender ID registered on your kwtSMS account.",
            ["ERR009"] = "Message is empty. Provide a non-empty message text.",
            ["ERR010"] = "Account balance is zero. Recharge credits at kwtsms.com.",
            ["ERR011"] = "Insufficient balance for this send. Buy more credits at kwtsms.com.",
            ["ERR012"] = "Message is too long (over 6 SMS pages). Shorten your message.",
            ["ERR013"] = "Send queue is full (1000 messages). Wait a moment and try again.",
            ["ERR019"] = "No delivery reports found for this message.",
            ["ERR020"] = "Message ID does not exist. Make sure you saved the msg-id from the send response.",
            ["ERR021"] = "No delivery report available for this message yet.",
            ["ERR022"] = "Delivery reports are not ready yet. Try again after 24 hours.",
            ["ERR023"] = "Unknown delivery report error. Contact kwtSMS support.",
            ["ERR024"] = "Your IP address is not in the API whitelist. Add it at kwtsms.com -> Account -> API -> IP Lockdown, or disable IP lockdown.",
            ["ERR025"] = "Invalid phone number. Make sure the number includes the country code (e.g., 96598765432 for Kuwait, not 98765432).",
            ["ERR026"] = "This country is not activated on your account. Contact kwtSMS support to enable the destination country.",
            ["ERR027"] = "HTML tags are not allowed in the message. Remove any HTML content and try again.",
            // 2026-10-22 15:36 +03: ERR028 text corrected, protection is opt-in per account with a per-account window (10 s default), not a fixed 15 s.
            ["ERR028"] = "Sent to the same number too soon. Duplicate SMS protection is on for this account, so the whole request was rejected and nothing was charged. Wait for the account's window (10 seconds by default) before resending.",
            ["ERR029"] = "Message ID does not exist or is incorrect.",
            ["ERR030"] = "Message is stuck in the send queue with an error. Delete it at kwtsms.com -> Queue to recover credits.",
            ["ERR031"] = "Message rejected: bad language detected.",
            ["ERR032"] = "Message rejected: spam detected.",
            ["ERR033"] = "No active coverage found. Contact kwtSMS support.",
            ["ERR_INVALID_INPUT"] = "One or more phone numbers are invalid. See details above."
        };

        /// <summary>
        /// Adds a developer-friendly "action" field to an API error response dictionary.
        /// </summary>
        /// <param name="data">Dictionary containing at least "result" and optionally "code".</param>
        /// <returns>The same dictionary with "action" added if an error code is present.</returns>
        public static Dictionary<string, object?> EnrichError(Dictionary<string, object?> data)
        {
            if (data == null) return new Dictionary<string, object?>();

            object? resultObj;
            if (!data.TryGetValue("result", out resultObj)) return data;

            var result = resultObj?.ToString();
            if (result != "ERROR") return data;

            object? codeObj;
            if (!data.TryGetValue("code", out codeObj)) return data;

            var code = codeObj?.ToString();
            if (code != null && Errors.TryGetValue(code, out var action))
            {
                data["action"] = action;
            }

            return data;
        }
    }
}
