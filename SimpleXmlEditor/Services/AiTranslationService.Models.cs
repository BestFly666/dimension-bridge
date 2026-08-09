using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SimpleXmlEditor.Services
{
    public partial class AiTranslationService
    {
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
                // 直接传参，不临时覆盖 _apiKey：
                // 否则与正在运行的后台翻译（读 _apiKey 构造鉴权头）竞争，可能用错 Key
                return await GetGeminiModelsAsync(apiKey);
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

        private async Task<List<string>> GetGeminiModelsAsync(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return new List<string>();

            try
            {
                var url = "https://generativelanguage.googleapis.com/v1beta/models";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Remove("x-goog-api-key");
                request.Headers.Add("x-goog-api-key", apiKey);
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
    }
}
