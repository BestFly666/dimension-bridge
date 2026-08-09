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

        /// <summary>
        /// 对称写入缓存双键（Key + MD5(原文)），保持与 SyncEntriesToCache 一致，
        /// 避免不同写入路径只写单键导致另一键残留旧译文。
        /// </summary>
        public void SetCacheEntry(string key, string originalText, string translation)
        {
            if (string.IsNullOrWhiteSpace(originalText))
                return;

            Cache[key] = translation;
            var cacheKey = GetCacheKey(originalText);
            if (cacheKey != null)
                Cache[cacheKey] = translation;
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
                    // 与 DeleteProgressFile/RestoreTranslationProgress 串行化，避免并发写删同一文件
                    await _progressFileLock.WaitAsync();
                    try
                    {
                        await File.WriteAllTextAsync(progressPath, JsonConvert.SerializeObject(progress, Formatting.Indented));
                    }
                    finally
                    {
                        _progressFileLock.Release();
                    }
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

                // 与 SaveTranslationProgressAsync/DeleteProgressFile 串行化，避免读到半写的文件
                _progressFileLock.Wait();
                try
                {
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
                finally
                {
                    _progressFileLock.Release();
                }
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
                    // 与 SaveTranslationProgressAsync 串行化：等待在途保存完成后删除
                    _progressFileLock.Wait();
                    try
                    {
                        if (File.Exists(progressPath))
                            File.Delete(progressPath);
                    }
                    finally
                    {
                        _progressFileLock.Release();
                    }
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
