using System;
using System.IO;
using System.Linq;
using Xunit;
using KwtSMS;

namespace KwtSMS.Tests
{
    // 2026-10-22 15:36 +03: summary corrected, test sends hold credits until deleted from the queue.
    /// <summary>
    /// Real API integration tests. Skipped if CSHARP_USERNAME / CSHARP_PASSWORD are not set.
    /// Always uses test_mode=true (not delivered; delete the queued test messages from the Sending Queue to release their held credits).
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection("HttpClient")]
    public class IntegrationTests
    {
        private readonly string? _username;
        private readonly string? _password;

        public IntegrationTests()
        {
            _username = Environment.GetEnvironmentVariable("CSHARP_USERNAME");
            _password = Environment.GetEnvironmentVariable("CSHARP_PASSWORD");
        }

        private KwtSmsClient CreateClient()
        {
            return new KwtSmsClient(_username!, _password!, "KWT-SMS", testMode: true, logFile: "");
        }

        private void SkipIfNoCredentials()
        {
            Skip.If(string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password),
                "CSHARP_USERNAME / CSHARP_PASSWORD not set");
        }

        // 2026-11-05 08:23 +03: live sends go only to KWTSMS_TEST_NUMBER (skip when unset), int-testnumber.
        private static string Number() => TestNumber.Get();

        // ── Verify ──

        [SkippableFact]
        public void Integration_Verify_ValidCredentials()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var (ok, balance, error) = client.Verify();

            Assert.True(ok, $"Verify failed: {error}");
            Assert.NotNull(balance);
            Assert.True(balance >= 0, $"Balance should be non-negative, got {balance}");
        }

        // 2026-11-07 00:03 +03: wrong-credential test mocked; live failed logins trip the security log (task neg-auth-tests).
        // Integration_Verify_WrongCredentials removed: covered offline by ClientTests.Verify_WrongCredentials (mocked ERR003).

        // ── Balance ──

        [SkippableFact]
        public void Integration_Balance()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var balance = client.Balance();

            Assert.NotNull(balance);
            Assert.True(balance >= 0);
        }

        // ── Send ──

        [SkippableFact]
        public void Integration_Send_ValidKuwaitNumber()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send(Number(), "Integration test from C# client");

            // In test mode, should be OK or a known error
            Assert.NotNull(result.Result);
        }

        [SkippableFact]
        public void Integration_Send_InvalidInput_Email()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send("user@email.com", "Test");

            Assert.Equal("ERROR", result.Result);
            Assert.NotNull(result.Invalid);
        }

        [SkippableFact]
        public void Integration_Send_InvalidInput_TooShort()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send("123", "Test");

            Assert.Equal("ERROR", result.Result);
        }

        [SkippableFact]
        public void Integration_Send_MixedValidInvalid()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send(Number() + ",abc,user@email.com", "Test mixed");

            // Valid number should be sent, invalid ones reported
            Assert.NotNull(result.Invalid);
            Assert.Equal(2, result.Invalid!.Count);
        }

        [SkippableFact]
        public void Integration_Send_PlusPrefixNormalization()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send("+" + Number(),"Test with + prefix");

            Assert.NotNull(result.Result);
        }

        [SkippableFact]
        public void Integration_Send_DoubleZeroNormalization()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send("00" + Number(),"Test with 00 prefix");

            Assert.NotNull(result.Result);
        }

        [SkippableFact]
        public void Integration_Send_ArabicDigits()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send(TestNumber.ToArabicIndic(Number()), "Test Arabic digits");

            Assert.NotNull(result.Result);
        }

        [SkippableFact]
        public void Integration_Send_DuplicateNumbers()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send("+" + Number() + ",00" + Number() + "," + Number(), "Test dedup");

            // Should deduplicate to single number
            Assert.NotNull(result.Result);
        }

        [SkippableFact]
        public void Integration_Send_EmptySender()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send(Number(), "Test empty sender", sender: "");

            // API should reject empty sender or use default
            Assert.NotNull(result.Result);
        }

        [SkippableFact]
        public void Integration_Send_WrongSender()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Send(Number(), "Test wrong sender", sender: "NONEXISTENT-SENDER");

            Assert.NotNull(result.Result);
        }

        // ── SenderIds ──

        [SkippableFact]
        public void Integration_SenderIds()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.SenderIds();

            Assert.Equal("OK", result.Result);
            Assert.NotEmpty(result.SenderIds);
        }

        // ── Coverage ──

        [SkippableFact]
        public void Integration_Coverage()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Coverage();

            Assert.NotNull(result.Result);
        }

        // ── Validate ──

        [SkippableFact]
        public void Integration_Validate()
        {
            SkipIfNoCredentials();
            var client = CreateClient();

            var result = client.Validate(Number(), "+96512345678");

            Assert.Null(result.Error);
        }

        // 2026-11-05 08:23 +03: the live 250-number bulk test was removed (it sent to a generated
        // number range); ClientTests.Send_Bulk250_BatchesInto200And50 covers the 200 + 50 batching
        // offline, int-testnumber.
    }
}
