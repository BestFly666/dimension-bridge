using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
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
    ///   - SubstituteTerms: used only when the caller explicitly wants in-place term
    ///     replacement (e.g., post-processing). NOT used in the hot translation path.
    /// </summary>
    public class GlossaryManager : IGlossaryManager
    {
        private static readonly string DictFile = Path.Combine(Environment.CurrentDirectory, "translation_dictionary.json");
        private static readonly string GlossaryFile = Path.Combine(Environment.CurrentDirectory, "glossary_terms.json");

        /// <summary>Key: English term (case-insensitive), Value: GlossaryTerm entry</summary>
        public Dictionary<string, GlossaryTerm> Terms { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Sorted by key length descending for longest-match-first</summary>
        private List<KeyValuePair<string, GlossaryTerm>> _sortedTerms = new();

        /// <summary>
        /// Inverted index: lowercase word → set of glossary terms whose English contains that word.
        /// Enables O(entry_word_count × avg_candidates) matching instead of O(glossary_size × batch_size).
        /// </summary>
        private Dictionary<string, HashSet<string>> _invertedIndex = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Maximum glossary terms injected into a single batch prompt.</summary>
        private const int MAX_GLOSSARY_CONTEXT_TERMS = 50;

        /// <summary>Regex cache shared across all methods (thread-safe reads, rebuild on import)</summary>
        private static readonly Dictionary<string, Regex> _regexCache = new();

        public int Count => Terms.Count;

        public GlossaryManager()
        {
            Load();
        }

        // ─── Load / Save ────────────────────────────────────────────

        public void Load()
        {
            try
            {
                if (File.Exists(GlossaryFile))
                {
                    var json = File.ReadAllText(GlossaryFile, Encoding.UTF8);
                    Terms = new Dictionary<string, GlossaryTerm>(StringComparer.OrdinalIgnoreCase);

                    // Try loading new format (array of GlossaryTerm) first
                    try
                    {
                        var termsList = JsonConvert.DeserializeObject<List<GlossaryTerm>>(json);
                        if (termsList != null && termsList.Count > 0)
                        {
                            foreach (var t in termsList.Where(t => !string.IsNullOrEmpty(t.English)))
                                Terms[t.English] = t;
                        }
                    }
                    catch
                    {
                        // Fall back to old format (Dictionary<string, string>)
                        var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (loaded != null)
                        {
                            foreach (var kvp in loaded)
                            {
                                if (kvp.Key == "\uFEFFEn" || kvp.Key == "En" || kvp.Key.Length < 2)
                                    continue;
                                Terms[kvp.Key] = new GlossaryTerm(kvp.Key, kvp.Value);
                            }
                        }
                    }
                }
                else if (File.Exists(DictFile))
                {
                    var json = File.ReadAllText(DictFile, Encoding.UTF8);
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    Terms = new Dictionary<string, GlossaryTerm>(StringComparer.OrdinalIgnoreCase);
                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                        {
                            if (kvp.Key == "\uFEFFEn" || kvp.Key == "En" || kvp.Key == "\u04F5" ||
                                string.IsNullOrWhiteSpace(kvp.Key) || kvp.Key.Length < 2)
                                continue;
                            Terms[kvp.Key] = new GlossaryTerm(kvp.Key, kvp.Value);
                        }
                    }
                }
                _regexCache.Clear();
                RebuildSortedList();
            }
            catch (Exception ex)
            {
                Terms = new Dictionary<string, GlossaryTerm>(StringComparer.OrdinalIgnoreCase);
                _sortedTerms = new List<KeyValuePair<string, GlossaryTerm>>();
                System.Diagnostics.Debug.WriteLine($"Glossary load error: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                // Save as array of full GlossaryTerm objects (new format)
                var list = Terms.Values.OrderBy(t => t.English).ToList();
                var json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(GlossaryFile, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Glossary save error: {ex.Message}");
            }
        }

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
                    if (_invertedIndex.TryGetValue(word, out var termKeys))
                    {
                        foreach (var key in termKeys)
                            candidates.Add(key);
                    }
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

        /// <summary>
        /// Check if text contains a whole-word match for the given term.
        /// Uses cached Regex for performance.
        /// </summary>
        public static bool ContainsWholeWord(string text, string term)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
                return false;
            if (term.Length > text.Length)
                return false;

            var regex = GetOrCreateRegex(term);
            return regex.IsMatch(text);
        }

        // ─── Regex cache ────────────────────────────────────────────

        private static Regex GetOrCreateRegex(string term)
        {
            lock (_regexCache)
            {
                if (!_regexCache.TryGetValue(term, out var regex))
                {
                    var pattern = $@"\b{Regex.Escape(term)}\b";
                    regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    _regexCache[term] = regex;
                }
                return regex;
            }
        }

        // ─── Import ─────────────────────────────────────────────────

        public (int added, int updated, int skipped) ImportCsv(string filePath)
        {
            int added = 0, updated = 0, skipped = 0;

            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length < 2) return (0, 0, 0);

                var firstLine = lines[0].Trim().TrimStart('\uFEFF');
                bool hasHeader = CsvHelper.IsHeaderLine(firstLine);
                int startIndex = hasHeader ? 1 : 0;

                for (int i = startIndex; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var parts = CsvHelper.ParseCsvLine(line);
                    if (parts.Count < 2) { skipped++; continue; }

                    var english = parts[0].Trim();
                    var chinese = parts[1].Trim();
                    var category = parts.Count >= 3 ? parts[2].Trim() : "";

                    if (string.IsNullOrEmpty(english) || string.IsNullOrEmpty(chinese))
                    { skipped++; continue; }
                    if (english == "\uFEFFEn" || english == "En" || english.Length < 2)
                    { skipped++; continue; }
                    if (chinese == "Ch" || chinese == "Cat")
                    { skipped++; continue; }

                    var key = english;
                    if (Terms.ContainsKey(key))
                    {
                        if (Terms[key].Chinese != chinese)
                            updated++;
                    }
                    else
                    {
                        added++;
                    }
                    Terms[key] = new GlossaryTerm(key, chinese, category);
                }

                _regexCache.Clear();
                RebuildSortedList();
                Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Glossary CSV import error: {ex.Message}");
            }

            return (added, updated, skipped);
        }

        public (int added, int updated) ImportJson(string filePath)
        {
            int added = 0, updated = 0;

            try
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                var entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                if (entries != null)
                {
                    foreach (var kvp in entries)
                    {
                        var key = kvp.Key.Trim();
                        var value = kvp.Value?.Trim() ?? "";
                        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value) || key.Length < 2)
                            continue;

                        if (Terms.ContainsKey(key))
                        {
                            if (Terms[key].Chinese != value)
                                updated++;
                        }
                        else
                        {
                            added++;
                        }
                        Terms[key] = new GlossaryTerm(key, value, "");
                    }

                    _regexCache.Clear();
                    RebuildSortedList();
                    Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Glossary JSON import error: {ex.Message}");
            }

            return (added, updated);
        }

        // ─── CRUD ───────────────────────────────────────────────────

        public void SetEntry(string source, string translation, string category = "", string status = "confirmed", string tags = "")
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(translation))
                return;
            var term = new GlossaryTerm(source, translation, category, status, tags);
            if (Terms.ContainsKey(source))
                term.CreatedAt = Terms[source].CreatedAt;
            Terms[source] = term;
            _regexCache.Clear();
            RebuildSortedList();
            Save();
        }

        public void SetTerm(GlossaryTerm term)
        {
            if (string.IsNullOrEmpty(term.English) || string.IsNullOrEmpty(term.Chinese))
                return;
            if (Terms.ContainsKey(term.English))
                term.CreatedAt = Terms[term.English].CreatedAt;
            term.UpdatedAt = DateTime.Now;
            Terms[term.English] = term;
            _regexCache.Clear();
            RebuildSortedList();
            Save();
        }

        public bool RemoveEntry(string source)
        {
            var removed = Terms.Remove(source);
            if (removed)
            {
                _regexCache.Clear();
                RebuildSortedList();
                Save();
            }
            return removed;
        }

        public void Clear()
        {
            Terms.Clear();
            _sortedTerms.Clear();
            _regexCache.Clear();
            Save();
        }

        // ─── Search & Filter ─────────────────────────────────────────

        /// <summary>Filter terms by search text (matches English, Chinese, or Tags)</summary>
        public List<GlossaryTerm> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Terms.Values.OrderBy(t => t.English).ToList();

            var q = query.Trim();
            return Terms.Values
                .Where(t => t.English.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                         || t.Chinese.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                         || t.Tags.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                         || t.Category.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
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

        // ─── Conflict Detection ──────────────────────────────────────

        /// <summary>
        /// Scan translation entries for terminology conflicts.
        /// Each entry is (key, sourceText, translatedText).
        /// A conflict is when a glossary term's English appears in the source text,
        /// but its Chinese translation does NOT appear in the translated text.
        /// </summary>
        public List<GlossaryConflict> DetectConflicts(IEnumerable<(string key, string source, string translation)> entries)
        {
            var conflicts = new List<GlossaryConflict>();

            foreach (var (key, source, translation) in entries)
            {
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

        // ─── Merge from ExpertProfile ────────────────────────────────

        /// <summary>
        /// Merge terms from an expert profile's glossary into the unified glossary.
        /// Uses the profile name as category for imported terms.
        /// </summary>
        public (int added, int updated) MergeFromProfile(string profileName, Dictionary<string, string> profileGlossary)
        {
            int added = 0, updated = 0;
            if (profileGlossary == null) return (added, updated);

            foreach (var kvp in profileGlossary)
            {
                if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value))
                    continue;

                if (Terms.ContainsKey(kvp.Key))
                {
                    if (Terms[kvp.Key].Chinese != kvp.Value)
                    {
                        updated++;
                        Terms[kvp.Key].Chinese = kvp.Value;
                        Terms[kvp.Key].Category = profileName;
                        Terms[kvp.Key].UpdatedAt = DateTime.Now;
                    }
                }
                else
                {
                    added++;
                    Terms[kvp.Key] = new GlossaryTerm(kvp.Key, kvp.Value, profileName);
                }
            }

            if (added + updated > 0)
            {
                _regexCache.Clear();
                RebuildSortedList();
                Save();
            }

            return (added, updated);
        }

        // ─── Export ──────────────────────────────────────────────────

        public void ExportCsv(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("English,Chinese,Category,Status,Tags");
            foreach (var term in Terms.Values.OrderBy(t => t.English))
            {
                var english = CsvHelper.EscapeCsvField(term.English);
                var chinese = CsvHelper.EscapeCsvField(term.Chinese);
                var category = CsvHelper.EscapeCsvField(term.Category);
                var status = CsvHelper.EscapeCsvField(term.Status);
                var tags = CsvHelper.EscapeCsvField(term.Tags);
                sb.AppendLine($"{english},{chinese},{category},{status},{tags}");
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportJson(string filePath)
        {
            var list = Terms.Values.OrderBy(t => t.English).ToList();
            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            File.WriteAllText(filePath, json, Encoding.UTF8);
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
