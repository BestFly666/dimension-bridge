using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    /// <summary>
    /// MainWindow partial: 文件加载/保存、配置初始化、模型自动加载、
    /// 窗口生命周期与对外 API。
    /// </summary>
    public partial class MainWindow
    {
        private void InitializeFromConfig()
        {
            _viewModel.LoadConfig();
            _viewModel.ConfigService.MigrateLegacyApiKey();
            _viewModel.AiTranslationService.ApiKey = _viewModel.ConfigService.GetApiKey();

            BatchSizeTxt.Text = _viewModel.BatchSize.ToString();
            _viewModel.ProfileManager.EnsureDefaultsExist();
            RefreshExpertProfileCombo();
            ApplyLocalization();

            if (!string.IsNullOrEmpty(_viewModel.LastLoadedFilePath) && File.Exists(_viewModel.LastLoadedFilePath))
            {
                LoadXml(_viewModel.LastLoadedFilePath);
                AddLog($"📂 {LocalizationManager.GetString("LogAutoLoad", Path.GetFileName(_viewModel.LastLoadedFilePath))}");
            }

            UpdateCacheInfo();
            UpdateGlossaryInfo();
            AddLog($"✅ {LocalizationManager.GetString("LogStarted")}");

            AutoLoadModelsAsync();
        }

        private async void AutoLoadModelsAsync()
        {
            try
            {
                if (_viewModel.AiProvider != AIProvider.GoogleGemini)
                {
                    LoadStaticModels();
                    return;
                }

                if (!string.IsNullOrEmpty(_viewModel.AiTranslationService.ApiKey))
                {
                    AddLog($"🔄 {LocalizationManager.GetString("LogAutoRefreshModels")}");
                    var models = await _viewModel.AiTranslationService.FetchAvailableModelsAsync(_viewModel.AiTranslationService.ApiKey, _viewModel.AiProvider);

                    if (models.Count > 0)
                    {
                        AddLog($"✅ {LocalizationManager.GetString("LogAutoModelsLoaded", models.Count)}");

                        if (string.IsNullOrEmpty(_viewModel.AiTranslationService.Model) || !models.Contains(_viewModel.AiTranslationService.Model))
                        {
                            _viewModel.AiTranslationService.Model = models.FirstOrDefault() ?? "";
                            _viewModel.SaveConfig();
                            AddLog($"🔧 {LocalizationManager.GetString("LogAutoModelSelected", _viewModel.AiTranslationService.Model)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoLoadModels error: {ex.Message}");
            }
        }

        private void LoadStaticModels()
        {
            if (!AiTranslationService.StaticModels.ContainsKey(_viewModel.AiProvider)) return;

            _viewModel.AiTranslationService.ModelPricing.Clear();
            _viewModel.AiTranslationService.ModelLimits.Clear();

            if (AiTranslationService.ProviderRateLimits.ContainsKey(_viewModel.AiProvider))
            {
                foreach (var kvp in AiTranslationService.ProviderRateLimits[_viewModel.AiProvider])
                {
                    _viewModel.AiTranslationService.ModelLimits[kvp.Key] = kvp.Value;
                }
            }

            var models = AiTranslationService.StaticModels[_viewModel.AiProvider];

            if (string.IsNullOrEmpty(_viewModel.AiTranslationService.Model) || !models.Contains(_viewModel.AiTranslationService.Model))
            {
                _viewModel.AiTranslationService.Model = models.FirstOrDefault() ?? "";
                _viewModel.SaveConfig();
            }

            AddLog($"✅ {LocalizationManager.GetString("LogDeepSeekModels", models.Count)}");
        }

        protected override void OnClosed(EventArgs e)
        {
            _filterTimer?.Stop();
            _autoSaveTimer?.Stop();
            _viewModel.AiTranslationService.Dispose();
            base.OnClosed(e);
        }

        public async Task<List<string>> FetchAvailableModelsAsync(string apiKey, AIProvider? provider = null)
        {
            return await _viewModel.AiTranslationService.FetchAvailableModelsAsync(apiKey, provider ?? _viewModel.AiProvider);
        }

        public IConfigService ConfigService => _viewModel.ConfigService;

        public Dictionary<string, (int requestsPerMinute, int requestsPerDay, int tokensPerMinute)> GetModelLimits(AIProvider? provider = null)
        {
            if (provider.HasValue && AiTranslationService.ProviderRateLimits.ContainsKey(provider.Value))
            {
                var result = new Dictionary<string, (int requestsPerMinute, int requestsPerDay, int tokensPerMinute)>();
                foreach (var kvp in AiTranslationService.ProviderRateLimits[provider.Value])
                {
                    result[kvp.Key] = kvp.Value;
                }
                return result;
            }
            return new Dictionary<string, (int requestsPerMinute, int requestsPerDay, int tokensPerMinute)>(_viewModel.AiTranslationService.ModelLimits);
        }

        private void LoadXml(string fileName = "stable_us.xml", bool isTranslationFile = false)
        {
            try
            {
                if (!File.Exists(fileName))
                {
                    AddLog($"❌ {LocalizationManager.GetString("LogFileNotFound", fileName)}");
                    return;
                }

                var loadedEntries = _viewModel.XmlRepository.LoadXml(fileName, isTranslationFile);

                if (isTranslationFile && _viewModel.Entries.Count > 0)
                {
                    var lookup = new Dictionary<string, LocalizationEntry>();
                    foreach (var e in loadedEntries)
                    {
                        lookup.TryAdd(e.Key, e);
                    }

                    int matched = 0;
                    foreach (var existing in _viewModel.Entries)
                    {
                        if (lookup.TryGetValue(existing.Key, out var translated)
                            && !string.IsNullOrEmpty(translated.Translation))
                        {
                            existing.Translation = translated.Translation;
                            _viewModel.ConfigService.Cache.TryAdd(existing.Key, translated.Translation);
                            matched++;
                        }
                    }

                    StatusText.Text = LocalizationManager.GetString("MergedTranslations", matched);
                    AddLog($"✅ {LocalizationManager.GetString("LogTranslationMerged", matched, _viewModel.Entries.Count)}");

                    UpdateCacheInfo();
                    UpdateGlossaryInfo();

                    var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
                    view?.Refresh();

                    _viewModel.RestoreTranslationProgress(_viewModel.Entries);
                    _viewModel.RestoreScores(_viewModel.Entries);
                }
                else
                {
                    _viewModel.Entries.Clear();

                    foreach (var entry in loadedEntries)
                    {
                        _viewModel.ProcessEntry(entry);
                        entry.PropertyChanged += OnEntryPropertyChanged;
                    }

                    FilterKeyBox.Text = "";
                    FilterBox.Text = "";
                    FilterTranslationBox.Text = "";

                    StatusText.Text = LocalizationManager.GetString("LoadedEntries", _viewModel.Entries.Count);
                    AddLog($"✅ {LocalizationManager.GetString("LogXmlLoaded", _viewModel.Entries.Count)}");
                    _viewModel.LastLoadedFilePath = fileName;
                    _viewModel.ConfigService.Config.LastLoadedFilePath = fileName;
                    _viewModel.ConfigService.SaveConfig();
                    FilterCountText.Text = LocalizationManager.GetString("TotalCount", _viewModel.Entries.Count);

                    var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
                    view?.Refresh();

                    _viewModel.RestoreTranslationProgress(_viewModel.Entries);
                    _viewModel.RestoreScores(_viewModel.Entries);

                    // 按黑名单前缀刷新条目标记，状态列显示 🚫（并应用黑名单隐藏筛选）
                    _viewModel.RefreshBlacklistFlags();
                    ApplyFilter();

                    CurrentFileTab.Text = System.IO.Path.GetFileName(fileName);
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ {LocalizationManager.GetString("ErrorLoadingXml", ex.Message)}");
                MessageBox.Show(LocalizationManager.GetString("ErrorLoadingXml", ex.Message), LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveXml(string fileName = "stable_us.xml")
        {
            if (_viewModel.SaveXml(fileName))
            {
                UpdateCacheInfo();
            }
            else
            {
                MessageBox.Show(LocalizationManager.GetString("ErrorSavingXml", ""), LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
