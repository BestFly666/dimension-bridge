using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace SimpleXmlEditor.Services
{
    public class AppConfig
    {
        // Encrypted API key stored via DPAPI (Windows only)
        public string EncryptedApiKey { get; set; } = "";
        public string GeminiModel { get; set; } = "";
        public string TargetLanguage { get; set; } = "Turkish";
        public string ProgramLanguage { get; set; } = "zh";
        public string CustomPrompt { get; set; } = "";
        public string ActiveExpertProfile { get; set; } = "";
        public string AiProvider { get; set; } = "GoogleGemini";
        public string LastLoadedFilePath { get; set; } = "";
        public int BatchSize { get; set; } = 50;
    }

    public class ConfigService : IConfigService
    {
        private readonly string _appDataDir;
        private readonly string _configPath;
        private readonly string _cachePath;
        private readonly object _cacheLock = new object();

        public ConcurrentDictionary<string, string> Cache { get; private set; } = new();
        public AppConfig Config { get; private set; } = new();

        public event Action<string> LogMessage;

        public ConfigService()
        {
            _appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SimpleXmlEditor");
            Directory.CreateDirectory(_appDataDir);
            _configPath = Path.Combine(_appDataDir, "config.json");
            _cachePath = Path.Combine(_appDataDir, "translation_cache.json");
        }

        private void RaiseLog(string message)
        {
            LogMessage?.Invoke(message);
        }

        public void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    Config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                    RaiseLog("Config loaded");
                }

                if (File.Exists(_cachePath))
                {
                    var cacheJson = File.ReadAllText(_cachePath);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(cacheJson) ?? new Dictionary<string, string>();
                    Cache = new ConcurrentDictionary<string, string>(dict);
                    RaiseLog($"Cache loaded - {Cache.Count} entries");
                }
            }
            catch (Exception ex)
            {
                RaiseLog($"Config load error: {ex.Message}");
                Config = new AppConfig();
                Cache = new ConcurrentDictionary<string, string>();
            }
        }

        /// <summary>
        /// Encrypts API key using Windows DPAPI and stores it as Base64.
        /// Falls back to plaintext storage only if DPAPI is unavailable (non-Windows).
        /// </summary>
        public void SetApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Config.EncryptedApiKey = "";
                return;
            }

            try
            {
                byte[] encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(apiKey),
                    null,
                    DataProtectionScope.CurrentUser);
                Config.EncryptedApiKey = Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                RaiseLog($"API key encryption failed: {ex.Message}");
                // Fallback: store plaintext with a LEGACY prefix so GetApiKey can detect it
                Config.EncryptedApiKey = "LEGACY:" + apiKey;
                RaiseLog("WARNING: API key stored in plaintext due to DPAPI failure (non-Windows environment?)");
            }
        }

        /// <summary>
        /// Decrypts API key from DPAPI-encrypted storage.
        /// Supports migration from old plaintext configs (legacy GeminiApiKey field)
        /// and non-Windows fallback (LEGACY: prefix).
        /// </summary>
        public string GetApiKey()
        {
            if (string.IsNullOrEmpty(Config.EncryptedApiKey))
                return "";

            // Non-Windows fallback or legacy migration path
            if (Config.EncryptedApiKey.StartsWith("LEGACY:", StringComparison.Ordinal))
                return Config.EncryptedApiKey.Substring(7);

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(Config.EncryptedApiKey);
                byte[] decrypted = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                RaiseLog($"API key decryption failed: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Migrates old config.json with plaintext GeminiApiKey to encrypted format.
        /// </summary>
        public bool MigrateLegacyApiKey()
        {
            if (!File.Exists(_configPath)) return false;

            try
            {
                var json = File.ReadAllText(_configPath);
                var tempConfig = JsonConvert.DeserializeAnonymousType(json, new { GeminiApiKey = "", EncryptedApiKey = "" });

                if (!string.IsNullOrEmpty(tempConfig.GeminiApiKey) && string.IsNullOrEmpty(tempConfig.EncryptedApiKey))
                {
                    var legacyKey = tempConfig.GeminiApiKey;
                    SetApiKey(legacyKey);
                    SaveConfig();
                    RaiseLog("Migrated legacy plaintext API key to encrypted storage");
                    return true;
                }
            }
            catch (Exception ex)
            {
                RaiseLog($"Migration failed: {ex.Message}");
            }

            return false;
        }

        public void SaveConfig()
        {
            try
            {
                var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
                RaiseLog("Config saved");
            }
            catch (Exception ex)
            {
                RaiseLog($"Config save error: {ex.Message}");
            }
        }

        public void SaveCache()
        {
            try
            {
                File.WriteAllText(_cachePath, JsonConvert.SerializeObject(Cache, Formatting.Indented));
            }
            catch (Exception ex)
            {
                RaiseLog($"Cache write error: {ex.Message}");
            }
        }

        public void SyncEntriesToCache(IEnumerable<LocalizationEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Translation) && !string.IsNullOrWhiteSpace(entry.Value))
                {
                    Cache[entry.Key] = entry.Translation;
                    var cacheKey = GetCacheKey(entry.Value);
                    if (cacheKey != null)
                        Cache[cacheKey] = entry.Translation;
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
        public void SaveTranslationProgress(IEnumerable<LocalizationEntry> entries)
        {
            try
            {
                var progressPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translation_progress.json");
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
                    File.WriteAllText(progressPath, JsonConvert.SerializeObject(progress, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                RaiseLog($"Progress save failed: {ex.Message}");
            }
        }

        public int RestoreTranslationProgress(IEnumerable<LocalizationEntry> entries)
        {
            try
            {
                var progressPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translation_progress.json");
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
                    RaiseLog($"Restored {restoredCount} translations from crash recovery file");
                }

                return restoredCount;
            }
            catch (Exception ex)
            {
                RaiseLog($"Recovery file error: {ex.Message}");
                return 0;
            }
        }

        public void DeleteProgressFile()
        {
            try
            {
                var progressPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translation_progress.json");
                if (File.Exists(progressPath))
                {
                    File.Delete(progressPath);
                }
            }
            catch (Exception ex)
            {
                RaiseLog($"Progress file delete failed: {ex.Message}");
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

        public void UpdateConfig(Action<AppConfig> updater)
        {
            updater?.Invoke(Config);
            SaveConfig();
        }
    }
}
