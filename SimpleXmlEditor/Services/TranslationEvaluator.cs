using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor.Services
{
    /// <summary>
    /// AI evaluation result for a single translation.
    /// </summary>
    public class EvaluationResult
    {
        public double Score { get; set; }
        public string Explanation { get; set; } = "";
        public string Improvement { get; set; } = "";
        public string ProviderName { get; set; } = "";
        public string TranslatedText { get; set; } = "";
    }

    /// <summary>
    /// Multi-agent voting result with weighted consensus.
    /// </summary>
    public class VotingResult
    {
        public string OriginalText { get; set; } = "";
        /// <summary>Entry key this vote belongs to (set for batch voting).</summary>
        public string EntryKey { get; set; } = "";
        public List<EvaluationResult> AgentResults { get; set; } = new();
        public double AverageScore { get; set; }
        public string BestTranslation { get; set; } = "";
        public string ConsensusSummary { get; set; } = "";
    }

    /// <summary>
    /// Service for AI-powered translation quality evaluation and multi-agent voting.
    /// Reuses the existing AiTranslationService for API communication.
    /// Partial 类拆分：Prompts（提示词构建）/ Parsing（响应解析）/ Utils（结果聚合）。
    /// </summary>
    public partial class TranslationEvaluator : ITranslationEvaluator
    {
        private readonly IAiTranslationService _aiService;
        private readonly IConfigService _configService;
        private AiTranslationService _evalAiService;
        private readonly Dictionary<string, AiTranslationService> _evalServices = new();

        public event Action<string> LogMessage;

        public TranslationEvaluator(IAiTranslationService aiService, IConfigService configService = null)
        {
            _aiService = aiService;
            _configService = configService;
        }

        /// <summary>
        /// 返回评估专用 API 实例（优先多模型列表第一组；否则单组配置；否则翻译 API）。
        /// </summary>
        private IAiTranslationService GetActiveAiService()
        {
            if (_configService == null)
                return _aiService;

            var cfg = _configService.Config;

            // 多模型列表优先：返回第一组有效配置
            if (cfg.EvaluationModels != null && cfg.EvaluationModels.Count > 0)
            {
                foreach (var m in cfg.EvaluationModels)
                {
                    var key = _configService.GetEvaluationModelKey(m);
                    if (string.IsNullOrEmpty(m.Provider) || string.IsNullOrEmpty(m.Model))
                        continue;
                    if (Enum.TryParse<AIProvider>(m.Provider, out var provider))
                        return GetOrCreateEvalService(provider, key, m.Model);
                }
            }

            if (string.IsNullOrEmpty(cfg.EvaluationAiProvider) || string.IsNullOrEmpty(cfg.EvaluationModel))
                return _aiService;

            var evalKey = _configService.GetEvaluationApiKey();
            if (string.IsNullOrEmpty(evalKey))
                return _aiService;

            // 懒初始化评估专用服务实例
            if (_evalAiService == null)
            {
                _evalAiService = new AiTranslationService(_configService);
                _evalAiService.LogMessage += msg => LogMessage?.Invoke(msg);
            }

            // 每次调用时同步配置（用户可能随时改设置）
            if (Enum.TryParse<AIProvider>(cfg.EvaluationAiProvider, out var provider2))
            {
                _evalAiService.SetConfiguration(provider2, evalKey, cfg.EvaluationModel, _aiService.TargetLanguage);
                return _evalAiService;
            }

            return _aiService;
        }

        /// <summary>
        /// 返回评估专用 API 实例列表（多模型投票：每个配置一个实例）。
        /// 无有效配置时回退为单个翻译 API 实例，保证行为与未配置评估模型时一致。
        /// </summary>
        private List<IAiTranslationService> GetActiveAiServices()
        {
            var services = new List<IAiTranslationService>();
            if (_configService != null)
            {
                var cfg = _configService.Config;
                if (cfg.EvaluationModels != null && cfg.EvaluationModels.Count > 0)
                {
                    foreach (var m in cfg.EvaluationModels)
                    {
                        var key = _configService.GetEvaluationModelKey(m);
                        if (string.IsNullOrEmpty(m.Provider) || string.IsNullOrEmpty(m.Model))
                            continue;
                        if (Enum.TryParse<AIProvider>(m.Provider, out var provider))
                            services.Add(GetOrCreateEvalService(provider, key, m.Model));
                    }
                    if (services.Count > 0)
                        return services;
                }
            }
            services.Add(GetActiveAiService());
            return services;
        }

        /// <summary>按 (厂商, 模型) 缓存并返回评估专用服务实例。</summary>
        private AiTranslationService GetOrCreateEvalService(AIProvider provider, string apiKey, string model)
        {
            var cacheKey = $"{provider}|{model}";
            if (!_evalServices.TryGetValue(cacheKey, out var svc))
            {
                svc = new AiTranslationService(_configService);
                svc.LogMessage += msg => LogMessage?.Invoke(msg);
                _evalServices[cacheKey] = svc;
            }
            svc.SetConfiguration(provider, apiKey, model, _aiService.TargetLanguage);
            return svc;
        }

        private void RaiseLog(string message)
        {
            LogMessage?.Invoke(message);
        }

        /// <summary>
        /// Evaluate a single translation: rate quality (0-10), explain reasoning, suggest improvements.
        /// </summary>
        public async Task<EvaluationResult> EvaluateAsync(
            string originalText,
            string translatedText,
            string targetLanguage,
            string context = "")
        {
            var prompt = BuildEvaluationPrompt(originalText, translatedText, targetLanguage, context);
            var response = await GetActiveAiService().TranslateBatchAsync(prompt, 2);

            return ParseEvaluationResponse(response, originalText, translatedText);
        }

        /// <summary>
        /// Multi-agent voting: evaluates all candidates from 3 agent perspectives
        /// in a SINGLE API call (instead of N agents * M candidates separate calls).
        /// Token cost: roughly 1x the cost of a normal batch translation call.
        /// </summary>
        public async Task<VotingResult> VoteAsync(
            string originalText,
            string[] candidateTranslations,
            string targetLanguage,
            string context = "")
        {
            if (candidateTranslations == null || candidateTranslations.Length == 0)
                return new VotingResult { OriginalText = originalText };

            try
            {
                var allResults = new List<EvaluationResult>();
                foreach (var service in GetActiveAiServices())
                {
                    var prompt = BuildBatchedVotingPrompt(originalText, candidateTranslations, targetLanguage, context);
                    var response = await service.TranslateBatchAsync(prompt, 2, disableThinking: false);
                    allResults.AddRange(ParseBatchedVotingResponse(response, originalText, candidateTranslations));
                }

                if (allResults.Count == 0)
                    return new VotingResult { OriginalText = originalText };

                return BuildVotingResult(originalText, allResults);
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("LogVotingFailed", ex.Message));
                return new VotingResult { OriginalText = originalText };
            }
        }

        /// <summary>
        /// Generate N alternative translation candidates for a single source text (for voting).
        /// Uses ONE API call; AI returns all candidates in a JSON array.
        /// </summary>
        public async Task<string[]> GenerateCandidatesAsync(
            string originalText,
            string targetLanguage,
            string context = "",
            int count = 2)
        {
            if (string.IsNullOrEmpty(originalText) || count <= 0)
                return Array.Empty<string>();

            try
            {
                var prompt = BuildCandidatePrompt(originalText, targetLanguage, context, count);
                var response = await GetActiveAiService().TranslateBatchAsync(prompt, 2, disableThinking: false);
                var candidates = ParseCandidateResponse(response);
                return candidates.Take(count).ToArray();
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("LogCandidateGenFailed", ex.Message));
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Evaluate MULTIPLE entries in a single API call (batch acceleration).
        /// Items: (entryKey, originalText, translatedText).
        /// </summary>
        public async Task<List<EvaluationResult>> EvaluateBatchAsync(
            List<(string Key, string Original, string Translated)> items,
            string targetLanguage,
            string context = "",
            int batchSize = 20)
        {
            var allResults = new List<EvaluationResult>();
            if (items == null || items.Count == 0)
                return allResults;

            foreach (var chunk in Chunk(items, batchSize))
            {
                try
                {
                    var prompt = BuildBatchEvaluationPrompt(chunk, targetLanguage, context);
                    var response = await GetActiveAiService().TranslateBatchAsync(prompt, 2);
                    var parsed = ParseBatchEvaluationResponse(response, chunk);
                    allResults.AddRange(parsed);

                    if (parsed.Count == 0)
                    {
                        RaiseLog(LocalizationManager.GetString("LogEvalParseFallback"));
                        foreach (var item in chunk)
                        {
                            try
                            {
                                var single = await EvaluateAsync(item.Original, item.Translated, targetLanguage, context);
                                if (single != null)
                                {
                                    single.TranslatedText = item.Key;
                                    allResults.Add(single);
                                }
                            }
                            catch (Exception ex)
                            {
                                RaiseLog(LocalizationManager.GetString("LogPerEntryEvalFailed", item.Key, ex.Message));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    RaiseLog(LocalizationManager.GetString("LogEvalChunkFailed", ex.Message));
                }
            }

            return allResults;
        }

        /// <summary>
        /// Vote MULTIPLE entries in a single API call (batch acceleration).
        /// Items: (entryKey, originalText, candidateTranslations).
        /// </summary>
        public async Task<List<VotingResult>> VoteBatchAsync(
            List<(string Key, string Original, string[] Candidates)> items,
            string targetLanguage,
            string context = "",
            int batchSize = 10)
        {
            var allResults = new List<VotingResult>();
            if (items == null || items.Count == 0)
                return allResults;

            foreach (var chunk in Chunk(items, batchSize))
            {
                try
                {
                    var parsedList = new List<VotingResult>();
                    foreach (var service in GetActiveAiServices())
                    {
                        var prompt = BuildBatchVotingPrompt(chunk, targetLanguage, context);
                        var response = await service.TranslateBatchAsync(prompt, 2, disableThinking: false);
                        parsedList.AddRange(ParseBatchVotingResponse(response, chunk));
                    }

                    // 按条目合并多模型结果；单模型（每条目仅一份）保留 AI 返回的 best 原样
                    var merged = new List<VotingResult>();
                    foreach (var g in parsedList.GroupBy(v => v.EntryKey))
                    {
                        var group = g.ToList();
                        merged.Add(group.Count == 1 ? group[0] : MergeVotingResults(group));
                    }
                    allResults.AddRange(merged);

                    if (merged.Count == 0)
                    {
                        RaiseLog(LocalizationManager.GetString("LogVotingParseFallback"));
                        foreach (var item in chunk)
                        {
                            try
                            {
                                var single = await VoteAsync(item.Original, item.Candidates, targetLanguage, context);
                                if (single != null)
                                {
                                    single.EntryKey = item.Key;
                                    allResults.Add(single);
                                }
                            }
                            catch (Exception ex)
                            {
                                RaiseLog(LocalizationManager.GetString("LogPerEntryVotingFailed", item.Key, ex.Message));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    RaiseLog(LocalizationManager.GetString("LogVotingChunkFailed", ex.Message));
                }
            }

            return allResults;
        }
    }
}
