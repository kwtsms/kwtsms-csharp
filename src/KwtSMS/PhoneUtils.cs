using System;
using System.Collections.Generic;
using System.Text;

namespace KwtSMS
{
    /// <summary>
    /// Phone number normalization and validation utilities.
    /// </summary>
    public static class PhoneUtils
    {
        // ── Normalization ────────────────────────────────────────────────────────

        /// <summary>
        /// Normalize a phone number to kwtSMS-accepted format (digits only, no leading zeros).
        /// Converts Arabic-Indic and Extended Arabic-Indic digits to Latin,
        /// strips all non-digit characters, removes leading zeros, and strips
        /// the domestic trunk prefix (leading zero after the country code).
        /// Example: 9660559123456 → 966559123456 (Saudi domestic trunk 0 removed).
        /// </summary>
        public static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrEmpty(phone)) return "";

            var sb = new StringBuilder(phone!.Length);

            foreach (var ch in phone)
            {
                if (ch >= '\u0660' && ch <= '\u0669')
                    sb.Append((char)('0' + (ch - '\u0660')));
                else if (ch >= '\u06F0' && ch <= '\u06F9')
                    sb.Append((char)('0' + (ch - '\u06F0')));
                else if (ch >= '0' && ch <= '9')
                    sb.Append(ch);
            }

            var result = sb.ToString().TrimStart('0');
            if (result.Length == 0) return "";

            // Strip domestic trunk prefix (leading 0 after country code).
            // e.g. 9660559... → 966559...,  9710512... → 971512...,  20010... → 2010...
            var cc = FindCountryCode(result);
            if (cc != null)
            {
                var local = result.Substring(cc.Length);
                if (local.Length > 0 && local[0] == '0')
                    result = cc + local.TrimStart('0');
            }

            return result;
        }

        // ── Country-level validation ─────────────────────────────────────────────

        /// <summary>
        /// Per-country phone validation rules.
        /// localLengths: valid digit counts AFTER the country code.
        /// mobileStartDigits: required first character(s) of the local number (null = any).
        /// Countries not listed here pass with generic E.164 validation only (7-15 digits).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, PhoneRule> PhoneRules =
            new Dictionary<string, PhoneRule>(StringComparer.Ordinal)
            {
                // === GCC ===
                ["965"] = new PhoneRule(new[] { 8 }, new[] { "4", "5", "6", "9" }),  // Kuwait
                ["966"] = new PhoneRule(new[] { 9 }, new[] { "5" }),                  // Saudi Arabia
                ["971"] = new PhoneRule(new[] { 9 }, new[] { "5" }),                  // UAE
                ["973"] = new PhoneRule(new[] { 8 }, new[] { "3", "6" }),             // Bahrain
                ["974"] = new PhoneRule(new[] { 8 }, new[] { "3", "5", "6", "7" }),  // Qatar
                ["968"] = new PhoneRule(new[] { 8 }, new[] { "7", "9" }),             // Oman
                // === Levant ===
                ["962"] = new PhoneRule(new[] { 9 }, new[] { "7" }),                  // Jordan
                ["961"] = new PhoneRule(new[] { 7, 8 }, new[] { "3", "7", "8" }),    // Lebanon
                ["970"] = new PhoneRule(new[] { 9 }, new[] { "5" }),                  // Palestine
                ["964"] = new PhoneRule(new[] { 10 }, new[] { "7" }),                 // Iraq
                ["963"] = new PhoneRule(new[] { 9 }, new[] { "9" }),                  // Syria
                // === Other Arab ===
                ["967"] = new PhoneRule(new[] { 9 }, new[] { "7" }),                  // Yemen
                ["20"]  = new PhoneRule(new[] { 10 }, new[] { "1" }),                 // Egypt
                ["218"] = new PhoneRule(new[] { 9 }, new[] { "9" }),                  // Libya
                ["216"] = new PhoneRule(new[] { 8 }, new[] { "2", "4", "5", "9" }),  // Tunisia
                ["212"] = new PhoneRule(new[] { 9 }, new[] { "6", "7" }),             // Morocco
                ["213"] = new PhoneRule(new[] { 9 }, new[] { "5", "6", "7" }),       // Algeria
                ["249"] = new PhoneRule(new[] { 9 }, new[] { "9" }),                  // Sudan
                // === Non-Arab Middle East ===
                ["98"]  = new PhoneRule(new[] { 10 }, new[] { "9" }),                 // Iran
                ["90"]  = new PhoneRule(new[] { 10 }, new[] { "5" }),                 // Turkey
                ["972"] = new PhoneRule(new[] { 9 }, new[] { "5" }),                  // Israel
                // === South Asia ===
                ["91"]  = new PhoneRule(new[] { 10 }, new[] { "6", "7", "8", "9" }), // India
                ["92"]  = new PhoneRule(new[] { 10 }, new[] { "3" }),                 // Pakistan
                ["880"] = new PhoneRule(new[] { 10 }, new[] { "1" }),                 // Bangladesh
                ["94"]  = new PhoneRule(new[] { 9 }, new[] { "7" }),                  // Sri Lanka
                ["960"] = new PhoneRule(new[] { 7 }, new[] { "7", "9" }),             // Maldives
                // === East Asia ===
                ["86"]  = new PhoneRule(new[] { 11 }, new[] { "1" }),                 // China
                ["81"]  = new PhoneRule(new[] { 10 }, new[] { "7", "8", "9" }),       // Japan
                ["82"]  = new PhoneRule(new[] { 10 }, new[] { "1" }),                 // South Korea
                ["886"] = new PhoneRule(new[] { 9 }, new[] { "9" }),                  // Taiwan
                // === Southeast Asia ===
                ["65"]  = new PhoneRule(new[] { 8 }, new[] { "8", "9" }),             // Singapore
                ["60"]  = new PhoneRule(new[] { 9, 10 }, new[] { "1" }),              // Malaysia
                ["62"]  = new PhoneRule(new[] { 9, 10, 11, 12 }, new[] { "8" }),      // Indonesia
                ["63"]  = new PhoneRule(new[] { 10 }, new[] { "9" }),                 // Philippines
                ["66"]  = new PhoneRule(new[] { 9 }, new[] { "6", "8", "9" }),        // Thailand
                ["84"]  = new PhoneRule(new[] { 9 }, new[] { "3", "5", "7", "8", "9" }), // Vietnam
                ["95"]  = new PhoneRule(new[] { 9 }, new[] { "9" }),                  // Myanmar
                ["855"] = new PhoneRule(new[] { 8, 9 }, new[] { "1", "6", "7", "8", "9" }), // Cambodia
                ["976"] = new PhoneRule(new[] { 8 }, new[] { "6", "8", "9" }),        // Mongolia
                // === Europe ===
                ["44"]  = new PhoneRule(new[] { 10 }, new[] { "7" }),                 // UK
                ["33"]  = new PhoneRule(new[] { 9 }, new[] { "6", "7" }),             // France
                ["49"]  = new PhoneRule(new[] { 10, 11 }, new[] { "1" }),             // Germany
                ["39"]  = new PhoneRule(new[] { 10 }, new[] { "3" }),                 // Italy
                ["34"]  = new PhoneRule(new[] { 9 }, new[] { "6", "7" }),             // Spain
                ["31"]  = new PhoneRule(new[] { 9 }, new[] { "6" }),                  // Netherlands
                ["32"]  = new PhoneRule(new[] { 9 }),                                  // Belgium
                ["41"]  = new PhoneRule(new[] { 9 }, new[] { "7" }),                  // Switzerland
                ["43"]  = new PhoneRule(new[] { 10 }, new[] { "6" }),                 // Austria
                ["47"]  = new PhoneRule(new[] { 8 }, new[] { "4", "9" }),             // Norway
                ["48"]  = new PhoneRule(new[] { 9 }),                                  // Poland
                ["30"]  = new PhoneRule(new[] { 10 }, new[] { "6" }),                 // Greece
                ["420"] = new PhoneRule(new[] { 9 }, new[] { "6", "7" }),             // Czech Republic
                ["46"]  = new PhoneRule(new[] { 9 }, new[] { "7" }),                  // Sweden
                ["45"]  = new PhoneRule(new[] { 8 }),                                  // Denmark
                ["40"]  = new PhoneRule(new[] { 9 }, new[] { "7" }),                  // Romania
                ["36"]  = new PhoneRule(new[] { 9 }),                                  // Hungary
                ["380"] = new PhoneRule(new[] { 9 }),                                  // Ukraine
                // === Americas ===
                ["1"]   = new PhoneRule(new[] { 10 }),                                 // USA/Canada
                ["52"]  = new PhoneRule(new[] { 10 }),                                 // Mexico
                ["55"]  = new PhoneRule(new[] { 11 }),                                 // Brazil
                ["57"]  = new PhoneRule(new[] { 10 }, new[] { "3" }),                 // Colombia
                ["54"]  = new PhoneRule(new[] { 10 }, new[] { "9" }),                 // Argentina
                ["56"]  = new PhoneRule(new[] { 9 }, new[] { "9" }),                  // Chile
                ["58"]  = new PhoneRule(new[] { 10 }, new[] { "4" }),                 // Venezuela
                ["51"]  = new PhoneRule(new[] { 9 }, new[] { "9" }),                  // Peru
                ["593"] = new PhoneRule(new[] { 9 }, new[] { "9" }),                  // Ecuador
                ["53"]  = new PhoneRule(new[] { 8 }, new[] { "5", "6" }),             // Cuba
                // === Africa ===
                ["27"]  = new PhoneRule(new[] { 9 }, new[] { "6", "7", "8" }),        // South Africa
                ["234"] = new PhoneRule(new[] { 10 }, new[] { "7", "8", "9" }),       // Nigeria
                ["254"] = new PhoneRule(new[] { 9 }, new[] { "1", "7" }),             // Kenya
                ["233"] = new PhoneRule(new[] { 9 }, new[] { "2", "5" }),             // Ghana
                ["251"] = new PhoneRule(new[] { 9 }, new[] { "7", "9" }),             // Ethiopia
                ["255"] = new PhoneRule(new[] { 9 }, new[] { "6", "7" }),             // Tanzania
                ["256"] = new PhoneRule(new[] { 9 }, new[] { "7" }),                  // Uganda
                ["237"] = new PhoneRule(new[] { 9 }, new[] { "6" }),                  // Cameroon
                ["225"] = new PhoneRule(new[] { 10 }),                                 // Ivory Coast
                ["221"] = new PhoneRule(new[] { 9 }, new[] { "7" }),                  // Senegal
                ["252"] = new PhoneRule(new[] { 9 }, new[] { "6", "7" }),             // Somalia
                ["250"] = new PhoneRule(new[] { 9 }, new[] { "7" }),                  // Rwanda
                // === Oceania ===
                ["61"]  = new PhoneRule(new[] { 9 }, new[] { "4" }),                  // Australia
                ["64"]  = new PhoneRule(new[] { 8, 9, 10 }, new[] { "2" }),           // New Zealand
            };

        /// <summary>
        /// Human-readable country names keyed by calling code.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> CountryNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["965"] = "Kuwait",       ["966"] = "Saudi Arabia", ["971"] = "UAE",
                ["973"] = "Bahrain",      ["974"] = "Qatar",        ["968"] = "Oman",
                ["962"] = "Jordan",       ["961"] = "Lebanon",      ["970"] = "Palestine",
                ["964"] = "Iraq",         ["963"] = "Syria",        ["967"] = "Yemen",
                ["98"]  = "Iran",         ["90"]  = "Turkey",       ["972"] = "Israel",
                ["20"]  = "Egypt",        ["218"] = "Libya",        ["216"] = "Tunisia",
                ["212"] = "Morocco",      ["213"] = "Algeria",      ["249"] = "Sudan",
                ["91"]  = "India",        ["92"]  = "Pakistan",     ["880"] = "Bangladesh",
                ["94"]  = "Sri Lanka",    ["960"] = "Maldives",
                ["86"]  = "China",        ["81"]  = "Japan",        ["82"]  = "South Korea",
                ["886"] = "Taiwan",       ["65"]  = "Singapore",    ["60"]  = "Malaysia",
                ["62"]  = "Indonesia",    ["63"]  = "Philippines",  ["66"]  = "Thailand",
                ["84"]  = "Vietnam",      ["95"]  = "Myanmar",      ["855"] = "Cambodia",
                ["976"] = "Mongolia",
                ["44"]  = "UK",           ["33"]  = "France",       ["49"]  = "Germany",
                ["39"]  = "Italy",        ["34"]  = "Spain",        ["31"]  = "Netherlands",
                ["32"]  = "Belgium",      ["41"]  = "Switzerland",  ["43"]  = "Austria",
                ["47"]  = "Norway",       ["48"]  = "Poland",       ["30"]  = "Greece",
                ["420"] = "Czech Republic", ["46"] = "Sweden",      ["45"]  = "Denmark",
                ["40"]  = "Romania",      ["36"]  = "Hungary",      ["380"] = "Ukraine",
                ["1"]   = "USA/Canada",   ["52"]  = "Mexico",       ["55"]  = "Brazil",
                ["57"]  = "Colombia",     ["54"]  = "Argentina",    ["56"]  = "Chile",
                ["58"]  = "Venezuela",    ["51"]  = "Peru",         ["593"] = "Ecuador",
                ["53"]  = "Cuba",
                ["27"]  = "South Africa", ["234"] = "Nigeria",      ["254"] = "Kenya",
                ["233"] = "Ghana",        ["251"] = "Ethiopia",     ["255"] = "Tanzania",
                ["256"] = "Uganda",       ["237"] = "Cameroon",     ["225"] = "Ivory Coast",
                ["221"] = "Senegal",      ["252"] = "Somalia",      ["250"] = "Rwanda",
                ["61"]  = "Australia",    ["64"]  = "New Zealand",
            };

        /// <summary>
        /// Find the country calling code prefix in a normalized phone number.
        /// Tries 3-digit codes first, then 2-digit, then 1-digit (longest match wins).
        /// Returns null if no match is found in PhoneRules.
        /// </summary>
        public static string? FindCountryCode(string normalized)
        {
            if (normalized.Length >= 3 && PhoneRules.ContainsKey(normalized.Substring(0, 3)))
                return normalized.Substring(0, 3);
            if (normalized.Length >= 2 && PhoneRules.ContainsKey(normalized.Substring(0, 2)))
                return normalized.Substring(0, 2);
            if (normalized.Length >= 1 && PhoneRules.ContainsKey(normalized.Substring(0, 1)))
                return normalized.Substring(0, 1);
            return null;
        }

        /// <summary>
        /// Validate a normalized phone number against country-specific format rules.
        /// Checks local number length and mobile starting digits.
        /// Numbers with no matching country rule pass with generic E.164 only.
        /// </summary>
        /// <returns>(Valid: bool, Error: string?) tuple.</returns>
        public static (bool Valid, string? Error) ValidatePhoneFormat(string normalized)
        {
            var cc = FindCountryCode(normalized);
            if (cc == null) return (true, null);

            var rule = PhoneRules[cc];
            var local = normalized.Substring(cc.Length);
            CountryNames.TryGetValue(cc, out var country);
            country = country ?? $"+{cc}";

            // Check local number length
            if (!Array.Exists(rule.LocalLengths, l => l == local.Length))
            {
                var expected = string.Join(" or ", rule.LocalLengths);
                return (false, $"Invalid {country} number: expected {expected} digits after +{cc}, got {local.Length}");
            }

            // Check mobile starting digits
            if (rule.MobileStartDigits != null && rule.MobileStartDigits.Length > 0 && local.Length > 0)
            {
                var firstChar = local[0].ToString();
                var valid = Array.Exists(rule.MobileStartDigits, d => d == firstChar);
                if (!valid)
                {
                    var prefixes = string.Join(", ", rule.MobileStartDigits);
                    return (false, $"Invalid {country} mobile number: after +{cc} must start with {prefixes}");
                }
            }

            return (true, null);
        }

        // ── Input validation ──────────────────────────────────────────────────────

        /// <summary>
        /// Validate a raw phone number input before sending to the API.
        /// Normalizes, checks E.164 length bounds, then validates against
        /// country-specific format rules (length and mobile prefix).
        /// Never throws exceptions.
        /// </summary>
        public static PhoneValidationResult ValidatePhoneInput(object? phone)
        {
            var input = phone?.ToString() ?? "";
            input = input.Trim();

            if (string.IsNullOrWhiteSpace(input))
                return new PhoneValidationResult { IsValid = false, Error = "Phone number is required", Normalized = "" };

            if (input.Contains("@"))
                return new PhoneValidationResult { IsValid = false, Error = $"'{input}' is an email address, not a phone number", Normalized = "" };

            var normalized = NormalizePhone(input);

            if (string.IsNullOrEmpty(normalized))
                return new PhoneValidationResult { IsValid = false, Error = $"'{input}' is not a valid phone number, no digits found", Normalized = "" };

            if (normalized.Length < 7)
                return new PhoneValidationResult { IsValid = false, Error = $"'{input}' is too short ({normalized.Length} digits, minimum is 7)", Normalized = normalized };

            if (normalized.Length > 15)
                return new PhoneValidationResult { IsValid = false, Error = $"'{input}' is too long ({normalized.Length} digits, maximum is 15)", Normalized = normalized };

            // Country-specific format validation
            var (valid, error) = ValidatePhoneFormat(normalized);
            if (!valid)
                return new PhoneValidationResult { IsValid = false, Error = error, Normalized = normalized };

            return new PhoneValidationResult { IsValid = true, Error = null, Normalized = normalized };
        }
    }

    /// <summary>
    /// Per-country phone validation rule: expected local digit count(s) and mobile prefix(es).
    /// </summary>
    public sealed class PhoneRule
    {
        /// <summary>Valid digit counts for the local part (after country code).</summary>
        public int[] LocalLengths { get; }

        /// <summary>Valid first character(s) of the local number. Null means any prefix accepted.</summary>
        public string[]? MobileStartDigits { get; }

        public PhoneRule(int[] localLengths, string[]? mobileStartDigits = null)
        {
            LocalLengths = localLengths;
            MobileStartDigits = mobileStartDigits;
        }
    }
}
