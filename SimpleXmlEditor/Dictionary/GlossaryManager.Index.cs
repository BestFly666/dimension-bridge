using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Dictionary
{
    /// <summary>注入 AI 提示词的术语条目（原文 + 译文 + 分类，分类帮助模型区分专有名词词义）。</summary>
    public sealed record GlossaryContextTerm(string Key, string Chinese, string Category);

    /// <summary>
    /// GlossaryManager: index & search responsibilities — inverted-index construction,
    /// whole-word matching, glossary-context building, and search/filter helpers.
    /// </summary>
    public partial class GlossaryManager
    {
        // ─── Index construction ─────────────────────────────────────

        private void RebuildSortedList()
        {
            _sortedTerms = Terms
                .Where(t => !string.IsNullOrEmpty(t.Key) && t.Key.Length >= 2)
                .OrderByDescending(t => t.Key.Length)
                .ToList();

            // Build inverted index: word → set of glossary term keys
            _invertedIndex = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var term in Terms.Where(t => !string.IsNullOrEmpty(t.Key) && t.Key.Length >= 2))
            {
                var words = term.Key.Split(new[] { ' ', '-', '_', '/', '.' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    if (word.Length < 2) continue;
                    if (!_invertedIndex.TryGetValue(word, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _invertedIndex[word] = set;
                    }
                    set.Add(term.Key);
                }
            }
        }

        // ─── Regex cache ────────────────────────────────────────────

        private static Regex GetOrCreateRegex(string term)
        {
            // ConcurrentDictionary：并发读（后台翻译线程）与写（UI 增删术语）均安全，无需额外锁
            return _regexCache.GetOrAdd(term, static t => BuildTermRegex(t));
        }

        private static Regex BuildTermRegex(string term)
        {
            // 宽容边界：下划线/撇号/连字符等视为边界（覆盖 dark_jedi、Jedi's），
            // 可选复数/所有格后缀（覆盖 Jedis、Stormtroopers、boxes）。
            // 词内拼接（JediMaster、JedisX）仍不匹配，避免误伤。
            //
            // 空格/标点宽容（解决"术语值差了个空格或标点就匹配不到"）：
            //   1) 术语中的 空格/连字符/下划线/斜杠/句点 → [\s\-/_.]+（一个或多个分隔符可互换）
            //      覆盖 "Star Destroyer" ↔ "Star-Destroyer" ↔ "Star_Destroyer" 等写法差异
            //   2) 分隔符处允许插入一个可选修饰词（class/mk/mark/type 等），
            //      覆盖 "Procursator Star Destroyer" ↔ "Procursator-class Star Destroyer"
            //   3) 术语本身的修饰词 token 也可选（当术语还有其他核心词时），
            //      覆盖反向 "Executor-class Star Dreadnought" ↔ "Executor Star Dreadnought"
            //   4) 术语中的撇号 → ['']?（可有可无），覆盖 "Hutt's" ↔ "Hutts"
            var parts = new StringBuilder();
            // 按分隔符分词（保留分隔符），如 "Executor-class Star" → [Executor, -, class, ' ', Star]
            var tokens = Regex.Split(term, @"([\s\-/_.]+)").Where(t => t.Length > 0).ToArray();
            // 术语中是否含"核心词"（非修饰词）——只有含核心词时修饰词才可省略
            bool hasCoreWord = tokens.Any(t =>
                !Regex.IsMatch(t, @"^[\s\-/_.]+$") &&
                !Regex.IsMatch(t, $"^{ModifierTokenPattern}$", RegexOptions.IgnoreCase));

            for (int i = 0; i < tokens.Length; i++)
            {
                var tok = tokens[i];
                if (Regex.IsMatch(tok, @"^[\s\-/_.]+$"))
                {
                    // 分隔符：前一 token 是可选修饰词时本分隔符也可选（修饰词被省略时仍能衔接），
                    // 否则必选；后一 token 非可选修饰词时允许插入一个修饰词
                    bool prevIsOptionalModifier = i > 0 && IsModifierToken(tokens[i - 1], ModifierTokenPattern) && hasCoreWord;
                    bool nextIsOptionalModifier = i + 1 < tokens.Length && IsModifierToken(tokens[i + 1], ModifierTokenPattern) && hasCoreWord;
                    parts.Append(prevIsOptionalModifier ? @"[\s\-/_.]*" : @"[\s\-/_.]+");
                    if (!nextIsOptionalModifier)
                        parts.Append($@"({ModifierTokenPattern}[\s\-/_.]+)?");
                }
                else
                {
                    var escaped = EscapeTermToken(tok);
                    bool isModifier = IsModifierToken(tok, ModifierTokenPattern) && hasCoreWord;
                    // 修饰词可选但不吞尾随分隔符（分隔符由后续 token 提供）
                    parts.Append(isModifier
                        ? $@"(?:{escaped}(?:es|s|'s)?)?"
                        : escaped);
                }
            }
            var pattern = $@"(?<![A-Za-z0-9]){parts}(?:es|s|'s)?(?![A-Za-z0-9])";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        private static bool IsModifierToken(string tok, string modifierTokenPattern) =>
            Regex.IsMatch(tok, $"^{modifierTokenPattern}$", RegexOptions.IgnoreCase);

        /// <summary>术语 token 转义：逐字符转义，每个普通字符后允许插入一个可选撇号（Hutts ↔ Hutt's 双向）；撇号本身可选。</summary>
        private static string EscapeTermToken(string tok)
        {
            var sb = new StringBuilder();
            foreach (char ch in tok)
            {
                if (ch == '\'' || ch == '\u2019')
                {
                    sb.Append(@"['\u2019]?");
                }
                else
                {
                    sb.Append(Regex.Escape(ch.ToString()));
                    sb.Append(@"['\u2019]?");
                }
            }
            return sb.ToString();
        }

        // ─── Exact match lookup ─────────────────────────────────────

        /// <summary>
        /// Exact full-string lookup. Does NOT increment any counter (caller tracks hits).
        /// </summary>
        public bool TryGetValue(string sourceText, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(sourceText)) return false;

            var normalized = sourceText.Trim();
            if (Terms.TryGetValue(normalized, out var term))
            {
                translated = term.Chinese;
                return true;
            }
            return false;
        }

        // ─── Term-level matching (for AI prompt context) ────────────

        /// <summary>
        /// Find all glossary terms that appear as whole words within text.
        /// </summary>
        public HashSet<string> FindMatchingTerms(string text)
        {
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text) || _sortedTerms.Count == 0)
                return matched;

            foreach (var term in _sortedTerms)
            {
                if (term.Key.Length > text.Length) continue;
                if (ContainsWholeWord(text, term.Key))
                    matched.Add(term.Key);
            }
            return matched;
        }

        /// <summary>
        /// Get matching terms with their translations.
        /// </summary>
        public Dictionary<string, string> GetMatchingTerms(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var keys = FindMatchingTerms(text);
            foreach (var key in keys)
            {
                if (Terms.TryGetValue(key, out var term))
                    result[key] = term.Chinese;
            }
            return result;
        }

        // ─── Whole-word check ───────────────────────────────────────

        /// <summary>
        /// Fast glossary context builder using inverted index.
        /// For a batch of entries, finds up to MaxGlossaryContextTerms matching terms.
        /// Returns ordered list of (term_key → chinese_translation, category) for prompt injection.
        /// 
        /// Performance: O(batch_word_count × avg_candidates_per_word) instead of
        /// O(glossary_size × batch_size). With 100k glossary and 50 entries per batch,
        /// this is ~1000x faster than iterating all glossary terms.
        /// </summary>
        public List<GlossaryContextTerm> GetGlossaryContextTerms(List<LocalizationEntry> entries)
        {
            var result = new List<GlossaryContextTerm>();
            if (entries.Count == 0 || _invertedIndex.Count == 0)
                return result;

            // Collect all unique candidate term keys from inverted index
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Value) || entry.Value.Length < 2) continue;

                // Tokenize entry text into words
                var words = entry.Value.Split(new[] { ' ', '-', '_', '/', '.', ',', ':', ';', '!', '?',
                    '"', '\'', '(', ')', '[', ']', '{', '}', '<', '>', '\t', '\n', '\r' },
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    if (word.Length < 2) continue;
                    AddIndexCandidates(word, candidates);

                    // 复数宽容：去尾 s/es/'s 后再查倒排（覆盖 "Stormtroopers"→"Stormtrooper"、"boxes"→"box"）
                    string stem = word;
                    if (word.EndsWith("'s", StringComparison.OrdinalIgnoreCase))
                        stem = word[..^2];
                    else if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase))
                        stem = word[..^2];
                    else if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                        stem = word[..^1];
                    if (stem.Length >= 2 && stem != word)
                        AddIndexCandidates(stem, candidates);
                }
            }

            if (candidates.Count == 0) return result;

            // 先全局验证：candidates → 实际匹配的术语（termKey → 命中条目数），
            // 同时记录每条 entry 各自命中了哪些术语。
            // 注意：不能先按长度排序再验证截断——批量候选几百个时，长术语会先
            // 验证通过占满 MaxGlossaryContextTerms，短/冷门术语（如 A-Wing）
            // 被挤出导致术语注入失效（单条生效、批量失效的根因）。
            var matchedTerms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var entryMatched = new List<HashSet<string>>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                entryMatched.Add(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            foreach (var termKey in candidates)
            {
                if (!Terms.ContainsKey(termKey)) continue; // filter to existing only (safety)
                int hits = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (IsTermRelated(entries[i].Value, termKey))
                    {
                        hits++;
                        entryMatched[i].Add(termKey);
                    }
                }
                if (hits > 0)
                    matchedTerms[termKey] = hits;
            }

            if (matchedTerms.Count == 0) return result;

            // 第一优先：预算均分给每条 entry（每 entry 最多 quota 个匹配术语，长度降序），
            // 保证每条文本的术语都有机会注入。避免按全局长度排序时，长句条目命中大量术语
            // 导致短术语（如 StarViper）被长术语/靠前条目挤出——这是批量翻译术语注入
            // "时好时坏"的根因（同一条目单独翻译正常、批量翻译失效）。
            var chosen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int entryQuota = Math.Max(1, MaxGlossaryContextTerms / Math.Max(1, entries.Count));
            foreach (var set in entryMatched)
            {
                if (chosen.Count >= MaxGlossaryContextTerms) break;
                foreach (var key in set.OrderByDescending(k => k.Length).Take(entryQuota))
                {
                    chosen.Add(key);
                    if (chosen.Count >= MaxGlossaryContextTerms) break;
                }
            }

            // 第二优先：剩余名额按命中条目数降序补充（多条目共有的核心术语），
            // 同命中数按长度降序（更具体术语优先）。
            if (chosen.Count < MaxGlossaryContextTerms)
            {
                foreach (var kvp in matchedTerms
                    .Where(k => !chosen.Contains(k.Key))
                    .OrderByDescending(k => k.Value)
                    .ThenByDescending(k => k.Key.Length))
                {
                    chosen.Add(kvp.Key);
                    if (chosen.Count >= MaxGlossaryContextTerms) break;
                }
            }

            foreach (var key in chosen)
            {
                if (Terms.TryGetValue(key, out var term))
                    result.Add(new GlossaryContextTerm(term.English, term.Chinese, term.Category));
            }

            return result;
        }

        /// <summary>从倒排索引收集包含指定词的所有术语 key 到候选集。</summary>
        private void AddIndexCandidates(string word, HashSet<string> candidates)
        {
            if (_invertedIndex.TryGetValue(word, out var termKeys))
            {
                foreach (var key in termKeys)
                    candidates.Add(key);
            }
        }
    }
}
