using System;
using System.Linq;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    public partial class MainViewModel
    {
        public void RefreshBlacklistFlags()
        {
            foreach (var entry in Entries)
            {
                entry.IsBlacklisted = _blacklistManager.IsBlocked(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Process an entry during load: cache write/read, glossary application,
        /// and adding to the Entries collection.
        /// 多语言支持：原文列始终显示文件原始内容（不因语言清空或移动）；
        /// 中文源文本的审校/修正由翻译流程的 [EXISTING ZH] 标记处理，不在加载阶段改动数据。
        /// </summary>
        public LocalizationEntry ProcessEntry(LocalizationEntry entry)
        {
            entry.RowNumber = Entries.Count + 1;

            if (!string.IsNullOrEmpty(entry.Translation))
            {
                if (!string.IsNullOrWhiteSpace(entry.Value))
                    // 双键对称写（Key + MD5(原文)），与 SyncEntriesToCache 保持一致
                    _configService.SetCacheEntry(entry.Key, entry.Value, entry.Translation);
            }
            else if (!string.IsNullOrWhiteSpace(entry.Value))
            {
                if (_configService.Cache.TryGetValue(entry.Key, out var cachedByKey))
                {
                    entry.Translation = ApplyCleanedCacheValue(entry, cachedByKey);
                }
                else
                {
                    var cacheKey = _configService.GetCacheKey(entry.Value);
                    if (cacheKey != null && _configService.Cache.TryGetValue(cacheKey, out var cachedByValue))
                    {
                        entry.Translation = ApplyCleanedCacheValue(entry, cachedByValue);
                    }
                }

                TryApplyDictionary(entry);
            }

            Entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// 从缓存恢复译文时清洗 AI 回显污染（译文混入 [KEY]/原文），
        /// 并写回清洗结果自愈缓存。清洗后为空则不应用（保持待译状态）。
        /// </summary>
        private string ApplyCleanedCacheValue(LocalizationEntry entry, string cached)
        {
            var cleaned = AiResponseParser.CleanTranslationEcho(cached, entry.Key, entry.Value);
            if (string.IsNullOrEmpty(cleaned))
                return null;

            if (cleaned != cached)
                // 自愈：覆盖污染的双键缓存，避免下次加载再次读到带 KEY 的旧值
                _configService.SetCacheEntry(entry.Key, entry.Value, cleaned);
            return cleaned;
        }

        /// <summary>
        /// Try to apply glossary lookup. Only exact-match on Key or Value.
        /// Term-level substitution is handled by BuildGlossaryContext via AI prompt.
        /// </summary>
        public bool TryApplyDictionary(LocalizationEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.Translation))
                return false;

            // Exact match on Key (e.g., "UPGRADE_TECH" → "科技升级")
            if (_glossary.TryGetValue(entry.Key, out var dictTranslation))
            {
                entry.Translation = dictTranslation;
                IncrementGlossaryHits();
                return true;
            }
            // Exact match on entire Value (single-word entries like "Jedi" → "绝地")
            if (_glossary.TryGetValue(entry.Value, out dictTranslation))
            {
                entry.Translation = dictTranslation;
                IncrementGlossaryHits();
                return true;
            }
            return false;
        }

        /// <summary>Save entries to XML. Returns true on success.</summary>
        public bool SaveXml(string fileName = "stable_us.xml")
        {
            try
            {
                SyncEntriesToCache(Entries);

                var entriesList = Entries.ToList();
                _xmlRepository.SaveXml(fileName, entriesList);

                SaveConfig();
                RaiseStatusMessage(LocalizationManager.GetString("SavedEntries", Entries.Count, System.IO.Path.GetFileName(fileName)));
                OnLogMessage($"💾 {LocalizationManager.GetString("LogXmlSaved", fileName, Entries.Count)}");
                return true;
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ {LocalizationManager.GetString("ErrorSavingXml", ex.Message)}");
                return false;
            }
        }
    }
}
