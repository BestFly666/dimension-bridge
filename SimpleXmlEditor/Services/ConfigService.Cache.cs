using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor.Services
{
    public partial class ConfigService
    {
        public ConcurrentDictionary<string, string> Cache { get; private set; } = new();

        public void SaveCache()
        {
            try
            {
                File.WriteAllText(_cachePath, JsonConvert.SerializeObject(Cache, Formatting.Indented));
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("LogCacheWriteError", ex.Message));
            }
        }

        public void SyncEntriesToCache(IEnumerable<LocalizationEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Value))
                    continue;

                var cacheKey = GetCacheKey(entry.Value);

                if (!string.IsNullOrEmpty(entry.Translation))
                {
                    Cache[entry.Key] = entry.Translation;
                    if (cacheKey != null)
                        Cache[cacheKey] = entry.Translation;
                }
                else
                {
                    // 译文为空时，必须从 Cache 中移除旧值，防止重新打开时被污染
                    Cache.TryRemove(entry.Key, out _);
                    if (cacheKey != null)
                        Cache.TryRemove(cacheKey, out _);
                }
            }
        }

        public Dictionary<string, string> GetCacheForSave(IEnumerable<LocalizationEntry> entries)
        {
            var cache = new Dictionary<string, string>();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Value) && !string.IsNullOrEmpty(entry.Translation))
                {
                    cache[entry.Value] = entry.Translation;
                }
            }
            return cache;
        }
        public async Task SaveTranslationProgressAsync(IEnumerable<LocalizationEntry> entries)
        {
            try
            {
                var progressPath = _progressPath;
                var progress = new Dictionary<string, string>();

                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.Value) && !string.IsNullOrEmpty(entry.Translation))
                    {
                        progress[entry.Value] = entry.Translation;
                    }
                }

                if (progress.Count > 0)
                {
                    await File.WriteAllTextAsync(progressPath, JsonConvert.SerializeObject(progress, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("LogProgressSaveError", ex.Message));
            }
        }

        public int RestoreTranslationProgress(IEnumerable<LocalizationEntry> entries)
        {
            try
            {
                var progressPath = _progressPath;
                if (!File.Exists(progressPath)) return 0;

                var json = File.ReadAllText(progressPath);
                var progress = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (progress == null || progress.Count == 0) return 0;

                int restoredCount = 0;
                var entryList = entries.ToList();

                foreach (var entry in entryList)
                {
                    if (string.IsNullOrEmpty(entry.Value)) continue;
                    if (!string.IsNullOrEmpty(entry.Translation)) continue;
                    if (progress.TryGetValue(entry.Value, out var translation))
                    {
                        entry.Translation = translation;
                        restoredCount++;
                    }
                }

                if (restoredCount > 0)
                {
                    RaiseLog(LocalizationManager.GetString("LogCrashRecovery", restoredCount));
                }

                return restoredCount;
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("LogRecoveryError", ex.Message));
                return 0;
            }
        }

        public void DeleteProgressFile()
        {
            try
            {
                var progressPath = _progressPath;
                if (File.Exists(progressPath))
                {
                    File.Delete(progressPath);
                }
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("LogProgressDeleteError", ex.Message));
            }
        }

        public string GetCacheKey(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
            return System.Convert.ToHexString(hash);
        }
    }
}
