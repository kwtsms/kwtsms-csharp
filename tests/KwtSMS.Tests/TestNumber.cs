// FILE:      tests/KwtSMS.Tests/TestNumber.cs
// CREATED:   2026-11-05 08:23 +03
// TASK:      int-testnumber (Boss 2026-11-05 00:33)
// AUTHOR:    M Alhassoun
// WHAT:      Shared test helper: the test phone number and its format variants.
// WHY:       Tests must not hardcode a phone number. The number comes from the
//            KWTSMS_TEST_NUMBER environment variable (an international Kuwait
//            mobile, digits only, 965XXXXXXXX) and every test that needs one
//            is skipped when it is unset or malformed.
// USAGE:     KWTSMS_TEST_NUMBER=965XXXXXXXX dotnet test --filter "Category!=Integration"
//            CI reads it from the repository secret of the same name.
//            Tests: [SkippableFact] / [SkippableTheory], then TestNumber.Get()
//            or TestNumber.Variant("kind"). Compare with TestNumber.AssertSame
//            so a failure never prints the number. Never log the value.
// DEPENDS:   xunit, Xunit.SkippableFact (Skip.If), already referenced.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace KwtSMS.Tests
{
    public static class TestNumber
    {
        /// <summary>For values only passed through unchanged (for example a mocked response echo).</summary>
        public const string Placeholder = "965XXXXXXXX";

        private const string SkipReason = "KWTSMS_TEST_NUMBER not set (digits only, 965 plus 8 digits)";

        // [0-9], not \d: .NET \d also matches Arabic-Indic digits.
        private static readonly string? Value = Read();

        private static string? Read()
        {
            var n = (Environment.GetEnvironmentVariable("KWTSMS_TEST_NUMBER") ?? "").Trim();
            return Regex.IsMatch(n, "^965[0-9]{8}$") ? n : null;
        }

        /// <summary>KWTSMS_TEST_NUMBER, or skips the running test when it is unset or malformed.
        /// Only call it from a [SkippableFact] or [SkippableTheory].</summary>
        public static string Get()
        {
            Skip.If(Value == null, SkipReason);
            return Value!;
        }

        /// <summary>Latin digits to Arabic-Indic (U+0660-0669).</summary>
        public static string ToArabicIndic(string s) => Shift(s, '٠');

        /// <summary>Latin digits to Extended Arabic-Indic / Persian (U+06F0-06F9).</summary>
        public static string ToPersian(string s) => Shift(s, '۰');

        private static string Shift(string s, char zero) =>
            new string(s.Select(c => c >= '0' && c <= '9' ? (char)(zero + (c - '0')) : c).ToArray());

        /// <summary>The test number written in the format named by kind. Skips when unset.</summary>
        public static string Variant(string kind)
        {
            var n = Get();
            var cc = n.Substring(0, 3);
            var a = n.Substring(3, 4);
            var b = n.Substring(7);
            switch (kind)
            {
                case "plain": return n;
                case "plus": return "+" + n;
                case "00": return "00" + n;
                case "000": return "000" + n;
                case "padded": return "  +" + n + "  ";
                case "spaces": return cc + " " + a + " " + b;
                case "dashes": return cc + "-" + a + "-" + b;
                case "dots": return cc + "." + a + "." + b;
                case "parens": return "(" + cc + ") " + a + b;
                case "mixed": return "+00 " + cc + "-" + a + " " + b;
                case "arabic": return ToArabicIndic(n);
                case "arabic-plus": return "+" + ToArabicIndic(n);
                case "arabic-00": return ToArabicIndic("00" + n);
                case "arabic-spaces": return ToArabicIndic(cc + " " + a + " " + b);
                case "arabic-dashes": return ToArabicIndic(cc + "-" + a + "-" + b);
                case "arabic-dash-space": return ToArabicIndic(cc + "-" + a + " " + b);
                case "arabic-cc-latin": return ToArabicIndic(cc) + a + b;
                case "persian": return ToPersian(n);
                case "persian-plus": return "+" + ToPersian(n);
                case "persian-spaces": return ToPersian(cc + " " + a + " " + b);
                default: throw new ArgumentException("unknown variant kind: " + kind);
            }
        }

        /// <summary>Assert equality without printing either value (they may hold the number).</summary>
        public static void AssertSame(string expected, string? actual)
        {
            Assert.True(expected == actual, "phone number does not match the expected value (values hidden)");
        }

        /// <summary>Assert the list holds the number without printing the list.</summary>
        public static void AssertContains(string expected, IEnumerable<string> actual)
        {
            Assert.True(actual.Contains(expected), "phone number not found in the list (values hidden)");
        }
    }
}
