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
        
        Dictionary<string, (double input, double output)> ModelPricing { get; }
        Dictionary<string, (int requestsPerMinute, int requestsPerDay, int tokensPerMinute)> ModelLimits { get; }
        ConcurrentQueue<DateTime> RecentRequests { get; }
        
        event Action<string> LogMessage;
        
        // Statistics callbacks (raised by the service itself for single-entry translation paths)
        event Action<int> CacheHit;
        event Action<int> ApiCallCounted;
        event Action<int, int> ApiCharsCounted; // (inputChars, outputChars)
        
        Task<List<string>> FetchAvailableModelsAsync(string apiKey, AIProvider? provider = null);
        Task<string> TranslateSingleAsync(string text, int maxRetries = 3);
        Task<string> TranslateBatchAsync(string prompt, int maxRetries = 3);
        double CalculateCost(int inputChars, int outputChars, string modelName);
        int CalculateOptimalDelay();
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
        void SyncScoresToCache(IEnumerable<LocalizationEntry> entries);
        int RestoreScores(IEnumerable<LocalizationEntry> entries);
        void SyncEntriesToCache(IEnumerable<LocalizationEntry> entries);
        void SaveTranslationProgress(IEnumerable<LocalizationEntry> entries);
        Dictionary<string, string> GetCacheForSave(IEnumerable<LocalizationEntry> entries);
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
        Dictionary<string, GlossaryTerm> Terms { get; }
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