using System.Collections.Generic;
using System.Linq;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    public partial class MainViewModel
    {
        private void ExecuteConsistencyScan()
        {
            OnLogMessage($"🔍 {LocalizationManager.GetString("ConsistencyScanning")}");
            var issues = ScanConsistencyIssues();
            ConsistencyScanCompleted?.Invoke(issues);
        }

        /// <summary>
        /// Smart Pre-translate: fill translations from glossary and cache without API calls.
        /// </summary>
        public void SmartPreTranslate(List<LocalizationEntry> selected)
        {
            var entries = selected?.ToList() ?? new List<LocalizationEntry>();
            if (entries.Count == 0)
                entries = Entries.Where(en => !string.IsNullOrEmpty(en.Value)).ToList();

            if (entries.Count == 0)
            {
                PreTranslateCompleted?.Invoke(null);
                return;
            }

            var glossaryFilled = 0;
            var cacheFilled = 0;

            // Record undo snapshot before mutating translations
            var toFill = entries.Where(en => string.IsNullOrEmpty(en.Translation)).ToList();
            PushUndoSnapshot(toFill);

            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Translation))
                    continue;

                // Try glossary first
                if (_glossary.TryGetValue(entry.Key, out var dictVal))
                {
                    entry.Translation = dictVal;
                    glossaryFilled++;
                    continue;
                }
                if (_glossary.TryGetValue(entry.Value, out dictVal))
                {
                    entry.Translation = dictVal;
                    glossaryFilled++;
                    continue;
                }

                // Try cache
                var cacheKey = _configService.GetCacheKey(entry.Value);
                if (cacheKey != null && _configService.Cache.TryGetValue(cacheKey, out var cached))
                {
                    entry.Translation = cached;
                    cacheFilled++;
                }
            }

            PreTranslateCompleted?.Invoke(new PreTranslateOutcome { GlossaryFilled = glossaryFilled, CacheFilled = cacheFilled });
        }

        /// <summary>Consistency scan: check same source text translated differently.</summary>
        public List<ConsistencyIssue> ScanConsistencyIssues()
        {
            var issues = new List<ConsistencyIssue>();
            var groups = Entries
                .Where(en => !string.IsNullOrEmpty(en.Value) && !string.IsNullOrEmpty(en.Translation))
                .GroupBy(en => en.Value)
                .Where(g => g.Select(en => en.Translation).Distinct().Count() > 1);

            foreach (var group in groups)
            {
                issues.Add(new ConsistencyIssue
                {
                    Source = group.Key,
                    Translations = group.Select(en => en.Translation).Distinct().ToList(),
                    Keys = group.Select(en => en.Key).Distinct().ToList()
                });
            }

            return issues;
        }
    }
}
