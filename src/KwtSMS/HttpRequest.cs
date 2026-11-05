using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KwtSMS
{
    /// <summary>
    /// Internal HTTP request handler for kwtSMS API calls.
    /// </summary>
    internal static class HttpRequest
    {
        private static HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        internal static HttpClient Client
        {
            get => _client;
            set => _client = value;
        }

        /// <summary>
        /// POST JSON to a kwtSMS API endpoint and parse the JSON response.
        /// Reads HTTP 4xx/5xx response bodies (kwtSMS returns JSON error details in 403s).
        /// </summary>
        internal static Dictionary<string, object?> Post(
            string endpoint,
            Dictionary<string, object?> payload,
            string logFile,
            string userAgent = "")
        {
            Dictionary<string, object?>? response = null;
            string? errorMsg = null;

            try
            {
                var url = "https://www.kwtsms.com/API/" + endpoint + "/";
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType!.MediaType = "application/json";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                // 2026-11-05 14:15 +03: "kwtsms-csharp/<version>", prefixed by the app token when given (#939).
                // Without validation: "a:b" style tokens are fine on the wire but rejected by ProductInfoHeaderValue.
                request.Headers.TryAddWithoutValidation("User-Agent", userAgent.Length > 0 ? userAgent : KwtSmsClient.BuildUserAgent());

                var httpResponse = _client.SendAsync(request).GetAwaiter().GetResult();
                var body = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (string.IsNullOrWhiteSpace(body))
                {
                    response = new Dictionary<string, object?>
                    {
                        ["result"] = "ERROR",
                        ["code"] = "NETWORK_ERROR",
                        ["description"] = $"HTTP {(int)httpResponse.StatusCode}: empty response body"
                    };
                    errorMsg = response["description"]?.ToString();
                    Logger.WriteLog(logFile, endpoint, payload, response, false, errorMsg);
                    return ApiErrors.EnrichError(response);
                }

                response = ParseJsonResponse(body);

                var ok = response.ContainsKey("result") &&
                         response["result"]?.ToString() == "OK";

                if (!ok)
                {
                    response = ApiErrors.EnrichError(response);
                    errorMsg = response.ContainsKey("description")
                        ? response["description"]?.ToString()
                        : "Unknown error";
                }

                Logger.WriteLog(logFile, endpoint, payload, response, ok, errorMsg);
                return response;
            }
            catch (TaskCanceledException)
            {
                response = new Dictionary<string, object?>
                {
                    ["result"] = "ERROR",
                    ["code"] = "TIMEOUT",
                    ["description"] = "Request timed out after 15 seconds"
                };
                errorMsg = response["description"]?.ToString();
                Logger.WriteLog(logFile, endpoint, payload, response, false, errorMsg);
                return ApiErrors.EnrichError(response);
            }
            catch (HttpRequestException ex)
            {
                // Log the real exception detail; return a generic message to the caller
                // to avoid leaking internal service URLs or infrastructure details.
                errorMsg = $"Network error: {ex.Message}";
                response = new Dictionary<string, object?>
                {
                    ["result"] = "ERROR",
                    ["code"] = "NETWORK_ERROR",
                    ["description"] = "Could not connect to the SMS service. Check your network connection."
                };
                Logger.WriteLog(logFile, endpoint, payload, response, false, errorMsg);
                return ApiErrors.EnrichError(response);
            }
            catch (Exception ex)
            {
                // Log the real exception detail; return a generic message to the caller.
                errorMsg = $"Client error: {ex.Message}";
                response = new Dictionary<string, object?>
                {
                    ["result"] = "ERROR",
                    ["code"] = "CLIENT_ERROR",
                    ["description"] = "An unexpected error occurred in the SMS client."
                };
                Logger.WriteLog(logFile, endpoint, payload, response, false, errorMsg);
                return ApiErrors.EnrichError(response);
            }
        }

        private static Dictionary<string, object?> ParseJsonResponse(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                return JsonElementToDict(doc.RootElement);
            }
            catch
            {
                return new Dictionary<string, object?>
                {
                    ["result"] = "ERROR",
                    ["code"] = "PARSE_ERROR",
                    ["description"] = $"Failed to parse API response: {body}"
                };
            }
        }

        internal static Dictionary<string, object?> JsonElementToDict(JsonElement element)
        {
            var dict = new Dictionary<string, object?>();

            if (element.ValueKind != JsonValueKind.Object)
            {
                dict["_raw"] = element.ToString();
                return dict;
            }

            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = JsonElementToObject(prop.Value);
            }

            return dict;
        }

        internal static object? JsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var longVal))
                        return longVal;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(JsonElementToObject(item));
                    }
                    return list;
                case JsonValueKind.Object:
                    return JsonElementToDict(element);
                default:
                    return element.ToString();
            }
        }
    }
}
