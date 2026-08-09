using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.ExpertProfiles;
using SimpleXmlEditor.Services;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    /// <summary>验证批次翻译失败时自动拆半重试的兜底逻辑。</summary>
    public class TranslationOrchestratorTests
    {
        [Fact]
        public async Task TranslateBatchAsync_EmptyResponse_HalvesAndRecovers()
        {
            // 多条目请求返回空响应（模拟输出 token 截断），单条目请求返回有效 JSON
            var ai = new FakeAiService();
            var orch = CreateOrchestrator(ai);

            var entries = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "K1", Value = "aaa" },
                new LocalizationEntry { Key = "K2", Value = "bbb" },
                new LocalizationEntry { Key = "K3", Value = "ccc" },
                new LocalizationEntry { Key = "K4", Value = "ddd" },
            };

            var result = await orch.TranslateBatchAsync(entries, forceRefresh: false, customPrompt: null);

            Assert.Equal(4, result.Count);
            Assert.Equal("TR:aaa", result["aaa"]);
            Assert.Equal("TR:bbb", result["bbb"]);
            Assert.Equal("TR:ccc", result["ccc"]);
            Assert.Equal("TR:ddd", result["ddd"]);
            // 4 条失败 → 2+2 失败 → 1×4 成功：共 7 次调用
            Assert.True(ai.Calls >= 7, $"expected >= 7 calls, got {ai.Calls}");
        }

        [Fact]
        public async Task TranslateBatchAsync_Exception_HalvesAndRecovers()
        {
            // 多条目请求抛异常（模拟超时），单条目请求成功
            var ai = new FakeAiService { ThrowOnMulti = true };
            var orch = CreateOrchestrator(ai);

            var entries = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "K1", Value = "aaa" },
                new LocalizationEntry { Key = "K2", Value = "bbb" },
                new LocalizationEntry { Key = "K3", Value = "ccc" },
            };

            var result = await orch.TranslateBatchAsync(entries, forceRefresh: false, customPrompt: null);

            Assert.Equal(3, result.Count);
            Assert.Equal("TR:aaa", result["aaa"]);
            Assert.Equal("TR:ccc", result["ccc"]);
        }

        private static TranslationOrchestrator CreateOrchestrator(FakeAiService ai)
        {
            return new TranslationOrchestrator(
                ai,
                new FakeConfigService(),
                new FakeGlossaryManager(),
                new FakeExpertProfileManager(),
                _ => { });
        }

        // ─── Fakes ─────────────────────────────────────────────

        private class FakeAiService : IAiTranslationService
        {
            public int Calls;
            public bool ThrowOnMulti;

            public AIProvider CurrentProvider { get; set; }
            public string ApiKey { get; set; } = "k";
            public string Model { get; set; } = "m";
            public string TargetLanguage { get; set; } = "zh";
            public HttpClient HttpClient => new HttpClient();
            public ConcurrentDictionary<string, (double input, double output)> ModelPricing => new();
            public ConcurrentDictionary<string, (int requestsPerMinute, int requestsPerDay, int tokensPerMinute)> ModelLimits => new();
            public ConcurrentQueue<DateTime> RecentRequests => new();

            public event Action<string> LogMessage;

            public Task<List<string>> FetchAvailableModelsAsync(string apiKey, AIProvider? provider = null)
                => Task.FromResult(new List<string>());

            public Task<string> TranslateBatchAsync(string prompt, int maxRetries = 3, bool? disableThinking = null)
            {
                Calls++;
                // 匹配新格式：index. [KEY] "text"（KEY 为条目标识，测试中不校验其内容）
                var matches = Regex.Matches(prompt, @"(?m)^\d+\. \[(.*?)\] ""(.*)""");
                if (matches.Count > 1)
                {
                    if (ThrowOnMulti)
                        throw new HttpRequestException("simulated timeout");
                    return Task.FromResult("");
                }
                var text = matches.Count == 1 ? matches[0].Groups[2].Value : "";
                var json = $"{{\"translations\":[{{\"index\":1,\"translation\":\"TR:{text}\"}}]}}";
                return Task.FromResult(json);
            }

            public double CalculateCost(int inputChars, int outputChars, string modelName) => 0;
            public int GetModelTokenLimit(string modelName) => 0;
            public void TrackRequest() { }
            public void Dispose() { }
        }

        private class FakeConfigService : IConfigService
        {
            public ConcurrentDictionary<string, string> Cache { get; } = new();
            public AppConfig Config { get; } = new AppConfig();
            public event Action<string> LogMessage;

            public void LoadConfig() { }
            public void SaveConfig() { }
            public void SaveCache() { }
            public void SaveScoreCache() { }
            public void ClearScoreCache() { }
            public void SyncScoresToCache(IEnumerable<LocalizationEntry> entries) { }
            public int RestoreScores(IEnumerable<LocalizationEntry> entries) => 0;
            public void SyncEntriesToCache(IEnumerable<LocalizationEntry> entries) { }
        public Task SaveTranslationProgressAsync(IEnumerable<LocalizationEntry> entries) => Task.CompletedTask;
            public void SetCacheEntry(string key, string originalText, string translation)
            {
                if (string.IsNullOrWhiteSpace(originalText)) return;
                Cache[key] = translation;
                var cacheKey = GetCacheKey(originalText);
                if (cacheKey != null) Cache[cacheKey] = translation;
            }

            public int RestoreTranslationProgress(IEnumerable<LocalizationEntry> entries) => 0;
            public void DeleteProgressFile() { }
            // 与生产实现一致：缓存键为原文的 MD5 hex（大写）
            public string GetCacheKey(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                using var md5 = System.Security.Cryptography.MD5.Create();
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
                return System.Convert.ToHexString(hash);
            }
            public void UpdateConfig(Action<AppConfig> updater) { }
            public void SetApiKey(string apiKey) { }
            public string GetApiKey() => "";
            public void SetEvaluationApiKey(string apiKey) { }
            public string GetEvaluationApiKey() => "";
            public void SaveEvaluationModels(List<(string Provider, string Model, string ApiKey)> models) { }
            public string GetEvaluationModelKey(EvaluationModelConfig model) => "";
            public bool MigrateLegacyApiKey() => false;
        }

        private class FakeGlossaryManager : IGlossaryManager
        {
            public ConcurrentDictionary<string, GlossaryTerm> Terms { get; } = new();
            public int Count => 0;
            public event Action<string> LogMessage;

            public bool TryGetValue(string sourceText, out string translated)
            {
                translated = "";
                return false;
            }

            public Dictionary<string, string> GetGlossaryContextTerms(List<LocalizationEntry> entries) => new();
            public (int added, int updated, int skipped) ImportCsv(string filePath) => (0, 0, 0);
            public (int added, int updated) ImportJson(string filePath) => (0, 0);
            public void SetEntry(string source, string translation, string category = "", string status = "confirmed", string tags = "") { }
            public void SetTerm(GlossaryTerm term) { }
            public bool RemoveEntry(string source) => false;
            public void Clear() { }
            public void Load() { }
            public List<GlossaryTerm> Search(string query) => new();
            public List<string> GetAllCategories() => new();
            public void ExportCsv(string filePath) { }
            public void ExportJson(string filePath) { }
            public (int added, int updated) MergeFromProfile(string profileName, Dictionary<string, string> profileGlossary) => (0, 0);
            public List<GlossaryConflict> DetectConflicts(
                IEnumerable<(string key, string source, string translation)> entries,
                Action<int, int> onProgress = null) => new();
        }

        private class FakeExpertProfileManager : IExpertProfileManager
        {
            public List<ExpertProfile> Profiles { get; } = new();
            public string ActiveProfileName { get; set; } = "";
            public ExpertProfile ActiveProfile => null;
            public event Action<string> LogMessage;

            public void LoadProfiles() { }
            public void SaveProfiles() { }
            public void AddProfile(ExpertProfile profile) { }
            public void DeleteProfile(string name) { }
            public ExpertProfile GetProfile(string name) => null;
            public void EnsureDefaultsExist() { }
        }
    }
}
