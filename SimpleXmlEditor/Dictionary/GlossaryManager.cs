using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Dictionary
{
    /// <summary>
    /// Term-level glossary manager. Matches individual terms within source text
    /// (as opposed to simple whole-string key matching).
    /// Supports word-boundary matching, longest-match-first, and category tracking.
    ///
    /// Design intent:
    ///   - FindMatchingTerms/ContainsWholeWord: used by BuildGlossaryContext to inject
    ///     matched terms into the AI prompt as translation guidance.
    ///   - TryGetValue (exact match): only for entries whose ENTIRE text matches a glossary
    ///     term (e.g., "UPGRADE_TECH" → "科技升级", or single-word entries).
    ///   - GetMatchingTerms: term-level lookup for AI context injection.
    ///
    /// 线程安全：Terms / _regexCache 用 ConcurrentDictionary；
    /// _sortedTerms / _invertedIndex 通过"整表重建 + 引用替换"保证读者永远看到完整状态
    /// （后台翻译线程读，UI 线程写）。
    /// </summary>
    public partial class GlossaryManager : IGlossaryManager
    {
        private static readonly string DictFile = Path.Combine(Environment.CurrentDirectory, "translation_dictionary.json");
        private static readonly string GlossaryFile = Path.Combine(Environment.CurrentDirectory, "glossary_terms.json");

        /// <summary>Key: English term (case-insensitive), Value: GlossaryTerm entry</summary>
        public ConcurrentDictionary<string, GlossaryTerm> Terms { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Sorted by key length descending for longest-match-first</summary>
        private List<KeyValuePair<string, GlossaryTerm>> _sortedTerms = new();

        /// <summary>
        /// Inverted index: lowercase word → set of glossary terms whose English contains that word.
        /// Enables O(entry_word_count × avg_candidates) matching instead of O(glossary_size × batch_size).
        /// </summary>
        private Dictionary<string, HashSet<string>> _invertedIndex = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Maximum glossary terms injected into a single batch prompt.</summary>
        private const int MAX_GLOSSARY_CONTEXT_TERMS = 200;

        /// <summary>Regex cache shared across all methods (ConcurrentDictionary：并发读写安全)</summary>
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();

        /// <summary>类名/型号修饰词（class/mk/type 等），匹配宽容与宽松判定共用。</summary>
        private static readonly string ModifierTokenPattern = @"(?:class|mark|mk|type|series|version|model|variant|generation|prototype|standard)";

        public int Count => Terms.Count;

        public GlossaryManager()
        {
            Load();
        }
    }

    /// <summary>
    /// A single glossary term entry with full metadata.
    /// </summary>
    public class GlossaryTerm
    {
        public string English { get; set; }
        public string Chinese { get; set; }
        public string Category { get; set; }
        public string Status { get; set; } = "confirmed";   // pending | confirmed | rejected
        public string Tags { get; set; } = "";               // comma-separated tags
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public GlossaryTerm() { }

        public GlossaryTerm(string english, string chinese, string category = "", string status = "confirmed", string tags = "")
        {
            English = english;
            Chinese = chinese;
            Category = category;
            Status = status;
            Tags = tags;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public string[] TagList => string.IsNullOrEmpty(Tags)
            ? Array.Empty<string>()
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray();
    }
}
