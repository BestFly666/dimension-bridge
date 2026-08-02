using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
    /// </summary>
    public class TranslationEvaluator : ITranslationEvaluator
    {
        private readonly IAiTranslationService _aiService;
        private readonly IConfigService _configService;
        private AiTranslationService _evalAiService;

        public event Action<string> LogMessage;

        public TranslationEvaluator(IAiTranslationService aiService, IConfigService configService = null)
        {
            _aiService = aiService;
            _configService = configService;
        }

        /// <summary>
        /// 返回评估专用 API 实例（如果配置了不同厂商），否则回退到翻译 API。
        /// </summary>
        private IAiTranslationService GetActiveAiService()
        {
            if (_configService == null)
                return _aiService;

            var cfg = _configService.Config;
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
            if (Enum.TryParse<AIProvider>(cfg.EvaluationAiProvider, out var provider))
            {
                _evalAiService.SetConfiguration(provider, evalKey, cfg.EvaluationModel, _aiService.TargetLanguage);
                return _evalAiService;
            }

            return _aiService;
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
                var prompt = BuildBatchedVotingPrompt(originalText, candidateTranslations, targetLanguage, context);
                var response = await GetActiveAiService().TranslateBatchAsync(prompt, 2);

                var results = ParseBatchedVotingResponse(response, originalText, candidateTranslations);

                if (results.Count == 0)
                    return new VotingResult { OriginalText = originalText };

                var grouped = results
                    .GroupBy(r => r.TranslatedText)
                    .Select(g => new
                    {
                        Translation = g.Key,
                        AvgScore = g.Average(r => r.Score),
                        Count = g.Count()
                    })
                    .OrderByDescending(g => g.AvgScore)
                    .ToList();

                var best = grouped.First();
                var avgScore = results.Average(r => r.Score);

                return new VotingResult
                {
                    OriginalText = originalText,
                    AgentResults = results,
                    AverageScore = Math.Round(avgScore, 1),
                    BestTranslation = best.Translation,
                    ConsensusSummary = $"Best: \"{best.Translation}\" (avg {best.AvgScore:F1}/10 from {best.Count} votes)"
                };
            }
            catch (Exception ex)
            {
                RaiseLog($"⚠ Voting failed: {ex.Message}");
                return new VotingResult { OriginalText = originalText };
            }
        }

        private string BuildBatchedVotingPrompt(string original, string[] candidates, string targetLang, string context)
        {
            var candidateList = string.Join("\n", candidates.Select((c, i) => $"{i + 1}. \"{c}\""));
            return $@"You are a multi-agent translation review panel. Evaluate translation candidates from 3 perspectives.

Original ({targetLang}): ""{original}""

Context: {(string.IsNullOrEmpty(context) ? "General gaming UI text" : context)}

Candidates:
{candidateList}

For EACH candidate, evaluate from ALL 3 perspectives:
- Fluency: naturalness, flow, readability
- Accuracy: whether meaning is preserved exactly
- Style: tone, register, gaming context fit

Return JSON with ALL evaluations:
{{
  ""evaluations"": [
    {{
      ""candidate"": 1,
      ""agent"": ""Fluency"",
      ""score"": 9.0,
      ""explanation"": ""Brief reason""
    }},
    {{
      ""candidate"": 1,
      ""agent"": ""Accuracy"",
      ""score"": 8.5,
      ""explanation"": ""Brief reason""
    }},
    {{
      ""candidate"": 1,
      ""agent"": ""Style"",
      ""score"": 9.0,
      ""explanation"": ""Brief reason""
    }},
    {{
      ""candidate"": 2,
      ""agent"": ""Fluency"",
      ""score"": 8.0,
      ""explanation"": ""Brief reason""
    }},
    ...
  ]
}}

You MUST include all {candidates.Length} candidates × 3 agents = {candidates.Length * 3} evaluations.
Only return the JSON, no other text.";
        }

        private string BuildEvaluationPrompt(string original, string translated, string targetLang, string context)
        {
            return $@"You are a professional game localization quality evaluator. Evaluate the following translation.

Original ({targetLang}): {original}

Translation: {translated}

Context: {(string.IsNullOrEmpty(context) ? "General gaming UI text" : context)}

Rate the translation on a 0-10 scale and provide:
1. Score (0-10, where 10 is perfect)
2. Brief explanation of the rating
3. Suggested improvement (if score < 8)

Return in this exact JSON format:
{{
  ""score"": 8.5,
  ""explanation"": ""Brief explanation of strengths and weaknesses"",
  ""improvement"": ""Better translation suggestion here""
}}

Only return the JSON, no other text.";
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
                var response = await GetActiveAiService().TranslateBatchAsync(prompt, 2);
                var candidates = ParseCandidateResponse(response);
                return candidates.Take(count).ToArray();
            }
            catch (Exception ex)
            {
                RaiseLog($"⚠ Candidate generation failed: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        private string BuildCandidatePrompt(string original, string targetLang, string context, int count)
        {
            return $@"You are a professional game localization translator. Translate the following English text to {targetLang} and generate {count} DIFFERENT translation candidates.

Original (English): {original}
Target language: {targetLang}

Context: {(string.IsNullOrEmpty(context) ? "General gaming UI text" : context)}

All candidates MUST be in {targetLang}, NOT in English. The candidates should differ in wording/style but ALL preserve the exact meaning and fit gaming UI tone.

Return in this exact JSON format:
{{
  ""candidates"": [
    ""first candidate translation in {targetLang}"",
    ""second candidate translation in {targetLang}""
  ]
}}

Only return the JSON, no other text.";
        }

        private string[] ParseCandidateResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return Array.Empty<string>();

            try
            {
                var clean = TrimCodeFence(response);
                var json = JObject.Parse(clean);
                var arr = json["candidates"] as JArray;
                if (arr == null || arr.Count == 0)
                    return Array.Empty<string>();

                return arr
                    .Select(t => t?.ToString()?.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToArray();
            }
            catch
            {
                // Fallback: extract quoted strings
                var regex = new System.Text.RegularExpressions.Regex("\"([^\"]{2,})\"");
                var matches = regex.Matches(response);
                return matches.Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToArray();
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
                        RaiseLog("⚠ Batch evaluation parse failed, falling back to per-entry");
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
                                RaiseLog($"⚠ Per-entry evaluation failed for {item.Key}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    RaiseLog($"⚠ Batch evaluation chunk failed: {ex.Message}");
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
                    var prompt = BuildBatchVotingPrompt(chunk, targetLanguage, context);
                    var response = await GetActiveAiService().TranslateBatchAsync(prompt, 2);
                    var parsed = ParseBatchVotingResponse(response, chunk);
                    allResults.AddRange(parsed);

                    if (parsed.Count == 0)
                    {
                        RaiseLog("⚠ Batch voting parse failed, falling back to per-entry");
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
                                RaiseLog($"⚠ Per-entry voting failed for {item.Key}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    RaiseLog($"⚠ Batch voting chunk failed: {ex.Message}");
                }
            }

            return allResults;
        }

        private string BuildBatchEvaluationPrompt(List<(string Key, string Original, string Translated)> items, string targetLang, string context)
        {
            var lines = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                lines.AppendLine($"### Entry {i + 1}");
                lines.AppendLine($"Original ({targetLang}): {items[i].Original}");
                lines.AppendLine($"Translation: {items[i].Translated}");
                lines.AppendLine();
            }

            return $@"You are a professional game localization quality evaluator. Evaluate ALL {items.Count} translations below.

Context: {(string.IsNullOrEmpty(context) ? "General gaming UI text" : context)}

{lines}

For EACH entry, rate 0-10 and provide brief explanation + improvement (if score < 8).

Return in this exact JSON format:
{{
  ""evaluations"": [
    {{ ""index"": 1, ""score"": 8.5, ""explanation"": ""brief reason"", ""improvement"": ""suggestion or empty"" }},
    {{ ""index"": 2, ""score"": 6.0, ""explanation"": ""brief reason"", ""improvement"": ""suggestion or empty"" }}
  ]
}}

Include ALL {items.Count} entries. Only return the JSON, no other text.";
        }

        private string BuildBatchVotingPrompt(List<(string Key, string Original, string[] Candidates)> items, string targetLang, string context)
        {
            var lines = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                lines.AppendLine($"### Entry {i + 1}");
                lines.AppendLine($"Original (English): {items[i].Original}");
                lines.AppendLine($"Target language: {targetLang}");
                for (int c = 0; c < items[i].Candidates.Length; c++)
                    lines.AppendLine($"  Candidate {c + 1}: \"{items[i].Candidates[c]}\"");
                lines.AppendLine();
            }

            return $@"You are a multi-agent translation review panel. For EACH entry below, evaluate its candidates from 3 perspectives (Fluency, Accuracy, Style), then pick the BEST candidate. All candidates are {targetLang} translations of the English original.

Context: {(string.IsNullOrEmpty(context) ? "General gaming UI text" : context)}

{lines}

Return in this exact JSON format:
{{
  ""votes"": [
    {{
      ""index"": 1,
      ""scores"": [ {{ ""candidate"": 1, ""agent"": ""Fluency"", ""score"": 9.0, ""explanation"": ""brief"" }}, {{ ""candidate"": 1, ""agent"": ""Accuracy"", ""score"": 8.0 }} ],
      ""best"": 1
    }}
  ]
}}

Include ALL {items.Count} entries. Only return the JSON, no other text.";
        }

        private List<EvaluationResult> ParseBatchEvaluationResponse(string response, List<(string Key, string Original, string Translated)> items)
        {
            var results = new List<EvaluationResult>();
            if (string.IsNullOrEmpty(response))
                return results;

            try
            {
                var clean = TrimCodeFence(response);
                var json = JObject.Parse(clean);
                var evaluations = json["evaluations"] as JArray;
                if (evaluations == null)
                    return results;

                foreach (var eval in evaluations)
                {
                    var index = eval["index"]?.ToObject<int>() ?? 0;
                    if (index < 1 || index > items.Count)
                        continue;

                    var item = items[index - 1];
                    results.Add(new EvaluationResult
                    {
                        TranslatedText = item.Key,
                        Score = eval["score"]?.ToObject<double>() ?? 5.0,
                        Explanation = eval["explanation"]?.ToString() ?? "",
                        Improvement = eval["improvement"]?.ToString() ?? ""
                    });
                }
            }
            catch
            {
                return results;
            }

            return results;
        }

        private List<VotingResult> ParseBatchVotingResponse(string response, List<(string Key, string Original, string[] Candidates)> items)
        {
            var results = new List<VotingResult>();
            if (string.IsNullOrEmpty(response))
                return results;

            try
            {
                var clean = TrimCodeFence(response);
                var json = JObject.Parse(clean);
                var votes = json["votes"] as JArray;
                if (votes == null)
                    return results;

                foreach (var vote in votes)
                {
                    var index = vote["index"]?.ToObject<int>() ?? 0;
                    if (index < 1 || index > items.Count)
                        continue;

                    var item = items[index - 1];
                    var agentResults = new List<EvaluationResult>();

                    var scores = vote["scores"] as JArray;
                    if (scores != null)
                    {
                        foreach (var s in scores)
                        {
                            var candIdx = s["candidate"]?.ToObject<int>() ?? 0;
                            if (candIdx < 1 || candIdx > item.Candidates.Length)
                                continue;

                            agentResults.Add(new EvaluationResult
                            {
                                TranslatedText = item.Candidates[candIdx - 1],
                                Score = s["score"]?.ToObject<double>() ?? 5.0,
                                Explanation = s["explanation"]?.ToString() ?? "",
                                ProviderName = s["agent"]?.ToString() ?? "Agent"
                            });
                        }
                    }

                    var bestIdx = vote["best"]?.ToObject<int>() ?? 0;
                    var best = bestIdx >= 1 && bestIdx <= item.Candidates.Length ? item.Candidates[bestIdx - 1] : item.Candidates.FirstOrDefault() ?? "";

                    var avg = agentResults.Count > 0 ? agentResults.Average(r => r.Score) : 5.0;

                    results.Add(new VotingResult
                    {
                        EntryKey = item.Key,
                        OriginalText = item.Original,
                        AgentResults = agentResults,
                        AverageScore = Math.Round(avg, 1),
                        BestTranslation = best,
                        ConsensusSummary = $"Best: \"{best}\" (avg {avg:F1}/10 from {agentResults.Count} votes)"
                    });
                }
            }
            catch
            {
                return results;
            }

            return results;
        }

        private string TrimCodeFence(string response)
        {
            var clean = response.Trim();
            if (clean.StartsWith("```json"))
                clean = clean.Substring(7);
            else if (clean.StartsWith("```"))
                clean = clean.Substring(3);
            if (clean.EndsWith("```"))
                clean = clean.Substring(0, clean.Length - 3);
            return clean.Trim();
        }

        private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }

        private EvaluationResult ParseEvaluationResponse(string response, string original, string translated)
        {
            var result = new EvaluationResult
            {
                TranslatedText = translated,
                Score = 5.0,
                Explanation = "Evaluation unavailable",
                Improvement = ""
            };

            if (string.IsNullOrEmpty(response))
                return result;

            try
            {
                var cleanResponse = response.Trim();
                if (cleanResponse.StartsWith("```json"))
                    cleanResponse = cleanResponse.Substring(7);
                if (cleanResponse.EndsWith("```"))
                    cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
                cleanResponse = cleanResponse.Trim();

                var json = JObject.Parse(cleanResponse);
                result.Score = json["score"]?.ToObject<double>() ?? 5.0;
                result.Explanation = json["explanation"]?.ToString() ?? "";
                result.Improvement = json["improvement"]?.ToString() ?? "";
            }
            catch
            {
                // Fallback: try to extract score with regex
                var match = System.Text.RegularExpressions.Regex.Match(response, @"(\d+(?:\.\d+)?)\s*/\s*10");
                if (match.Success)
                {
                    result.Score = double.Parse(match.Groups[1].Value);
                }
                result.Explanation = response.Length > 200
                    ? response.Substring(0, 200) + "..."
                    : response;
            }

            return result;
        }

        private List<EvaluationResult> ParseBatchedVotingResponse(string response, string original, string[] candidates)
        {
            var results = new List<EvaluationResult>();

            if (string.IsNullOrEmpty(response))
                return results;

            try
            {
                var cleanResponse = response.Trim();
                if (cleanResponse.StartsWith("```json"))
                    cleanResponse = cleanResponse.Substring(7);
                if (cleanResponse.EndsWith("```"))
                    cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
                cleanResponse = cleanResponse.Trim();

                var json = JObject.Parse(cleanResponse);
                var evaluations = json["evaluations"] as JArray;

                if (evaluations != null)
                {
                    foreach (var eval in evaluations)
                    {
                        var candidateIdx = eval["candidate"]?.ToObject<int>() ?? 0;
                        var agent = eval["agent"]?.ToString() ?? "";
                        var score = eval["score"]?.ToObject<double>() ?? 5.0;
                        var explanation = eval["explanation"]?.ToString() ?? "";

                        if (candidateIdx > 0 && candidateIdx <= candidates.Length)
                        {
                            results.Add(new EvaluationResult
                            {
                                TranslatedText = candidates[candidateIdx - 1],
                                Score = score,
                                Explanation = explanation,
                                ProviderName = agent,
                                Improvement = ""
                            });
                            RaiseLog($"🗳 {agent} scored {score:F1}/10 for: {candidates[candidateIdx - 1]}");
                        }
                    }
                }
            }
            catch
            {
                RaiseLog("⚠ Batch voting parse failed, trying regex fallback");
                // Fallback: try to extract individual scores
                var regex = new System.Text.RegularExpressions.Regex(
                    @"(\w+)\s*[:：]\s*(\d+(?:\.\d+)?)\s*/\s*10",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var matches = regex.Matches(response);
                for (int i = 0; i < matches.Count && i < candidates.Length; i++)
                {
                    results.Add(new EvaluationResult
                    {
                        TranslatedText = candidates[i],
                        Score = double.Parse(matches[i].Groups[2].Value),
                        ProviderName = matches[i].Groups[1].Value
                    });
                }
            }

            return results;
        }
    }
}
