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

        public event Action<string> LogMessage;

        public TranslationEvaluator(IAiTranslationService aiService)
        {
            _aiService = aiService;
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
            var response = await _aiService.TranslateBatchAsync(prompt, 2);

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
                var response = await _aiService.TranslateBatchAsync(prompt, 2);

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
