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
        }

        public void SaveConfig()
        {
            _configService.Config.ActiveExpertProfile = ActiveExpertProfileName;
            _configService.Config.CustomPrompt = CustomPrompt;
            _configService.Config.LastLoadedFilePath = LastLoadedFilePath;
            _configService.Config.BatchSize = BatchSize;
            _configService.Config.AiProvider = AiProvider.ToString();
            _configService.Config.ProgramLanguage = ProgramLanguage;
            _configService.SetApiKey(_aiTranslationService.ApiKey);
            _configService.Config.GeminiModel = _aiTranslationService.Model;
            _configService.Config.TargetLanguage = _aiTranslationService.TargetLanguage;
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
