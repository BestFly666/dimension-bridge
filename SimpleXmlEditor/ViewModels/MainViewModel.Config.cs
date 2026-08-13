using System;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    public partial class MainViewModel
    {
        public void LoadConfig()
        {
            _configService.LoadConfig();
            
            if (_configService.Config.ActiveExpertProfile != null)
                ActiveExpertProfileName = _configService.Config.ActiveExpertProfile;
            if (_configService.Config.CustomPrompt != null)
                CustomPrompt = _configService.Config.CustomPrompt;
            if (_configService.Config.LastLoadedFilePath != null)
                LastLoadedFilePath = _configService.Config.LastLoadedFilePath;
            BatchSize = _configService.Config.BatchSize;
            MaxConcurrentBatches = _configService.Config.MaxConcurrentBatches;
            MaxGlossaryContextTerms = _configService.Config.MaxGlossaryContextTerms;
            AiProvider = Enum.TryParse<AIProvider>(_configService.Config.AiProvider, out var provider) ? provider : AIProvider.GoogleGemini;
            if (_configService.Config.ProgramLanguage != null)
                ProgramLanguage = _configService.Config.ProgramLanguage;
            LocalizationManager.CurrentLanguage = ProgramLanguage;
            if (_configService.Config.EncryptedApiKey != null)
                _aiTranslationService.ApiKey = _configService.GetApiKey();
            if (_configService.Config.GeminiModel != null)
                _aiTranslationService.Model = _configService.Config.GeminiModel;
            if (_configService.Config.TargetLanguage != null)
                _aiTranslationService.TargetLanguage = _configService.Config.TargetLanguage;

            // 加载累计统计（重启后恢复 API 调用/命中/费用）
            _cacheHits = (int)_configService.Config.TotalCacheHits;
            _apiCalls = (int)_configService.Config.TotalApiCalls;
            _glossaryHits = (int)_configService.Config.TotalGlossaryHits;
            _totalInputChars = (int)_configService.Config.TotalInputChars;
            _totalOutputChars = (int)_configService.Config.TotalOutputChars;
            _totalCost = _configService.Config.TotalCostUsd;

            // 术语注入上限同步到 GlossaryManager（属性 setter 已同步，此处显式确保一致）
            _glossary.MaxGlossaryContextTerms = _configService.Config.MaxGlossaryContextTerms;
        }

        public void SaveConfig()
        {
            _configService.Config.ActiveExpertProfile = ActiveExpertProfileName;
            _configService.Config.CustomPrompt = CustomPrompt;
            _configService.Config.LastLoadedFilePath = LastLoadedFilePath;
            _configService.Config.BatchSize = BatchSize;
            _configService.Config.MaxConcurrentBatches = MaxConcurrentBatches;
            _configService.Config.MaxGlossaryContextTerms = MaxGlossaryContextTerms;
            _configService.Config.AiProvider = AiProvider.ToString();
            _configService.Config.ProgramLanguage = ProgramLanguage;
            _configService.SetApiKey(_aiTranslationService.ApiKey);
            _configService.Config.GeminiModel = _aiTranslationService.Model;
            _configService.Config.TargetLanguage = _aiTranslationService.TargetLanguage;

            // 同步累计统计到配置后写盘（重启保留）
            _configService.Config.TotalCacheHits = _cacheHits;
            _configService.Config.TotalApiCalls = _apiCalls;
            _configService.Config.TotalGlossaryHits = _glossaryHits;
            _configService.Config.TotalInputChars = _totalInputChars;
            _configService.Config.TotalOutputChars = _totalOutputChars;
            _configService.Config.TotalCostUsd = _totalCost;
            _configService.SaveConfig();
        }

        public void UpdateCacheInfo()
        {
            var cacheCount = _configService.Cache.Count;
            StatusMessage = $"📊 {cacheCount} {LocalizationManager.GetString("CacheInfo", CacheHits)}";
        }

        public void UpdateDictInfo()
        {
            var dictCount = _glossary.Count;
            StatusMessage = $"📖 {dictCount} {LocalizationManager.GetString("DictionaryInfo", GlossaryHits)}";
        }
    }
}
