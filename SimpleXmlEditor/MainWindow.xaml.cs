using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;
using SimpleXmlEditor.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleXmlEditor
{
    /// <summary>
    /// Pure UI layer. All business logic (translation, evaluation, voting, caching,
    /// consistency scanning) lives in MainViewModel / services. This class only:
    ///  - forwards UI events to ViewModel commands/methods
    ///  - renders ViewModel state via events (status, progress, evaluation results)
    ///  - manages window lifecycle, theme, and localization
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private ReviewExporter _reviewExporter = new ReviewExporter();
        private System.Windows.Threading.DispatcherTimer _filterTimer;

        private bool _isDarkMode = false;
        private bool _showUntranslatedOnly = false;
        // Suppresses checkbox→row selection sync while bulk-selecting a whole column.
        private bool _suppressSelectionSync = false;
        // Column-select mode: >= 0 means a whole column was selected via its letter strip.
        private int _selectedColumnIndex = -1;
        private bool _logCollapsed = false;
        private const double LogPanelDefaultWidth = 380;

        public MainWindow()
        {
            InitializeComponent();
            // ViewModel may be injected by DI or manually created
            _viewModel = App.Services?.GetService<MainViewModel>() ?? new MainViewModel();
            SubscribeViewModelEvents();

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

        // ═══════════════════════════════════════════════════════════
        //  ViewModel event wiring (rendering only — no business logic)
        // ═══════════════════════════════════════════════════════════

        private void SubscribeViewModelEvents()
        {
            _viewModel.LogMessage += msg => Dispatcher.Invoke(() => AddLog(msg));
            _viewModel.StatusMessageChanged += msg => Dispatcher.Invoke(() => StatusText.Text = msg);

            _viewModel.TranslationStarted += total => Dispatcher.Invoke(() =>
            {
                ShowControlButtons(true);
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Maximum = Math.Max(total, 1);
                ProgressBar.Value = 0;
                StatusIndicator.Text = _viewModel.GetTranslationStatusIndicator();
            });

            _viewModel.TranslationProgressChanged += (translated, total) => Dispatcher.Invoke(() =>
            {
                if (total > 0) ProgressBar.Maximum = total;
                ProgressBar.Value = translated;
                UpdateProgressDisplay();
            });

            _viewModel.TranslationFinished += () => Dispatcher.Invoke(() =>
            {
                ShowControlButtons(false);
                PauseBtn.Content = $"⏸️ {LocalizationManager.GetString("Pause")}";
                StatusIndicator.Text = "⚪";
                ProgressText.Text = "";
                SpeedText.Text = "";
                EtaText.Text = "";
                CostText.Text = "";
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.Value = 0;
                UpdateCacheInfo();
                UpdateGlossaryInfo();
            });

            _viewModel.TranslationErrorOccurred += msg => Dispatcher.Invoke(() =>
                MessageBox.Show(msg, LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error));

            _viewModel.EvaluationStatusText += msg => Dispatcher.Invoke(() => EvalResult.Text = msg);
            _viewModel.VotingStatusText += msg => Dispatcher.Invoke(() => EvalResult.Text = msg);

            _viewModel.EvaluationCompleted += outcome => Dispatcher.Invoke(() => RenderEvaluationOutcome(outcome));
            _viewModel.VotingCompleted += outcome => Dispatcher.Invoke(() => RenderVotingOutcome(outcome));
            _viewModel.PreTranslateCompleted += outcome => Dispatcher.Invoke(() => RenderPreTranslateOutcome(outcome));
            _viewModel.ConsistencyScanCompleted += issues => Dispatcher.Invoke(() => RenderConsistencyScan(issues));

            _viewModel.ConfirmationRequested += (message, title) =>
            {
                var tcs = new TaskCompletionSource<bool>();
                Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    tcs.SetResult(result == MessageBoxResult.Yes);
                });
                return tcs.Task;
            };

            _viewModel.MessageRequested += (message, title) => Dispatcher.Invoke(() =>
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
        }

        private void RenderEvaluationOutcome(EvaluationOutcome outcome)
        {
            if (outcome == null)
            {
                EvalResult.Text = "❌";
                return;
            }

            if (outcome.Failed)
            {
                EvalResult.Text = $"❌ {LocalizationManager.GetString("EvalNoResults")}";
                return;
            }

            if (outcome.SingleResult != null)
            {
                var result = outcome.SingleResult;
                var scoreText = result.Score >= 8 ? "🟢" : result.Score >= 5 ? "🟡" : "🔴";
                EvalResult.Text = $"{scoreText} {result.Score:F1}/10 - {outcome.EntryKey}";
                EvalResult.ToolTip = LocalizationManager.GetString("EvalScoreToolTip", result.Score, result.Explanation, result.Improvement);
                AddLog($"📊 {LocalizationManager.GetString("LogEvalResult", outcome.EntryKey, result.Score, result.Explanation)}");
                if (!string.IsNullOrEmpty(result.Improvement))
                    AddLog($"💡 {LocalizationManager.GetString("LogEvalSuggestion", result.Improvement)}");
                return;
            }

            // Batch evaluation
            if (outcome.Results.Count > 0)
            {
                ShowEvaluationWindow(outcome.Results, outcome.ResultMap);
                EvalResult.Text = $"📊 {LocalizationManager.GetString("EvalBatchSummary", outcome.AverageScore, outcome.HighCount, outcome.LowCount)}";
                EvalResult.ToolTip = LocalizationManager.GetString("LogBatchEvalComplete", outcome.Results.Count, outcome.AverageScore, outcome.HighCount, outcome.LowCount);
                AddLog($"📊 {LocalizationManager.GetString("LogBatchEvalComplete", outcome.Results.Count, outcome.AverageScore, outcome.HighCount, outcome.LowCount)}");
            }
        }

        private void RenderVotingOutcome(VotingOutcome outcome)
        {
            if (outcome == null)
            {
                EvalResult.Text = "❌";
                return;
            }

            if (outcome.Failed)
            {
                EvalResult.Text = $"❌ {LocalizationManager.GetString("VoteFailed")}";
                return;
            }

            if (outcome.HasSingleResult && outcome.SingleResult != null)
            {
                var result = outcome.SingleResult;
                EvalResult.Text = $"🗳 {LocalizationManager.GetString("Best")}: {result.BestTranslation}";
                EvalResult.ToolTip = LocalizationManager.GetString("VoteResultToolTip", result.AverageScore, result.ConsensusSummary, result.AgentResults.Count);
                AddLog($"🗳 {LocalizationManager.GetString("LogVoteConsensus", result.ConsensusSummary)}");

                foreach (var agentResult in result.AgentResults)
                {
                    AddLog(LocalizationManager.GetString("LogVoteAgentDetail", agentResult.ProviderName, agentResult.Score, agentResult.Explanation));
                }
                return;
            }

            EvalResult.Text = $"🗳 {LocalizationManager.GetString("VoteBatchResult", outcome.Completed, outcome.BestCount)}";
            AddLog($"🗳 {LocalizationManager.GetString("LogBatchVoteComplete", outcome.Completed, outcome.BestCount)}");
        }

        private void RenderPreTranslateOutcome(PreTranslateOutcome outcome)
        {
            if (outcome == null)
            {
                MessageBox.Show(LocalizationManager.GetString("SelectEntriesFirst"), LocalizationManager.GetString("MsgTip"));
                return;
            }

            var msg = LocalizationManager.GetString("PreTranslateResult", outcome.Total, outcome.GlossaryFilled, outcome.CacheFilled);
            AddLog($"🔮 {LocalizationManager.GetString("LogPreTranslate", outcome.Total, outcome.GlossaryFilled, outcome.CacheFilled)}");
            MessageBox.Show(msg, LocalizationManager.GetString("PreTranslate"), MessageBoxButton.OK, MessageBoxImage.Information);

            EntriesGrid.Items.Refresh();
            UpdateGlossaryInfo();
            UpdateCacheInfo();
        }

        private void RenderConsistencyScan(List<string> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                AddLog($"✅ {LocalizationManager.GetString("ConsistencyNoIssues")}");
                MessageBox.Show(LocalizationManager.GetString("ConsistencyNoIssues"),
                    LocalizationManager.GetString("ConsistencyScanTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var sb = new StringBuilder();
                foreach (var issue in issues)
                    sb.AppendLine(issue);
                AddLog($"⚠ {LocalizationManager.GetString("LogConsistencyScan", issues.Count, _viewModel.Entries.Count)}");
                AddLog(sb.ToString());
                MessageBox.Show(LocalizationManager.GetString("ConsistencyIssuesFound", issues.Count),
                    LocalizationManager.GetString("ConsistencyScanTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Window lifecycle / init
        // ═══════════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════════
        //  Localization / theme
        // ═══════════════════════════════════════════════════════════

        private void ApplyLocalization()
        {
            Func<string, string> L = LocalizationManager.GetString;  // shorthand

            // Update window title and app name
            this.Title = L("WindowTitle");
            AppNameText.Text = L("AppName");

            // Update main UI buttons
            LoadBtn.Content = $"📁 {L("Load")}";
            SaveBtn.Content = $"💾 {L("Save")}";
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
            ClearLogBtn.Content = "🗑️";  // emoji-only, no text to localize

            // Update cache clear button (audit #14: was hardcoded Chinese)
            ClearCacheBtn.Content = $"🗑 {L("ClearCache")}";

            // Update menu items
            MenuEvaluate.Header = $"🤖 {L("EvaluateBtn")} (F5)";
            MenuEvaluate.ToolTip = L("EvaluateToolTip");
            MenuVote.Header = $"🗳 {L("VoteBtn")} (F6)";
            MenuVote.ToolTip = L("VoteToolTip");
            MenuClearDict.Header = $"🗑️ {L("ClearDict")}";
            MenuExportReview.Header = $"📋 {L("ExportReview")}";

            // Update top-level menu headers
            MenuFile.Header = $"📁 {L("MenuFile")}";
            MenuEdit.Header = $"✏️ {L("MenuEdit")}";
            MenuView.Header = $"👁 {L("MenuView")}";
            MenuTranslate.Header = $"🌐 {L("MenuTranslate")}";
            MenuQuality.Header = $"⭐ {L("MenuQuality")}";
            MenuTools.Header = $"🔧 {L("MenuTools")}";
            MenuHelp.Header = $"❓ {L("MenuHelp")}";

            // Update menu items
            MenuOpen.Header = $"📂 {L("MenuOpen")} (Ctrl+O)";
            MenuSave.Header = $"💾 {L("MenuSave")} (Ctrl+S)";
            MenuExit.Header = L("MenuExit");
            MenuDarkMode.Header = _isDarkMode ? L("MenuLightMode") : L("MenuDarkMode");
            MenuShowFilter.Header = L("MenuShowFilter");
            MenuShowLog.Header = L("MenuShowLog");
            MenuTranslateSelected.Header = $"🎯 {L("TranslateSelected")}";
            MenuTranslateAll.Header = $"🚀 {L("TranslateAll")}";
            MenuSmartPreTrans.Header = $"🔮 {L("MenuSmartPre")}";
            MenuSmartPreTrans.ToolTip = L("PreTranslateTip");
            MenuConsistency.Header = $"🔍 {L("MenuConsistency")}";
            MenuShortcuts.Header = $"⌨ {L("MenuShortcuts")}";
            MenuAbout.Header = $"ℹ️ {L("MenuAbout")}";

            // Update settings/glossary/statistics menu items
            MenuSettings.Header = $"⚙️ {L("Settings")}";
            MenuStatistics.Header = $"📊 {L("Stats")}";
            MenuGlossary.Header = $"📖 {L("Glossary")}";
            MenuGlossary.ToolTip = L("TipGlossary");
            MenuUndo.Header = $"↩️ {L("Undo")}";
            MenuUndo.ToolTip = L("TipUndo");
            MenuReplace.Header = $"🔄 {L("BatchReplace")}";
            MenuReplace.ToolTip = L("TipBatchReplace");

            // Update batch label
            BatchLabelText.Text = $"{L("BatchLabel")}:";

            // Update DataGrid headers (columns: ✓, Status, Key, Original, Translation)
            if (EntriesGrid.Columns.Count >= 5)
            {
                EntriesGrid.Columns[0].Header = "✓";
                EntriesGrid.Columns[1].Header = L("Status");
                EntriesGrid.Columns[2].Header = L("Key");
                EntriesGrid.Columns[3].Header = L("Original");
                EntriesGrid.Columns[4].Header = L("Translation");
            }

            // Update filter tooltips
            FilterKeyBox.ToolTip = L("TipFilterKey");
            FilterBox.ToolTip = L("TipFilterOriginal");
            FilterTranslationBox.ToolTip = L("TipFilterTranslation");
            ClearFilterBtn.ToolTip = L("ClearFilter");

            // Update filter button text (replaces XAML "✕")
            ClearFilterBtn.Content = $"✕ {L("FilterClear")}";

            // Update untranslated toggle
            UntranslatedToggle.Content = L("ShowUntranslatedOnly");

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

        private void ApplyTheme()
        {
            if (_isDarkMode)
            {
                // ── Dark mode ──
                this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E"));

                EntriesGrid.AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27273A"));
                EntriesGrid.RowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E"));
                EntriesGrid.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4"));

                FilterKeyBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244"));
                FilterKeyBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4"));
                FilterBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244"));
                FilterBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4"));
                FilterTranslationBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244"));
                FilterTranslationBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4"));
            }
            else
            {
                // ── Light mode (defaults) ──
                this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F2F5"));

                EntriesGrid.AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFB"));
                EntriesGrid.RowBackground = new SolidColorBrush(Colors.White);
                EntriesGrid.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#37474F"));

                FilterKeyBox.Background = new SolidColorBrush(Colors.White);
                FilterKeyBox.Foreground = new SolidColorBrush(Colors.Black);
                FilterBox.Background = new SolidColorBrush(Colors.White);
                FilterBox.Foreground = new SolidColorBrush(Colors.Black);
                FilterTranslationBox.Background = new SolidColorBrush(Colors.White);
                FilterTranslationBox.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Public helpers used by child windows (SettingsWindow)
        // ═══════════════════════════════════════════════════════════

        public async Task<List<string>> FetchAvailableModelsAsync(string apiKey, AIProvider? provider = null)
        {
            return await _viewModel.AiTranslationService.FetchAvailableModelsAsync(apiKey, provider ?? _viewModel.AiProvider);
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

        // ═══════════════════════════════════════════════════════════
        //  DataGrid helpers
        // ═══════════════════════════════════════════════════════════

        private List<LocalizationEntry> GetSelectedEntries()
        {
            // Column-select mode: the whole column = every visible row.
            if (_selectedColumnIndex >= 0 && _selectedColumnIndex < EntriesGrid.Columns.Count)
            {
                var all = new List<LocalizationEntry>();
                foreach (var item in EntriesGrid.Items)
                {
                    if (item is LocalizationEntry entry) all.Add(entry);
                }
                return all;
            }

            var list = new List<LocalizationEntry>();
            foreach (var item in EntriesGrid.SelectedItems)
            {
                if (GetEntryFromSelectionItem(item) is LocalizationEntry entry && !list.Contains(entry))
                    list.Add(entry);
            }
            return list;
        }

        // With SelectionUnit=Cell, selection items can be DataGridCell (or the row item itself).
        private static LocalizationEntry GetEntryFromSelectionItem(object item)
        {
            return item switch
            {
                LocalizationEntry entry => entry,
                System.Windows.Controls.DataGridCell cell => cell.DataContext as LocalizationEntry,
                _ => null
            };
        }

        private void EntriesGrid_Loaded(object sender, RoutedEventArgs e)
        {
            AttachColumnHeaderEvents();
        }

        // Excel-style column header: double click auto-sizes the column.
        // (Whole-column selection happens via the letter strip button in the header template.)
        private void AttachColumnHeaderEvents()
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

        // Clicking a column letter strip selects the whole column.
        private void ColumnLetterBtn_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // prevent the column header sort from firing
            if (sender is Button btn && btn.Tag is System.Windows.Controls.Primitives.DataGridColumnHeader header && header.Column != null)
            {
                SelectEntireColumn(header.Column);
            }
        }

        // Select the whole column (Excel-style) without materializing thousands of
        // DataGrid cells — check every row's checkbox instead and track the column
        // index for GetSelectedEntries.
        private void SelectEntireColumn(DataGridColumn column)
        {
            _selectedColumnIndex = column.DisplayIndex;
            _suppressSelectionSync = true;
            try
            {
                EntriesGrid.SelectedCells.Clear();
                foreach (var item in EntriesGrid.Items)
                {
                    if (item is LocalizationEntry entry) entry.IsSelected = true;
                }
            }
            finally
            {
                _suppressSelectionSync = false;
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

        private void EntriesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // A manual selection (cell / row / checkbox) exits column-select mode.
            if (!_suppressSelectionSync)
            {
                _selectedColumnIndex = -1;
            }

            // Sync DataGrid selection with IsSelected property
            // (selection items are DataGridCells when SelectionUnit=Cell)
            foreach (var item in e.AddedItems)
            {
                if (GetEntryFromSelectionItem(item) is LocalizationEntry entry)
                    entry.IsSelected = true;
            }

            foreach (var item in e.RemovedItems)
            {
                if (GetEntryFromSelectionItem(item) is LocalizationEntry entry)
                    entry.IsSelected = false;
            }
        }

        // Handle checkbox changes to sync with DataGrid selection
        private void OnEntrySelectionChanged(LocalizationEntry entry, bool isSelected)
        {
            // Skip while bulk-selecting a whole column to avoid an event storm.
            if (_suppressSelectionSync) return;

            // In cell-selection mode rows cannot be added to SelectedItems directly;
            // toggle the row container instead (selects/deselects all cells in the row).
            if (EntriesGrid.ItemContainerGenerator.ContainerFromItem(entry) is System.Windows.Controls.DataGridRow row)
            {
                row.IsSelected = isSelected;
            }
        }

        private void OnEntryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalizationEntry.IsSelected) && sender is LocalizationEntry changedEntry)
            {
                OnEntrySelectionChanged(changedEntry, changedEntry.IsSelected);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Load / Save (XML + plugin formats)
        // ═══════════════════════════════════════════════════════════

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

                    // Update file tab
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

        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            var allExt = new List<string> { "*.xml" };
            allExt.AddRange(_viewModel.PluginLoader.GetAllSupportedExtensions().Select(ext => $"*{ext}"));
            var filterExts = string.Join(";", allExt);

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationManager.GetString("SelectXmlFile"),
                Filter = $"{Localization.LocalizationManager.GetString("FileFilterAllSupported")} ({filterExts})|{filterExts}|{Localization.LocalizationManager.GetString("FileFilterXml")} (*.xml)|*.xml|{Localization.LocalizationManager.GetString("FileFilterPo")} (*.po)|*.po|{Localization.LocalizationManager.GetString("FileFilterJson")} (*.json)|*.json|{Localization.LocalizationManager.GetString("FileFilterAll")} (*.*)|*.*",
                DefaultExt = "xml",
                CheckFileExists = true,
                CheckPathExists = true,
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var plugin = _viewModel.PluginLoader.FindFormatPlugin(openFileDialog.FileName);
                if (plugin != null)
                {
                    // Load via plugin
                    var entries = plugin.Load(openFileDialog.FileName);
                    if (entries.Count > 0)
                    {
                        _viewModel.Entries = new ObservableCollection<LocalizationEntry>(entries);
                        EntriesGrid.ItemsSource = _viewModel.Entries;
                        _viewModel.LastLoadedFilePath = openFileDialog.FileName;
                        StatusText.Text = LocalizationManager.GetString("LoadedEntries", entries.Count, plugin.FormatName);
                        AddLog($"📂 {LocalizationManager.GetString("LogLoadedFile", openFileDialog.FileName, entries.Count, plugin.FormatName)}");
                        UpdateCacheInfo();
                        UpdateGlossaryInfo();
                        return;
                    }
                    // Plugin returned 0 entries — fall through to XML loading
                }

                // Load as XML
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
            var allExt = new List<string> { "*.xml" };
            allExt.AddRange(_viewModel.PluginLoader.GetAllSupportedExtensions().Select(ext => $"*{ext}"));
            var filterExts = string.Join(";", allExt);

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = LocalizationManager.GetString("SaveXmlFile"),
                Filter = $"{Localization.LocalizationManager.GetString("FileFilterAllSupported")} ({filterExts})|{filterExts}|{Localization.LocalizationManager.GetString("FileFilterXml")} (*.xml)|*.xml|{Localization.LocalizationManager.GetString("FileFilterPo")} (*.po)|*.po|{Localization.LocalizationManager.GetString("FileFilterJson")} (*.json)|*.json|{Localization.LocalizationManager.GetString("FileFilterAll")} (*.*)|*.*",
                DefaultExt = "xml",
                FileName = "localized.xml",
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var plugin = _viewModel.PluginLoader.FindFormatPlugin(saveFileDialog.FileName);
                if (plugin != null)
                {
                    plugin.Save(saveFileDialog.FileName, _viewModel.Entries.ToList());
                    AddLog($"💾 {Localization.LocalizationManager.GetString("LogSavedFile", _viewModel.Entries.Count, plugin.FormatName, saveFileDialog.FileName)}");
                    StatusText.Text = Localization.LocalizationManager.GetString("StatusSavedPlugin", _viewModel.Entries.Count, plugin.FormatName);
                }
                else
                {
                    SaveXml(saveFileDialog.FileName);
                }
            }
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

        // ═══════════════════════════════════════════════════════════
        //  Info label updates
        // ═══════════════════════════════════════════════════════════

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

        private void UpdateProgressDisplay()
        {
            StatusIndicator.Text = _viewModel.GetTranslationStatusIndicator();
            ProgressText.Text = Localization.LocalizationManager.GetString("ProgressDisplay", _viewModel.ProgressPercentage, _viewModel.TranslatedCount, _viewModel.TotalCount);
            SpeedText.Text = _viewModel.TranslationSpeed > 0 ? $"⚡ {Localization.LocalizationManager.GetString("SpeedDisplay", _viewModel.TranslationSpeed)}" : "";
            EtaText.Text = !string.IsNullOrEmpty(_viewModel.EstimatedTimeRemaining) && _viewModel.EstimatedTimeRemaining != "..."
                ? $"⏱ {Localization.LocalizationManager.GetString("EtaDisplay", _viewModel.EstimatedTimeRemaining)}" : "";
            CostText.Text = _viewModel.TotalCost > 0 ? $"💰 {Localization.LocalizationManager.GetString("CostDisplay", _viewModel.TotalCost)}" : "";
        }

        private void DeleteProgressFile()
        {
            _viewModel.ConfigService.DeleteProgressFile();
        }

        // ═══════════════════════════════════════════════════════════
        //  Translation command forwarding
        // ═══════════════════════════════════════════════════════════

        private void TranslateSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TranslateSelectedCommand.Execute(null);
        }

        private void TranslateAllBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TranslateAllCommand.Execute(null);
        }

        private void CtxTranslateSelected_Click(object sender, RoutedEventArgs e)
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0)
            {
                MessageBox.Show(LocalizationManager.GetString("SelectFirstToTranslate"), LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var toClear = entries.Where(en => !string.IsNullOrEmpty(en.Translation)).ToList();
            if (toClear.Count > 0)
                _viewModel.PushUndoSnapshot(toClear);
            foreach (var entry in entries)
            {
                entry.Translation = "";
            }
            _ = _viewModel.TranslateEntriesAsync(entries, forceRefresh: true);
        }

        private void BatchSizeTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel == null) return;

            if (int.TryParse(BatchSizeTxt.Text, out int value) && value > 0 && value <= 500)
            {
                _viewModel.BatchSize = value;
            }
        }

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsTranslationRunning) return;

            _viewModel.IsTranslationPaused = !_viewModel.IsTranslationPaused;

            if (_viewModel.IsTranslationPaused)
            {
                PauseBtn.Content = $"▶️ {LocalizationManager.GetString("Resume")}";
                StatusText.Text = LocalizationManager.GetString("TranslationPaused");
                StatusIndicator.Text = "🟡";
                AddLog($"⏸️ {LocalizationManager.GetString("LogPaused")}");
            }
            else
            {
                PauseBtn.Content = $"⏸️ {LocalizationManager.GetString("Pause")}";
                StatusText.Text = LocalizationManager.GetString("TranslationResumed");
                StatusIndicator.Text = "🟢";
                AddLog($"▶️ {LocalizationManager.GetString("LogResumed")}");
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsTranslationRunning) return;

            _viewModel.CancelTranslation();
            AddLog($"⏹️ {LocalizationManager.GetString("LogStopped")}");
            StatusText.Text = LocalizationManager.GetString("TranslationStopped");
        }

        private void ShowControlButtons(bool show)
        {
            PauseBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            StopBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            // Disable/enable translation buttons
            TranslateSelectedBtn.IsEnabled = !show;
            TranslateAllBtn.IsEnabled = !show;
        }

        // ═══════════════════════════════════════════════════════════
        //  Evaluation / voting command forwarding
        // ═══════════════════════════════════════════════════════════

        private void EvaluateBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EvaluateCommand.Execute(GetSelectedEntries());
        }

        private void VoteBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.VoteCommand.Execute(GetSelectedEntries());
        }

        private void CtxEvaluate_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EvaluateCommand.Execute(GetSelectedEntries());
        }

        private void CtxVote_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.VoteCommand.Execute(GetSelectedEntries());
        }

        private void ShowEvaluationWindow(List<EvaluationResult> results, Dictionary<string, EvaluationResult> resultMap)
        {
            var window = new EvaluationWindow(results, resultMap, (key, suggestion) =>
            {
                var entry = _viewModel.Entries.FirstOrDefault(e => e.Key == key);
                if (entry != null)
                {
                    _viewModel.PushUndoSnapshot(new[] { entry });
                    entry.Translation = suggestion;
                    AddLog($"📝 {Localization.LocalizationManager.GetString("LogAppliedSuggestion", key)}");
                    EntriesGrid.Items.Refresh();
                }
            });
            window.Owner = this;
            window.Show();
        }

        private void MenuSmartPreTrans_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SmartPreTranslateCommand.Execute(GetSelectedEntries());
        }

        private void MenuConsistency_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ConsistencyScanCommand.Execute(null);
        }

        // ═══════════════════════════════════════════════════════════
        //  Context menu handlers
        // ═══════════════════════════════════════════════════════════

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
            var toClear = entries.Where(en => !string.IsNullOrEmpty(en.Translation)).ToList();
            if (toClear.Count == 0) return;
            _viewModel.PushUndoSnapshot(toClear);
            foreach (var entry in toClear)
            {
                entry.Translation = "";
            }
            AddLog($"🗑️ {LocalizationManager.GetString("LogClearedTranslation", toClear.Count)}");
        }

        private void CtxSelectAll_Click(object sender, RoutedEventArgs e)
        {
            LocalizationEntry.BulkUpdateSuppression = true;
            foreach (var item in EntriesGrid.Items)
            {
                if (item is LocalizationEntry entry)
                    entry.IsSelected = true;
            }
            LocalizationEntry.BulkUpdateSuppression = false;
            EntriesGrid.Items.Refresh();
        }

        private void CtxSelectNone_Click(object sender, RoutedEventArgs e)
        {
            LocalizationEntry.BulkUpdateSuppression = true;
            foreach (var item in EntriesGrid.Items)
            {
                if (item is LocalizationEntry entry)
                    entry.IsSelected = false;
            }
            LocalizationEntry.BulkUpdateSuppression = false;
            EntriesGrid.Items.Refresh();
        }

        private void CtxInvertSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in EntriesGrid.Items)
            {
                if (item is LocalizationEntry entry)
                    entry.IsSelected = !entry.IsSelected;
            }
        }

        private void CtxMarkReviewed_Click(object sender, RoutedEventArgs e)
        {
            SetReviewStatus(ReviewStatus.Reviewed);
        }

        private void CtxMarkNeedsFix_Click(object sender, RoutedEventArgs e)
        {
            SetReviewStatus(ReviewStatus.NeedsFix);
        }

        private void CtxMarkUnreviewed_Click(object sender, RoutedEventArgs e)
        {
            SetReviewStatus(ReviewStatus.NotReviewed);
        }

        private void SetReviewStatus(ReviewStatus status)
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0) entries = _viewModel.Entries.ToList();
            foreach (var entry in entries)
            {
                entry.ReviewStatus = status;
            }
            var statusLabel = status == ReviewStatus.Reviewed
                ? Localization.LocalizationManager.GetString("ReviewStatusReviewed")
                : status == ReviewStatus.NeedsFix
                    ? Localization.LocalizationManager.GetString("ReviewStatusNeedsFix")
                    : Localization.LocalizationManager.GetString("ReviewStatusNotReviewed");
            AddLog($"📋 {Localization.LocalizationManager.GetString("MarkedEntriesAsStatus", entries.Count, statusLabel)}");
        }

        private void ExportReviewBtn_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"review_report_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                var result = _reviewExporter.Export(saveDialog.FileName, _viewModel.Entries);
                AddLog($"📋 {Localization.LocalizationManager.GetString("ExportReviewLog", result.Total, result.Reviewed, result.NeedsFix, result.NotReviewed)}");
                MessageBox.Show(Localization.LocalizationManager.GetString("ExportReviewMsg", result.Total, result.Reviewed, result.NeedsFix, result.NotReviewed),
                    Localization.LocalizationManager.GetString("ReviewReport"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"❌ {Localization.LocalizationManager.GetString("ExportFailed", ex.Message)}");
                MessageBox.Show(Localization.LocalizationManager.GetString("ExportFailed", ex.Message), Localization.LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Settings / tools
        // ═══════════════════════════════════════════════════════════

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
            var translated = _viewModel.Entries.Count(entry => !string.IsNullOrEmpty(entry.Translation));
            var untranslated = total - translated;
            var progress = total > 0 ? (translated * 100.0 / total) : 0;

            var stats = LocalizationManager.GetString("StatsInfo", total, translated, untranslated, progress, _viewModel.Glossary.Count, _viewModel.GlossaryHits, _viewModel.ConfigService.Cache.Count, _viewModel.CacheHits, _viewModel.ApiCalls);

            MessageBox.Show(stats, LocalizationManager.GetString("StatsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
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
                CurrentFileTab.Text = LocalizationManager.GetString("NoFileLoaded");

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
            var candidates = _viewModel.Entries.Where(en => string.IsNullOrEmpty(en.Translation)).ToList();
            if (candidates.Count > 0)
                _viewModel.PushUndoSnapshot(candidates);
            int applied = 0;
            foreach (var entry in candidates)
            {
                if (_viewModel.TryApplyDictionary(entry))
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

        // ═══════════════════════════════════════════════════════════
        //  Filtering
        // ═══════════════════════════════════════════════════════════

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

        private void UntranslatedToggle_Click(object sender, RoutedEventArgs e)
        {
            _showUntranslatedOnly = UntranslatedToggle.IsChecked == true;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
            if (view == null) return;

            var keyFilter = FilterKeyBox.Text.Trim();
            var filter = FilterBox.Text.Trim();
            var translationFilter = FilterTranslationBox.Text.Trim();

            if (string.IsNullOrEmpty(keyFilter) && string.IsNullOrEmpty(filter) && string.IsNullOrEmpty(translationFilter) && !_showUntranslatedOnly)
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
                        if (_showUntranslatedOnly && !string.IsNullOrEmpty(entry.Translation))
                            return false;

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

        // ═══════════════════════════════════════════════════════════
        //  Batch replace / undo
        // ═══════════════════════════════════════════════════════════

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
                var affected = new List<LocalizationEntry>();
                foreach (var entry in _viewModel.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.Translation) &&
                        entry.Translation.Contains(searchText))
                    {
                        affected.Add(entry);
                    }
                }

                if (affected.Count > 0)
                {
                    _viewModel.PushUndoSnapshot(affected);
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
            var restored = _viewModel.UndoLast();
            if (restored == 0)
            {
                MessageBox.Show(LocalizationManager.GetString("NothingToUndo"), LocalizationManager.GetString("MsgTip"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
            view?.Refresh();

            AddLog($"↩️ {LocalizationManager.GetString("LogUndo")}");
            MessageBox.Show(LocalizationManager.GetString("UndoComplete", restored), LocalizationManager.GetString("Undo"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ═══════════════════════════════════════════════════════════
        //  Shortcuts / log / expert profile combo
        // ═══════════════════════════════════════════════════════════

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;

            if (e.Key == System.Windows.Input.Key.S && ctrl)
            {
                e.Handled = true;
                QuickSave();
            }
            else if (e.Key == System.Windows.Input.Key.O && ctrl)
            {
                e.Handled = true;
                LoadBtn_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.Z && ctrl)
            {
                e.Handled = true;
                UndoBtn_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.T && ctrl)
            {
                e.Handled = true;
                var shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
                if (shift)
                    TranslateAllBtn_Click(null, null);
                else
                    TranslateSelectedBtn_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.F5)
            {
                e.Handled = true;
                EvaluateBtn_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.F6)
            {
                e.Handled = true;
                VoteBtn_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
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

        // Collapse / expand the right-side activity log panel.
        private void ToggleLogBtn_Click(object sender, RoutedEventArgs e)
        {
            _logCollapsed = !_logCollapsed;
            LogColumn.Width = _logCollapsed ? new GridLength(0) : new GridLength(LogPanelDefaultWidth);
            LogPanel.Visibility = _logCollapsed ? Visibility.Collapsed : Visibility.Visible;
            ToggleLogBtn.Content = _logCollapsed ? "▶" : "◀";
            ToggleLogBtn.ToolTip = _logCollapsed ? "Expand log" : "Collapse log";
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

        // ═══════════════════════════════════════════════════════════
        //  Menu handlers
        // ═══════════════════════════════════════════════════════════

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuDarkMode_Click(object sender, RoutedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ApplyTheme();
            ApplyLocalization();
        }

        private void MenuShowFilter_Click(object sender, RoutedEventArgs e)
        {
            // Reserved for future filter bar visibility toggle
        }

        private void MenuShowLog_Click(object sender, RoutedEventArgs e)
        {
            // Reserved for future log panel visibility toggle
        }

        private void MenuShortcuts_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(LocalizationManager.GetString("ShortcutsText"), LocalizationManager.GetString("ShortcutsTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(LocalizationManager.GetString("AboutText"), LocalizationManager.GetString("AboutTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseFileBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Entries.Clear();
            _viewModel.LastLoadedFilePath = null;
            _viewModel.ConfigService.Config.LastLoadedFilePath = null;
            _viewModel.SaveConfig();

            FilterKeyBox.Text = "";
            FilterBox.Text = "";
            FilterTranslationBox.Text = "";
            FilterCountText.Text = LocalizationManager.GetString("TotalCount", 0);
            CurrentFileTab.Text = LocalizationManager.GetString("NoFileLoaded");
            StatusText.Text = LocalizationManager.GetString("Ready");

            UpdateCacheInfo();
            UpdateGlossaryInfo();
            AddLog($"📂 {LocalizationManager.GetString("LogFileClosed")}");
        }
    }

    /// <summary>Converts a DataGrid column display index to an Excel-style letter (0 → A, 1 → B, ...).</summary>
    public class IndexToLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index && index >= 0 && index < 26)
            {
                return ((char)('A' + index)).ToString();
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
