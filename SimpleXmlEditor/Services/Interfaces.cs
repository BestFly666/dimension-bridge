using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.ExpertProfiles;

namespace SimpleXmlEditor.Services
{
    public interface IAiTranslationService : IDisposable
    {
        AIProvider CurrentProvider { get; set; }
        string ApiKey { get; set; }
        string Model { get; set; }
        string TargetLanguage { get; set; }
        HttpClient HttpClient { get; }
        
        ConcurrentDictionary<string, (double input, double output)> ModelPricing { get; }
        ConcurrentDictionary<string, (int requestsPerMinute, int requestsPerDay, int tokensPerMinute)> ModelLimits { get; }
        ConcurrentQueue<DateTime> RecentRequests { get; }
        
        event Action<string> LogMessage;
        
        Task<List<string>> FetchAvailableModelsAsync(string apiKey, AIProvider? provider = null);
        Task<string> TranslateBatchAsync(string prompt, int maxRetries = 3, bool? disableThinking = null);
        double CalculateCost(int inputChars, int outputChars, string modelName);
        int GetModelTokenLimit(string modelName);
        void TrackRequest();
    }

    public interface IXmlRepository
    {
        XmlFormat CurrentFormat { get; }
        
        event Action<string> LogMessage;
        
        List<LocalizationEntry> LoadXml(string fileName, bool isTranslationFile = false);
        void SaveXml(string fileName, List<LocalizationEntry> entries);
    }

    public interface IConfigService
    {
        ConcurrentDictionary<string, string> Cache { get; }
        AppConfig Config { get; }
        
        event Action<string> LogMessage;
        
        void LoadConfig();
        void SaveConfig();
        void SaveCache();
        void SaveScoreCache();
        void ClearScoreCache();
        void SyncScoresToCache(IEnumerable<LocalizationEntry> entries);
        int RestoreScores(IEnumerable<LocalizationEntry> entries);
        void SyncEntriesToCache(IEnumerable<LocalizationEntry> entries);
        /// <summary>对称写入缓存双键（Key + MD5(原文)），保持各写入路径一致。</summary>
        void SetCacheEntry(string key, string originalText, string translation);
        Task SaveTranslationProgressAsync(IEnumerable<LocalizationEntry> entries);
        int RestoreTranslationProgress(IEnumerable<LocalizationEntry> entries);
        void DeleteProgressFile();
        string GetCacheKey(string text);
        void UpdateConfig(Action<AppConfig> updater);
        
        // Secure API key management
        void SetApiKey(string apiKey);
        string GetApiKey();
        void SetEvaluationApiKey(string apiKey);
        string GetEvaluationApiKey();
        void SaveEvaluationModels(List<(string Provider, string Model, string ApiKey)> models);
        string GetEvaluationModelKey(EvaluationModelConfig model);
        bool MigrateLegacyApiKey();
    }

    public interface IGlossaryManager
    {
        ConcurrentDictionary<string, GlossaryTerm> Terms { get; }
        int Count { get; }
        bool TryGetValue(string sourceText, out string translated);
        Dictionary<string, string> GetGlossaryContextTerms(List<LocalizationEntry> entries);
        (int added, int updated, int skipped) ImportCsv(string filePath);
        (int added, int updated) ImportJson(string filePath);
        void SetEntry(string source, string translation, string category = "", string status = "confirmed", string tags = "");
        void SetTerm(GlossaryTerm term);
        bool RemoveEntry(string source);
        void Clear();
        void Load();
        List<GlossaryTerm> Search(string query);
        List<string> GetAllCategories();
        void ExportCsv(string filePath);
        void ExportJson(string filePath);
        (int added, int updated) MergeFromProfile(string profileName, Dictionary<string, string> profileGlossary);
        List<GlossaryConflict> DetectConflicts(
            IEnumerable<(string key, string source, string translation)> entries,
            Action<int, int> onProgress = null);
    }

    public interface IExpertProfileManager
    {
        List<ExpertProfile> Profiles { get; }
        string ActiveProfileName { get; set; }
        ExpertProfile ActiveProfile { get; }
        void LoadProfiles();
        void SaveProfiles();
        void AddProfile(ExpertProfile profile);
        void DeleteProfile(string name);
        ExpertProfile GetProfile(string name);
        void EnsureDefaultsExist();
    }

    /// <summary>
    /// 黑名单规则管理：两种匹配方式，命中即跳过翻译（不调用 API）。
    /// 1. Key 前缀匹配：Key 以任一前缀开头（Ordinal 大小写敏感）。
    /// 2. 原文精确匹配：原文文本与任一值完全相等（Ordinal 精确比较，避免误过滤）。
    /// 规则全局生效，持久化到 AppData 的 blacklist.json。
    /// </summary>
    public interface IBlacklistManager
    {
        /// <summary>当前 Key 前缀规则列表（只读视图）。</summary>
        IReadOnlyList<string> Prefixes { get; }
        /// <summary>当前原文精确匹配规则列表（只读视图）。</summary>
        IReadOnlyList<string> ExactOriginalTexts { get; }
        /// <summary>规则总数（前缀 + 原文）。</summary>
        int Count { get; }
        event Action<string> LogMessage;

        /// <summary>按 Key 前缀判断是否命中黑名单。</summary>
        bool IsBlocked(string key);
        /// <summary>按 Key 前缀 + 原文精确匹配判断是否命中（任一命中即 true）。</summary>
        bool IsBlocked(string key, string originalText);
        /// <summary>新增前缀规则（去重）。返回是否实际新增。</summary>
        bool AddPrefix(string prefix);
        /// <summary>删除前缀规则。返回是否实际删除。</summary>
        bool RemovePrefix(string prefix);
        /// <summary>新增原文精确匹配规则（去重）。返回是否实际新增。</summary>
        bool AddExactOriginalText(string text);
        /// <summary>删除原文精确匹配规则。返回是否实际删除。</summary>
        bool RemoveExactOriginalText(string text);
        /// <summary>清空全部规则并持久化。</summary>
        void Clear();
        void Load();
        void Save();
    }

    public interface ITranslationEvaluator
    {
        event Action<string> LogMessage;
        Task<EvaluationResult> EvaluateAsync(string originalText, string translatedText, string targetLanguage, string context = "");
        Task<VotingResult> VoteAsync(string originalText, string[] candidateTranslations, string targetLanguage, string context = "");
        /// <summary>Generate N alternative translation candidates for a single source text (for voting).</summary>
        Task<string[]> GenerateCandidatesAsync(string originalText, string targetLanguage, string context = "", int count = 2);
        /// <summary>Evaluate multiple entries in a single API call (batch acceleration).</summary>
        Task<List<EvaluationResult>> EvaluateBatchAsync(
            List<(string Key, string Original, string Translated)> items,
            string targetLanguage,
            string context = "",
            int batchSize = 20);
        /// <summary>Vote on multiple entries in a single API call (batch acceleration).</summary>
        Task<List<VotingResult>> VoteBatchAsync(
            List<(string Key, string Original, string[] Candidates)> items,
            string targetLanguage,
            string context = "",
            int batchSize = 10);
    }

    /// <summary>
    /// Plugin for supporting additional file formats beyond the built-in XML types.
    /// Each plugin reports its supported extensions, and provides Load/Save methods.
    /// </summary>
    public interface IFileFormatPlugin
    {
        string FormatName { get; }
        string[] FileExtensions { get; }
        List<LocalizationEntry> Load(string filePath);
        void Save(string filePath, List<LocalizationEntry> entries);
    }

    /// <summary>
    /// Plugin for post-processing translations after AI translation completes.
    /// Examples: formatting cleanup, consistency check, duplicate removal.
    /// </summary>
    public interface IPostProcessPlugin
    {
        string Name { get; }
        void Process(List<LocalizationEntry> entries, string sourceLanguage, string targetLanguage);
    }
}