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

    public partial class AiTranslationService : IAiTranslationService
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
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
            _configService = configService;
        }

        public AiTranslationService(HttpClient httpClient, IConfigService configService = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
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
            // 未知模型：不强制等待，靠 429 退避兜底
            if (!ModelLimits.ContainsKey(_model))
                return 0;

            var (requestsPerMinute, _, _) = ModelLimits[_model];

            var oneMinuteAgo = DateTime.Now.AddMinutes(-1);
            while (RecentRequests.Count > 0 && RecentRequests.TryPeek(out var oldestTime) && oldestTime < oneMinuteAgo)
            {
                RecentRequests.TryDequeue(out _);
            }

            var requestsInLastMinute = RecentRequests.Count;
            var remainingRequests = Math.Max(0, requestsPerMinute - requestsInLastMinute);

            // 配额尚未用完：无需等待（批次生成本身耗时已天然限流），避免"结果已出却被硬等"
            if (remainingRequests > 0)
                return 0;

            // 配额耗尽：等到最旧请求滑出一分钟窗口
            if (RecentRequests.TryPeek(out var oldestRequest))
            {
                var waitTime = (int)(60000 - (DateTime.Now - oldestRequest).TotalMilliseconds);
                return Math.Max(waitTime, 1000);
            }
            return 3000;
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
                        var delay = 3000 * (attempt + 2);
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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
