using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleXmlEditor.ExpertProfiles;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Services;
using SimpleXmlEditor.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleXmlEditor
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private Stack<Dictionary<string, string>> _undoStack = new Stack<Dictionary<string, string>>();

        private CancellationTokenSource _translationCancellationTokenSource;
        
        private System.Windows.Threading.DispatcherTimer _filterTimer;


        public MainWindow()
        {
            InitializeComponent();
            // ViewModel may be injected by DI or manually created
            _viewModel = App.Services?.GetService<MainViewModel>() ?? new MainViewModel();
            _viewModel.LogMessage += msg => Dispatcher.Invoke(() => AddLog(msg));
            
            EntriesGrid.ItemsSource = _viewModel.Entries;
            
            EntriesGrid.SelectionChanged += EntriesGrid_SelectionChanged;
            EntriesGrid.Loaded += EntriesGrid_Loaded;
            
            _filterTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _filterTimer.Tick += FilterTimer_Tick;
            
            this.KeyDown += MainWindow_KeyDown;
            
            InitializeFromConfig();
        }

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
            _viewModel.AiTranslationService.Dispose();
            base.OnClosed(e);
        }

        private void ApplyLocalization()
        {
            Func<string, string> L = LocalizationManager.GetString;  // shorthand

            // Update window title and app name
            this.Title = L("WindowTitle");
            AppNameText.Text = L("AppName");

            // Update main UI buttons
            LoadBtn.Content = $"📁 {L("Load")}";
            SaveBtn.Content = $"💾 {L("Save")}";
            QuickSaveBtn.Content = $"⚡ {L("QuickSave")}";
            QuickSaveBtn.ToolTip = L("TipQuickSave");
            SettingsBtn.Content = $"⚙️ {L("Settings")}";
            StatsBtn.Content = $"📊 {L("Stats")}";
            GlossaryBtn.Content = $"📖 {L("Glossary")}";
            GlossaryBtn.ToolTip = L("TipGlossary");
            ClearDictBtn.Content = $"🗑️ {L("ClearDict")}";
            BatchReplaceBtn.Content = $"🔄 {L("BatchReplace")}";
            BatchReplaceBtn.ToolTip = L("TipBatchReplace");
            UndoBtn.Content = $"↩️ {L("Undo")}";
            UndoBtn.ToolTip = L("TipUndo");
            SaveBtn.ToolTip = L("TipSaveAs");

            // Update expert profile combo default item
            foreach (ComboBoxItem item in ExpertProfileCombo.Items)
            {
                if (item.Tag?.ToString() == "")
                {
                    item.Content = L("NoExpertDefault");
                    break;
                }
            }

            // Update status
            StatusText.Text = L("Ready");

            // Update section titles
            AITranslationTitle.Text = $"🤖 {L("AITranslationCenter")}";
            TranslationDataTitle.Text = $"📋 {L("TranslationData")}";
            ActivityLogTitle.Text = L("ActivityLog");

            // Update translation buttons
            TranslateSelectedBtn.Content = $"🎯 {L("TranslateSelected")}";
            TranslateAllBtn.Content = $"🚀 {L("TranslateAll")}";
            ClearCacheBtn.Content = $"🗑️ {L("ClearCache")}";
            ClearLogBtn.Content = "🗑️";  // emoji-only, no text to localize

            // Update evaluation & voting buttons
            EvaluateBtn.Content = $"🤖 {L("EvaluateBtn")}";
            EvaluateBtn.ToolTip = L("EvaluateToolTip");
            VoteBtn.Content = $"🗳 {L("VoteBtn")}";
            VoteBtn.ToolTip = L("VoteToolTip");

            // Update batch label
            BatchLabelText.Text = $"{L("BatchLabel")}:";

            // Update DataGrid headers
            if (EntriesGrid.Columns.Count >= 6)
            {
                EntriesGrid.Columns[0].Header = "#";
                EntriesGrid.Columns[1].Header = "✓";
                EntriesGrid.Columns[2].Header = L("Status");
                EntriesGrid.Columns[3].Header = L("Key");
                EntriesGrid.Columns[4].Header = L("Original");
                EntriesGrid.Columns[5].Header = L("Translation");
            }

            // Update filter tooltips
            FilterKeyBox.ToolTip = L("TipFilterKey");
            FilterBox.ToolTip = L("TipFilterOriginal");
            FilterTranslationBox.ToolTip = L("TipFilterTranslation");
            ClearFilterBtn.ToolTip = L("ClearFilter");

            // Update filter button text (replaces XAML "✕")
            ClearFilterBtn.Content = $"✕ {L("FilterClear")}";

            // Update find bar
            FindLabelText.Text = $"🔍 {L("FindLabel")}";
            FindPrevBtn.Content = L("FindPrevious");
            FindNextBtn.Content = L("FindNext");

            // Update context menu
            CtxCopyKeyMenu.Header = $"📋 {L("CtxCopyKey")}";
            CtxCopyOriginalMenu.Header = $"📋 {L("CtxCopyOriginal")}";
            CtxCopyTranslationMenu.Header = $"📋 {L("CtxCopyTranslation")}";
            CtxClearTranslationMenu.Header = $"🗑️ {L("CtxClearTranslation")}";
            CtxTranslateSelectedMenu.Header = $"🌐 {L("CtxTranslateSelected")}";
            CtxEvaluateMenu.Header = $"🤖 {L("CtxEvaluate")}";
            CtxVoteMenu.Header = $"🗳 {L("CtxVote")}";
            CtxSelectAllMenu.Header = $"☑️ {L("CtxSelectAll")}";
            CtxSelectNoneMenu.Header = $"☐ {L("CtxSelectNone")}";
            CtxInvertSelectionMenu.Header = $"🔄 {L("CtxInvertSelection")}";

            // Update control buttons
            PauseBtn.Content = $"⏸️ {L("Pause")}";
            StopBtn.Content = $"⏹️ {L("Stop")}";

            // Update log panel labels
            RealTimeLabel.Text = $"🕒 {L("RealTime")}";
            AutoScrollLabel.Text = $"🔄 {L("AutoScroll")}";

            // Update cache/dict info (preserve the current counts)
            UpdateInfoLabels();
        }

        /// <summary>
        /// Update cache and dictionary info labels (called by ApplyLocalization and after count changes).
        /// </summary>
        private void UpdateInfoLabels()
        {
            CacheInfo.Text = LocalizationManager.GetString("CacheInfo", _viewModel.ConfigService.Cache.Count, _viewModel.CacheHits, _viewModel.ApiCalls, "");
            GlossaryInfo.Text = LocalizationManager.GetString("GlossaryInfo", _viewModel.Glossary.Count, _viewModel.GlossaryHits);
            FilterCountText.Text = LocalizationManager.GetString("TotalCount", _viewModel.Entries.Count);
        }

        private void EntriesGrid_Loaded(object sender, RoutedEventArgs e)
        {
            AttachColumnHeaderDoubleClick();
        }

        private void AttachColumnHeaderDoubleClick()
        {
            if (VisualTreeHelper.GetChildrenCount(EntriesGrid) == 0) return;

            var scrollViewer = FindVisualChild<System.Windows.Controls.Primitives.DataGridColumnHeadersPresenter>(EntriesGrid);
            if (scrollViewer == null) return;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(scrollViewer); i++)
            {
                if (VisualTreeHelper.GetChild(scrollViewer, i) is System.Windows.Controls.Primitives.DataGridColumnHeader header)
                {
                    header.MouseDoubleClick += (s, args) =>
                    {
                        if (header.Column != null)
                        {
                            header.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var currentWidth = header.Column.ActualWidth;
                                header.Column.Width = new DataGridLength(currentWidth, DataGridLengthUnitType.Pixel);
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                    };
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T typed) return typed;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private List<LocalizationEntry> GetSelectedEntries()
        {
            var list = new List<LocalizationEntry>();
            foreach (var item in EntriesGrid.SelectedItems)
            {
                if (item is LocalizationEntry entry)
                    list.Add(entry);
            }
            return list;
        }

        private void CtxCopyKey_Click(object sender, RoutedEventArgs e)
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0) return;
            Clipboard.SetText(string.Join("\n", entries.Select(en => en.Key)));
        }

        private void CtxCopyOriginal_Click(object sender, RoutedEventArgs e)
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0) return;
            Clipboard.SetText(string.Join("\n", entries.Select(en => en.Value)));
        }

        private void CtxCopyTranslation_Click(object sender, RoutedEventArgs e)
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0) return;
            Clipboard.SetText(string.Join("\n", entries.Select(en => en.Translation)));
        }

        private void CtxClearTranslation_Click(object sender, RoutedEventArgs e)
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0) return;
            foreach (var entry in entries)
            {
                entry.Translation = "";
            }
            AddLog($"🗑️ {LocalizationManager.GetString("LogClearedTranslation", entries.Count)}");
        }

        private async void CtxTranslateSelected_Click(object sender, RoutedEventArgs e)
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0)
            {
                MessageBox.Show(LocalizationManager.GetString("SelectFirstToTranslate"), LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            foreach (var entry in entries)
            {
                entry.Translation = "";
            }
            await TranslateEntries(entries, forceRefresh: true);
        }

        private void CtxSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in EntriesGrid.Items)
            {
                if (item is LocalizationEntry entry)
                    entry.IsSelected = true;
            }
            EntriesGrid.SelectAll();
        }

        private void CtxSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in EntriesGrid.Items)
            {
                if (item is LocalizationEntry entry)
                    entry.IsSelected = false;
            }
            EntriesGrid.UnselectAll();
        }

        private async void CtxEvaluate_Click(object sender, RoutedEventArgs e)
        {
            await RunEvaluateAsync();
        }

        private async void CtxVote_Click(object sender, RoutedEventArgs e)
        {
            await RunVoteAsync();
        }

        private async void EvaluateBtn_Click(object sender, RoutedEventArgs e)
        {
            await RunEvaluateAsync();
        }

        private async void VoteBtn_Click(object sender, RoutedEventArgs e)
        {
            await RunVoteAsync();
        }

        private async Task RunEvaluateAsync()
        {
            if (string.IsNullOrEmpty(_viewModel.AiTranslationService.ApiKey))
            {
                MessageBox.Show(LocalizationManager.GetString("EnterAPIKeyFirst"), LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var entries = GetSelectedEntries();
            if (entries.Count == 0)
                entries = _viewModel.Entries.Where(e => !string.IsNullOrEmpty(e.Translation)).ToList();

            if (entries.Count == 0)
            {
                AddLog($"⚠ {LocalizationManager.GetString("NoTranslatedToEvaluate")}");
                return;
            }

            var entry = entries.First();
            AddLog($"🤖 {LocalizationManager.GetString("LogEvaluating", entry.Key)}");
            EvalResult.Text = $"⏳ {LocalizationManager.GetString("EvalEvaluating")}";

            var result = await _viewModel.EvaluateEntry(entry);

            if (result != null)
            {
                var scoreText = result.Score >= 8 ? "🟢" : result.Score >= 5 ? "🟡" : "🔴";
                EvalResult.Text = $"{scoreText} {result.Score:F1}/10 - {entry.Key}";
                EvalResult.ToolTip = LocalizationManager.GetString("EvalScoreToolTip", result.Score, result.Explanation, result.Improvement);
                AddLog($"📊 {LocalizationManager.GetString("LogEvalResult", entry.Key, result.Score, result.Explanation)}");
                if (!string.IsNullOrEmpty(result.Improvement))
                    AddLog($"💡 {LocalizationManager.GetString("LogEvalSuggestion", result.Improvement)}");
            }
            else
            {
                EvalResult.Text = $"❌ {LocalizationManager.GetString("EvalFailed")}";
            }
        }

        private async Task RunVoteAsync()
        {
            if (string.IsNullOrEmpty(_viewModel.AiTranslationService.ApiKey))
            {
                MessageBox.Show(LocalizationManager.GetString("EnterAPIKeyFirst"), LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var entries = GetSelectedEntries();
            if (entries.Count == 0)
                entries = _viewModel.Entries.Where(e => !string.IsNullOrEmpty(e.Translation)).ToList();

            if (entries.Count == 0)
            {
                AddLog($"⚠ {LocalizationManager.GetString("NoTranslatedToVote")}");
                return;
            }

            var entry = entries.First();
            AddLog($"🗳 {LocalizationManager.GetString("LogVoting", entry.Key)}");
            EvalResult.Text = $"⏳ {LocalizationManager.GetString("EvalVoting")}";

            var result = await _viewModel.VoteEntry(entry);

            if (result != null)
            {
                EvalResult.Text = $"🗳 {LocalizationManager.GetString("Best")}: {result.BestTranslation}";
                EvalResult.ToolTip = LocalizationManager.GetString("VoteResultToolTip", result.AverageScore, result.ConsensusSummary, result.AgentResults.Count);
                AddLog($"🗳 {LocalizationManager.GetString("LogVoteConsensus", result.ConsensusSummary)}");

                foreach (var agentResult in result.AgentResults)
                {
                    AddLog(LocalizationManager.GetString("LogVoteAgentDetail", agentResult.ProviderName, agentResult.Score, agentResult.Explanation));
                }
            }
            else
            {
                EvalResult.Text = $"❌ {LocalizationManager.GetString("VoteFailed")}";
            }
        }

        private void CtxInvertSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in EntriesGrid.Items)
            {
                if (item is LocalizationEntry entry)
                    entry.IsSelected = !entry.IsSelected;
            }
        }

        private void EntriesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Sync DataGrid selection with IsSelected property
            foreach (LocalizationEntry entry in e.AddedItems)
            {
                entry.IsSelected = true;
            }
            
            foreach (LocalizationEntry entry in e.RemovedItems)
            {
                entry.IsSelected = false;
            }
        }

        // Handle checkbox changes to sync with DataGrid selection
        private void OnEntrySelectionChanged(LocalizationEntry entry, bool isSelected)
        {
            if (isSelected)
            {
                if (!EntriesGrid.SelectedItems.Contains(entry))
                {
                    EntriesGrid.SelectedItems.Add(entry);
                }
            }
            else
            {
                if (EntriesGrid.SelectedItems.Contains(entry))
                {
                    EntriesGrid.SelectedItems.Remove(entry);
                }
            }
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

                // If loading a translation file and source entries already exist, merge by Key
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

                    // Refresh info labels after merge
                    UpdateCacheInfo();
                    UpdateGlossaryInfo();

                    var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
                    view?.Refresh();

                    _viewModel.RestoreTranslationProgress(_viewModel.Entries);
                }
                else
                {
                    // Normal load: clear and rebuild
                    _viewModel.Entries.Clear();
                    
                    foreach (var entry in loadedEntries)
                    {
                        ProcessEntry(entry);
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
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ {LocalizationManager.GetString("ErrorLoadingXml", ex.Message)}");
                MessageBox.Show(LocalizationManager.GetString("ErrorLoadingXml", ex.Message), LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProcessEntry(LocalizationEntry entry)
        {
            entry.RowNumber = _viewModel.Entries.Count + 1;
            
            entry.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LocalizationEntry.IsSelected) && s is LocalizationEntry changedEntry)
                {
                    OnEntrySelectionChanged(changedEntry, changedEntry.IsSelected);
                }
            };

            var valueIsChinese = entry.Value.HasChineseChars();

            if (!string.IsNullOrEmpty(entry.Translation))
            {
                if (!string.IsNullOrWhiteSpace(entry.Value))
                    _viewModel.ConfigService.Cache.TryAdd(entry.Key, entry.Translation);
            }
            else if (valueIsChinese)
            {
                entry.Translation = entry.Value;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(entry.Value))
                {
                    if (_viewModel.ConfigService.Cache.TryGetValue(entry.Key, out var cachedByKey))
                    {
                        entry.Translation = cachedByKey;
                    }
                    else
                    {
                        var cacheKey = _viewModel.ConfigService.GetCacheKey(entry.Value);
                        if (cacheKey != null && _viewModel.ConfigService.Cache.TryGetValue(cacheKey, out var cachedByValue))
                        {
                            entry.Translation = cachedByValue;
                        }
                    }

                    TryApplyDictionary(entry);
                }
            }

            if (valueIsChinese)
            {
                entry.Value = "";
            }

            _viewModel.Entries.Add(entry);
        }

        /// <summary>
        /// Try to apply glossary lookup. Only exact-match on Key or Value.
        /// Term-level substitution is handled by BuildGlossaryContext via AI prompt.
        /// </summary>
        private bool TryApplyDictionary(LocalizationEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.Translation))
                return false;

            // Exact match on Key (e.g., "UPGRADE_TECH" → "科技升级")
            if (_viewModel.Glossary.TryGetValue(entry.Key, out var dictTranslation))
            {
                entry.Translation = dictTranslation;
                _viewModel.IncrementGlossaryHits();
                return true;
            }
            // Exact match on entire Value (single-word entries like "Jedi" → "绝地")
            if (_viewModel.Glossary.TryGetValue(entry.Value, out dictTranslation))
            {
                entry.Translation = dictTranslation;
                _viewModel.IncrementGlossaryHits();
                return true;
            }
            return false;
        }

        private void SaveXml(string fileName = "stable_us.xml")
        {
            try
            {
                _viewModel.SyncEntriesToCache(_viewModel.Entries);

                var entriesList = _viewModel.Entries.ToList();
                _viewModel.XmlRepository.SaveXml(fileName, entriesList);
                
                _viewModel.SaveConfig();
                UpdateCacheInfo();
                StatusText.Text = LocalizationManager.GetString("SavedEntries", _viewModel.Entries.Count, Path.GetFileName(fileName));
                AddLog($"💾 {LocalizationManager.GetString("LogXmlSaved", fileName, _viewModel.Entries.Count)}");
            }
            catch (Exception ex)
            {
                AddLog($"❌ {LocalizationManager.GetString("ErrorSavingXml", ex.Message)}");
                MessageBox.Show(LocalizationManager.GetString("ErrorSavingXml", ex.Message), LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> TranslateAsync(string text)
        {
            if (string.IsNullOrEmpty(_viewModel.AiTranslationService.ApiKey) || string.IsNullOrEmpty(_viewModel.AiTranslationService.Model))
                return null;

            // Check cache first
            var cacheKey = _viewModel.ConfigService.GetCacheKey(text);
            if (cacheKey != null && _viewModel.ConfigService.Cache.TryGetValue(cacheKey, out var cachedValue))
            {
                _viewModel.IncrementCacheHits();
                UpdateCacheInfo();
                return cachedValue;
            }

            // Dynamic retry logic based on model limits
            var maxRetries = _viewModel.AiTranslationService.ModelLimits.ContainsKey(_viewModel.AiTranslationService.Model) ? 
                Math.Min(5, _viewModel.AiTranslationService.ModelLimits[_viewModel.AiTranslationService.Model].requestsPerMinute / 10) : 3;
            maxRetries = Math.Max(2, maxRetries); // At least 2 retries

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Track this request for rate limiting
                    TrackRequest();

                    var translation = await _viewModel.AiTranslationService.TranslateSingleAsync(text);

                    if (!string.IsNullOrEmpty(translation))
                    {
                        _viewModel.ConfigService.Cache[cacheKey] = translation;
                        _viewModel.IncrementApiCalls();
                        
                        // Calculate and track costs
                        var inputChars = text.Length;
                        var outputChars = translation.Length;
                        _viewModel.TotalInputChars += inputChars;
                        _viewModel.TotalOutputChars += outputChars;
                        
                        var cost = _viewModel.AiTranslationService.CalculateCost(inputChars, outputChars, _viewModel.AiTranslationService.Model);
                        _viewModel.TotalCost += cost;
                        
                        UpdateCacheInfo();
                        AddLog($"💰 {LocalizationManager.GetString("LogSingleCost", cost.ToString("F6"), inputChars, outputChars)}");
                        
                        return translation;
                    }

                    return null;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    if (attempt < maxRetries - 1)
                    {
                        var delay = _viewModel.AiTranslationService.CalculateOptimalDelay() * (attempt + 2);
                        AddLog($"⏳ {LocalizationManager.GetString("LogRateLimit429", delay/1000, attempt + 1, maxRetries)}");
                        await Task.Delay(delay);
                        continue;
                    }
                    AddLog($"❌ {LocalizationManager.GetString("LogRateLimitExhausted", maxRetries)}");
                    return null;
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries - 1)
                    {
                        var delay = _viewModel.AiTranslationService.CalculateOptimalDelay();
                        AddLog($"⏳ {LocalizationManager.GetString("LogRetryError", delay/1000, ex.Message)}");
                        await Task.Delay(delay);
                        continue;
                    }
                    AddLog($"❌ {LocalizationManager.GetString("LogTranslationFailed", maxRetries, ex.Message)}");
                    return null;
                }
            }

            return null;
        }

        public async Task<List<string>> FetchAvailableModelsAsync(string apiKey, AIProvider? provider = null)
        {
            return await _viewModel.AiTranslationService.FetchAvailableModelsAsync(apiKey, provider ?? _viewModel.AiProvider);
        }

        // Public method to get model limits for SettingsWindow
        public AIProvider GetAiProvider()
        {
            return _viewModel.AiProvider;
        }

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

        private void TrackRequest()
        {
            _viewModel.TrackRequest();
        }

        private void UpdateCacheInfo()
        {
            var costText = _viewModel.TotalCost > 0 ? $" | {LocalizationManager.GetString("CostLabel")}: ${_viewModel.TotalCost:F4}" : "";
            CacheInfo.Text = $"💾 {LocalizationManager.GetString("CacheInfo", _viewModel.ConfigService.Cache.Count, _viewModel.CacheHits, _viewModel.ApiCalls, costText)}";
        }

        private void UpdateGlossaryInfo()
        {
            GlossaryInfo.Text = LocalizationManager.GetString("GlossaryInfo", _viewModel.Glossary.Count, _viewModel.GlossaryHits);
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.Text += $"[{timestamp}] {message}\n";
            LogTextBox.ScrollToEnd();
        }

        // Event Handlers
        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationManager.GetString("SelectXmlFile"),
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                DefaultExt = "xml",
                CheckFileExists = true,
                CheckPathExists = true,
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var dialog = new FileTypeDialog(this);
                dialog.ShowDialog();

                if (dialog.Result == FileTypeResult.Source)
                {
                    LoadXml(openFileDialog.FileName, isTranslationFile: false);
                }
                else if (dialog.Result == FileTypeResult.Translation)
                {
                    LoadXml(openFileDialog.FileName, isTranslationFile: true);
                }
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = LocalizationManager.GetString("SaveXmlFile"),
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                DefaultExt = "xml",
                FileName = "localized.xml",
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                SaveXml(saveFileDialog.FileName);
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow(_viewModel.AiTranslationService.ApiKey, _viewModel.AiTranslationService.Model, _viewModel.AiTranslationService.TargetLanguage, _viewModel.ProgramLanguage, _viewModel.CustomPrompt, _viewModel.ActiveExpertProfileName, _viewModel.AiProvider, this, _viewModel.ProfileManager);
            if (settings.ShowDialog() == true)
            {
                _viewModel.AiTranslationService.ApiKey = settings.ApiKey;
                _viewModel.AiTranslationService.Model = settings.Model;
                _viewModel.AiTranslationService.TargetLanguage = settings.TargetLanguage;
                _viewModel.AiProvider = settings.AiProvider;
                
                // Update program language if changed
                if (_viewModel.ProgramLanguage != settings.ProgramLanguage)
                {
                    _viewModel.ProgramLanguage = settings.ProgramLanguage;
                    LocalizationManager.CurrentLanguage = _viewModel.ProgramLanguage;
                    ApplyLocalization();
                }
                
                _viewModel.CustomPrompt = settings.CustomPrompt;
                _viewModel.ActiveExpertProfileName = settings.ActiveExpertProfile;
                
                _viewModel.SaveConfig();
                RefreshExpertProfileCombo();
                AddLog($"✅ {LocalizationManager.GetString("LogSettingsUpdated", _viewModel.AiProvider, _viewModel.AiTranslationService.Model, _viewModel.AiTranslationService.TargetLanguage, _viewModel.ActiveExpertProfileName.Length > 0 ? _viewModel.ActiveExpertProfileName : "None")}");
            }
        }

        private void StatsBtn_Click(object sender, RoutedEventArgs e)
        {
            var total = _viewModel.Entries.Count;
            var translated = _viewModel.Entries.Count(e => !string.IsNullOrEmpty(e.Translation));
            var untranslated = total - translated;
            var progress = total > 0 ? (translated * 100.0 / total) : 0;

            var stats = LocalizationManager.GetString("StatsInfo", total, translated, untranslated, progress, _viewModel.Glossary.Count, _viewModel.GlossaryHits, _viewModel.ConfigService.Cache.Count, _viewModel.CacheHits, _viewModel.ApiCalls);

            MessageBox.Show(stats, LocalizationManager.GetString("StatsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void TranslateSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = _viewModel.Entries.Where(entry => entry.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageBox.Show(LocalizationManager.GetString("SelectEntriesFirst"), LocalizationManager.GetString("MsgTip"));
                return;
            }

            foreach (var entry in selected)
            {
                entry.Translation = "";
            }

            await TranslateEntries(selected, forceRefresh: true);
        }

        private void BatchSizeTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel == null) return;
            
            if (int.TryParse(BatchSizeTxt.Text, out int value) && value > 0 && value <= 500)
            {
                _viewModel.BatchSize = value;
            }
        }

        private async void TranslateAllBtn_Click(object sender, RoutedEventArgs e)
        {
            var untranslated = _viewModel.Entries.Where(e => string.IsNullOrEmpty(e.Translation) && !string.IsNullOrEmpty(e.Value)).ToList();
            if (!untranslated.Any())
            {
                MessageBox.Show(LocalizationManager.GetString("NoUntranslatedEntries"), LocalizationManager.GetString("MsgTip"));
                return;
            }

            var result = MessageBox.Show(LocalizationManager.GetString("ConfirmTranslate", untranslated.Count), 
                LocalizationManager.GetString("MsgConfirm"), MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await TranslateEntries(untranslated);
            }
        }

        private async Task TranslateEntries(List<LocalizationEntry> entries, bool forceRefresh = false)
        {
            _translationCancellationTokenSource = new CancellationTokenSource();
            _viewModel.IsTranslationRunning = true;
            _viewModel.IsTranslationPaused = false;
            
            try
            {
                ShowControlButtons(true);
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = false;
                
                var successCount = 0;
                var failCount = 0;

                // Filter out entries that need translation
                var entriesToTranslate = entries.Where(e => !string.IsNullOrEmpty(e.Value) && string.IsNullOrEmpty(e.Translation)).ToList();
                
                if (!entriesToTranslate.Any())
                {
                    AddLog($"ℹ️ {LocalizationManager.GetString("LogNoTranslationNeeded")}");
                    StatusText.Text = LocalizationManager.GetString("NoEntriesForTranslation");
                    return;
                }

                // Create batches based on token limits
                var batches = _viewModel.Orchestrator.CreateBatches(entriesToTranslate, _viewModel.CustomPrompt, _viewModel.BatchSize);
                
                ProgressBar.Maximum = batches.Count;
                ProgressBar.Value = 0;

                AddLog($"🌍 {LocalizationManager.GetString("LogBatchStart", entriesToTranslate.Count, batches.Count, forceRefresh ? " (force refresh)" : "")}");
                AddLog($"📊 {LocalizationManager.GetString("LogBatchModel", _viewModel.AiTranslationService.Model)}");

                for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    // Check for cancellation
                    if (_translationCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        AddLog($"⏹️ {LocalizationManager.GetString("LogBatchCancelled", batchIndex + 1, batches.Count)}");
                        break;
                    }

                    // Handle pause
                    while (_viewModel.IsTranslationPaused && !_translationCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(500, _translationCancellationTokenSource.Token);
                    }

                    if (_translationCancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    var batch = batches[batchIndex];
                    var batchSize = batch.Count;
                    
                    StatusText.Text = LocalizationManager.GetString("TranslatingBatch", batchIndex + 1, batches.Count, batchSize);
                    ProgressBar.Value = batchIndex;
                    
                    AddLog($"🔄 {LocalizationManager.GetString("LogBatchProgress", batchIndex + 1, batches.Count, batchSize)}");

                    // Track request for rate limiting
                    TrackRequest();

                    var batchResults = await _viewModel.Orchestrator.TranslateBatchAsync(batch, forceRefresh, _viewModel.CustomPrompt);
                    
                    // Apply translations
                    var batchSuccessCount = 0;
                    var batchFailCount = 0;
                    
                    foreach (var entry in batch)
                    {
                        if (batchResults.ContainsKey(entry.Value))
                        {
                            entry.Translation = batchResults[entry.Value];
                            batchSuccessCount++;
                        }
                        else
                        {
                            batchFailCount++;
                        }
                    }

                    successCount += batchSuccessCount;
                    failCount += batchFailCount;

                    // Refresh cache info after each batch
                    UpdateCacheInfo();
                    UpdateGlossaryInfo();

                    if (batchFailCount > 0)
                    {
                        // Only log failed keys individually (for debugging)
                        var failedKeys = batch.Where(e => !batchResults.ContainsKey(e.Value))
                            .Select(e => e.Key.Length > 40 ? e.Key[..40] : e.Key);
                        AddLog($"❌ {LocalizationManager.GetString("LogBatchFails", batchFailCount, string.Join(", ", failedKeys.Take(5)))}");
                    }

                    // Incremental save: write progress to recovery file after each batch
                    _viewModel.ConfigService.SaveTranslationProgress(_viewModel.Entries);

                    AddLog($"📊 {LocalizationManager.GetString("LogBatchDone", batchIndex + 1, batches.Count, batchSuccessCount, batchFailCount)}");

                    // Use model-specific optimal delay between batches
                    if (batchIndex < batches.Count - 1 && !_translationCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        var delay = _viewModel.AiTranslationService.CalculateOptimalDelay();
                        StatusText.Text = LocalizationManager.GetString("WaitingRateLimit", delay/1000);
                        
                        try
                        {
                            await Task.Delay(delay, _translationCancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }

                ProgressBar.Value = batches.Count;

                // Auto-save if we have successful translations
                if (successCount > 0)
                {
                    SaveXml();
                    SaveCache();
                    AddLog($"💾 {LocalizationManager.GetString("LogCacheSaved")}");
                    // Translation complete — delete recovery file
                    DeleteProgressFile();
                }

                var statusMessage = _translationCancellationTokenSource.Token.IsCancellationRequested 
                    ? LocalizationManager.GetString("StatusStoppedResult", successCount, failCount)
                    : LocalizationManager.GetString("StatusBatchComplete", successCount, failCount);
                    
                StatusText.Text = statusMessage;
                AddLog($"🎉 {LocalizationManager.GetString("LogTranslationDone", statusMessage)}");
                
                if (failCount > 0)
                {
                    AddLog($"💡 {LocalizationManager.GetString("LogTipHeader")}");
                    AddLog(LocalizationManager.GetString("LogTip1"));
                    AddLog(LocalizationManager.GetString("LogTip2"));
                    AddLog(LocalizationManager.GetString("LogTip3"));
                }

                // Show efficiency stats
                var efficiency = entriesToTranslate.Count > 0 ? (successCount * 100.0 / entriesToTranslate.Count) : 0;
                AddLog($"📈 {LocalizationManager.GetString("LogEfficiency", efficiency.ToString("F1"), successCount, entriesToTranslate.Count)}");
                AddLog($"⚡ {LocalizationManager.GetString("LogBatchEfficiency", batches.Count, entriesToTranslate.Count, entriesToTranslate.Count - batches.Count)}");

                // Show rate limit summary
                if (_viewModel.AiTranslationService.ModelLimits.ContainsKey(_viewModel.AiTranslationService.Model))
                {
                    var limits = _viewModel.AiTranslationService.ModelLimits[_viewModel.AiTranslationService.Model];
                    var requestsInLastMinute = _viewModel.AiTranslationService.RecentRequests.Count;
                    AddLog($"📊 {LocalizationManager.GetString("LogRateLimitStatus", requestsInLastMinute, limits.requestsPerMinute)}");
                }
            }
            catch (OperationCanceledException)
            {
                AddLog($"⏹️ {LocalizationManager.GetString("LogTranslationCancelled")}");
                StatusText.Text = LocalizationManager.GetString("TranslationCancelled");
            }
            catch (Exception ex)
            {
                AddLog($"❌ {LocalizationManager.GetString("TranslationError", ex.Message)}");
                MessageBox.Show(LocalizationManager.GetString("TranslationError", ex.Message), LocalizationManager.GetString("MsgError"));
            }
            finally
            {
                _viewModel.IsTranslationRunning = false;
                _viewModel.IsTranslationPaused = false;
                ShowControlButtons(false);
                PauseBtn.Content = $"⏸️ {LocalizationManager.GetString("Pause")}"; // 重置暂停按钮
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.Value = 0;
                _translationCancellationTokenSource?.Dispose();
                _translationCancellationTokenSource = null;

                // Refresh cache and glossary info after translation completes
                UpdateCacheInfo();
                UpdateGlossaryInfo();
            }
        }

        private void DeleteProgressFile()
        {
            try
            {
                var progressPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translation_progress.json");
                if (File.Exists(progressPath))
                    File.Delete(progressPath);
            }
            catch (Exception ex)
            {
                AddLog($"⚠️ {LocalizationManager.GetString("LogProgressDeleteError", ex.Message)}");
            }
        }

        private void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(LocalizationManager.GetString("ConfirmClearCache", _viewModel.ConfigService.Cache.Count), 
                LocalizationManager.GetString("MsgConfirm"), MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.ConfigService.Cache.Clear();
                SaveCache();
                DeleteProgressFile();

                // Reset to initial state
                _viewModel.Entries.Clear();
                _viewModel.LastLoadedFilePath = null;
                _viewModel.ConfigService.Config.LastLoadedFilePath = null;
                _viewModel.SaveConfig();
                _viewModel.CacheHits = 0;
                _viewModel.ApiCalls = 0;
                _viewModel.GlossaryHits = 0;

                FilterKeyBox.Text = "";
                FilterBox.Text = "";
                FilterTranslationBox.Text = "";
                FilterCountText.Text = LocalizationManager.GetString("TotalCount", 0);
                StatusText.Text = LocalizationManager.GetString("Ready");

                UpdateCacheInfo();
                UpdateGlossaryInfo();

                var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
                view?.Refresh();

                AddLog($"🗑️ {LocalizationManager.GetString("LogCacheCleared")}");
            }
        }

        private void GlossaryBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = new GlossaryWindow(_viewModel.Glossary);
            window.Owner = this;
            window.ConflictsDetected += (_) =>
            {
                var entryList = _viewModel.Entries
                    .Where(ent => !string.IsNullOrEmpty(ent.Translation))
                    .Select(ent => (ent.Key, ent.Value, ent.Translation))
                    .ToList();
                var conflicts = _viewModel.Glossary.DetectConflicts(entryList);
                window.ShowConflicts(conflicts);
            };
            window.ShowDialog();
            // Re-apply glossary to entries
            int applied = 0;
            foreach (var entry in _viewModel.Entries)
            {
                if (TryApplyDictionary(entry))
                    applied++;
            }
            UpdateGlossaryInfo();
        }

        private void ClearDictBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(LocalizationManager.GetString("ConfirmClearDict", _viewModel.Glossary.Count), 
                LocalizationManager.GetString("MsgConfirm"), MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.Glossary.Clear();
                _viewModel.GlossaryHits = 0;
                UpdateGlossaryInfo();
                AddLog($"🗑️ {LocalizationManager.GetString("LogDictCleared")}");
            }
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filterTimer.Stop();
            _filterTimer.Start();
        }

        private void FilterTranslationBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filterTimer.Stop();
            _filterTimer.Start();
        }

        private void FilterKeyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filterTimer.Stop();
            _filterTimer.Start();
        }

        private void FilterTimer_Tick(object sender, EventArgs e)
        {
            _filterTimer.Stop();
            ApplyFilter();
        }

        private void ClearFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterKeyBox.Text = "";
            FilterBox.Text = "";
            FilterTranslationBox.Text = "";
            ApplyFilter();
        }


        private void BatchReplaceBtn_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new InputDialog(LocalizationManager.GetString("BatchReplaceDialogTitle"), 
                LocalizationManager.GetString("SearchTermLabel"), 
                LocalizationManager.GetString("ReplaceWithLabel"));
            
            if (inputDialog.ShowDialog() == true)
            {
                var searchText = inputDialog.Value1;
                var replaceText = inputDialog.Value2;
                
                if (string.IsNullOrEmpty(searchText))
                {
                    MessageBox.Show(LocalizationManager.GetString("SearchTermEmpty"), LocalizationManager.GetString("MsgTip"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Save state for undo before replacing
                var backup = new Dictionary<string, string>();
                foreach (var entry in _viewModel.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.Translation) && 
                        entry.Translation.Contains(searchText))
                    {
                        backup[entry.Key] = entry.Translation;
                    }
                }

                if (backup.Count > 0)
                {
                    _undoStack.Push(backup);
                }

                var matchCount = 0;
                foreach (var entry in _viewModel.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.Translation) && 
                        entry.Translation.Contains(searchText))
                    {
                        entry.Translation = entry.Translation.Replace(searchText, replaceText);
                        matchCount++;
                    }
                }

                var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
                view?.Refresh();

                AddLog($"🔄 {LocalizationManager.GetString("LogBatchReplace", matchCount)}");
                MessageBox.Show(LocalizationManager.GetString("ConfirmBatchReplace", matchCount), LocalizationManager.GetString("BatchReplaceDialogTitle"), 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UndoBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0)
            {
                MessageBox.Show(LocalizationManager.GetString("NothingToUndo"), LocalizationManager.GetString("MsgTip"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var backup = _undoStack.Pop();
            foreach (var entry in _viewModel.Entries)
            {
                if (backup.TryGetValue(entry.Key, out var originalTranslation))
                {
                    entry.Translation = originalTranslation;
                }
            }

            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
            view?.Refresh();

            AddLog($"↩️ {LocalizationManager.GetString("LogUndo")}");
            MessageBox.Show(LocalizationManager.GetString("UndoComplete", backup.Count), LocalizationManager.GetString("Undo"), 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyFilter()
        {
            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
            if (view == null) return;

            var keyFilter = FilterKeyBox.Text.Trim();
            var filter = FilterBox.Text.Trim();
            var translationFilter = FilterTranslationBox.Text.Trim();

            if (string.IsNullOrEmpty(keyFilter) && string.IsNullOrEmpty(filter) && string.IsNullOrEmpty(translationFilter))
            {
                view.Filter = null;
                FilterCountText.Text = LocalizationManager.GetString("TotalCount", _viewModel.Entries.Count);
            }
            else
            {
                view.Filter = item =>
                {
                    if (item is LocalizationEntry entry)
                    {
                        bool matchKeyFilter = true;
                        if (!string.IsNullOrEmpty(keyFilter))
                        {
                            matchKeyFilter = (entry.Key?.IndexOf(keyFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                        }

                        bool matchFilter = true;
                        if (!string.IsNullOrEmpty(filter))
                        {
                            matchFilter = (entry.Value?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                        }

                        bool matchTranslationFilter = true;
                        if (!string.IsNullOrEmpty(translationFilter))
                        {
                            matchTranslationFilter = (entry.Translation?.IndexOf(translationFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                        }

                        return matchKeyFilter && matchFilter && matchTranslationFilter;
                    }
                    return false;
                };
                view.Refresh();
                var visibleCount = view.Cast<LocalizationEntry>().Count();
                FilterCountText.Text = LocalizationManager.GetString("FilteredCount", visibleCount, _viewModel.Entries.Count);
            }
        }

        private void QuickSaveBtn_Click(object sender, RoutedEventArgs e)
        {
            QuickSave();
        }

        private void QuickSave()
        {
            try
            {
                // Commit any pending edit in DataGrid so changes are saved before reading
                EntriesGrid.CommitEdit(DataGridEditingUnit.Row, true);

                _viewModel.SyncEntriesToCache(_viewModel.Entries);
                SaveCache();
                _viewModel.SaveConfig();
                UpdateCacheInfo();
                UpdateGlossaryInfo();
                AddLog($"💾 {LocalizationManager.GetString("LogCacheUpdated", _viewModel.ConfigService.Cache.Count)}");
            }
            catch (Exception ex)
            {
                AddLog($"❌ {LocalizationManager.GetString("CacheSaveError", ex.Message)}");
                MessageBox.Show(LocalizationManager.GetString("CacheSaveError", ex.Message), LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCache()
        {
            try
            {
                _viewModel.ConfigService.SaveCache();
            }
            catch (Exception ex)
            {
                AddLog($"❌ {LocalizationManager.GetString("LogCacheWriteError", ex.Message)}");
            }
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.S && 
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                QuickSave();
            }
            else if (e.Key == System.Windows.Input.Key.Z && 
                     (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                UndoBtn_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.F && 
                     (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                ShowFindBar();
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                if (FindBarBorder.Visibility == Visibility.Visible)
                {
                    HideFindBar();
                    return;
                }
                FilterKeyBox.Text = "";
                FilterBox.Text = "";
                FilterTranslationBox.Text = "";
                ApplyFilter();
            }
        }

        private void ClearLogBtn_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Text = "";
            AddLog($"🗑️ {LocalizationManager.GetString("LogCleared")}");
        }

        private List<int> _findMatchIndices = new List<int>();
        private int _findCurrentIndex = -1;

        private void ShowFindBar()
        {
            FindBarBorder.Visibility = Visibility.Visible;
            FindTextBox.Focus();
            FindTextBox.SelectAll();
        }

        private void HideFindBar()
        {
            FindBarBorder.Visibility = Visibility.Collapsed;
            FindTextBox.Text = "";
            _findMatchIndices.Clear();
            _findCurrentIndex = -1;
            FindCountText.Text = "";
        }

        private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFindResults();
        }

        private void FindTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
                    FindPrevious();
                else
                    FindNext();
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                HideFindBar();
            }
        }

        private void FindNext_Click(object sender, RoutedEventArgs e)
        {
            FindNext();
        }

        private void FindPrev_Click(object sender, RoutedEventArgs e)
        {
            FindPrevious();
        }

        private void FindClose_Click(object sender, RoutedEventArgs e)
        {
            HideFindBar();
        }

        private void UpdateFindResults()
        {
            var keyword = FindTextBox.Text?.Trim();
            _findMatchIndices.Clear();
            _findCurrentIndex = -1;

            if (string.IsNullOrEmpty(keyword))
            {
                FindCountText.Text = "";
                return;
            }

            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource) as ICollectionView;
            var list = view?.Cast<LocalizationEntry>().ToList() ?? new List<LocalizationEntry>();

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry.Key.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.Value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.Translation.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _findMatchIndices.Add(i);
                }
            }

            FindCountText.Text = _findMatchIndices.Count > 0
                ? LocalizationManager.GetString("FindMatchCount", _findMatchIndices.Count)
                : LocalizationManager.GetString("FindNoMatch");

            if (_findMatchIndices.Count > 0)
            {
                _findCurrentIndex = 0;
                ScrollToMatch(_findMatchIndices[0]);
            }
        }

        private void FindNext()
        {
            if (_findMatchIndices.Count == 0) return;
            _findCurrentIndex = (_findCurrentIndex + 1) % _findMatchIndices.Count;
            ScrollToMatch(_findMatchIndices[_findCurrentIndex]);
            UpdateFindCountLabel();
        }

        private void FindPrevious()
        {
            if (_findMatchIndices.Count == 0) return;
            _findCurrentIndex--;
            if (_findCurrentIndex < 0)
                _findCurrentIndex = _findMatchIndices.Count - 1;
            ScrollToMatch(_findMatchIndices[_findCurrentIndex]);
            UpdateFindCountLabel();
        }

        private void UpdateFindCountLabel()
        {
            if (_findMatchIndices.Count == 0)
            {
                FindCountText.Text = LocalizationManager.GetString("FindNoMatch");
                return;
            }
            FindCountText.Text = $"{_findCurrentIndex + 1} / {_findMatchIndices.Count}";
        }

        private void ScrollToMatch(int index)
        {
            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource) as ICollectionView;
            var list = view?.Cast<LocalizationEntry>().ToList();
            if (list == null || index < 0 || index >= list.Count) return;

            var entry = list[index];
            EntriesGrid.SelectedItem = entry;
            EntriesGrid.ScrollIntoView(entry);
        }

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsTranslationRunning)
            {
                _viewModel.IsTranslationPaused = !_viewModel.IsTranslationPaused;
                
                if (_viewModel.IsTranslationPaused)
                {
                    PauseBtn.Content = $"▶️ {LocalizationManager.GetString("Resume")}";
                    StatusText.Text = LocalizationManager.GetString("TranslationPaused");
                    AddLog($"⏸️ {LocalizationManager.GetString("LogPaused")}");
                }
                else
                {
                    PauseBtn.Content = $"⏸️ {LocalizationManager.GetString("Pause")}";
                    StatusText.Text = LocalizationManager.GetString("TranslationResumed");
                    AddLog($"▶️ {LocalizationManager.GetString("LogResumed")}");
                }
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsTranslationRunning && _translationCancellationTokenSource != null)
            {
                _translationCancellationTokenSource.Cancel();
                AddLog($"⏹️ {LocalizationManager.GetString("LogStopped")}");
                StatusText.Text = LocalizationManager.GetString("TranslationStopped");
            }
        }

        private void ShowControlButtons(bool show)
        {
            PauseBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            StopBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            
            // Disable/enable translation buttons
            TranslateSelectedBtn.IsEnabled = !show;
            TranslateAllBtn.IsEnabled = !show;
        }

        private void RefreshExpertProfileCombo()
        {
            ExpertProfileCombo.Items.Clear();
            ExpertProfileCombo.Items.Add(new ComboBoxItem { Content = LocalizationManager.GetString("NoExpertDefault"), Tag = "" });

            foreach (var profile in _viewModel.ProfileManager.Profiles)
            {
                ExpertProfileCombo.Items.Add(new ComboBoxItem { Content = $"🧠 {profile.Name}", Tag = profile.Name });
            }

            // Select the active profile
            foreach (ComboBoxItem item in ExpertProfileCombo.Items)
            {
                if (item.Tag?.ToString() == _viewModel.ActiveExpertProfileName)
                {
                    item.IsSelected = true;
                    return;
                }
            }
            // Default: first item (no expert)
            if (ExpertProfileCombo.Items.Count > 0)
                ((ComboBoxItem)ExpertProfileCombo.Items[0]).IsSelected = true;
        }

        private void ExpertProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null) return;
            
            if (ExpertProfileCombo.SelectedItem is ComboBoxItem selectedItem)
            {
                var newProfile = selectedItem.Tag?.ToString() ?? "";
                if (_viewModel.ActiveExpertProfileName != newProfile)
                {
                    _viewModel.ActiveExpertProfileName = newProfile;
                    _viewModel.SaveConfig();
                    AddLog($"🧠 {LocalizationManager.GetString("LogExpertProfile", _viewModel.ActiveExpertProfileName.Length > 0 ? _viewModel.ActiveExpertProfileName : "None")}");
                }
            }
        }
    }

}
