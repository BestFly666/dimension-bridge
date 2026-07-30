using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.ExpertProfiles;

namespace SimpleXmlEditor.Services
{
    /// <summary>
    /// Orchestrates AI translation batches — prompt building, API calling,
    /// response parsing, caching, and glossary integration.
    /// Extracted from MainWindow.xaml.cs to separate UI from business logic.
    /// </summary>
    public class TranslationOrchestrator
    {
        private readonly IAiTranslationService _aiService;
        private readonly IConfigService _configService;
        private readonly IGlossaryManager _glossary;
        private readonly IExpertProfileManager _profileManager;
        private readonly Action<string> _logAction;

        // Mirror of MainWindow counters — owned by caller but updated here
        public Action<int> OnCacheHit;
        public Action<int> OnGlossaryHit;
        public Action<int> OnApiCall;
        public Action<int, int> OnApiChars; // (inputChars, outputChars)

        public TranslationOrchestrator(
            IAiTranslationService aiService,
            IConfigService configService,
            IGlossaryManager glossary,
            IExpertProfileManager profileManager,
            Action<string> logAction)
        {
            _aiService = aiService;
            _configService = configService;
            _glossary = glossary;
            _profileManager = profileManager;
            _logAction = logAction ?? (_ => { });
        }

        private void Log(string msg) => _logAction(msg);

        // ─── Batch Creation ──────────────────────────────────────

        public List<List<LocalizationEntry>> CreateBatches(
            List<LocalizationEntry> entries, string customPrompt, int batchSize)
        {
            var batches = new List<List<LocalizationEntry>>();
            var currentBatch = new List<LocalizationEntry>();
            int currentTokens = 0;

            int effectivePromptOverhead = 500; // rough estimate for prompt template

            foreach (var entry in entries)
            {
                int entryTokens = EstimateTokens(entry.Key) + EstimateTokens(entry.Value) + 10;

                // If adding this entry exceeds the batch size and we already have entries
                if (currentTokens + entryTokens > effectivePromptOverhead &&
                    currentBatch.Count > 0 && currentBatch.Count >= batchSize)
                {
                    batches.Add(currentBatch);
                    currentBatch = new List<LocalizationEntry>();
                    currentTokens = 0;
                }

                currentBatch.Add(entry);
                currentTokens += entryTokens;
            }

            if (currentBatch.Count > 0)
                batches.Add(currentBatch);

            return batches;
        }

        private static int EstimateTokens(string text)
        {
            return string.IsNullOrEmpty(text) ? 1 : text.Length / 4 + 1;
        }

        // ─── Batch Translation Core ─────────────────────────────

        /// <summary>
        /// Translate a single batch. Returns dict keyed by original text.
        /// </summary>
        public async Task<Dictionary<string, string>> TranslateBatchAsync(
            List<LocalizationEntry> batch, bool forceRefresh, string customPrompt)
        {
            var results = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(_aiService.ApiKey))
            {
                Log("API Key not set");
                return results;
            }
            if (string.IsNullOrEmpty(_aiService.Model))
            {
                Log("Model not selected");
                return results;
            }
            if (!batch.Any())
                return results;

            // Check glossary
            var uncachedEntries = new List<LocalizationEntry>();
            foreach (var entry in batch)
            {
                if (_glossary.TryGetValue(entry.Value, out var dictTranslation)
                    || _glossary.TryGetValue(entry.Key, out dictTranslation))
                {
                    results[entry.Value] = dictTranslation;
                    OnGlossaryHit?.Invoke(1);
                }
                else
                {
                    uncachedEntries.Add(entry);
                }
            }

            // Check cache (skip if forceRefresh)
            if (uncachedEntries.Count > 0 && !forceRefresh)
            {
                var stillUncached = new List<LocalizationEntry>();
                foreach (var entry in uncachedEntries)
                {
                    var cacheKey = _configService.GetCacheKey(entry.Value);
                    if (cacheKey != null && _configService.Cache.TryGetValue(cacheKey, out var cachedTranslation))
                    {
                        results[entry.Value] = cachedTranslation;
                        OnCacheHit?.Invoke(1);
                    }
                    else
                    {
                        stillUncached.Add(entry);
                    }
                }
                uncachedEntries = stillUncached;
            }

            if (!uncachedEntries.Any())
                return results;

            // Build prompt and call AI
            var prompt = BuildPrompt(uncachedEntries, customPrompt);

            try
            {
                var response = await _aiService.TranslateBatchAsync(prompt);

                if (!string.IsNullOrEmpty(response))
                {
                    var batchResults = ParseResponse(response, uncachedEntries);

                    foreach (var kvp in batchResults)
                    {
                        var cacheKey = _configService.GetCacheKey(kvp.Key);
                        if (cacheKey != null)
                            _configService.Cache[cacheKey] = kvp.Value;
                        results[kvp.Key] = kvp.Value;
                    }

                    OnApiCall?.Invoke(1);
                    OnApiChars?.Invoke(prompt.Length, response.Length);
                }

                return results;
            }
            catch (Exception ex)
            {
                Log($"Translation error: {ex.Message}");
                return results;
            }
        }

        // ─── Prompt Building ────────────────────────────────────

        private string BuildPrompt(List<LocalizationEntry> entries, string customPrompt)
        {
            var prompt = string.IsNullOrEmpty(customPrompt)
                ? PromptTemplates.DefaultBatchPrompt
                : customPrompt;

            prompt = prompt.Replace("{LANGUAGE}", _aiService.TargetLanguage);
            prompt = prompt.Replace("{CONTEXT}", "game localization");

            var expertContext = BuildExpertContext();
            prompt = prompt.Replace("{EXPERT_CONTEXT}", expertContext);

            var glossary = BuildGlossaryContext(entries);
            prompt = prompt.Replace("{GLOSSARY}", glossary);

            var textsBuilder = new StringBuilder();
            var hasChineseSource = false;
            for (int i = 0; i < entries.Count; i++)
            {
                var isChinese = HasChineseChars(entries[i].Value);
                if (isChinese) hasChineseSource = true;

                // Sanitize text: escape quotes and limit length to prevent prompt injection
                var safeText = SanitizePromptText(entries[i].Value);
                var tag = isChinese
                    ? " [EXISTING ZH — review & correct, NOT re-translate from scratch]"
                    : "";
                textsBuilder.AppendLine($"{i + 1}. \"{safeText}\"{tag}");
            }

            prompt = prompt.Replace("{TEXTS}", textsBuilder.ToString().TrimEnd());
            prompt = prompt.Replace("{MIXED_SOURCE_NOTE}",
                hasChineseSource
                    ? "\n!! Some entries above are already in Chinese (marked [EXISTING ZH]). For those, review the existing translation and provide a CORRECTED / IMPROVED Chinese version. Fix terminology errors and awkward phrasing, but do NOT try to re-translate from scratch."
                    : "");

            return prompt;
        }

        /// <summary>
        /// Sanitizes text for safe inclusion in prompts by escaping quotes
        /// and truncating overly long entries to prevent injection attacks.
        /// </summary>
        private static string SanitizePromptText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Limit individual text length to prevent prompt overflow
            const int maxLength = 4000;
            if (text.Length > maxLength)
                text = text.Substring(0, maxLength) + "...[truncated]";

            // Escape quotes to break out of the "..." wrapper
            text = text.Replace("\\", "\\\\");
            text = text.Replace("\"", "\\\"");

            // Strip control characters that could break JSON parsing
            var cleanChars = text.Where(c => c >= 32 || c == '\n' || c == '\t').ToArray();
            return new string(cleanChars);
        }

        private string BuildExpertContext()
        {
            if (string.IsNullOrEmpty(_profileManager.ActiveProfileName))
                return "";

            var profile = _profileManager.GetProfile(_profileManager.ActiveProfileName);
            if (profile == null)
                return "";

            return profile.BuildExpertContextBlock(_aiService.TargetLanguage);
        }

        private string BuildGlossaryContext(List<LocalizationEntry> entries)
        {
            // Use inverted-index based fast matching (capped at MAX_GLOSSARY_CONTEXT_TERMS)
            var relevantTerms = _glossary.GetGlossaryContextTerms(entries);

            if (relevantTerms.Count == 0)
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("\n!! CRITICAL GLOSSARY — Use these EXACT translations for the following terms:");
            foreach (var term in relevantTerms)
                sb.AppendLine($"  \"{term.Key}\" = \"{term.Value}\"");
            sb.AppendLine("When these terms appear in ANY translated text, you MUST use the glossary translation above.");

            return sb.ToString();
        }

        private static bool HasChineseChars(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) return true;
            }
            return false;
        }

        // ─── Response Parsing ───────────────────────────────────

        private Dictionary<string, string> ParseResponse(string response, List<LocalizationEntry> entries)
        {
            var results = new Dictionary<string, string>();

            try
            {
                var clean = response.Trim();
                if (clean.StartsWith("```json"))
                    clean = clean[7..];
                if (clean.EndsWith("```"))
                    clean = clean[..^3];
                clean = clean.Trim();

                var json = JObject.Parse(clean);
                var translations = json["translations"] as JArray;

                if (translations != null)
                {
                    foreach (var t in translations)
                    {
                        var idx = t["index"]?.ToObject<int>() ?? 0;
                        var text = t["translation"]?.ToString()?.Trim();
                        if (idx > 0 && idx <= entries.Count && !string.IsNullOrEmpty(text))
                            results[entries[idx - 1].Value] = text;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Parse error: {ex.Message} — trying fallback");
                ParseFallback(response, entries, results);
            }

            return results;
        }

        private void ParseFallback(string response, List<LocalizationEntry> entries, Dictionary<string, string> results)
        {
            var clean = response.Trim();
            if (clean.StartsWith("```json")) clean = clean[7..];
            if (clean.EndsWith("```")) clean = clean[..^3];
            clean = clean.Trim();

            // Strategy 1: Extract JSON fragment
            var jsonStart = clean.IndexOf('{');
            var jsonEnd = clean.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                try
                {
                    var jsonStr = clean[jsonStart..(jsonEnd + 1)];
                    var jsonResponse = JObject.Parse(jsonStr);
                    var translations = jsonResponse["translations"] as JArray;
                    if (translations != null)
                    {
                        foreach (var t in translations)
                        {
                            var idx = t["index"]?.ToObject<int>() ?? 0;
                            var text = t["translation"]?.ToString()?.Trim();
                            if (idx > 0 && idx <= entries.Count && !string.IsNullOrEmpty(text))
                                results[entries[idx - 1].Value] = text;
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback JSON fragment parse failed: {ex.Message}");
                }
            }

            // Strategy 2: Regex for "N. \"translation\"" pattern
            var lines = clean.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var regex = new Regex(@"(\d+)[\.\s]\s*[""""](.+?)[""""]");
            foreach (var line in lines)
            {
                var match = regex.Match(line.Trim());
                if (match.Success && int.TryParse(match.Groups[1].Value, out var idx))
                {
                    var text = match.Groups[2].Value.Trim();
                    if (idx > 0 && idx <= entries.Count && !string.IsNullOrEmpty(text))
                        results[entries[idx - 1].Value] = text;
                }
            }

            // Strategy 3: Line-by-line parsing
            if (results.Count == 0)
            {
                for (int i = 0; i < Math.Min(lines.Length, entries.Count); i++)
                {
                    var line = lines[i].Trim();
                    line = line.Replace($"{i + 1}.", "").Replace("-", "").Trim();
                    if (line.StartsWith("\"") && line.EndsWith("\""))
                        line = line[1..^1];
                    if (line.StartsWith("\u201C") && line.EndsWith("\u201D"))
                        line = line[1..^1];
                    if (!string.IsNullOrEmpty(line) && !line.Contains("{") && !line.Contains("}") && !line.Contains("index"))
                        results[entries[i].Value] = line;
                }
            }
        }
    }
}
