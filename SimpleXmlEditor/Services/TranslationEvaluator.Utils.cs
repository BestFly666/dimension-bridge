using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleXmlEditor.Services
{
    public partial class TranslationEvaluator
    {
        /// <summary>按候选译文分组聚合投票评分，均分最高者为 best。</summary>
        private static VotingResult BuildVotingResult(string originalText, List<EvaluationResult> results)
        {
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

        /// <summary>合并同一条目的多模型投票结果（各模型评分一起参与分组均分）。</summary>
        private static VotingResult MergeVotingResults(IEnumerable<VotingResult> results)
        {
            var list = results.ToList();
            if (list.Count == 0)
                return null;

            var allAgents = list.SelectMany(r => r.AgentResults).ToList();
            if (allAgents.Count == 0)
                return list.First();

            var first = list.First();
            var merged = BuildVotingResult(first.OriginalText, allAgents);
            merged.EntryKey = first.EntryKey;
            return merged;
        }

        private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }
}
