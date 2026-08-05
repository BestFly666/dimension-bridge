using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor.Services
{
    public partial class TranslationEvaluator
    {
        private string[] ParseCandidateResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return Array.Empty<string>();

            try
            {
                var clean = AiResponseParser.StripCodeFence(response);
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
                var regex = new Regex("\"([^\"]{2,})\"");
                var matches = regex.Matches(response);
                return matches.Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToArray();
            }
        }

        private List<EvaluationResult> ParseBatchEvaluationResponse(string response, List<(string Key, string Original, string Translated)> items)
        {
            var results = new List<EvaluationResult>();
            if (string.IsNullOrEmpty(response))
                return results;

            try
            {
                var clean = AiResponseParser.StripCodeFence(response);
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
                var clean = AiResponseParser.StripCodeFence(response);
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
                var cleanResponse = AiResponseParser.StripCodeFence(response);

                var json = JObject.Parse(cleanResponse);
                result.Score = json["score"]?.ToObject<double>() ?? 5.0;
                result.Explanation = json["explanation"]?.ToString() ?? "";
                result.Improvement = json["improvement"]?.ToString() ?? "";
            }
            catch
            {
                // Fallback: try to extract score with regex
                var match = Regex.Match(response, @"(\d+(?:\.\d+)?)\s*/\s*10");
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
                var cleanResponse = AiResponseParser.StripCodeFence(response);

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
                            RaiseLog(LocalizationManager.GetString("LogAgentScored", agent, score, candidates[candidateIdx - 1]));
                        }
                    }
                }
            }
            catch
            {
                RaiseLog(LocalizationManager.GetString("LogVotingRegexFallback"));
                // Fallback: try to extract individual scores
                var regex = new Regex(
                    @"(\w+)\s*[:：]\s*(\d+(?:\.\d+)?)\s*/\s*10",
                    RegexOptions.IgnoreCase);
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
