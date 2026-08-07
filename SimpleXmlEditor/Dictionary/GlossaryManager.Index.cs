using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Dictionary
{
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
            lock (_regexCache)
            {
                if (!_regexCache.TryGetValue(term, out var regex))
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
                    const string modifierToken = @"(?:class|mark|mk|type|series|version|model|variant|generation|prototype|standard)";
                    var parts = new StringBuilder();
                    // 按分隔符分词（保留分隔符），如 "Executor-class Star" → [Executor, -, class, ' ', Star]
                    var tokens = Regex.Split(term, @"([\s\-/_.]+)").Where(t => t.Length > 0).ToArray();
                    // 术语中是否含"核心词"（非修饰词）——只有含核心词时修饰词才可省略
                    bool hasCoreWord = tokens.Any(t =>
                        !Regex.IsMatch(t, @"^[\s\-/_.]+$") &&
                        !Regex.IsMatch(t, $"^{modifierToken}$", RegexOptions.IgnoreCase));

                    for (int i = 0; i < tokens.Length; i++)
                    {
                        var tok = tokens[i];
                        if (Regex.IsMatch(tok, @"^[\s\-/_.]+$"))
                        {
                            // 分隔符：前一 token 是可选修饰词时本分隔符也可选（修饰词被省略时仍能衔接），
                            // 否则必选；后一 token 非可选修饰词时允许插入一个修饰词
                            bool prevIsOptionalModifier = i > 0 && IsModifierToken(tokens[i - 1], modifierToken) && hasCoreWord;
                            bool nextIsOptionalModifier = i + 1 < tokens.Length && IsModifierToken(tokens[i + 1], modifierToken) && hasCoreWord;
                            parts.Append(prevIsOptionalModifier ? @"[\s\-/_.]*" : @"[\s\-/_.]+");
                            if (!nextIsOptionalModifier)
                                parts.Append($@"({modifierToken}[\s\-/_.]+)?");
                        }
                        else
                        {
                            var escaped = EscapeTermToken(tok);
                            bool isModifier = IsModifierToken(tok, modifierToken) && hasCoreWord;
                            // 修饰词可选但不吞尾随分隔符（分隔符由后续 token 提供）
                            parts.Append(isModifier
                                ? $@"(?:{escaped}(?:es|s|'s)?)?"
                                : escaped);
                        }
                    }
                    var pattern = $@"(?<![A-Za-z0-9]){parts}(?:es|s|'s)?(?![A-Za-z0-9])";
                    regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    _regexCache[term] = regex;
                }
                return regex;
            }
        }

        private static bool IsModifierToken(string tok, string modifierToken) =>
            Regex.IsMatch(tok, $"^{modifierToken}$", RegexOptions.IgnoreCase);

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

        /// <summary>
        /// In-place term substitution. Longest-match-first.
        /// Returns (modified text, count of unique terms substituted).
        /// All matching is done against the ORIGINAL text to avoid cascading
        /// false matches within already-replaced Chinese segments.
        /// </summary>
        public (string text, int count) SubstituteTerms(string text)
        {
            if (string.IsNullOrEmpty(text) || _sortedTerms.Count == 0)
                return (text, 0);

            // Phase 1: find all matching positions in the ORIGINAL text
            var replacements = new List<(int start, int length, string chinese)>();
            foreach (var term in _sortedTerms)
            {
                if (term.Key.Length > text.Length) continue;

                var regex = GetOrCreateRegex(term.Key);
                foreach (Match m in regex.Matches(text))
                {
                    // Only add if this position hasn't been claimed by a longer term
                    bool overlaps = false;
                    foreach (var existing in replacements)
                    {
                        if (m.Index < existing.start + existing.length &&
                            m.Index + m.Length > existing.start)
                        {
                            overlaps = true;
                            break;
                        }
                    }
                    if (!overlaps)
                        replacements.Add((m.Index, m.Length, term.Value.Chinese));
                }
            }

            if (replacements.Count == 0)
                return (text, 0);

            // Phase 2: apply replacements from right to left (preserve indices)
            replacements.Sort((a, b) => b.start.CompareTo(a.start));
            var result = new StringBuilder(text);
            foreach (var (start, length, chinese) in replacements)
            {
                result.Remove(start, length);
                result.Insert(start, chinese);
            }

            _regexCache.Clear(); // prevent unbounded growth
            return (result.ToString(), replacements.Count);
        }

        // ─── Whole-word check ───────────────────────────────────────

        public bool ContainsTerm(string text, string term)
        {
            return ContainsWholeWord(text, term);
        }

        /// <summary>
        /// Fast glossary context builder using inverted index.
        /// For a batch of entries, finds up to MAX_GLOSSARY_CONTEXT_TERMS matching terms.
        /// Returns dictionary of (term_key → chinese_translation) for prompt injection.
        /// 
        /// Performance: O(batch_word_count × avg_candidates_per_word) instead of
        /// O(glossary_size × batch_size). With 100k glossary and 50 entries per batch,
        /// this is ~1000x faster than iterating all glossary terms.
        /// </summary>
        public Dictionary<string, string> GetGlossaryContextTerms(List<LocalizationEntry> entries)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

            // Verify each candidate with ContainsWholeWord, longest match first
            var sortedCandidates = candidates
                .Where(c => _sortedTerms.Any(t => t.Key == c)) // filter to existing only (safety)
                .OrderByDescending(c => c.Length);

            foreach (var termKey in sortedCandidates)
            {
                foreach (var entry in entries)
                {
                    if (ContainsWholeWord(entry.Value, termKey))
                    {
                        if (Terms.TryGetValue(termKey, out var term))
                            result[termKey] = term.Chinese;
                        break; // found match for this term, check next term
                    }
                }

                if (result.Count >= MAX_GLOSSARY_CONTEXT_TERMS)
                    break;
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

        /// <summary>
        /// Check if text contains a whole-word match for the given term.
        /// Uses cached Regex for performance.
        /// </summary>
        public static bool ContainsWholeWord(string text, string term)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
                return false;
            // 注意：不做 term.Length > text.Length 的预检——宽容匹配下
            // 术语可能因含修饰词（如 "Executor-class"）而比原文长，仍可能语义匹配。

            var regex = GetOrCreateRegex(term);
            return regex.IsMatch(text);
        }

        // ─── Search & Filter ─────────────────────────────────────────

        /// <summary>Filter terms by search text (matches English, Chinese, or Tags)</summary>
        public List<GlossaryTerm> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Terms.Values.OrderBy(t => t.English).ToList();

            // 支持多关键词：空格/全角空格分隔，所有关键词命中才显示（AND）
            var keywords = query.Trim()
                .Split(new[] { ' ', '\u3000' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToList();

            return Terms.Values
                .Where(t => keywords.All(k =>
                    (t.English ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0
                    || (t.Chinese ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0
                    || (t.Tags ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0
                    || (t.Category ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(t => t.English)
                .ToList();
        }

        /// <summary>Get all unique categories</summary>
        public List<string> GetAllCategories()
        {
            return Terms.Values
                .Where(t => !string.IsNullOrEmpty(t.Category))
                .Select(t => t.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        /// <summary>Get all unique tags</summary>
        public List<string> GetAllTags()
        {
            return Terms.Values
                .SelectMany(t => t.TagList)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        /// <summary>Get terms filtered by status</summary>
        public List<GlossaryTerm> FilterByStatus(string status)
        {
            return Terms.Values
                .Where(t => t.Status == status)
                .OrderBy(t => t.English)
                .ToList();
        }

        /// <summary>Get terms filtered by category</summary>
        public List<GlossaryTerm> FilterByCategory(string category)
        {
            return Terms.Values
                .Where(t => t.Category == category)
                .OrderBy(t => t.English)
                .ToList();
        }
    }
}
