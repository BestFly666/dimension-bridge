using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor.Services
{
    public enum AIProvider
    {
        GoogleGemini,
        DeepSeek,
        Doubao,
        Qianwen,
        Zhipu,
        Moonshot,
        Wenxin,
        Xunfei
    }

    public static class ProviderConfig
    {
        public static readonly Dictionary<AIProvider, string> ApiBaseUrls = new()
        {
            { AIProvider.GoogleGemini, "https://generativelanguage.googleapis.com" },
            { AIProvider.DeepSeek, "https://api.deepseek.com" },
            { AIProvider.Doubao, "https://ark.cn-beijing.volces.com/api/v3" },
            { AIProvider.Qianwen, "https://dashscope.aliyuncs.com/compatible-mode" },
            { AIProvider.Zhipu, "https://open.bigmodel.cn/api/paas/v4" },
            { AIProvider.Moonshot, "https://api.moonshot.cn/v1" },
            { AIProvider.Wenxin, "https://qianfan.cloud.baidu.com/v2" },
            { AIProvider.Xunfei, "https://spark-api-open.xfyun.cn/v1" }
        };

        public static readonly Dictionary<AIProvider, bool> UsesOpenAiFormat = new()
        {
            { AIProvider.GoogleGemini, false },
            { AIProvider.DeepSeek, true },
            { AIProvider.Doubao, true },
            { AIProvider.Qianwen, true },
            { AIProvider.Zhipu, true },
            { AIProvider.Moonshot, true },
            { AIProvider.Wenxin, true },
            { AIProvider.Xunfei, true }
        };
    }

    public class AiTranslationService : IAiTranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigService _configService;
        private AIProvider _currentProvider = AIProvider.GoogleGemini;
        private string _apiKey = "";
        private string _model = "";
        private string _targetLanguage = "Turkish";

        public Dictionary<string, (double input, double output)> ModelPricing { get; private set; } = new();
        public Dictionary<string, (int requestsPerMinute, int requestsPerDay, int tokensPerMinute)> ModelLimits { get; private set; } = new();
        public ConcurrentQueue<DateTime> RecentRequests { get; private set; } = new();

        public event Action<string> LogMessage;

        // Statistics callbacks (raised for single-entry translation paths)
        public event Action<int> CacheHit;
        public event Action<int> ApiCallCounted;
        public event Action<int, int> ApiCharsCounted; // (inputChars, outputChars)

        public AIProvider CurrentProvider
        {
            get => _currentProvider;
            set => _currentProvider = value;
        }

        public string ApiKey
        {
            get => _apiKey;
            set => _apiKey = value;
        }

        public string Model
        {
            get => _model;
            set => _model = value;
        }

        public string TargetLanguage
        {
            get => _targetLanguage;
            set => _targetLanguage = value;
        }

        public HttpClient HttpClient => _httpClient;

        public AiTranslationService(IConfigService configService = null)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            _configService = configService;
        }

        public AiTranslationService(HttpClient httpClient, IConfigService configService = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            _configService = configService;
        }

        public void SetConfiguration(AIProvider provider, string apiKey, string model, string targetLanguage)
        {
            _currentProvider = provider;
            _apiKey = apiKey;
            _model = model;
            _targetLanguage = targetLanguage;
        }

        private void RaiseLog(string message)
        {
            // Sanitize logs to prevent leaking sensitive data like API keys
            message = SanitizeLogMessage(message);
            LogMessage?.Invoke(message);
        }

        /// <summary>
        /// Sanitizes log messages by masking API keys and sensitive headers.
        /// </summary>
        private string SanitizeLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;

            // Mask API keys appearing in URLs (?key=...)
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"key=[A-Za-z0-9_\-]{10,}",
                "key=[REDACTED]");

            // Mask Authorization headers
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"Authorization:\s*Bearer\s+[A-Za-z0-9_\-\.]+",
                "Authorization: Bearer [REDACTED]");

            // Mask x-goog-api-key headers
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"x-goog-api-key:\s*[A-Za-z0-9_\-]+",
                "x-goog-api-key: [REDACTED]");

            return message;
        }

        public static readonly Dictionary<AIProvider, List<string>> StaticModels = new()
        {
            { AIProvider.DeepSeek, new List<string> { "deepseek-v4-flash", "deepseek-v4-pro" } },
            { AIProvider.Doubao, new List<string> { "doubao-pro-32k", "doubao-pro-128k", "doubao-lite-32k", "doubao-lite-128k", "doubao-thinking-pro" } },
            { AIProvider.Qianwen, new List<string> { "qwen-plus", "qwen-max", "qwen-turbo", "qwen-long", "qwen2.5-7b", "qwen2.5-72b" } },
            { AIProvider.Zhipu, new List<string> { "glm-4", "glm-4-flash", "glm-4-air", "glm-4-long", "glm-4-plus", "glm-4.5" } },
            { AIProvider.Moonshot, new List<string> { "moonshot-v1-8k", "moonshot-v1-32k", "moonshot-v1-128k" } },
            { AIProvider.Wenxin, new List<string> { "ernie-4.0-turbo", "ernie-4.0", "ernie-3.5", "ernie-speed" } },
            { AIProvider.Xunfei, new List<string> { "general-v3.5", "general-v3", "general-v2", "general-1.5" } }
        };

        public static readonly Dictionary<AIProvider, Dictionary<string, (int rpm, int rpd, int tpm)>> ProviderRateLimits = new()
        {
            { AIProvider.DeepSeek, new Dictionary<string, (int, int, int)> { ["deepseek-v4-flash"] = (100, -1, -1), ["deepseek-v4-pro"] = (100, -1, -1) } },
            { AIProvider.Doubao, new Dictionary<string, (int, int, int)> { ["doubao-pro-32k"] = (30, -1, -1), ["doubao-pro-128k"] = (30, -1, -1), ["doubao-lite-32k"] = (60, -1, -1), ["doubao-lite-128k"] = (60, -1, -1), ["doubao-thinking-pro"] = (20, -1, -1) } },
            { AIProvider.Qianwen, new Dictionary<string, (int, int, int)> { ["qwen-plus"] = (50, -1, -1), ["qwen-max"] = (30, -1, -1), ["qwen-turbo"] = (100, -1, -1), ["qwen-long"] = (20, -1, -1), ["qwen2.5-7b"] = (100, -1, -1), ["qwen2.5-72b"] = (30, -1, -1) } },
            { AIProvider.Zhipu, new Dictionary<string, (int, int, int)> { ["glm-4"] = (50, -1, -1), ["glm-4-flash"] = (100, -1, -1), ["glm-4-air"] = (100, -1, -1), ["glm-4-long"] = (20, -1, -1), ["glm-4-plus"] = (50, -1, -1), ["glm-4.5"] = (50, -1, -1) } },
            { AIProvider.Moonshot, new Dictionary<string, (int, int, int)> { ["moonshot-v1-8k"] = (60, -1, -1), ["moonshot-v1-32k"] = (60, -1, -1), ["moonshot-v1-128k"] = (60, -1, -1) } },
            { AIProvider.Wenxin, new Dictionary<string, (int, int, int)> { ["ernie-4.0-turbo"] = (50, -1, -1), ["ernie-4.0"] = (30, -1, -1), ["ernie-3.5"] = (50, -1, -1), ["ernie-speed"] = (100, -1, -1) } },
            { AIProvider.Xunfei, new Dictionary<string, (int, int, int)> { ["general-v3.5"] = (50, -1, -1), ["general-v3"] = (50, -1, -1), ["general-v2"] = (50, -1, -1), ["general-1.5"] = (100, -1, -1) } }
        };

        public async Task<List<string>> FetchAvailableModelsAsync(string apiKey, AIProvider? provider = null)
        {
            var currentProvider = provider ?? _currentProvider;

            if (currentProvider == AIProvider.GoogleGemini)
            {
                var originalApiKey = _apiKey;
                _apiKey = apiKey;
                try
                {
                    return await GetGeminiModelsAsync();
                }
                finally
                {
                    _apiKey = originalApiKey;
                }
            }

            // 优先尝试从厂商 API 动态拉取模型列表（OpenAI 兼容接口 GET /models）
            // 失败时回退到 StaticModels，保证离线或接口异常时仍可用
            if (ProviderConfig.UsesOpenAiFormat.ContainsKey(currentProvider)
                && ProviderConfig.UsesOpenAiFormat[currentProvider])
            {
                try
                {
                    var dynamicModels = await FetchOpenAiCompatModelsAsync(apiKey, currentProvider);
                    if (dynamicModels.Count > 0)
                    {
                        ModelPricing.Clear();
                        ModelLimits.Clear();
                        EnsureRateLimitsFromStatic(currentProvider);
                        return dynamicModels;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Dynamic model fetch failed for {currentProvider}: {ex.Message}. Falling back to static list.");
                }
            }

            // 回退到静态列表
            if (StaticModels.ContainsKey(currentProvider))
            {
                var models = StaticModels[currentProvider];
                ModelPricing.Clear();
                ModelLimits.Clear();
                EnsureRateLimitsFromStatic(currentProvider);
                return models;
            }

            return new List<string>();
        }

        /// <summary>
        /// 从 OpenAI 兼容厂商的 GET /models 接口动态拉取可用模型列表。
        /// 适用于 DeepSeek / 智谱 / Moonshot / 千问 / 豆包 / 文心 / 讯飞 等厂商。
        /// </summary>
        private async Task<List<string>> FetchOpenAiCompatModelsAsync(string apiKey, AIProvider provider)
        {
            if (string.IsNullOrEmpty(apiKey))
                return new List<string>();

            var baseUrl = ProviderConfig.ApiBaseUrls[provider];
            var url = $"{baseUrl}/models";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Remove("Authorization");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseText);

            var models = new List<string>();
            var data = json["data"] as JArray;
            if (data != null)
            {
                foreach (var item in data)
                {
                    var id = item["id"]?.ToString();
                    if (!string.IsNullOrEmpty(id))
                        models.Add(id);
                }
            }

            return models;
        }

        /// <summary>
        /// 将 StaticModels 中的速率限制信息填充到 ModelLimits 缓存，
        /// 作为动态获取模型时的默认速率限制兜底。
        /// </summary>
        private void EnsureRateLimitsFromStatic(AIProvider provider)
        {
            if (ProviderRateLimits.ContainsKey(provider))
            {
                foreach (var kvp in ProviderRateLimits[provider])
                {
                    ModelLimits[kvp.Key] = kvp.Value;
                }
            }
        }

        private async Task<List<string>> GetGeminiModelsAsync()
        {
            if (string.IsNullOrEmpty(_apiKey))
                return new List<string>();

            try
            {
                var url = "https://generativelanguage.googleapis.com/v1beta/models";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                SetGeminiAuthHeader(request);
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseText = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseText);

                var models = new List<string>();
                ModelPricing.Clear();
                ModelLimits.Clear();

                if (json["models"] is JArray modelsArray)
                {
                    foreach (var model in modelsArray)
                    {
                        var modelName = model["name"]?.ToString().Replace("models/", "");
                        var methods = model["supportedGenerationMethods"] as JArray;

                        if (methods != null && methods.Any(m => m.ToString() == "generateContent"))
                        {
                            models.Add(modelName ?? string.Empty);

                            var inputTokenLimit = model["inputTokenLimit"]?.ToObject<int>() ?? 0;
                            var outputTokenLimit = model["outputTokenLimit"]?.ToObject<int>() ?? 0;

                            var estimatedLimits = EstimateRateLimits(modelName, inputTokenLimit, outputTokenLimit);
                            ModelLimits[modelName] = estimatedLimits;
                        }
                    }
                }

                return models;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fetch models error: {ex.Message}");
                return new List<string>();
            }
        }

        public (int requestsPerMinute, int requestsPerDay, int tokensPerMinute) EstimateRateLimits(string modelName, int inputTokenLimit, int outputTokenLimit)
        {
            var rateLimits = new Dictionary<string, (int rpm, int rpd, int tpm)>
            {
                { "gemini-3-pro-preview", (5, 50, 250000) },
                { "gemini-3-flash-preview", (10, 200, 1000000) },
                { "gemini-3-flash-thinking", (5, 50, 250000) },
                { "gemini-2.5-pro", (2, 50, 250000) },
                { "gemini-2.5-pro-001", (2, 50, 250000) },
                { "gemini-2.5-flash", (15, 1500, 1000000) },
                { "gemini-2.5-flash-001", (15, 1500, 1000000) },
                { "gemini-2.5-flash-lite", (30, 2000, 1000000) },
                { "gemini-2.5-flash-lite-001", (30, 2000, 1000000) },
                { "gemini-2.5-flash-8b", (30, 2000, 1000000) },
                { "gemini-2.0-pro", (5, 100, 500000) },
                { "gemini-2.0-flash", (15, 1500, 1000000) },
                { "gemini-2.0-flash-001", (15, 1500, 1000000) },
                { "gemini-2.0-flash-lite", (30, 1500, 1000000) },
                { "gemini-2.0-flash-exp", (15, 1500, 1000000) },
                { "gemini-2.5-flash-image", (10, 1500, -1) },
                { "gemini-2.0-flash-image", (10, 1500, -1) },
                { "imagen-3.0-generate-002", (2, 100, -1) },
                { "imagen-3.0-capability-001", (2, 100, -1) },
                { "gemini-2.5-flash-audio", (5, 500, -1) },
                { "gemini-live-2.5-flash", (3, -1, -1) },
                { "gemini-exp-2026", (5, 50, 250000) },
                { "gemini-2.5-pro-exp-0205", (5, 50, 250000) },
                { "gemini-2.0-flash-thinking-exp", (5, 50, 250000) },
                { "learnlm-1.5-pro-experimental", (5, 50, 250000) },
                { "gemma-2-27b-it", (15, 1500, 250000) },
                { "gemma-2-9b-it", (30, 2000, 500000) },
                { "gemma-2-2b-it", (30, -1, 1000000) },
                { "text-embedding-005", (100, 10000, -1) },
                { "text-multilingual-embedding-002", (100, 10000, -1) },
                { "gemini-1.5-pro-latest", (2, 50, 32000) },
                { "gemini-1.5-flash-latest", (15, 1500, 1000000) },
                { "gemini-1.5-flash-8b-latest", (15, 1500, 1000000) },
                { "gemini-1.5-pro", (2, 50, 32000) },
                { "gemini-1.5-flash", (15, 1500, 1000000) },
                { "gemini-pro", (60, 1500, 120000) },
                { "aqa", (5, 100, -1) },
                { "med-gemini-preview", (2, 20, 100000) },
                { "gemini-flash-latest", (15, 1500, 1000000) },
                { "gemini-flash-lite-latest", (30, 2000, 1000000) },
                { "gemini-pro-latest", (5, 100, 500000) }
            };

            if (rateLimits.ContainsKey(modelName))
                return rateLimits[modelName];

            foreach (var kvp in rateLimits)
            {
                if (modelName.Contains(kvp.Key) || kvp.Key.Contains(modelName))
                    return kvp.Value;
            }

            if (modelName.Contains("3-pro") || modelName.Contains("3.0"))
                return (5, 50, 250000);
            else if (modelName.Contains("3-flash") || modelName.Contains("3-"))
                return (10, 200, 1000000);
            else if (modelName.Contains("2.5-pro"))
                return (2, 50, 250000);
            else if (modelName.Contains("2.5-flash-lite") || (modelName.Contains("2.5") && modelName.Contains("lite")))
                return (30, 2000, 1000000);
            else if (modelName.Contains("2.5-flash") || modelName.Contains("2.5"))
                return (15, 1500, 1000000);
            else if (modelName.Contains("2.0-pro"))
                return (5, 100, 500000);
            else if (modelName.Contains("2.0-flash-lite") || (modelName.Contains("2.0") && modelName.Contains("lite")))
                return (30, 1500, 1000000);
            else if (modelName.Contains("2.0-flash") || modelName.Contains("2.0"))
                return (15, 1500, 1000000);
            else if (modelName.Contains("1.5-pro"))
                return (2, 50, 32000);
            else if (modelName.Contains("1.5-flash"))
                return (15, 1500, 1000000);
            else if (modelName.Contains("gemma"))
                return (30, 2000, 500000);
            else if (modelName.Contains("exp") || modelName.Contains("preview") || modelName.Contains("experimental"))
                return (5, 50, 250000);
            else if (modelName.Contains("embedding"))
                return (100, 10000, -1);
            else if (modelName.Contains("image") || modelName.Contains("imagen"))
                return (10, 1500, -1);
            else if (modelName.Contains("audio") || modelName.Contains("live"))
                return (5, 500, -1);

            return (2, 20, 10000);
        }

        public int GetModelTokenLimit(string modelName)
        {
            var tokenLimits = new Dictionary<string, int>
            {
                { "gemini-3-pro-preview", 2000000 },
                { "gemini-3-flash-preview", 1000000 },
                { "gemini-2.5-pro", 2000000 },
                { "gemini-2.5-flash", 1000000 },
                { "gemini-2.0-flash", 1000000 },
                { "gemini-1.5-pro", 2000000 },
                { "gemini-1.5-flash", 1000000 },
                { "gemini-pro", 30720 }
            };

            if (tokenLimits.ContainsKey(modelName))
                return tokenLimits[modelName];

            foreach (var kvp in tokenLimits)
            {
                if (modelName.Contains(kvp.Key) || kvp.Key.Contains(modelName))
                    return kvp.Value;
            }

            return 30720;
        }

        public double CalculateCost(int inputChars, int outputChars, string modelName)
        {
            const int charsPerToken = 4;

            if (ModelPricing.ContainsKey(modelName))
            {
                var (inputPrice, outputPrice) = ModelPricing[modelName];
                return (inputChars * inputPrice / (charsPerToken * 1000000.0)) + (outputChars * outputPrice / (charsPerToken * 1000000.0));
            }

            var genericInputPrice = 0.000075;
            var genericOutputPrice = 0.0003;
            return (inputChars * genericInputPrice / 1000.0) + (outputChars * genericOutputPrice / 1000.0);
        }

        public int CalculateOptimalDelay()
        {
            if (!ModelLimits.ContainsKey(_model))
                return 3000;

            var (requestsPerMinute, _, _) = ModelLimits[_model];

            var oneMinuteAgo = DateTime.Now.AddMinutes(-1);
            while (RecentRequests.Count > 0 && RecentRequests.TryPeek(out var oldestTime) && oldestTime < oneMinuteAgo)
            {
                RecentRequests.TryDequeue(out _);
            }

            var requestsInLastMinute = RecentRequests.Count;
            var remainingRequests = Math.Max(0, requestsPerMinute - requestsInLastMinute);

            if (remainingRequests == 0)
            {
                if (RecentRequests.TryPeek(out var oldestRequest))
                {
                    var waitTime = (int)(60000 - (DateTime.Now - oldestRequest).TotalMilliseconds);
                    return Math.Max(waitTime, 1000);
                }
                return 3000;
            }

            var optimalDelay = (int)(60000.0 / requestsPerMinute);
            optimalDelay = (int)(optimalDelay * 1.2);
            optimalDelay = Math.Max(1000, Math.Min(optimalDelay, 30000));

            return optimalDelay;
        }

        public void TrackRequest()
        {
            RecentRequests.Enqueue(DateTime.Now);
        }

        public async Task<string> TranslateBatchAsync(string prompt, int maxRetries = 3)
        {
            try
            {
                if (_currentProvider == AIProvider.GoogleGemini)
                {
                    return await TranslateBatchGeminiAsync(prompt);
                }
                else if (ProviderConfig.UsesOpenAiFormat[_currentProvider])
                {
                    return await TranslateBatchOpenAiCompatAsync(prompt, maxRetries);
                }
                else
                {
                    return await TranslateBatchGeminiAsync(prompt);
                }
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("TranslationError", ex.Message));
                return null;
            }
        }

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
                    topK = 40
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

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
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

        public async Task<string> TranslateSingleAsync(string text, int maxRetries = 3)
        {
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_model))
                return null;

            // Check cache first (skip API call when a cached translation exists)
            var cacheKey = _configService?.GetCacheKey(text);
            if (cacheKey != null && _configService != null
                && _configService.Cache.TryGetValue(cacheKey, out var cachedValue))
            {
                CacheHit?.Invoke(1);
                return cachedValue;
            }

            var prompt = string.Format(PromptTemplates.SingleTranslatePrompt, _targetLanguage, text);

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    TrackRequest();

                    string result;
                    if (_currentProvider == AIProvider.GoogleGemini)
                    {
                        result = await TranslateSingleGeminiAsync(text, prompt);
                    }
                    else if (ProviderConfig.UsesOpenAiFormat[_currentProvider])
                    {
                        result = await TranslateSingleOpenAiCompatAsync(text, prompt);
                    }
                    else
                    {
                        result = await TranslateSingleGeminiAsync(text, prompt);
                    }

                    if (!string.IsNullOrEmpty(result))
                    {
                        // Write to cache and raise cost/billing statistics
                        if (cacheKey != null && _configService != null)
                            _configService.Cache[cacheKey] = result;
                        ApiCallCounted?.Invoke(1);
                        ApiCharsCounted?.Invoke(text.Length, result.Length);
                    }

                    return result;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    if (attempt < maxRetries - 1)
                    {
                        var delay = CalculateOptimalDelay() * (attempt + 2);
                        RaiseLog(LocalizationManager.GetString("LogRateLimit429", delay / 1000, attempt + 1, maxRetries));
                        await Task.Delay(delay);
                        continue;
                    }
                    return null;
                }
                catch (Exception)
                {
                    if (attempt < maxRetries - 1)
                    {
                        var delay = CalculateOptimalDelay();
                        await Task.Delay(delay);
                        continue;
                    }
                    return null;
                }
            }

            return null;
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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
