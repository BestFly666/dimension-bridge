using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleXmlEditor.Dictionary
{
    /// <summary>
    /// GlossaryManager: conflict detection responsibilities.
    /// </summary>
    public partial class GlossaryManager
    {
        // ─── Conflict Detection ──────────────────────────────────────

        /// <summary>
        /// Scan translation entries for terminology conflicts.
        /// Each entry is (key, sourceText, translatedText).
        /// A conflict is when a glossary term's English appears in the source text,
        /// but its Chinese translation does NOT appear in the translated text.
        /// </summary>
        public List<GlossaryConflict> DetectConflicts(
            IEnumerable<(string key, string source, string translation)> entries,
            Action<int, int> onProgress = null)
        {
            var conflicts = new List<GlossaryConflict>();
            var entryList = entries.ToList();
            int total = entryList.Count;
            int processed = 0;
            int step = Math.Max(1, total / 20); // 自适应步长：全程约上报 20 次，避免日志刷屏

            foreach (var (key, source, translation) in entryList)
            {
                processed++;

                // 按步长周期性上报进度
                if (onProgress != null && (processed % step == 0 || processed == total))
                    onProgress(processed, total);

                if (string.IsNullOrEmpty(translation))
                    continue;

                var matchedTerms = FindMatchingTerms(source);
                foreach (var termKey in matchedTerms)
                {
                    if (Terms.TryGetValue(termKey, out var term))
                    {
                        if (!translation.Contains(term.Chinese))
                        {
                            conflicts.Add(new GlossaryConflict
                            {
                                EntryKey = key,
                                SourceText = source,
                                Translation = translation,
                                TermEnglish = term.English,
                                TermChinese = term.Chinese,
                                Category = term.Category
                            });
                        }
                    }
                }
            }

            return conflicts;
        }
    }

    /// <summary>
    /// Represents a detected terminology conflict between source text
    /// and the glossary's expected translation.
    /// </summary>
    public class GlossaryConflict
    {
        public string EntryKey { get; set; } = "";
        public string SourceText { get; set; } = "";
        public string Translation { get; set; } = "";
        public string TermEnglish { get; set; } = "";
        public string TermChinese { get; set; } = "";
        public string Category { get; set; } = "";
    }
}
