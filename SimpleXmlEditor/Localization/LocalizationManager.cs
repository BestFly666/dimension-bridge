using System;
using System.Collections.Generic;
using System.Globalization;

namespace SimpleXmlEditor.Localization
{
    public static class LocalizationManager
    {
        private static Dictionary<string, Dictionary<string, string>> _translations = new();
        private static string _currentLanguage = "en";

        static LocalizationManager()
        {
            InitializeTranslations();
        }

        public static string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                _currentLanguage = value;
                LanguageChanged?.Invoke();
            }
        }

        public static event Action LanguageChanged;

        public static string GetString(string key)
        {
            if (_translations.ContainsKey(_currentLanguage) &&
                _translations[_currentLanguage].ContainsKey(key))
            {
                return _translations[_currentLanguage][key];
            }

            // Fallback to English
            if (_translations.ContainsKey("en") &&
                _translations["en"].ContainsKey(key))
            {
                return _translations["en"][key];
            }

            return key; // Return key if translation not found
        }

        /// <summary>
        /// Get a formatted string (supports {0}, {1}, ... placeholders).
        /// </summary>
        public static string GetString(string key, params object[] args)
        {
            var template = GetString(key);
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        public static List<(string Code, string Name)> GetAvailableLanguages()
        {
            return new List<(string, string)>
            {
                ("en", "English"),
                ("tr", "Türkçe"),
                ("es", "Español"),
                ("fr", "Français"),
                ("de", "Deutsch"),
                ("it", "Italiano"),
                ("pt", "Português"),
                ("ru", "Русский"),
                ("ja", "日本語"),
                ("ko", "한국어"),
                ("zh", "中文"),
                ("ar", "العربية"),
                ("hi", "हिन्दी"),
                ("nl", "Nederlands")
            };
        }

        private static void InitializeTranslations()
        {
            // =========================================================================
            // English (default)
            // =========================================================================
            _translations["en"] = new Dictionary<string, string>
            {
                // === Window titles ===
                ["WindowTitle"] = "XML AI Translator by Veloxcity",
                ["SettingsTitle"] = "Settings - XML AI Translator",

                // === Main UI buttons ===
                ["Load"] = "Load",
                ["Save"] = "Save",
                ["Settings"] = "Settings",
                ["Stats"] = "Stats",
                ["ImportDict"] = "Import Dict",
                ["ClearDict"] = "Clear Dict",
                ["BatchReplace"] = "Batch Replace",
                ["Undo"] = "Undo",
                ["QuickSave"] = "Quick Save",
                ["NoExpertDefault"] = "None (Default)",

                // === AI Translation buttons ===
                ["TranslateSelected"] = "Translate Selected",
                ["TranslateAll"] = "Translate All",
                ["Translate"] = "Translate",
                ["Pause"] = "Pause",
                ["Resume"] = "Resume",
                ["Continue"] = "Continue",
                ["Stop"] = "Stop",
                ["ClearCache"] = "Clear Cache",

                // === Section titles ===
                ["AITranslationCenter"] = "AI Translation Center",
                ["TranslationData"] = "Translation Data",
                ["ActivityLog"] = "Activity Log",

                // === Data Grid ===
                ["Select"] = "Select",
                ["Status"] = "Status",
                ["Key"] = "Key",
                ["Original"] = "Original",
                ["Translation"] = "Translation",

                // === Status bar ===
                ["Ready"] = "Ready",
                ["LoadedEntries"] = "Loaded {0} entries",
                ["MergedTranslations"] = "Merged {0} translations",
                ["SavedEntries"] = "Saved {0} entries to {1}",
                ["NoEntriesForTranslation"] = "No entries need translation",
                ["TranslatingBatch"] = "Translating batch {0}/{1} ({2} entries)...",
                ["WaitingRateLimit"] = "Waiting {0}s before next batch (rate limit optimization)...",
                ["TranslationPaused"] = "Translation paused",
                ["TranslationResumed"] = "Translation resumed",
                ["TranslationStopped"] = "Translation stopped",
                ["TranslationCancelled"] = "Translation cancelled",
                ["NoUntranslatedEntries"] = "No untranslated entries",

                // === Cache/Glossary info ===
                ["CacheInfo"] = "Cache: {0} | Hits: {1} | API: {2}{3}",
                ["Glossary"] = "Glossary",
                ["TipGlossary"] = "Open Glossary Manager",
                ["GlossaryInfo"] = "Glossary: {0} | Hits: {1}",
                ["DictInfo"] = "Dict: {0} | Hits: {1}",
                ["ClearDict"] = "Clear Glossary",
                ["ConfirmClearDict"] = "Clear ALL {0} glossary terms?\nThis cannot be undone!",
                ["LogDictCleared"] = "Glossary cleared",

                // === Glossary Window ===
                ["GlossaryWindowTitle"] = "Glossary Manager",
                ["GlossaryAdd"] = "Add",
                ["GlossaryEdit"] = "Edit",
                ["GlossaryDelete"] = "Delete",
                ["GlossaryImport"] = "Import",
                ["GlossaryExport"] = "Export",
                ["GlossaryMergeProfile"] = "Merge Profile",
                ["GlossaryDetectConflicts"] = "Detect Conflicts",
                ["GlossaryRefresh"] = "Refresh",
                ["GlossaryClose"] = "Close",
                ["GlossaryCancel"] = "Cancel",
                ["GlossarySave"] = "Save",
                ["GlossaryMerge"] = "Merge",
                ["GlossaryFilterAll"] = "All",
                ["GlossaryColEnglish"] = "English (Source)",
                ["GlossaryColChinese"] = "Chinese (Target)",
                ["GlossaryColCategory"] = "Category",
                ["GlossaryColStatus"] = "Status",
                ["GlossaryColTags"] = "Tags",
                ["GlossaryStatus_confirmed"] = "confirmed",
                ["GlossaryStatus_pending"] = "pending",
                ["GlossaryStatus_rejected"] = "rejected",
                ["GlossaryTermCount"] = "{0} terms total ({1} shown)",
                ["GlossaryStatusSummary"] = "{0} confirmed, {1} pending, {2} rejected",
                ["GlossaryCategoryCount"] = "{0} categories",
                ["GlossarySelectHint"] = "Please select a term first",
                ["GlossaryDeleteConfirm"] = "Delete term \"{0}\"?",
                ["GlossaryImportResult"] = "Import complete: {0} added, {1} updated, {2} skipped",
                ["GlossaryExportResult"] = "Exported {0} terms successfully",
                ["GlossaryNoProfiles"] = "No expert profiles available. Create one in Settings first.",
                ["GlossaryMergeProfileHelp"] = "Select an expert profile to merge its glossary terms:",
                ["GlossarySelectProfileHint"] = "Please select a profile first",
                ["GlossaryMergeResult"] = "Merge complete: {0} terms added, {1} updated",
                ["GlossaryNoConflicts"] = "No terminology conflicts detected. All translations match the glossary!",
                ["GlossaryConflictsTitle"] = "Glossary Conflicts",
                ["GlossaryConflictCount"] = "Detected {0} terminology conflict(s)",
                ["GlossaryExpectedTranslation"] = "Expected",
                ["GlossaryActualTranslation"] = "Actual Translation",
                ["GlossaryEntryKey"] = "Entry Key",
                ["GlossaryRequiredFields"] = "English and Chinese fields are required.",
                ["GlossaryCompare"] = "Compare",

                // === Filter ===
                ["FilterKey"] = "Filter Key",
                ["FilterOriginal"] = "Filter Original",
                ["FilterTranslation"] = "Filter Translation",
                ["ClearFilter"] = "Clear Filter",
                ["FilteredCount"] = "Filtered {0} / {1} entries",

                // === Activity Log ===
                ["RealTime"] = "Real-time",
                ["AutoScroll"] = "Auto-scroll",
                ["ClearLog"] = "Clear Log",

                // === Settings ===
                ["AIConfiguration"] = "AI Configuration",
                ["ConfigureSettings"] = "Configure your AI translation settings",
                ["APIKey"] = "API Key",
                ["EnterAPIKey"] = "Enter your Google Gemini API key from AI Studio",
                ["AIModel"] = "AI Model",
                ["SelectModel"] = "Select an AI model with rate limits and pricing info",
                ["Refresh"] = "Refresh",
                ["Loading"] = "Loading...",
                ["TargetLanguage"] = "Target Language",
                ["SelectTargetLanguage"] = "Select the target language for translation",
                ["ProgramLanguage"] = "Program Language",
                ["SelectProgramLanguage"] = "Select the interface language",
                ["CustomPrompt"] = "Custom Prompt",
                ["CustomPromptHelp"] = "Custom AI translation prompt. Available variables: {LANGUAGE}, {CONTEXT}, {TEXTS}, {EXPERT_CONTEXT}, {GLOSSARY}, {MIXED_SOURCE_NOTE}.",
                ["Reset"] = "Reset",
                ["QuickTips"] = "Quick Tips",
                ["TipAPIKey"] = "Get API key from your chosen AI provider's platform",
                ["TipModels"] = "Each provider offers multiple models to choose from",
                ["TipRateLimits"] = "Rate limits are automatically optimized per model",
                ["TipCache"] = "Translation cache reduces API costs",
                ["TipLanguages"] = "30 target languages supported",
                ["SaveApply"] = "Save & Apply",
                ["Cancel"] = "Cancel",
                ["NewProfile"] = "New Profile",
                ["EditProfile"] = "Edit: {0}",

                // === MessageBox titles ===
                ["MsgError"] = "Error",
                ["MsgSuccess"] = "Success",
                ["MsgConfirm"] = "Confirm",
                ["MsgWarning"] = "Warning",
                ["MsgTip"] = "Tip",

                // === MessageBox messages ===
                ["EnterAPIKeyFirst"] = "Please enter an API Key",
                ["SelectModelFirst"] = "Please select a model",
                ["EnterProfileName"] = "Please enter a profile name.",
                ["SelectEntriesFirst"] = "Please select entries to translate first",
                ["SearchTermEmpty"] = "Search term cannot be empty",
                ["NoModelsFound"] = "No models found. Please check your API key.",
                ["ModelsFoundSuccess"] = "Found {0} models with rate limit information",
                ["ErrorFetchingModels"] = "Error fetching models: {0}",
                ["ConfigLoadError"] = "Config load error: {0}",
                ["ConfigSaveError"] = "Config save error: {0}",
                ["ErrorLoadingXml"] = "Error loading XML: {0}",
                ["ErrorSavingXml"] = "Error saving XML: {0}",
                ["TranslationError"] = "Translation error: {0}",
                ["ImportError"] = "Import error: {0}",
                ["CacheSaveError"] = "Cache save failed: {0}",

                // === Confirm dialogs ===
                ["ConfirmTranslate"] = "Will translate {0} entries. This may take some time.",
                ["ConfirmClearCache"] = "Clear {0} cached translations?",
                ["ConfirmClearDict"] = "Clear {0} dictionary entries? This does not affect completed translations.",
                ["ConfirmDeleteProfile"] = "Are you sure you want to delete profile \"{0}\"? This cannot be undone.",
                ["ConfirmBatchReplace"] = "Replace complete! Modified {0} translations.\n\nClick Undo to revert.",

                // === Operation results ===
                ["TranslationComplete"] = "Translation complete: {0} success, {1} failed",
                ["TranslationStoppedResult"] = "Translation stopped: {0} success, {1} failed",
                ["NoEntriesNeedTranslation"] = "No entries need translation",
                ["ImportCsvDone"] = "Import complete!\nAdded: {0}\nUpdated: {1}\nSkipped: {2}",
                ["ImportJsonDone"] = "Import complete!\nAdded: {0}\nUpdated: {1}",
                ["BatchReplaceDone"] = "Batch replace complete - {0} replacements",
                ["UndoComplete"] = "Undo complete! Restored {0} translations",
                ["NothingToUndo"] = "Nothing to undo",
                ["ModelsFetchSuccess"] = "Found {0} models with rate limits, {1} with pricing info, {2} with limits",

                // === File dialog titles ===
                ["SelectXmlFile"] = "Select XML Localization File",
                ["SaveXmlFile"] = "Save XML Localization File",
                ["ImportDictTitle"] = "Import Translation Dictionary",

                // === Input dialog labels ===
                ["BatchReplaceDialogTitle"] = "Batch Replace",
                ["SearchTermLabel"] = "Search Term",
                ["ReplaceWithLabel"] = "Replace With",

                // === Log messages ===
                ["LogAutoLoad"] = "Auto-loaded last file: {0}",
                ["LogStarted"] = "Program started",
                ["LogConfigLoaded"] = "Config loaded - API key: ...",
                ["LogConfigSaved"] = "Config saved",
                ["LogCacheLoaded"] = "Cache loaded - {0} entries",
                ["LogFileNotFound"] = "{0} not found",
                ["LogXmlLoaded"] = "XML loaded - {0} entries",
                ["LogTranslationMerged"] = "Translation merged - {0}/{1} entries matched",
                ["LogXmlSaved"] = "XML saved to {0} - {1} entries",
                ["LogBatchCost"] = "Batch translation cost: ${0} ({1} entries, input: {2} chars, output: {3} chars)",
                ["LogSingleCost"] = "Translation cost: ${0} (input: {1} chars, output: {2} chars)",
                ["LogParseError"] = "Batch parse error: {0}",
                ["LogRateLimit429"] = "Rate limit (HTTP 429), waiting {0}s before retry {1}/{2}",
                ["LogRateLimitExhausted"] = "Translation failed after {0} retries: rate limit",
                ["LogRetryError"] = "Error, waiting {0}s before retry: {1}",
                ["LogTranslationFailed"] = "Translation error after {0} retries: {1}",
                ["LogModelInfo"] = "{0}: {1} (input: {2}, output: {3} tokens)",
                ["LogRateLimitEstimate"] = "{0} estimated rate limit: {1}/min, {2}/day",
                ["LogModelsFound"] = "Found {0} models, {1} with pricing, {2} with limits",
                ["LogDeepSeekModels"] = "Static model list loaded: {0} models",
                ["LogRateLimitReached"] = "Rate limit reached ({0}/min), waiting {1}s",
                ["LogOptimalDelay"] = "{0} optimal delay: {1}s ({2} remaining requests/min)",
                ["LogGenericPricing"] = "Using generic pricing for {0} - refresh models for accurate pricing",
                ["LogSettingsUpdated"] = "Settings updated - Provider: {0}, Model: {1}, Language: {2}, Expert: {3}",
                ["LogNoTranslationNeeded"] = "No entries need translation",
                ["LogBatchStart"] = "Starting batch translation: {0} entries, {1} batches{2}",
                ["LogBatchModel"] = "Model: {0} (rate limit options checked)",
                ["LogBatchCancelled"] = "Translation cancelled at batch {0}/{1}",
                ["LogBatchProgress"] = "Processing batch {0}/{1}: {2} entries",
                ["LogBatchFails"] = "Failed {0} entries: {1}",
                ["LogBatchDone"] = "Batch {0}/{1} complete: {2} success, {3} failed | Progress saved",
                ["LogCacheSaved"] = "Cache saved",
                ["LogTranslationDone"] = "{0}",
                ["LogTipHeader"] = "Tips to reduce failures:",
                ["LogTip1"] = "  - Wait a few seconds and retry",
                ["LogTip2"] = "  - Reduce batch size in code (currently 50)",
                ["LogTip3"] = "  - Decrease number of concurrent threads",
                ["LogEfficiency"] = "Translation efficiency: {0}% ({1}/{2})",
                ["LogBatchEfficiency"] = "Batch efficiency: {0} API calls instead of {1} (saved {2} calls)",
                ["LogRateLimitStatus"] = "Rate limit status: {0}/{1} used this minute",
                ["LogTranslationCancelled"] = "Translation cancelled",
                ["LogProgressSaveError"] = "Progress save failed: {0}",
                ["LogCrashRecovery"] = "Restored {0} translations from crash recovery file",
                ["LogRecoveryError"] = "Recovery file error: {0}",
                ["LogProgressDeleteError"] = "Progress file delete failed: {0}",
                ["LogCacheCleared"] = "Cache cleared",
                ["LogCsvImported"] = "CSV dictionary imported - Added {0}, Updated {1}",
                ["LogJsonImported"] = "JSON dictionary imported - Added {0}, Updated {1}",
                ["LogDictApplied"] = "Dictionary matched {0} existing entries",
                ["LogDictCleared"] = "Dictionary cleared",
                ["LogBatchReplace"] = "Batch replace complete - {0} replacements",
                ["LogUndo"] = "Undo applied - last batch replace reverted",
                ["LogCacheUpdated"] = "Cache updated - {0} entries",
                ["LogCacheWriteError"] = "Cache write error: {0}",
                ["LogCleared"] = "Log cleared",
                ["LogPaused"] = "Translation paused",
                ["LogResumed"] = "Translation resumed",
                ["LogStopped"] = "Translation stopped",
                ["LogExpertProfile"] = "Expert config: {0}",
                ["LogAutoRefreshModels"] = "Auto-refreshing available models...",
                ["LogAutoModelsLoaded"] = "Auto-loaded {0} models",
                ["LogAutoModelSelected"] = "Auto-selected model: {0}",
                ["LogClearedTranslation"] = "Cleared {0} translations",
                ["StatusStoppedResult"] = "Translation stopped: {0} success, {1} failed",
                ["StatusBatchComplete"] = "Batch translation complete: {0} success, {1} failed",

                // === Filter bar labels ===
                ["FilterLabel"] = "Filter",
                ["FilterKeyColumn"] = "Key",
                ["FilterOriginalColumn"] = "Original",
                ["FilterTranslationColumn"] = "Translation",
                ["FilterClear"] = "Clear",
                ["TotalCount"] = "{0} total entries",

                // === Settings window ===
                ["SettingsSubtitle"] = "Configure AI translation and expert profiles",
                ["GeneralSettings"] = "General Settings",
                ["ExpertProfiles"] = "Expert Profiles",
                ["AiProviderLabel"] = "AI Provider",
                ["SelectAiProvider"] = "Select AI translation service provider",
                ["EnterYourApiKey"] = "Enter your API key",
                ["SavedProfiles"] = "Saved Profiles",
                ["AddProfile"] = "Add Profile",
                ["ProfileEditTitle"] = "Edit Profile",
                ["ProfileNameLabel"] = "Profile Name",
                ["ProfileDescLabel"] = "Description",
                ["ProfileContextLabel"] = "Context & Thinking Instructions",
                ["ProfileContextHelp"] = "Tell AI how to understand this domain. Use {LANGUAGE} for target language.",
                ["ProfileGlossaryLabel"] = "Glossary",
                ["ProfileGlossaryHelp"] = "One term per line. Format: source = translated term",
                ["SaveProfileBtn"] = "Save",
                ["ExpertSystemTitle"] = "Expert Profile System",
                ["ExpertSystemDesc"] = "Expert profiles let you define domain-specific knowledge. Each contains thinking instructions and a terminology glossary. When activated, this knowledge is injected into every translation request for maximum accuracy.",
                ["BatchLabel"] = "Batch",
                ["AppName"] = "XML AI Translator",

                // === Find bar ===
                ["FindLabel"] = "Find:",
                ["FindNoMatch"] = "No matches",
                ["FindMatchCount"] = "{0} matches",
                ["FindPrevious"] = "Prev",
                ["FindNext"] = "Next",

                // === Context menu ===
                ["CtxCopyKey"] = "Copy Key",
                ["CtxCopyOriginal"] = "Copy Original",
                ["CtxCopyTranslation"] = "Copy Translation",
                ["CtxClearTranslation"] = "Clear Translation",
                ["CtxTranslateSelected"] = "Translate Selected",
                ["CtxEvaluate"] = "AI Evaluate",
                ["CtxVote"] = "Agent Vote",
                ["CtxSelectAll"] = "Select All",
                ["CtxSelectNone"] = "Select None",
                ["CtxInvertSelection"] = "Invert Selection",

                // === Prompt messages ===
                ["MsgPrompt"] = "Prompt",
                ["SelectFirstToTranslate"] = "Please select entries to translate first",
                ["EnterApiKeyFirstMsg"] = "Please enter an API key first",

                // === Stats dialog ===
                ["StatsTitle"] = "Statistics",
                ["StatsInfo"] = "Total: {0}\nTranslated: {1}\nUntranslated: {2}\nProgress: {3:F1}%\n\nDictionary: {4} entries | Hits: {5}\nCache: {6} | Hits: {7}\nAPI calls: {8}",

                // === ToolTips ===
                ["TipSaveAs"] = "Save As...",
                ["TipQuickSave"] = "Quick Save (Ctrl+S) - Save to currently opened file",
                ["TipImportDict"] = "Import CSV/JSON dictionary",
                ["TipBatchReplace"] = "Batch search and replace translations",
                ["TipUndo"] = "Undo last batch replace",
                ["TipFilterKey"] = "Filter by Key (e.g. TEXT_TOOLTIP_...)",
                ["TipFilterOriginal"] = "Filter by original text",
                ["TipFilterTranslation"] = "Filter by translation text",

                // === Dialogs ===
                ["OK"] = "OK",
                ["FileTypeTitle"] = "Select File Type",
                ["FileTypePrompt"] = "Is this file source or translation?",
                ["SourceFile"] = "Source",
                ["TranslationFile"] = "Translation",

                // === Custom prompt help ===
                ["CustomPromptSyntaxHelp"] = "Customize the AI translation prompt. Available variables: {LANGUAGE} (target language), {CONTEXT} (content type), {TEXTS} (data to translate), {EXPERT_CONTEXT} (expert profile knowledge, auto-replaced), {GLOSSARY} (glossary terms, auto-injected), {MIXED_SOURCE_NOTE} (correction directive for mixed batches).",

                // === Quick tips ===
                ["QuickTipsTitle"] = "Quick Tips",
                ["QuickTipsContent"] = "• Supports 8 AI providers (Google, DeepSeek, Doubao, Qianwen, Zhipu, Moonshot, Wenxin, Xunfei)\n• Each provider offers multiple models to choose from\n• Batch translation with automatic concurrency\n• Glossary auto-injection for consistent translations\n• Translation cache reduces API costs\n• AI evaluation and multi-agent voting for quality\n• 30 target languages supported\n• Expert profiles inject domain knowledge for specialized translation",

                // === Misc labels ===
                ["CostLabel"] = "Cost",
                ["LoadModelRefreshHint"] = "(click Refresh to load full list)",

                // === AI Evaluation & Voting ===
                ["EvaluateBtn"] = "Evaluate",
                ["VoteBtn"] = "Agent Vote",
                ["EvaluateToolTip"] = "AI quality evaluation (0-10 score)",
                ["VoteToolTip"] = "Multi-agent voting for best translation",
                ["EvalEvaluating"] = "Evaluating...",
                ["EvalVoting"] = "Voting...",
                ["EvalFailed"] = "Evaluation failed",
                ["VoteFailed"] = "Voting failed",
                ["Best"] = "Best",
                ["NoTranslatedToEvaluate"] = "No translated entries to evaluate",
                ["NoTranslatedToVote"] = "No translated entries for voting",
                ["LogEvaluating"] = "Evaluating: {0}",
                ["LogVoting"] = "Agent voting: {0}",
                ["LogEvalResult"] = "{0}: {1:F1}/10 — {2}",
                ["LogEvalSuggestion"] = "Suggestion: {0}",
                ["LogVoteConsensus"] = "{0}",
                ["LogVoteAgentDetail"] = "  {0}: {1:F1}/10 — {2}",
                ["EvalScoreToolTip"] = "Score: {0:F1}/10\nExplanation: {1}\nSuggestion: {2}",
                ["VoteResultToolTip"] = "Average Score: {0:F1}/10\nConsensus: {1}\nAgents: {2} votes",

                // === Target language names ===
                ["Lang_Turkish"] = "Turkish",
                ["Lang_Spanish"] = "Spanish",
                ["Lang_French"] = "French",
                ["Lang_German"] = "German",
                ["Lang_Italian"] = "Italian",
                ["Lang_Portuguese"] = "Portuguese",
                ["Lang_Russian"] = "Russian",
                ["Lang_Japanese"] = "Japanese",
                ["Lang_Korean"] = "Korean",
                ["Lang_Chinese_Simplified"] = "Chinese (Simplified)",
                ["Lang_Chinese_Traditional"] = "Chinese (Traditional)",
                ["Lang_Arabic"] = "Arabic",
                ["Lang_Hindi"] = "Hindi",
                ["Lang_Dutch"] = "Dutch",
                ["Lang_Swedish"] = "Swedish",
                ["Lang_Norwegian"] = "Norwegian",
                ["Lang_Danish"] = "Danish",
                ["Lang_Finnish"] = "Finnish",
                ["Lang_Polish"] = "Polish",
                ["Lang_Czech"] = "Czech",
                ["Lang_Hungarian"] = "Hungarian",
                ["Lang_Romanian"] = "Romanian",
                ["Lang_Greek"] = "Greek",
                ["Lang_Bulgarian"] = "Bulgarian",
                ["Lang_Ukrainian"] = "Ukrainian",
                ["Lang_Thai"] = "Thai",
                ["Lang_Vietnamese"] = "Vietnamese",
                ["Lang_Indonesian"] = "Indonesian",
                ["Lang_Hebrew"] = "Hebrew",
                ["Lang_Persian"] = "Persian",
            };

            // =========================================================================
            // Chinese (Simplified)
            // =========================================================================
            _translations["zh"] = new Dictionary<string, string>
            {
                // === Window titles ===
                ["WindowTitle"] = "XML AI 翻译器 by Veloxcity",
                ["SettingsTitle"] = "设置 - XML AI 翻译器",

                // === Main UI buttons ===
                ["Load"] = "加载",
                ["Save"] = "保存",
                ["Settings"] = "设置",
                ["Stats"] = "统计",
                ["ImportDict"] = "导入对照表",
                ["ClearDict"] = "清除对照表",
                ["BatchReplace"] = "批量替换",
                ["Undo"] = "撤销",
                ["QuickSave"] = "快速保存",
                ["NoExpertDefault"] = "无专家（默认）",

                // === AI Translation buttons ===
                ["TranslateSelected"] = "翻译选中",
                ["TranslateAll"] = "全部翻译",
                ["Translate"] = "翻译",
                ["Pause"] = "暂停",
                ["Resume"] = "继续",
                ["Continue"] = "继续",
                ["Stop"] = "停止",
                ["ClearCache"] = "清除缓存",

                // === Section titles ===
                ["AITranslationCenter"] = "AI 翻译中心",
                ["TranslationData"] = "翻译数据",
                ["ActivityLog"] = "活动日志",

                // === Data Grid ===
                ["Select"] = "选择",
                ["Status"] = "状态",
                ["Key"] = "键",
                ["Original"] = "原文",
                ["Translation"] = "译文",

                // === Status bar ===
                ["Ready"] = "就绪",
                ["LoadedEntries"] = "已加载 {0} 条记录",
                ["MergedTranslations"] = "已合并 {0} 条译文",
                ["SavedEntries"] = "已保存 {0} 条记录至 {1}",
                ["NoEntriesForTranslation"] = "没有需要翻译的条目",
                ["TranslatingBatch"] = "正在翻译第 {0}/{1} 批 ({2} 条)...",
                ["WaitingRateLimit"] = "等待 {0} 秒后开始下一批（速率限制优化）...",
                ["TranslationPaused"] = "翻译已暂停",
                ["TranslationResumed"] = "翻译已继续",
                ["TranslationStopped"] = "翻译已停止",
                ["TranslationCancelled"] = "翻译已取消",
                ["NoUntranslatedEntries"] = "没有未翻译的条目",

                // === Cache/Glossary info ===
                ["CacheInfo"] = "缓存: {0} | 命中: {1} | API: {2}{3}",
                ["Glossary"] = "术语表",
                ["TipGlossary"] = "打开术语管理器",
                ["GlossaryInfo"] = "术语表: {0} | 命中: {1}",
                ["DictInfo"] = "对照表: {0} | 命中: {1}",
                ["ClearDict"] = "清除术语表",
                ["ConfirmClearDict"] = "确定要清除全部 {0} 个术语吗？\n此操作不可撤销！",
                ["LogDictCleared"] = "术语表已清除",

                // === Glossary Window ===
                ["GlossaryWindowTitle"] = "术语管理器",
                ["GlossaryAdd"] = "新增",
                ["GlossaryEdit"] = "编辑",
                ["GlossaryDelete"] = "删除",
                ["GlossaryImport"] = "导入",
                ["GlossaryExport"] = "导出",
                ["GlossaryMergeProfile"] = "合并专家配置",
                ["GlossaryDetectConflicts"] = "冲突检测",
                ["GlossaryRefresh"] = "刷新",
                ["GlossaryClose"] = "关闭",
                ["GlossaryCancel"] = "取消",
                ["GlossarySave"] = "保存",
                ["GlossaryMerge"] = "合并",
                ["GlossaryFilterAll"] = "全部",
                ["GlossaryColEnglish"] = "英文（源）",
                ["GlossaryColChinese"] = "中文（目标）",
                ["GlossaryColCategory"] = "分类",
                ["GlossaryColStatus"] = "状态",
                ["GlossaryColTags"] = "标签",
                ["GlossaryStatus_confirmed"] = "已确认",
                ["GlossaryStatus_pending"] = "待审核",
                ["GlossaryStatus_rejected"] = "已拒绝",
                ["GlossaryTermCount"] = "{0} 个术语（显示 {1}）",
                ["GlossaryStatusSummary"] = "{0} 已确认, {1} 待审核, {2} 已拒绝",
                ["GlossaryCategoryCount"] = "{0} 个分类",
                ["GlossarySelectHint"] = "请先选择一个术语",
                ["GlossaryDeleteConfirm"] = "确定要删除术语 \"{0}\" 吗？",
                ["GlossaryImportResult"] = "导入完成: 新增 {0}, 更新 {1}, 跳过 {2}",
                ["GlossaryExportResult"] = "成功导出 {0} 个术语",
                ["GlossaryNoProfiles"] = "没有可用的专家配置。请先在设置中创建。",
                ["GlossaryMergeProfileHelp"] = "选择一个要合并术语的专家配置:",
                ["GlossarySelectProfileHint"] = "请先选择一个配置",
                ["GlossaryMergeResult"] = "合并完成: 新增 {0} 个术语, 更新 {1} 个",
                ["GlossaryNoConflicts"] = "未检测到术语冲突。所有翻译与术语表一致！",
                ["GlossaryConflictsTitle"] = "术语冲突",
                ["GlossaryConflictCount"] = "检测到 {0} 个术语冲突",
                ["GlossaryExpectedTranslation"] = "期望译文",
                ["GlossaryActualTranslation"] = "实际译文",
                ["GlossaryEntryKey"] = "条目 Key",
                ["GlossaryRequiredFields"] = "英文和中文字段为必填。",
                ["GlossaryCompare"] = "对比",

                // === Filter ===
                ["FilterKey"] = "筛选 Key",
                ["FilterOriginal"] = "筛选原文",
                ["FilterTranslation"] = "筛选译文",
                ["ClearFilter"] = "清除筛选",
                ["FilteredCount"] = "筛选 {0} / {1} 条",

                // === Activity Log ===
                ["RealTime"] = "实时",
                ["AutoScroll"] = "自动滚动",
                ["ClearLog"] = "清除日志",

                // === Settings ===
                ["AIConfiguration"] = "AI 配置",
                ["ConfigureSettings"] = "配置你的 AI 翻译设置",
                ["APIKey"] = "API 密钥",
                ["EnterAPIKey"] = "输入你的 Google Gemini API 密钥",
                ["AIModel"] = "AI 模型",
                ["SelectModel"] = "选择一个 AI 模型（含速率限制和价格信息）",
                ["Refresh"] = "刷新",
                ["Loading"] = "加载中...",
                ["TargetLanguage"] = "目标语言",
                ["SelectTargetLanguage"] = "选择翻译目标语言",
                ["ProgramLanguage"] = "界面语言",
                ["SelectProgramLanguage"] = "选择程序界面语言",
                ["CustomPrompt"] = "自定义提示词",
                ["CustomPromptHelp"] = "自定义 AI 翻译提示词。可用变量：{LANGUAGE}、{CONTEXT}、{TEXTS}、{EXPERT_CONTEXT}、{GLOSSARY}、{MIXED_SOURCE_NOTE}。",
                ["Reset"] = "重置",
                ["QuickTips"] = "快速提示",
                ["TipAPIKey"] = "从所选 AI 提供商平台获取 API 密钥",
                ["TipModels"] = "各提供商提供多种模型，可按需选择",
                ["TipRateLimits"] = "速率限制会根据模型自动优化",
                ["TipCache"] = "翻译缓存可降低 API 费用",
                ["TipLanguages"] = "支持 30 种目标语言翻译",
                ["SaveApply"] = "保存并应用",
                ["Cancel"] = "取消",
                ["NewProfile"] = "新建配置",
                ["EditProfile"] = "编辑: {0}",

                // === MessageBox titles ===
                ["MsgError"] = "错误",
                ["MsgSuccess"] = "成功",
                ["MsgConfirm"] = "确认",
                ["MsgWarning"] = "警告",
                ["MsgTip"] = "提示",

                // === MessageBox messages ===
                ["EnterAPIKeyFirst"] = "请输入 API 密钥",
                ["SelectModelFirst"] = "请选择一个模型",
                ["EnterProfileName"] = "请输入配置名称。",
                ["SelectEntriesFirst"] = "请先选择要翻译的条目",
                ["SearchTermEmpty"] = "搜索词不能为空",
                ["NoModelsFound"] = "未找到模型，请检查 API 密钥。",
                ["ModelsFoundSuccess"] = "找到 {0} 个模型（含速率限制信息）",
                ["ErrorFetchingModels"] = "获取模型出错: {0}",
                ["ConfigLoadError"] = "配置加载错误: {0}",
                ["ConfigSaveError"] = "配置保存错误: {0}",
                ["ErrorLoadingXml"] = "加载 XML 出错: {0}",
                ["ErrorSavingXml"] = "保存 XML 出错: {0}",
                ["TranslationError"] = "翻译出错: {0}",
                ["ImportError"] = "导入出错: {0}",
                ["CacheSaveError"] = "缓存保存失败: {0}",

                // === Confirm dialogs ===
                ["ConfirmTranslate"] = "将翻译 {0} 个条目，可能需要一些时间。",
                ["ConfirmClearCache"] = "清除 {0} 条缓存翻译？",
                ["ConfirmClearDict"] = "清除 {0} 条对照表数据？不影响已完成的翻译。",
                ["ConfirmDeleteProfile"] = "确定要删除配置 \"{0}\" 吗？此操作不可撤销。",
                ["ConfirmBatchReplace"] = "替换完成！共修改 {0} 条译文\n\n可点击撤销按钮恢复",

                // === Operation results ===
                ["TranslationComplete"] = "翻译完成: {0} 成功, {1} 失败",
                ["TranslationStoppedResult"] = "翻译已停止: {0} 成功, {1} 失败",
                ["NoEntriesNeedTranslation"] = "没有需要翻译的条目",
                ["ImportCsvDone"] = "导入完成！\n新增: {0}\n更新: {1}\n跳过: {2}",
                ["ImportJsonDone"] = "导入完成！\n新增: {0}\n更新: {1}",
                ["BatchReplaceDone"] = "批量替换完成 - 替换 {0} 处",
                ["UndoComplete"] = "已撤销！恢复了 {0} 条译文",
                ["NothingToUndo"] = "没有可撤销的操作",
                ["ModelsFetchSuccess"] = "找到 {0} 个模型, 含费率信息的 {1} 个, 含速率限制的 {2} 个",

                // === File dialog titles ===
                ["SelectXmlFile"] = "选择 XML 本地化文件",
                ["SaveXmlFile"] = "保存 XML 本地化文件",
                ["ImportDictTitle"] = "导入翻译对照表",

                // === Input dialog labels ===
                ["BatchReplaceDialogTitle"] = "批量替换",
                ["SearchTermLabel"] = "搜索词：",
                ["ReplaceWithLabel"] = "替换为：",

                // === Log messages ===
                ["LogAutoLoad"] = "自动加载上次文件: {0}",
                ["LogStarted"] = "程序已启动",
                ["LogConfigLoaded"] = "配置已加载 - API 密钥: ...",
                ["LogConfigSaved"] = "配置已保存",
                ["LogCacheLoaded"] = "缓存已加载 - {0} 条",
                ["LogFileNotFound"] = "{0} 未找到",
                ["LogXmlLoaded"] = "XML 已加载 - {0} 条记录",
                ["LogTranslationMerged"] = "译文已合并 - {0}/{1} 条匹配",
                ["LogXmlSaved"] = "XML 已保存至 {0} - {1} 条记录",
                ["LogBatchCost"] = "批量翻译费用: ${0} ({1} 条, 输入: {2} 字符, 输出: {3} 字符)",
                ["LogSingleCost"] = "翻译费用: ${0} (输入: {1} 字符, 输出: {2} 字符)",
                ["LogParseError"] = "解析批量回复出错: {0}",
                ["LogRateLimit429"] = "速率限制 (HTTP 429), 等待 {0}s 后重试 {1}/{2}",
                ["LogRateLimitExhausted"] = "翻译失败 ({0} 次重试后): 速率限制",
                ["LogRetryError"] = "出错, {0}s 后重试: {1}",
                ["LogTranslationFailed"] = "翻译错误 ({0} 次重试后): {1}",
                ["LogModelInfo"] = "{0}: {1} (输入: {2}, 输出: {3} 令牌)",
                ["LogRateLimitEstimate"] = "{0} 预估速率限制: {1}/分钟, {2}/天",
                ["LogModelsFound"] = "找到 {0} 个模型, 含费率信息的 {1} 个, 含速率限制的 {2} 个",
                ["LogDeepSeekModels"] = "静态模型列表已加载: {0} 个模型",
                ["LogRateLimitReached"] = "达到速率限制 ({0}/分钟), 等待 {1}s",
                ["LogOptimalDelay"] = "{0} 最优延迟: {1}s ({2} 剩余请求/分钟)",
                ["LogGenericPricing"] = "使用 {0} 的通用定价 - 刷新模型获取准确价格",
                ["LogSettingsUpdated"] = "设置已更新 - 提供商: {0}, 模型: {1}, 语言: {2}, 专家: {3}",
                ["LogNoTranslationNeeded"] = "没有需要翻译的条目",
                ["LogBatchStart"] = "开始批量翻译: {0} 条, {1} 个批次{2}",
                ["LogBatchModel"] = "模型: {0}（已检查速率限制选项）",
                ["LogBatchCancelled"] = "翻译在批次 {0}/{1} 停止",
                ["LogBatchProgress"] = "处理批次 {0}/{1}: {2} 条",
                ["LogBatchFails"] = "失败 {0} 条: {1}",
                ["LogBatchDone"] = "批次 {0}/{1} 完成: {2} 成功, {3} 失败 | 进度已保存",
                ["LogCacheSaved"] = "缓存已保存",
                ["LogTranslationDone"] = "{0}",
                ["LogTipHeader"] = "减少失败的小技巧:",
                ["LogTip1"] = "  - 稍等几秒后重试",
                ["LogTip2"] = "  - 减小代码中的批次大小（当前 50）",
                ["LogTip3"] = "  - 减少并发线程数",
                ["LogEfficiency"] = "翻译效率: {0}% ({1}/{2})",
                ["LogBatchEfficiency"] = "批量效率: {0} 次 API 调用替代 {1} 次 (节省 {2} 次调用)",
                ["LogRateLimitStatus"] = "速率限制状态: {0}/{1} 本分钟已用",
                ["LogTranslationCancelled"] = "翻译已取消",
                ["LogProgressSaveError"] = "进度保存失败: {0}",
                ["LogCrashRecovery"] = "已从崩溃恢复文件恢复 {0} 条翻译",
                ["LogRecoveryError"] = "恢复进度文件出错: {0}",
                ["LogProgressDeleteError"] = "进度文件删除失败: {0}",
                ["LogCacheCleared"] = "缓存已清除",
                ["LogCsvImported"] = "CSV 对照表导入完成 - 新增 {0}, 更新 {1}",
                ["LogJsonImported"] = "JSON 对照表导入完成 - 新增 {0}, 更新 {1}",
                ["LogDictApplied"] = "对照表匹配到 {0} 条已有条目",
                ["LogDictCleared"] = "对照表已清除",
                ["LogBatchReplace"] = "批量替换完成 - 替换 {0} 处",
                ["LogUndo"] = "已撤销上一次批量替换",
                ["LogCacheUpdated"] = "缓存已更新 - {0} 条",
                ["LogCacheWriteError"] = "缓存写入错误: {0}",
                ["LogCleared"] = "日志已清除",
                ["LogPaused"] = "翻译已暂停",
                ["LogResumed"] = "翻译已继续",
                ["LogStopped"] = "翻译已停止",
                ["LogExpertProfile"] = "专家配置: {0}",
                ["LogAutoRefreshModels"] = "正在自动刷新可用模型...",
                ["LogAutoModelsLoaded"] = "自动加载了 {0} 个模型",
                ["LogAutoModelSelected"] = "自动选择模型: {0}",
                ["LogClearedTranslation"] = "已清空 {0} 条译文",
                ["StatusStoppedResult"] = "翻译已停止: {0} 成功, {1} 失败",
                ["StatusBatchComplete"] = "批量翻译完成: {0} 成功, {1} 失败",

                // === Filter bar labels ===
                ["FilterLabel"] = "筛选",
                ["FilterKeyColumn"] = "Key",
                ["FilterOriginalColumn"] = "原文",
                ["FilterTranslationColumn"] = "译文",
                ["FilterClear"] = "清除",
                ["TotalCount"] = "共 {0} 条",

                // === Find bar ===
                ["FindLabel"] = "查找:",
                ["FindNoMatch"] = "无匹配",
                ["FindMatchCount"] = "{0} 个匹配",
                ["FindPrevious"] = "上一处",
                ["FindNext"] = "下一处",

                // === Context menu ===
                ["CtxCopyKey"] = "复制 Key",
                ["CtxCopyOriginal"] = "复制原文",
                ["CtxCopyTranslation"] = "复制译文",
                ["CtxClearTranslation"] = "清空译文",
                ["CtxTranslateSelected"] = "翻译选中项",
                ["CtxEvaluate"] = "AI 评估",
                ["CtxVote"] = "代理投票",
                ["CtxSelectAll"] = "全选",
                ["CtxSelectNone"] = "全不选",
                ["CtxInvertSelection"] = "反选",

                // === Prompt messages ===
                ["MsgPrompt"] = "提示",
                ["SelectFirstToTranslate"] = "请先选择要翻译的条目",
                ["EnterApiKeyFirstMsg"] = "请先输入 API 密钥",

                // === Stats dialog ===
                ["StatsTitle"] = "统计",
                ["StatsInfo"] = "总条目: {0}\n已翻译: {1}\n未翻译: {2}\n进度: {3:F1}%\n\n对照表: {4} 词条 | 命中: {5}\n缓存: {6} | 命中: {7}\nAPI 调用: {8}",

                // === ToolTips ===
                ["TipSaveAs"] = "另存为...",
                ["TipQuickSave"] = "快速保存 (Ctrl+S) - 保存到当前打开的文件",
                ["TipImportDict"] = "导入 CSV/JSON 对照表",
                ["TipBatchReplace"] = "批量搜索替换译文内容",
                ["TipUndo"] = "撤销上一次批量替换",
                ["TipFilterKey"] = "筛选 Key（如 TEXT_TOOLTIP_...）",
                ["TipFilterOriginal"] = "筛选原文",
                ["TipFilterTranslation"] = "筛选译文",

                // === Dialogs ===
                ["OK"] = "确定",
                ["FileTypeTitle"] = "选择文件类型",
                ["FileTypePrompt"] = "这个文件是原文还是译文？",
                ["SourceFile"] = "原文",
                ["TranslationFile"] = "译文",

                // === Settings window ===
                ["SettingsSubtitle"] = "配置 AI 翻译和专家配置",
                ["GeneralSettings"] = "常规设置",
                ["ExpertProfiles"] = "专家配置",
                ["AiProviderLabel"] = "AI 提供商",
                ["SelectAiProvider"] = "选择 AI 翻译服务提供商",
                ["EnterYourApiKey"] = "输入你的 API 密钥",
                ["SavedProfiles"] = "已保存的配置",
                ["AddProfile"] = "新建配置",
                ["ProfileEditTitle"] = "编辑配置",
                ["ProfileNameLabel"] = "配置名称",
                ["ProfileDescLabel"] = "描述",
                ["ProfileContextLabel"] = "上下文 & 思考指令",
                ["ProfileContextHelp"] = "告诉 AI 如何理解这个领域。可以用 {LANGUAGE} 指代目标语言。",
                ["ProfileGlossaryLabel"] = "术语对照表",
                ["ProfileGlossaryHelp"] = "每行一条。格式：英文术语 = 目标翻译（例如：Jedi = 绝地）",
                ["SaveProfileBtn"] = "保存",
                ["ExpertSystemTitle"] = "专家配置系统",
                ["ExpertSystemDesc"] = "专家配置让你可以为 AI 翻译器定义特定领域的知识。每个配置包含思考指令和术语对照表。激活后，这些知识会自动注入每次翻译请求，确保专有名词和领域术语的翻译准确性。",
                ["BatchLabel"] = "批次",
                ["AppName"] = "XML AI 翻译器",

                // === Custom prompt help ===
                ["CustomPromptSyntaxHelp"] = "自定义 AI 翻译提示词。可用变量：{LANGUAGE}（目标语言）、{CONTEXT}（内容类型）、{TEXTS}（待翻译数据）、{EXPERT_CONTEXT}（专家配置知识，自动替换）、{GLOSSARY}（术语对照表，自动注入）、{MIXED_SOURCE_NOTE}（混合批次修正指令）。",

                // === Quick tips ===
                ["QuickTipsTitle"] = "快速提示",
                ["QuickTipsContent"] = "• 支持 8 个主流 AI 提供商（Google/DeepSeek/豆包/千问/智谱/Kimi/文心/讯飞）\n• 各提供商提供多种模型，可按需选择\n• 批量翻译自动并发处理，提升效率\n• 术语对照表自动注入，确保翻译一致性\n• 翻译缓存降低 API 费用\n• AI 评估与多代理投票提升翻译质量\n• 支持 30 种目标语言翻译\n• 专家配置可注入领域知识，增强专业翻译",

                // === Misc labels ===
                ["CostLabel"] = "费用",
                ["LoadModelRefreshHint"] = "（点击刷新加载完整列表）",

                // === AI Evaluation & Voting ===
                ["EvaluateBtn"] = "AI 评估",
                ["VoteBtn"] = "代理投票",
                ["EvaluateToolTip"] = "AI 翻译质量评估（0-10 评分）",
                ["VoteToolTip"] = "多代理投票选出最佳译文",
                ["EvalEvaluating"] = "评估中...",
                ["EvalVoting"] = "投票中...",
                ["EvalFailed"] = "评估失败",
                ["VoteFailed"] = "投票失败",
                ["Best"] = "最佳",
                ["NoTranslatedToEvaluate"] = "没有已翻译的条目可评估",
                ["NoTranslatedToVote"] = "没有已翻译的条目可投票",
                ["LogEvaluating"] = "正在评估: {0}",
                ["LogVoting"] = "代理投票: {0}",
                ["LogEvalResult"] = "{0}: {1:F1}/10 — {2}",
                ["LogEvalSuggestion"] = "改进建议: {0}",
                ["LogVoteConsensus"] = "{0}",
                ["LogVoteAgentDetail"] = "  {0}: {1:F1}/10 — {2}",
                ["EvalScoreToolTip"] = "评分: {0:F1}/10\n解释: {1}\n建议: {2}",
                ["VoteResultToolTip"] = "平均分: {0:F1}/10\n共识: {1}\n投票数: {2}",

                // === Target language names ===
                ["Lang_Turkish"] = "土耳其语",
                ["Lang_Spanish"] = "西班牙语",
                ["Lang_French"] = "法语",
                ["Lang_German"] = "德语",
                ["Lang_Italian"] = "意大利语",
                ["Lang_Portuguese"] = "葡萄牙语",
                ["Lang_Russian"] = "俄语",
                ["Lang_Japanese"] = "日语",
                ["Lang_Korean"] = "韩语",
                ["Lang_Chinese_Simplified"] = "简体中文",
                ["Lang_Chinese_Traditional"] = "繁体中文",
                ["Lang_Arabic"] = "阿拉伯语",
                ["Lang_Hindi"] = "印地语",
                ["Lang_Dutch"] = "荷兰语",
                ["Lang_Swedish"] = "瑞典语",
                ["Lang_Norwegian"] = "挪威语",
                ["Lang_Danish"] = "丹麦语",
                ["Lang_Finnish"] = "芬兰语",
                ["Lang_Polish"] = "波兰语",
                ["Lang_Czech"] = "捷克语",
                ["Lang_Hungarian"] = "匈牙利语",
                ["Lang_Romanian"] = "罗马尼亚语",
                ["Lang_Greek"] = "希腊语",
                ["Lang_Bulgarian"] = "保加利亚语",
                ["Lang_Ukrainian"] = "乌克兰语",
                ["Lang_Thai"] = "泰语",
                ["Lang_Vietnamese"] = "越南语",
                ["Lang_Indonesian"] = "印尼语",
                ["Lang_Hebrew"] = "希伯来语",
                ["Lang_Persian"] = "波斯语",
            };
        }
    }
}
