using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SimpleXmlEditor.Localization;

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
        public int MaxConcurrentBatches { get; set; } = 3;
        public bool DisableThinking { get; set; } = true;

        // 评估/投票专用模型配置（留空则使用翻译模型）
        public string EvaluationAiProvider { get; set; } = "";
        public string EvaluationModel { get; set; } = "";
        public string EncryptedEvaluationApiKey { get; set; } = "";

        // 评估/投票专用模型列表（支持多模型投票；为空则回退到上方单组配置）
        public List<EvaluationModelConfig> EvaluationModels { get; set; } = new();
    }

    /// <summary>评估/投票专用模型配置条目（支持多模型投票）。</summary>
    public class EvaluationModelConfig
    {
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
        public string EncryptedApiKey { get; set; } = "";
    }

    public partial class ConfigService : IConfigService
    {
        private readonly string _appDataDir;
        private readonly string _configPath;
        private readonly string _cachePath;
        private readonly string _scoreCachePath;
        private readonly string _progressPath;
        private readonly object _cacheLock = new object();

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
            _scoreCachePath = Path.Combine(_appDataDir, "score_cache.json");
            _progressPath = Path.Combine(_appDataDir, "translation_progress.json");

            // 清理旧版本残留在程序目录（bin，随 Debug/Release 构建变化）的进度文件，
            // 统一缓存/进度文件到 AppData，避免"缓存文件变来变去"与删除译文被旧数据复活
            try
            {
                var legacyProgressPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translation_progress.json");
                if (File.Exists(legacyProgressPath))
                {
                    File.Delete(legacyProgressPath);
                }
            }
            catch
            {
                // 旧文件清理失败不影响主流程
            }
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
                    RaiseLog(LocalizationManager.GetString("LogConfigLoaded"));
                }

                if (File.Exists(_cachePath))
                {
                    var cacheJson = File.ReadAllText(_cachePath);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(cacheJson) ?? new Dictionary<string, string>();
                    Cache = new ConcurrentDictionary<string, string>(dict);
                    RaiseLog(LocalizationManager.GetString("LogCacheLoaded", Cache.Count));
                }

                if (File.Exists(_scoreCachePath))
                {
                    var scoreJson = File.ReadAllText(_scoreCachePath);
                    var scoreDict = JsonConvert.DeserializeObject<Dictionary<string, ScoreCacheItem>>(scoreJson)
                                    ?? new Dictionary<string, ScoreCacheItem>();
                    ScoreCache = new ConcurrentDictionary<string, ScoreCacheItem>(scoreDict);
                    RaiseLog(LocalizationManager.GetString("LogScoreCacheLoaded", ScoreCache.Count));
                }
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("ConfigLoadError", ex.Message));
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
                RaiseLog(LocalizationManager.GetString("LogApiKeyEncryptFailed", ex.Message));
                // Fallback: store plaintext with a LEGACY prefix so GetApiKey can detect it
                Config.EncryptedApiKey = "LEGACY:" + apiKey;
                RaiseLog(LocalizationManager.GetString("LogApiKeyPlaintextWarning"));
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
                RaiseLog(LocalizationManager.GetString("LogApiKeyDecryptFailed", ex.Message));
                return "";
            }
        }

        public void SetEvaluationApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Config.EncryptedEvaluationApiKey = "";
                return;
            }
            try
            {
                byte[] encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(apiKey), null, DataProtectionScope.CurrentUser);
                Config.EncryptedEvaluationApiKey = Convert.ToBase64String(encrypted);
            }
            catch
            {
                Config.EncryptedEvaluationApiKey = "LEGACY:" + apiKey;
            }
        }

        public string GetEvaluationApiKey()
        {
            if (string.IsNullOrEmpty(Config.EncryptedEvaluationApiKey))
                return "";
            if (Config.EncryptedEvaluationApiKey.StartsWith("LEGACY:", StringComparison.Ordinal))
                return Config.EncryptedEvaluationApiKey.Substring(7);
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(Config.EncryptedEvaluationApiKey);
                byte[] decrypted = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 批量保存评估/投票模型列表（明文 API Key 加密后存储）。
        /// 空的 Provider/Model 条目会被过滤；Key 为空时保持空（表示使用翻译模型 Key）。
        /// </summary>
        public void SaveEvaluationModels(List<(string Provider, string Model, string ApiKey)> models)
        {
            Config.EvaluationModels = (models ?? new List<(string, string, string)>())
                .Where(m => !string.IsNullOrEmpty(m.Provider) && !string.IsNullOrEmpty(m.Model))
                .Select(m => new EvaluationModelConfig
                {
                    Provider = m.Provider,
                    Model = m.Model,
                    EncryptedApiKey = EncryptSecret(m.ApiKey)
                })
                .ToList();
        }

        /// <summary>解密读取评估/投票模型的 API Key。</summary>
        public string GetEvaluationModelKey(EvaluationModelConfig model)
        {
            return DecryptSecret(model?.EncryptedApiKey ?? "");
        }

        private static string EncryptSecret(string plain)
        {
            if (string.IsNullOrEmpty(plain))
                return "";
            try
            {
                byte[] encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                return "LEGACY:" + plain;
            }
        }

        private static string DecryptSecret(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted))
                return "";
            if (encrypted.StartsWith("LEGACY:", StringComparison.Ordinal))
                return encrypted.Substring(7);
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encrypted);
                byte[] decrypted = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
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
                    RaiseLog(LocalizationManager.GetString("LogMigratedLegacyKey"));
                    return true;
                }
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("LogMigrationFailed", ex.Message));
            }

            return false;
        }

        public void SaveConfig()
        {
            try
            {
                var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
                RaiseLog(LocalizationManager.GetString("LogConfigSaved"));
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("ConfigSaveError", ex.Message));
            }
        }

        public void UpdateConfig(Action<AppConfig> updater)
        {
            updater?.Invoke(Config);
            SaveConfig();
        }
    }
}
