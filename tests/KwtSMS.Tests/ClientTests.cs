using System;
using System.Collections.Generic;
using System.Linq; // 2026-11-05 08:23 +03: SequenceEqual in the mocked bulk test, int-testnumber.
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using KwtSMS;

namespace KwtSMS.Tests
{
    /// <summary>
    /// Serializes all test classes that share the static HttpClient.
    /// Prevents races between tests that swap HttpRequest.Client.
    /// </summary>
    [CollectionDefinition("HttpClient", DisableParallelization = true)]
    public class HttpClientCollection { }

    /// <summary>
    /// Tests for KwtSmsClient using mocked HTTP responses.
    /// No network calls are made.
    /// </summary>
    [Collection("HttpClient")]
    public class ClientTests : IDisposable
    {
        private readonly HttpClient _originalClient;

        public ClientTests()
        {
            // Save original client to restore later
            _originalClient = GetInternalClient();
        }

        public void Dispose()
        {
            // Restore original client
            SetInternalClient(_originalClient);
        }

        private KwtSmsClient CreateClientWithMock(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            var mockHandler = new MockHttpHandler(handler);
            var mockClient = new HttpClient(mockHandler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            SetInternalClient(mockClient);
            return new KwtSmsClient("testuser", "testpass", "TEST-SENDER", true, "");
        }

        private static HttpClient GetInternalClient()
        {
            return HttpRequest.Client;
        }

        private static void SetInternalClient(HttpClient client)
        {
            HttpRequest.Client = client;
        }

        // ── Verify tests ──

        [Fact]
        public void Verify_Success()
        {
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    result = "OK",
                    available = 150.0,
                    purchased = 1000.0
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var (ok, balance, error) = client.Verify();

            Assert.True(ok);
            Assert.Equal(150.0, balance);
            Assert.Null(error);
            Assert.Equal(150.0, client.CachedBalance);
            Assert.Equal(1000.0, client.CachedPurchased);
        }

        [Fact]
        public void Verify_WrongCredentials()
        {
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    result = "ERROR",
                    code = "ERR003",
                    description = "Authentication error, username or password are not correct."
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var (ok, balance, error) = client.Verify();

            Assert.False(ok);
            Assert.Null(balance);
            Assert.Contains("Authentication error", error);
        }

        // ── Send tests ──
        // 2026-11-05 08:23 +03: Send/Validate tests take the number from KWTSMS_TEST_NUMBER
        // (TestNumber, skip when unset); response echoes use TestNumber.Placeholder; the live
        // 250-number bulk test is replaced by Send_Bulk250_BatchesInto200And50 below, int-testnumber.

        [SkippableFact]
        public void Send_Success()
        {
            var n = TestNumber.Get();
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["result"] = "OK",
                    ["msg-id"] = "abc123",
                    ["numbers"] = 1,
                    ["points-charged"] = 1,
                    ["balance-after"] = 149.0,
                    ["unix-timestamp"] = 1684763355
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Send(n,"Test message");

            Assert.Equal("OK", result.Result);
            Assert.Equal("abc123", result.MsgId);
            Assert.Equal(1, result.Numbers);
            Assert.Equal(1, result.PointsCharged);
            Assert.Equal(149.0, result.BalanceAfter);
            Assert.Equal(149.0, client.CachedBalance);
        }

        [Fact]
        public void Send_InvalidNumber_LocalRejection()
        {
            var client = CreateClientWithMock(req =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Send("abc", "Test message");

            Assert.Equal("ERROR", result.Result);
            Assert.Equal("ERR_INVALID_INPUT", result.Code);
        }

        [SkippableFact]
        public void Send_EmptyMessage_AfterCleaning()
        {
            var n = TestNumber.Get();
            var client = CreateClientWithMock(req =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                };
            });

            // Only emojis, empty after cleaning
            var result = client.Send(n,"\U0001F600\U0001F601\U0001F602");

            Assert.Equal("ERROR", result.Result);
            Assert.Equal("ERR009", result.Code);
        }

        [SkippableFact]
        public void Send_MixedValidInvalid()
        {
            var n = TestNumber.Get();
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["result"] = "OK",
                    ["msg-id"] = "mixed123",
                    ["numbers"] = 1,
                    ["points-charged"] = 1,
                    ["balance-after"] = 148.0,
                    ["unix-timestamp"] = 1684763355
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Send(n + ",abc,user@email.com", "Test");

            Assert.Equal("OK", result.Result);
            Assert.NotNull(result.Invalid);
            Assert.Equal(2, result.Invalid!.Count);
        }

        [SkippableFact]
        public void Send_DeduplicatesNumbers()
        {
            var n = TestNumber.Get();
            string? capturedBody = null;
            var client = CreateClientWithMock(req =>
            {
                capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["result"] = "OK",
                    ["msg-id"] = "dedup123",
                    ["numbers"] = 1,
                    ["points-charged"] = 1,
                    ["balance-after"] = 147.0,
                    ["unix-timestamp"] = 1684763355
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            // Same number in different formats
            var result = client.Send("+" + n + ",00" + n + "," + n, "Test");

            Assert.Equal("OK", result.Result);
            // The body should contain the number only once
            Assert.NotNull(capturedBody);
            Assert.DoesNotContain(",", GetMobileFromBody(capturedBody!));
        }

        [SkippableFact]
        public void Send_ERR028_RateLimit()
        {
            var n = TestNumber.Get();
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    result = "ERROR",
                    code = "ERR028",
                    description = "Sent to the same number too soon"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Send(n,"Test");

            Assert.Equal("ERROR", result.Result);
            Assert.Equal("ERR028", result.Code);
            Assert.NotNull(result.Action);
            // 2026-10-22 15:36 +03: ERR028 action no longer states a fixed 15 s window.
            Assert.Contains("too soon", result.Action);
        }

        [SkippableFact]
        public void Send_ERR010_ZeroBalance()
        {
            var n = TestNumber.Get();
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    result = "ERROR",
                    code = "ERR010",
                    description = "Zero balance"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Send(n,"Test");

            Assert.Equal("ERROR", result.Result);
            Assert.Contains("kwtsms.com", result.Action);
        }

        [SkippableFact]
        public void Send_NetworkError()
        {
            var n = TestNumber.Get();
            var client = CreateClientWithMock(req =>
            {
                throw new HttpRequestException("Connection refused");
            });

            var result = client.Send(n,"Test");

            Assert.Equal("ERROR", result.Result);
        }

        // ── Validate tests ──

        [SkippableFact]
        public void Validate_Success()
        {
            var n = TestNumber.Get();
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["result"] = "OK",
                    ["mobile"] = new Dictionary<string, object>
                    {
                        ["OK"] = new[] { n },
                        ["ER"] = new[] { "123" },
                        ["NR"] = new[] { "966558724477" }
                    }
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Validate(n, "123", "966558724477");

            TestNumber.AssertContains(n, result.Ok);
            Assert.Null(result.Error);
        }

        [Fact]
        public void Validate_AllInvalid()
        {
            var client = CreateClientWithMock(req =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Validate("abc", "user@email.com");

            Assert.Equal("No valid phone numbers to validate.", result.Error);
            Assert.Equal(2, result.Rejected.Count);
        }

        // ── SenderIds tests ──

        [Fact]
        public void SenderIds_Success()
        {
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["result"] = "OK",
                    ["senderid"] = new[] { "KWT-SMS", "MYAPP" }
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.SenderIds();

            Assert.Equal("OK", result.Result);
            Assert.Contains("KWT-SMS", result.SenderIds);
            Assert.Contains("MYAPP", result.SenderIds);
        }

        // ── Coverage tests ──

        [Fact]
        public void Coverage_Success()
        {
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["result"] = "OK",
                    ["prefixes"] = new[] { "965", "966", "971" }
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Coverage();

            Assert.Equal("OK", result.Result);
            Assert.Contains("965", result.Prefixes);
        }

        // ── Status tests ──

        [Fact]
        public void Status_Success()
        {
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    result = "OK",
                    status = "sent",
                    description = "Message successfully sent"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Status("abc123");

            Assert.Equal("OK", result.Result);
            Assert.Equal("sent", result.Status);
        }

        [Fact]
        public void Status_NotFound()
        {
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    result = "ERROR",
                    code = "ERR029",
                    description = "Message does not exist"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Status("nonexistent");

            Assert.Equal("ERROR", result.Result);
            Assert.Equal("ERR029", result.Code);
            Assert.NotNull(result.Action);
        }

        // ── DLR tests ──

        [Fact]
        public void Dlr_Success()
        {
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["result"] = "OK",
                    ["report"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["Number"] = TestNumber.Placeholder,
                            ["Status"] = "Received by recipient"
                        }
                    }
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Dlr("abc123");

            Assert.Equal("OK", result.Result);
            Assert.Single(result.Report);
            Assert.Equal(TestNumber.Placeholder, result.Report[0].Number);
        }

        [Fact]
        public void Dlr_NoReports()
        {
            var client = CreateClientWithMock(req =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    result = "ERROR",
                    code = "ERR019",
                    description = "No reports found"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var result = client.Dlr("abc123");

            Assert.Equal("ERROR", result.Result);
            Assert.Equal("ERR019", result.Code);
        }

        // ── Bulk send (mocked) ──

        // 2026-11-05 08:23 +03: offline replacement for the removed live 250-number bulk test.
        // Numbers are generated here and only ever reach the mock handler, int-testnumber.
        [Fact]
        public void Send_Bulk250_BatchesInto200And50()
        {
            var batchSizes = new List<int>();
            var sentNumbers = new List<string>();
            double balance = 1000.0;
            var client = CreateClientWithMock(req =>
            {
                var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                var batch = GetMobileFromBody(body).Split(',');
                batchSizes.Add(batch.Length);
                sentNumbers.AddRange(batch);
                balance -= batch.Length;
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["result"] = "OK",
                    ["msg-id"] = "bulk" + batchSizes.Count,
                    ["numbers"] = batch.Length,
                    ["points-charged"] = batch.Length,
                    ["balance-after"] = balance,
                    ["unix-timestamp"] = 1684763355
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var numbers = new string[250];
            for (int i = 0; i < numbers.Length; i++)
                numbers[i] = "9655" + i.ToString("D7");

            var result = client.Send(string.Join(",", numbers), "C# client bulk test (250 numbers, mocked)");

            Assert.Equal(new[] { 200, 50 }, batchSizes);
            Assert.True(numbers.SequenceEqual(sentNumbers), "every number sent once, in order (values hidden)");
            Assert.Equal("OK", result.Result);
            Assert.Equal("bulk1,bulk2", result.MsgId);
            Assert.Equal(250, result.Numbers);
            Assert.Equal(250, result.PointsCharged);
            Assert.Equal(750.0, result.BalanceAfter);
            Assert.Equal(750.0, client.CachedBalance);
        }

        // ── Helper ──

        private static string GetMobileFromBody(string body)
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("mobile").GetString() ?? "";
        }
    }
}
