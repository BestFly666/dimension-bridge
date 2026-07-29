using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

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
        void SyncEntriesToCache(IEnumerable<LocalizationEntry> entries);
        Dictionary<string, string> GetCacheForSave(IEnumerable<LocalizationEntry> entries);
        int RestoreTranslationProgress(IEnumerable<LocalizationEntry> entries);
        void DeleteProgressFile();
        string GetCacheKey(string text);
        void UpdateConfig(Action<AppConfig> updater);
        
        // Secure API key management
        void SetApiKey(string apiKey);
        string GetApiKey();
        bool MigrateLegacyApiKey();
    }
}