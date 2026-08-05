using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor.Services
{
    public partial class AiTranslationService
    {
        private async Task<string> TranslateBatchGeminiAsync(string prompt)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    topP = 0.8,
                    topK = 40,
                    maxOutputTokens = 8192
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            SetGeminiAuthHeader(request);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            var responseJson = JObject.Parse(responseText);

            return responseJson["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()?.Trim();
        }

        private async Task<string> TranslateBatchOpenAiCompatAsync(string prompt, int maxRetries)
        {
            var baseUrl = ProviderConfig.ApiBaseUrls[_currentProvider];
            var url = $"{baseUrl}/chat/completions";

            // 8192 让单次输出容纳更多译文（如 100 条/批），显著减少批次数与限流等待
            var maxTokens = 8192;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = PromptTemplates.SystemPrompt },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3,
                    max_tokens = maxTokens
                };

                var json = JsonConvert.SerializeObject(requestBody);

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                SetAuthHeader(request);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    var responseJson = JObject.Parse(responseText);
                    return responseJson["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                var statusCode = (int)response.StatusCode;

                if (statusCode == 400 && maxTokens > 4096)
                {
                    // 部分模型 max_tokens 输出上限只有 4096：收到 400 自动降级重试
                    maxTokens = 4096;
                    continue;
                }

                if (statusCode == 429)
                {
                    var delay = (attempt + 1) * 3000;
                    RaiseLog(LocalizationManager.GetString("LogRateLimitedRetry", attempt + 1, maxRetries, delay / 1000));
                    await Task.Delay(delay);
                    continue;
                }

                if (statusCode == 401 || statusCode == 403)
                    throw new InvalidOperationException($"[HTTP {statusCode}] Invalid API Key or access denied.");

                if (statusCode == 402)
                {
                    var errMsg = "Balance exhausted. Please top up your account.";
                    try
                    {
                        var errJson = JObject.Parse(errorBody);
                        var detail = errJson["error"]?["message"]?.ToString();
                        if (!string.IsNullOrEmpty(detail))
                            errMsg = detail;
                    }
                    catch (Exception parseEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parsing 402 error body: {parseEx.Message}");
                    }
                    throw new InvalidOperationException($"[HTTP 402] {errMsg}");
                }

                var otherMsg = $"HTTP {statusCode}";
                try
                {
                    var errJson = JObject.Parse(errorBody);
                    var detail = errJson["error"]?["message"]?.ToString();
                    if (!string.IsNullOrEmpty(detail))
                        otherMsg = detail;
                }
                catch (Exception parseEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing HTTP {statusCode} error body: {parseEx.Message}");
                }
                throw new InvalidOperationException($"[HTTP {statusCode}] {otherMsg}");
            }

            throw new InvalidOperationException("Translation failed after max retries (rate limited).");
        }

        private async Task<string> TranslateSingleGeminiAsync(string text, string prompt)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            SetGeminiAuthHeader(request);

            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new HttpRequestException("429");

            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            var responseJson = JObject.Parse(responseText);

            return responseJson["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()?.Trim();
        }

        private async Task<string> TranslateSingleOpenAiCompatAsync(string text, string prompt)
        {
            var baseUrl = ProviderConfig.ApiBaseUrls[_currentProvider];
            var url = $"{baseUrl}/chat/completions";

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = PromptTemplates.SystemPrompt },
                    new { role = "user", content = prompt }
                },
                temperature = 0.3,
                max_tokens = 4096
            };

            var json = JsonConvert.SerializeObject(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            SetAuthHeader(request);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            var responseJson = JObject.Parse(responseText);

            return responseJson["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim();
        }

        private void SetAuthHeader(HttpRequestMessage request)
        {
            request.Headers.Remove("Authorization");
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        }

        private void SetGeminiAuthHeader(HttpRequestMessage request)
        {
            request.Headers.Remove("x-goog-api-key");
            request.Headers.Add("x-goog-api-key", _apiKey);
        }
    }
}
