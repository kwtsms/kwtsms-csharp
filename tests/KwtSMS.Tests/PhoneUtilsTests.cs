using System;
using Xunit;
using KwtSMS;

namespace KwtSMS.Tests
{
    public class PhoneUtilsTests
    {
        // ── NormalizePhone ──

        // 2026-11-05 08:23 +03: Kuwait number tests read KWTSMS_TEST_NUMBER through TestNumber
        // (skip when unset); variants are built at runtime and passed by kind so test names
        // and failure messages never show a number. Replaces the hardcoded sample, int-testnumber.
        [SkippableTheory]
        [InlineData("plain")]
        [InlineData("plus")]
        [InlineData("00")]
        [InlineData("000")]
        [InlineData("spaces")]
        [InlineData("dashes")]
        [InlineData("dots")]
        [InlineData("parens")]
        [InlineData("mixed")]
        [InlineData("arabic")]
        [InlineData("arabic-plus")]
        [InlineData("arabic-00")]
        [InlineData("arabic-spaces")]
        [InlineData("arabic-dashes")]
        [InlineData("arabic-cc-latin")]
        [InlineData("persian")]
        [InlineData("persian-plus")]
        [InlineData("persian-spaces")]
        public void NormalizePhone_KuwaitVariants(string kind)
        {
            var input = TestNumber.Variant(kind);
            TestNumber.AssertSame(TestNumber.Get(), PhoneUtils.NormalizePhone(input));
        }

        [Fact]
        public void NormalizePhone_EmptyString()
        {
            Assert.Equal("", PhoneUtils.NormalizePhone(""));
        }

        [Fact]
        public void NormalizePhone_Null()
        {
            Assert.Equal("", PhoneUtils.NormalizePhone(null));
        }

        [Fact]
        public void NormalizePhone_NoDigits()
        {
            Assert.Equal("", PhoneUtils.NormalizePhone("abc"));
        }

        [Fact]
        public void NormalizePhone_ArabicIndicSaudiNumber()
        {
            // ٩٦٦٥٥١٢٣٤٥٦٧
            Assert.Equal("966551234567", PhoneUtils.NormalizePhone("\u0669\u0666\u0666\u0665\u0665\u0661\u0662\u0663\u0664\u0665\u0666\u0667"));
        }

        [Fact]
        public void NormalizePhone_ArabicIndicUAENumber()
        {
            // ٩٧١٥٠٤٤٩٦٦٧٧
            Assert.Equal("971504496677", PhoneUtils.NormalizePhone("\u0669\u0667\u0661\u0665\u0660\u0664\u0664\u0669\u0666\u0666\u0667\u0667"));
        }

        // ── ValidatePhoneInput ──

        // 2026-11-05 08:23 +03: valid Kuwait inputs built from KWTSMS_TEST_NUMBER, int-testnumber.
        [SkippableTheory]
        [InlineData("plain")]
        [InlineData("plus")]
        [InlineData("padded")]
        [InlineData("arabic")]
        [InlineData("arabic-plus")]
        [InlineData("arabic-dash-space")]
        [InlineData("arabic-cc-latin")]
        [InlineData("persian")]
        public void Validate_KuwaitVariants(string kind)
        {
            var result = PhoneUtils.ValidatePhoneInput(TestNumber.Variant(kind));
            Assert.True(result.IsValid, "expected valid (value hidden)");
            Assert.True(result.Error == null, "expected no error (text hidden, it may hold the number)");
            TestNumber.AssertSame(TestNumber.Get(), result.Normalized);
        }

        [SkippableFact]
        public void Validate_NonStringInput()
        {
            var n = TestNumber.Get();
            var result = PhoneUtils.ValidatePhoneInput(long.Parse(n));
            Assert.True(result.IsValid, "expected valid (value hidden)");
            TestNumber.AssertSame(n, result.Normalized);
        }

        [Fact]
        public void Validate_Empty()
        {
            var result = PhoneUtils.ValidatePhoneInput("");
            Assert.False(result.IsValid);
            Assert.Equal("Phone number is required", result.Error);
        }

        [Fact]
        public void Validate_Null()
        {
            var result = PhoneUtils.ValidatePhoneInput(null);
            Assert.False(result.IsValid);
            Assert.Equal("Phone number is required", result.Error);
        }

        [Fact]
        public void Validate_Blank()
        {
            var result = PhoneUtils.ValidatePhoneInput("   ");
            Assert.False(result.IsValid);
            Assert.Equal("Phone number is required", result.Error);
        }

        [Fact]
        public void Validate_EmailAddress()
        {
            var result = PhoneUtils.ValidatePhoneInput("user@gmail.com");
            Assert.False(result.IsValid);
            Assert.Contains("email address", result.Error);
        }

        [Fact]
        public void Validate_NoDigits()
        {
            var result = PhoneUtils.ValidatePhoneInput("abc");
            Assert.False(result.IsValid);
            Assert.Contains("no digits found", result.Error);
        }

        [Fact]
        public void Validate_TooShort()
        {
            var result = PhoneUtils.ValidatePhoneInput("123");
            Assert.False(result.IsValid);
            Assert.Contains("too short", result.Error);
            Assert.Contains("3 digits", result.Error);
        }

        [Fact]
        public void Validate_MinimumValid_7Digits()
        {
            // Use a number with no country code match to test generic E.164 minimum only
            var result = PhoneUtils.ValidatePhoneInput("2234567");
            Assert.True(result.IsValid);
            Assert.Equal("2234567", result.Normalized);
        }

        [Fact]
        public void Validate_MaximumValid_15Digits()
        {
            // Use a number with no country code match (999 prefix) to test generic E.164 maximum only
            var result = PhoneUtils.ValidatePhoneInput("999123456789012");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_TooLong()
        {
            var result = PhoneUtils.ValidatePhoneInput("1234567890123456");
            Assert.False(result.IsValid);
            Assert.Contains("too long", result.Error);
        }

        [Fact]
        public void Validate_ArabicIndicTooShort()
        {
            // ١٢٣ (too short after normalization)
            var result = PhoneUtils.ValidatePhoneInput("\u0661\u0662\u0663");
            Assert.False(result.IsValid);
            Assert.Contains("too short", result.Error);
        }

        [Fact]
        public void Validate_ArabicIndicSaudiNumber()
        {
            // ٩٦٦٥٥١٢٣٤٥٦٧
            var result = PhoneUtils.ValidatePhoneInput("\u0669\u0666\u0666\u0665\u0665\u0661\u0662\u0663\u0664\u0665\u0666\u0667");
            Assert.True(result.IsValid);
            Assert.Equal("966551234567", result.Normalized);
        }

        // ── Trunk prefix stripping ──

        [Fact]
        public void NormalizePhone_SaudiTrunkPrefix()
        {
            // 966 + 0551234567 → 966551234567
            Assert.Equal("966551234567", PhoneUtils.NormalizePhone("9660551234567"));
        }

        [Fact]
        public void NormalizePhone_UAETrunkPrefix()
        {
            // 971 + 0501234567 → 971501234567
            Assert.Equal("971501234567", PhoneUtils.NormalizePhone("9710501234567"));
        }

        [Fact]
        public void NormalizePhone_EgyptTrunkPrefix()
        {
            // 20 + 01012345678 → 201012345678
            Assert.Equal("201012345678", PhoneUtils.NormalizePhone("20001012345678"));
        }

        [Fact]
        public void Validate_SaudiTrunkPrefixNormalized()
        {
            // Input with domestic trunk 0 - should normalize and validate as valid Saudi
            var result = PhoneUtils.ValidatePhoneInput("9660551234567");
            Assert.True(result.IsValid);
            Assert.Equal("966551234567", result.Normalized);
        }

        [Fact]
        public void Validate_UAETrunkPrefixNormalized()
        {
            var result = PhoneUtils.ValidatePhoneInput("9710501234567");
            Assert.True(result.IsValid);
            Assert.Equal("971501234567", result.Normalized);
        }

        // ── Country-format validation ──

        [Fact]
        public void Validate_KuwaitInvalidMobilePrefix()
        {
            // 965 + 31234567: starts with 3, not a valid Kuwait mobile prefix
            var result = PhoneUtils.ValidatePhoneInput("96531234567");
            Assert.False(result.IsValid);
            Assert.Contains("Kuwait", result.Error);
            Assert.Contains("must start with", result.Error);
        }

        [Fact]
        public void Validate_SaudiInvalidLength()
        {
            // 966 + 51234567 = 8 local digits, Saudi expects 9
            var result = PhoneUtils.ValidatePhoneInput("96651234567");
            Assert.False(result.IsValid);
            Assert.Contains("Saudi Arabia", result.Error);
            Assert.Contains("expected 9", result.Error);
        }

        [Fact]
        public void Validate_UAEValidNumber()
        {
            // 971 + 501234567 = 9 local digits starting with 5
            var result = PhoneUtils.ValidatePhoneInput("971501234567");
            Assert.True(result.IsValid);
            Assert.Equal("971501234567", result.Normalized);
        }

        [SkippableFact]
        public void FindCountryCode_Kuwait()
        {
            Assert.Equal("965", PhoneUtils.FindCountryCode(TestNumber.Get()));
        }

        [Fact]
        public void FindCountryCode_Saudi()
        {
            Assert.Equal("966", PhoneUtils.FindCountryCode("966551234567"));
        }

        [Fact]
        public void FindCountryCode_USA()
        {
            Assert.Equal("1", PhoneUtils.FindCountryCode("12025551234"));
        }

        [Fact]
        public void FindCountryCode_Unknown()
        {
            Assert.Null(PhoneUtils.FindCountryCode("9991234567"));
        }

        [SkippableFact]
        public void ValidatePhoneFormat_ValidKuwait()
        {
            var (valid, error) = PhoneUtils.ValidatePhoneFormat(TestNumber.Get());
            Assert.True(valid, "expected valid (value hidden)");
            Assert.True(error == null, "expected no error (text hidden, it may hold the number)");
        }

        [Fact]
        public void ValidatePhoneFormat_InvalidKuwaitPrefix()
        {
            var (valid, error) = PhoneUtils.ValidatePhoneFormat("96531234567");
            Assert.False(valid);
            Assert.Contains("Kuwait", error);
        }

        [Fact]
        public void ValidatePhoneFormat_UnknownCountry()
        {
            // No rule for 999 prefix - passes through
            var (valid, error) = PhoneUtils.ValidatePhoneFormat("9991234567");
            Assert.True(valid);
            Assert.Null(error);
        }
    }
}
