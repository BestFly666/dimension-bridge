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
        /// <summary>Evaluate selected entries (or all translated entries) with AI quality scoring.</summary>
        public async Task EvaluateEntriesAsync(IEnumerable<LocalizationEntry> selection)
        {
            var entries = selection?.ToList() ?? new List<LocalizationEntry>();
            if (entries.Count == 0)
                entries = Entries.Where(e => !string.IsNullOrEmpty(e.Translation)).ToList();

            if (entries.Count == 0)
            {
                OnLogMessage($"⚠ {LocalizationManager.GetString("NoTranslatedToEvaluate")}");
                EvaluationCompleted?.Invoke(null);
                return;
            }

            // Single entry evaluation
            if (entries.Count == 1)
            {
                var entry = entries.First();
                OnLogMessage($"🤖 {LocalizationManager.GetString("LogEvaluating", entry.Key)}");
                EvaluationStatusText?.Invoke($"⏳ {LocalizationManager.GetString("EvalEvaluating")}");

                var result = await EvaluateEntry(entry);

                if (result == null)
                {
                    EvaluationCompleted?.Invoke(new EvaluationOutcome { Failed = true });
                    return;
                }

                var outcome = new EvaluationOutcome { SingleResult = result, EntryKey = entry.Key };
                outcome.ResultMap[entry.Key] = result;
                EvaluationCompleted?.Invoke(outcome);
                return;
            }

            // Batch evaluation for multiple entries (batched API calls for speed)
            OnLogMessage($"🤖 {LocalizationManager.GetString("LogBatchEvaluating", entries.Count)}");
            EvaluationStatusText?.Invoke($"⏳ {LocalizationManager.GetString("EvalBatchProgress", entries.Count)}");

            var context = GetEvaluationContext();
            var items = entries
                .Where(e => !string.IsNullOrEmpty(e.Translation))
                .Select(e => (e.Key, e.Value, e.Translation))
                .ToList();

            List<EvaluationResult> results;
            try
            {
                results = await _evaluator.EvaluateBatchAsync(items, _aiTranslationService.TargetLanguage, context);
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ {LocalizationManager.GetString("TranslationError", ex.Message)}");
                EvaluationCompleted?.Invoke(new EvaluationOutcome { Failed = true });
                return;
            }

            if (results.Count == 0)
            {
                EvaluationCompleted?.Invoke(new EvaluationOutcome { Failed = true });
                return;
            }

            // 安全构建 ResultMap：遇到重复键时后者覆盖前者，避免 ToDictionary 抛异常崩溃
            // 重复键来源：AI 返回 JSON 中 index 重复/错乱、fallback 路径默认值、XML 重复 Key
            var resultMap = new Dictionary<string, EvaluationResult>();
            foreach (var r in results)
            {
                var key = r.TranslatedText ?? "";
                resultMap[key] = r;
            }

            EvaluationCompleted?.Invoke(new EvaluationOutcome
            {
                Results = results,
                ResultMap = resultMap,
                AverageScore = results.Where(r => r.Score > 0).Select(r => r.Score).DefaultIfEmpty(0).Average(),
                HighCount = results.Count(r => r.Score >= 8),
                LowCount = results.Count(r => r.Score > 0 && r.Score < 5)
            });
        }

        /// <summary>
        /// Evaluate a single translation entry with AI quality scoring.
        /// </summary>
        public async Task<EvaluationResult> EvaluateEntry(LocalizationEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Value) || string.IsNullOrEmpty(entry.Translation))
                return null;

            IsEvaluating = true;
            try
            {
                var targetLang = _aiTranslationService.TargetLanguage;
                var context = GetEvaluationContext();
                var result = await _evaluator.EvaluateAsync(entry.Value, entry.Translation, targetLang, context);
                LastEvaluationResult = $"{entry.Key}: Score {result.Score:F1}/10 — {result.Explanation}";
                OnLogMessage($"📊 Evaluation: {entry.Key} → {result.Score:F1}/10");
                return result;
            }
            finally
            {
                IsEvaluating = false;
            }
        }

        /// <summary>
        /// Builds evaluation/voting context from the active expert profile.
        /// </summary>
        private string GetEvaluationContext()
        {
            try
            {
                var profile = _profileManager.ActiveProfile;
                if (profile != null)
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(profile.Description))
                        parts.Add(profile.Description);
                    if (!string.IsNullOrEmpty(profile.Context))
                        parts.Add(profile.Context);
                    if (parts.Count > 0)
                        return string.Join("\n", parts);
                }
            }
            catch { /* profile manager may be uninitialized; fall through to empty context */ }

            return "";
        }
    }
}
