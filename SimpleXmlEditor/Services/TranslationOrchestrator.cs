using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.ExpertProfiles;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Utils;

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

        /// <summary>
        /// 按"估算输出 token"动态分批，而不是按固定条目数硬切。
        /// 原因：AI 输出有 max_tokens 上限（4096 或 8192），若一批输出超限，
        /// 服务端截断 JSON → 解析 0 条 → 拆半重试，时间翻倍甚至多倍。
        /// 典型文本按 3800 token 预算切批 ≈ 20-30 条/批，一次成功、无重试风暴。
        /// </summary>
        public List<List<LocalizationEntry>> CreateBatches(
            List<LocalizationEntry> entries, string customPrompt, int batchSize)
        {
            // 安全输出预算：覆盖 max_tokens=4096 的模型（留 JSON 结构余量），
            // 8192 模型同样受益——小批输出更快且永不截断。
            const int maxOutputTokensPerBatch = 3800;

            var batches = new List<List<LocalizationEntry>>();
            var currentBatch = new List<LocalizationEntry>();
            int currentOutputTokens = 0;

            foreach (var entry in entries)
            {
                int entryOutputTokens = EstimateOutputTokens(entry.Value);

                // 切批：输出预算超限（防截断）或条目数达 batchSize 硬上限
                if (currentBatch.Count > 0 &&
                    (currentOutputTokens + entryOutputTokens > maxOutputTokensPerBatch ||
                     currentBatch.Count >= batchSize))
                {
                    batches.Add(currentBatch);
                    currentBatch = new List<LocalizationEntry>();
                    currentOutputTokens = 0;
                }

                currentBatch.Add(entry);
                currentOutputTokens += entryOutputTokens;
            }

            if (currentBatch.Count > 0)
                batches.Add(currentBatch);

            return batches;
        }

        /// <summary>估算一条文本翻译成中文后占用的输出 token（含 JSON 条目结构开销）。</summary>
        private static int EstimateOutputTokens(string text)
        {
            // 每条 JSON 条目开销：序号 + 引号 + 冒号 + 换行 ≈ 12 token
            const int perEntryOverhead = 12;
            if (string.IsNullOrEmpty(text)) return perEntryOverhead;

            // 中文 1 字符 ≈ 1.2 token；英文 ≈ 4 字符/token（保守估，防截断优先）
            return text.HasChineseChars()
                ? (int)(text.Length * 1.2) + perEntryOverhead
                : text.Length / 4 + perEntryOverhead;
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
                Log(LocalizationManager.GetString("LogApiKeyNotSet"));
                return results;
            }
            if (string.IsNullOrEmpty(_aiService.Model))
            {
                Log(LocalizationManager.GetString("LogModelNotSelected"));
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

                    if (batchResults.Count > 0)
                    {
                        // value → 条目标识（批次内原文可能重复，取首个条目 Key）
                        var entryKeyByValue = new Dictionary<string, string>();
                        foreach (var entry in uncachedEntries)
                            entryKeyByValue.TryAdd(entry.Value, entry.Key);

                        foreach (var kvp in batchResults)
                        {
                            // 双键对称写（Key + MD5(原文)），与 SyncEntriesToCache 保持一致
                            _configService.SetCacheEntry(entryKeyByValue[kvp.Key], kvp.Key, kvp.Value);
                            results[kvp.Key] = kvp.Value;
                        }

                        OnApiCall?.Invoke(1);
                        OnApiChars?.Invoke(prompt.Length, response.Length);

                        // 部分结果校验：AI 只返回了部分条目（JSON 截断/漏译）时，
                        // 静默接受会让这些条目丢失译文。把缺失条目拆半递归补译并合并。
                        var missing = uncachedEntries.Where(e => !batchResults.ContainsKey(e.Value)).ToList();
                        if (missing.Count > 0 && missing.Count < uncachedEntries.Count)
                        {
                            var retried = await RetryHalvedAsync(missing, forceRefresh, customPrompt);
                            foreach (var kvp in retried)
                            {
                                if (!results.ContainsKey(kvp.Key))
                                {
                                    results[kvp.Key] = kvp.Value;
                                    _configService.SetCacheEntry(entryKeyByValue[kvp.Key], kvp.Key, kvp.Value);
                                }
                            }
                        }
                        return results;
                    }
                }

                // 响应为空或解析出 0 条：通常是输出 token 超限导致 JSON 截断。
                // 自动拆半重试，避免大批次（如 50 条）整体失败、只能手动降批量的问题。
                if (uncachedEntries.Count > 1)
                    return await RetryHalvedAsync(uncachedEntries, forceRefresh, customPrompt);

                return results;
            }
            catch (Exception ex)
            {
                Log(LocalizationManager.GetString("TranslationError", ex.Message));

                // 异常（超时 / 400 长度超限）同样拆半重试
                if (uncachedEntries.Count > 1)
                    return await RetryHalvedAsync(uncachedEntries, forceRefresh, customPrompt);

                return results;
            }
        }

        /// <summary>将失败的批次拆成两半递归重试，合并两半结果。</summary>
        private async Task<Dictionary<string, string>> RetryHalvedAsync(
            List<LocalizationEntry> entries, bool forceRefresh, string customPrompt)
        {
            var half = entries.Count / 2;
            Log(LocalizationManager.GetString("LogBatchRetryHalve", entries.Count));

            var left = await TranslateBatchAsync(entries.GetRange(0, half), forceRefresh, customPrompt);
            var right = await TranslateBatchAsync(
                entries.GetRange(half, entries.Count - half), forceRefresh, customPrompt);

            var merged = new Dictionary<string, string>(left);
            foreach (var kvp in right)
                merged[kvp.Key] = kvp.Value;
            return merged;
        }

        // ─── Prompt Building ────────────────────────────────────

        private string BuildPrompt(List<LocalizationEntry> entries, string customPrompt)
        {
            var prompt = string.IsNullOrEmpty(customPrompt)
                ? PromptTemplates.DefaultBatchPrompt
                : customPrompt;

            prompt = prompt.Replace("{LANGUAGE}", _aiService.TargetLanguage);
            prompt = prompt.Replace("{CONTEXT}", "game localization");

            // 1) 先匹配术语：从词典中找出当前批次相关的术语
            var glossary = BuildGlossaryContext(entries);

            // 2) 术语并入专家提示词（成为专家知识的一部分）；
            //    未选专家档案时，术语仍作为独立块注入，保证术语指导不失效
            var expertContext = BuildExpertContext(glossary);
            if (string.IsNullOrEmpty(expertContext) && !string.IsNullOrEmpty(glossary))
                expertContext = glossary;
            if (!string.IsNullOrEmpty(expertContext))
            {
                prompt = prompt.Contains("{EXPERT_CONTEXT}")
                    ? prompt.Replace("{EXPERT_CONTEXT}", expertContext)
                    : prompt + "\n\n" + expertContext;
            }
            else
            {
                prompt = prompt.Replace("{EXPERT_CONTEXT}", "");
            }

            // 3) 术语已并入专家块（{EXPERT_CONTEXT} 替换或追加到提示词尾部）：
            //    若提示词模板仍含 {GLOSSARY} 占位符，替换为空，避免术语重复注入。
            prompt = prompt.Replace("{GLOSSARY}", "");

            var textsBuilder = new StringBuilder();
            var hasChineseSource = false;
            for (int i = 0; i < entries.Count; i++)
            {
                var isChinese = entries[i].Value.HasChineseChars();
                if (isChinese) hasChineseSource = true;

                // Sanitize text: escape quotes and limit length to prevent prompt injection
                var safeKey = PromptTextSanitizer.Sanitize(entries[i].Key);
                var safeText = PromptTextSanitizer.Sanitize(entries[i].Value);
                var tag = isChinese
                    ? " [EXISTING ZH — review & correct, NOT re-translate from scratch]"
                    : "";
                // 方括号内为条目 Key（如 TEXT_SPEECH_* / UNIT_*_DESCRIPTION），
                // 提供内容类型与场景线索，帮助模型判断语境、保持术语一致。
                textsBuilder.AppendLine($"{i + 1}. [{safeKey}] \"{safeText}\"{tag}");
            }

            prompt = prompt.Replace("{TEXTS}", textsBuilder.ToString().TrimEnd());
            prompt = prompt.Replace("{MIXED_SOURCE_NOTE}",
                hasChineseSource
                    ? "\n!! Some entries above are already in Chinese (marked [EXISTING ZH]). For those, review the existing translation and provide a CORRECTED / IMPROVED Chinese version. Fix terminology errors and awkward phrasing, but do NOT try to re-translate from scratch."
                    : "");

            return prompt;
        }

        /// <summary>
        /// 构建专家提示词块。glossary 为对当前批次匹配到的术语文本，
        /// 会被并入专家块，与专家 Context 一起注入 API。
        /// </summary>
        private string BuildExpertContext(string glossary)
        {
            if (string.IsNullOrEmpty(_profileManager.ActiveProfileName))
                return "";

            var profile = _profileManager.GetProfile(_profileManager.ActiveProfileName);
            if (profile == null)
                return "";

            return profile.BuildExpertContextBlock(_aiService.TargetLanguage, glossary);
        }

        private string BuildGlossaryContext(List<LocalizationEntry> entries)
        {
            // Use inverted-index based fast matching (capped at MAX_GLOSSARY_CONTEXT_TERMS)
            var relevantTerms = _glossary.GetGlossaryContextTerms(entries);

            if (relevantTerms.Count == 0)
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("\n!! GLOSSARY — Preferred translations (follow unless the context clearly conflicts):");
            foreach (var term in relevantTerms)
            {
                // 术语转义：防术语值含引号/控制字符时逃逸出提示词结构
                var safeKey = PromptTextSanitizer.Sanitize(term.Key);
                var safeValue = PromptTextSanitizer.Sanitize(term.Value);
                sb.AppendLine($"  \"{safeKey}\" = \"{safeValue}\"");
            }
            sb.AppendLine("When these terms appear in the text, use the glossary translation by default to keep terminology consistent.");
            sb.AppendLine("EXCEPTION: if a term is clearly used with a different meaning in this specific context (figurative use, part of a proper name, or a different sense), translate it naturally for that context instead. When in doubt, prefer the glossary translation.");

            return sb.ToString();
        }

        // ─── Response Parsing ───────────────────────────────────

        private Dictionary<string, string> ParseResponse(string response, List<LocalizationEntry> entries)
        {
            var results = new Dictionary<string, string>();
            var parsed = AiResponseParser.ParseTranslations(response, entries.Count);
            foreach (var kvp in parsed)
            {
                if (kvp.Key >= 1 && kvp.Key <= entries.Count)
                    results[entries[kvp.Key - 1].Value] = kvp.Value;
            }
            return results;
        }
    }
}
