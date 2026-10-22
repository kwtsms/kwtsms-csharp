using System;
using System.Collections.Generic;
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
    /// Mock HTTP handler for testing API responses without network calls.
    /// </summary>
    public class MockHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    public class ApiErrorsTests
    {
        [Fact]
        public void EnrichError_AddsAction_ERR003()
        {
            var data = new Dictionary<string, object?>
            {
                ["result"] = "ERROR",
                ["code"] = "ERR003",
                ["description"] = "Authentication error"
            };

            var enriched = ApiErrors.EnrichError(data);

            Assert.True(enriched.ContainsKey("action"));
            Assert.Contains("Wrong API username or password", enriched["action"]?.ToString());
        }

        [Fact]
        public void EnrichError_AddsAction_ERR026()
        {
            var data = new Dictionary<string, object?>
            {
                ["result"] = "ERROR",
                ["code"] = "ERR026",
                ["description"] = "Country not activated"
            };

            var enriched = ApiErrors.EnrichError(data);

            Assert.Contains("country", enriched["action"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EnrichError_AddsAction_ERR025()
        {
            var data = new Dictionary<string, object?>
            {
                ["result"] = "ERROR",
                ["code"] = "ERR025",
                ["description"] = "Invalid number"
            };

            var enriched = ApiErrors.EnrichError(data);

            Assert.Contains("country code", enriched["action"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EnrichError_AddsAction_ERR010()
        {
            var data = new Dictionary<string, object?>
            {
                ["result"] = "ERROR",
                ["code"] = "ERR010",
                ["description"] = "Zero balance"
            };

            var enriched = ApiErrors.EnrichError(data);

            Assert.Contains("kwtsms.com", enriched["action"]?.ToString());
        }

        [Fact]
        public void EnrichError_AddsAction_ERR024()
        {
            var data = new Dictionary<string, object?>
            {
                ["result"] = "ERROR",
                ["code"] = "ERR024",
                ["description"] = "IP not whitelisted"
            };

            var enriched = ApiErrors.EnrichError(data);

            Assert.Contains("IP", enriched["action"]?.ToString());
        }

        [Fact]
        public void EnrichError_AddsAction_ERR028()
        {
            var data = new Dictionary<string, object?>
            {
                ["result"] = "ERROR",
                ["code"] = "ERR028",
                ["description"] = "Rate limited"
            };

            var enriched = ApiErrors.EnrichError(data);

            // 2026-10-22 15:36 +03: ERR028 action no longer states a fixed 15 s window.
            Assert.Contains("too soon", enriched["action"]?.ToString());
        }

        [Fact]
        public void EnrichError_AddsAction_ERR008()
        {
            var data = new Dictionary<string, object?>
            {
                ["result"] = "ERROR",
                ["code"] = "ERR008",
                ["description"] = "Sender ID banned"
            };

            var enriched = ApiErrors.EnrichError(data);

            Assert.Contains("sender ID", enriched["action"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EnrichError_UnknownCode_NoAction()
        {
            var data = new Dictionary<string, object?>
            {
                ["result"] = "ERROR",
                ["code"] = "ERR999",
                ["description"] = "Something unknown"
            };

            var enriched = ApiErrors.EnrichError(data);

            Assert.False(enriched.ContainsKey("action"));
        }

        [Fact]
        public void EnrichError_OKResult_NoAction()
        {
            var data = new Dictionary<string, object?>
            {
                ["result"] = "OK"
            };

            var enriched = ApiErrors.EnrichError(data);

            Assert.False(enriched.ContainsKey("action"));
        }

        [Fact]
        public void EnrichError_NullInput_ReturnsEmptyDict()
        {
            var enriched = ApiErrors.EnrichError(null!);
            Assert.NotNull(enriched);
        }

        [Fact]
        public void AllErrorCodes_HaveActions()
        {
            var expectedCodes = new[]
            {
                "ERR001", "ERR002", "ERR003", "ERR004", "ERR005",
                "ERR006", "ERR007", "ERR008", "ERR009", "ERR010",
                "ERR011", "ERR012", "ERR013",
                "ERR019", "ERR020", "ERR021", "ERR022", "ERR023",
                "ERR024", "ERR025", "ERR026", "ERR027", "ERR028",
                "ERR029", "ERR030", "ERR031", "ERR032", "ERR033",
                "ERR_INVALID_INPUT"
            };

            foreach (var code in expectedCodes)
            {
                Assert.True(ApiErrors.Errors.ContainsKey(code), $"Missing error code: {code}");
                Assert.False(string.IsNullOrEmpty(ApiErrors.Errors[code]), $"Empty action for: {code}");
            }
        }

        [Fact]
        public void ErrorsDict_IsReadOnly()
        {
            // Verify the dictionary is IReadOnlyDictionary
            IReadOnlyDictionary<string, string> errors = ApiErrors.Errors;
            Assert.NotNull(errors);
            Assert.True(errors.Count >= 29);
        }
    }
}
