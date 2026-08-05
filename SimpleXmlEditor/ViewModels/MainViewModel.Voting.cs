using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    public partial class MainViewModel
    {
        /// <summary>Run multi-agent voting on selected entries (or all translated entries).</summary>
        public async Task VoteEntriesAsync(IEnumerable<LocalizationEntry> selection)
        {
            var entries = selection?.ToList() ?? new List<LocalizationEntry>();
            if (entries.Count == 0)
                entries = Entries.Where(e => !string.IsNullOrEmpty(e.Translation)).ToList();

            if (entries.Count == 0)
            {
                OnLogMessage($"⚠ {LocalizationManager.GetString("NoTranslatedToVote")}");
                VotingCompleted?.Invoke(null);
                return;
            }

            // Batch voting for multiple entries (candidate generation + batched API calls)
            if (entries.Count > 1)
            {
                OnLogMessage($"🗳 {LocalizationManager.GetString("LogBatchVoting", entries.Count)}");
                VotingStatusText?.Invoke($"⏳ {LocalizationManager.GetString("VoteBatchProgress", entries.Count)}");

                var context = GetEvaluationContext();
                var targetLang = _aiTranslationService.TargetLanguage;

                // Build candidate sets: current translation + AI-generated alternatives
                var items = new List<(string Key, string Original, string[] Candidates)>();
                var totalForCandidates = entries.Count(e => !string.IsNullOrEmpty(e.Translation));
                var candidateIdx = 0;
                foreach (var e in entries)
                {
                    if (string.IsNullOrEmpty(e.Translation)) continue;
                    candidateIdx++;
                    OnLogMessage($"📝 {LocalizationManager.GetString("LogGeneratingCandidate", candidateIdx, totalForCandidates, e.Key)}");
                    VotingStatusText?.Invoke($"📝 {LocalizationManager.GetString("VoteCandidateProgress", candidateIdx, totalForCandidates)}");

                    var candidates = new List<string> { e.Translation };
                    try
                    {
                        var generated = await _evaluator.GenerateCandidatesAsync(e.Value, targetLang, context, 2);
                        foreach (var g in generated)
                        {
                            if (!string.IsNullOrEmpty(g) && !candidates.Contains(g))
                                candidates.Add(g);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnLogMessage($"⚠ {LocalizationManager.GetString("TranslationError", ex.Message)}");
                    }
                    items.Add((e.Key, e.Value, candidates.ToArray()));
                }

                OnLogMessage($"🗳 {LocalizationManager.GetString("LogVotingStart", items.Count)}");
                VotingStatusText?.Invoke($"🗳 {LocalizationManager.GetString("VoteVotingProgress", items.Count)}");

                List<VotingResult> results;
                try
                {
                    results = await _evaluator.VoteBatchAsync(items, targetLang, context);
                }
                catch (Exception ex)
                {
                    OnLogMessage($"❌ {LocalizationManager.GetString("TranslationError", ex.Message)}");
                    VotingCompleted?.Invoke(new VotingOutcome { Failed = true });
                    return;
                }

                var completed = 0;
                var bestCount = 0;
                var needsReview = new List<VotingResult>();
                foreach (var vr in results)
                {
                    completed++;
                    var match = Entries.FirstOrDefault(en => en.Key == vr.EntryKey);
                    if (vr.BestTranslation == (match?.Translation ?? ""))
                    {
                        bestCount++;
                        continue;
                    }
                    if (match != null && !string.IsNullOrEmpty(vr.BestTranslation))
                        needsReview.Add(vr);
                }

                if (needsReview.Count > 0)
                    OnLogMessage($"🤝 {LocalizationManager.GetString("VoteNeedsReview", needsReview.Count)}");

                // 不自动覆盖译文：需人工确认的条目由 UI 弹出候选对比窗口，用户选定后调用 ApplyVotingSelections
                VotingCompleted?.Invoke(new VotingOutcome
                {
                    Completed = completed,
                    BestCount = bestCount,
                    NeedsReview = needsReview,
                    Results = results
                });
                return;
            }

            // Single entry voting
            var entry = entries.First();
            OnLogMessage($"🗳 {LocalizationManager.GetString("LogVoting", entry.Key)}");
            VotingStatusText?.Invoke($"⏳ {LocalizationManager.GetString("EvalVoting")}");

            var result = await VoteEntry(entry);

            if (result == null)
            {
                VotingCompleted?.Invoke(new VotingOutcome { Failed = true });
                return;
            }

            VotingCompleted?.Invoke(new VotingOutcome { SingleResult = result, HasSingleResult = true });
        }

        /// <summary>
        /// Run multi-agent voting on a single entry to find the best translation.
        /// Generates AI candidate alternatives first, then votes (candidate generation + context).
        /// </summary>
        public async Task<VotingResult> VoteEntry(LocalizationEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Value))
                return null;

            IsEvaluating = true;
            try
            {
                var targetLang = _aiTranslationService.TargetLanguage;
                var context = GetEvaluationContext();

                // Build candidate set: current translation + AI-generated alternatives
                var candidates = new List<string>();
                if (!string.IsNullOrEmpty(entry.Translation))
                    candidates.Add(entry.Translation);

                var generated = await _evaluator.GenerateCandidatesAsync(entry.Value, targetLang, context, 2);
                foreach (var g in generated)
                {
                    if (!string.IsNullOrEmpty(g) && !candidates.Contains(g))
                        candidates.Add(g);
                }
                if (candidates.Count == 0)
                    candidates.Add(entry.Value);

                var result = await _evaluator.VoteAsync(entry.Value, candidates.ToArray(), targetLang, context);
                LastEvaluationResult = result.ConsensusSummary;
                OnLogMessage($"🗳 Vote: {entry.Key} → {result.ConsensusSummary}");

                // 不在此处自动应用：若 AI 建议的译文与当前不同，由 UI 弹出候选对比窗口供用户选择
                return result;
            }
            finally
            {
                IsEvaluating = false;
            }
        }

        /// <summary>
        /// 应用用户在投票候选确认窗口中选定的译文（key → 选中的译文文本）。
        /// 值为空或与当前译文相同时跳过。
        /// </summary>
        public int ApplyVotingSelections(Dictionary<string, string> selections)
        {
            if (selections == null || selections.Count == 0)
                return 0;

            var applied = 0;
            foreach (var pair in selections)
            {
                var match = Entries.FirstOrDefault(en => en.Key == pair.Key);
                if (match == null || string.IsNullOrEmpty(pair.Value))
                    continue;
                if (match.Translation == pair.Value)
                    continue;

                PushUndoSnapshot(new[] { match });
                match.Translation = pair.Value;
                applied++;
            }

            if (applied > 0)
                OnLogMessage($"✅ {LocalizationManager.GetString("VoteAppliedBest", applied)}");
            return applied;
        }
    }
}
