using System;
using System.Collections.Generic;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    public partial class MainViewModel
    {
        public int RestoreTranslationProgress(IEnumerable<LocalizationEntry> entries)
        {
            return _configService.RestoreTranslationProgress(entries);
        }

        public void SyncEntriesToCache(IEnumerable<LocalizationEntry> entries)
        {
            _configService.SyncEntriesToCache(entries);
        }

        public void SyncScoresToCache(IEnumerable<LocalizationEntry> entries)
        {
            _configService.SyncScoresToCache(entries);
        }

        public void SaveScoreCache()
        {
            _configService.SaveScoreCache();
        }

        public int RestoreScores(IEnumerable<LocalizationEntry> entries)
        {
            return _configService.RestoreScores(entries);
        }

        /// <summary>Persist the translation cache to disk.</summary>
        public void SaveCache()
        {
            try
            {
                _configService.SaveCache();
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ {LocalizationManager.GetString("LogCacheWriteError", ex.Message)}");
            }
        }
    }
}
