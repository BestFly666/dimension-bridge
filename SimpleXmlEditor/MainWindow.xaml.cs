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
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SimpleXmlEditor.Dictionary;
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
    ///
    /// Split into partial classes:
    ///   MainWindow.Localization.cs  — ApplyLocalization, UpdateInfoLabels
    ///   MainWindow.Theme.cs        — ApplyTheme
    ///   MainWindow.Grid.cs         — DataGrid interaction, selection, column/row resize
    ///   MainWindow.Helpers.cs      — AddLog, UpdateCacheInfo, ShowControlButtons, ShowEvaluationWindow
    ///   MainWindow.Events.cs       — All UI event handlers (clicks, filters, menus, shortcuts)
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private ReviewExporter _reviewExporter = new ReviewExporter();
        private System.Windows.Threading.DispatcherTimer _filterTimer;
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(5);

        private bool _isDarkMode = false;
        private bool _showUntranslatedOnly = false;
        private bool _suppressSelectionSync = false;
        private bool _suppressSelectionChanged = false;
        private bool _logCollapsed = false;
        private const double LogPanelDefaultWidth = 380;

        public MainWindow()
        {
            InitializeComponent();
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

            // Excel 式自动保存：每 5 分钟自动保存缓存与配置（不直接写 XML，防止覆盖源文件）
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = AutoSaveInterval
            };
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            _autoSaveTimer.Start();

            this.KeyDown += MainWindow_KeyDown;

            InitializeFromConfig();
        }

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

                // 同步更新 DataGrid 中对应行的评分
                UpdateEntryScore(outcome.EntryKey, result.Score, result.Explanation, result.Improvement);
                return;
            }

            if (outcome.Results.Count > 0)
            {
                // 直接更新 DataGrid 评分列，不再弹窗（避免 EvaluationWindow UI 崩溃）
                int updated = 0;
                foreach (var entry in _viewModel.Entries)
                {
                    if (outcome.ResultMap.TryGetValue(entry.Key, out var result))
                    {
                        entry.EvaluationScore = result.Score;
                        entry.EvaluationImprovement = string.IsNullOrEmpty(result.Improvement)
                            ? result.Explanation
                            : $"{result.Explanation}\n💡 {result.Improvement}";
                        updated++;
                    }
                }

                EvalResult.Text = $"📊 {LocalizationManager.GetString("EvalBatchSummary", outcome.AverageScore, outcome.HighCount, outcome.LowCount)}";
                EvalResult.ToolTip = LocalizationManager.GetString("LogBatchEvalComplete", outcome.Results.Count, outcome.AverageScore, outcome.HighCount, outcome.LowCount);
                AddLog($"📊 {LocalizationManager.GetString("LogBatchEvalComplete", outcome.Results.Count, outcome.AverageScore, outcome.HighCount, outcome.LowCount)}");
                AddLog($"✅ {LocalizationManager.GetString("LogScoreUpdated", updated)}");
                _viewModel.SyncScoresToCache(_viewModel.Entries);
                _viewModel.SaveScoreCache();
            }
        }

        /// <summary>
        /// 更新 DataGrid 中指定 Key 的 entry 评分。
        /// </summary>
        private void UpdateEntryScore(string entryKey, double score, string explanation, string improvement)
        {
            if (string.IsNullOrEmpty(entryKey)) return;
            foreach (var entry in _viewModel.Entries)
            {
                if (entry.Key == entryKey)
                {
                    entry.EvaluationScore = score;
                    entry.EvaluationImprovement = string.IsNullOrEmpty(improvement)
                        ? explanation
                        : $"{explanation}\n💡 {improvement}";
                    break;
                }
            }
            _viewModel.SyncScoresToCache(_viewModel.Entries);
            _viewModel.SaveScoreCache();
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

                // 同步更新 DataGrid 评分列（用投票平均分）
                UpdateEntryScore(result.EntryKey, result.AverageScore, result.ConsensusSummary, "");

                // 若 AI 建议改动译文 → 弹出候选对比窗口由用户决定
                var currentTranslation = _viewModel.Entries
                    .FirstOrDefault(en => en.Key == result.EntryKey)?.Translation ?? "";
                if (!string.IsNullOrEmpty(result.BestTranslation) && result.BestTranslation != currentTranslation)
                    OpenVotingReview(new List<VotingResult> { result });
                return;
            }

            // 批量投票：更新 DataGrid 评分列 + 日志详情
            if (outcome.Results != null && outcome.Results.Count > 0)
            {
                int updated = 0;
                foreach (var vr in outcome.Results)
                {
                    if (string.IsNullOrEmpty(vr.EntryKey)) continue;
                    foreach (var entry in _viewModel.Entries)
                    {
                        if (entry.Key == vr.EntryKey)
                        {
                            entry.EvaluationScore = vr.AverageScore;
                            entry.EvaluationImprovement = $"🗳 {LocalizationManager.GetString("VoteBestTranslation")}: {vr.BestTranslation}\n{vr.ConsensusSummary}";
                            updated++;
                            break;
                        }
                    }
                }
                AddLog($"✅ {LocalizationManager.GetString("LogScoreUpdated", updated)}");
                if (updated > 0)
                {
                    _viewModel.SyncScoresToCache(_viewModel.Entries);
                    _viewModel.SaveScoreCache();
                }
            }

            EvalResult.Text = $"🗳 {LocalizationManager.GetString("VoteBatchResultDetail", outcome.Completed, outcome.BestCount, outcome.NeedsReview.Count)}";
            AddLog($"🗳 {LocalizationManager.GetString("LogBatchVoteComplete", outcome.Completed, outcome.BestCount)}");
            if (outcome.NeedsReview.Count > 0)
                OpenVotingReview(outcome.NeedsReview);
        }

        /// <summary>
        /// 弹出投票候选对比窗口：列出 AI 建议改动的条目及其候选译文（带评分），
        /// 用户确认所选后批量应用到条目。
        /// </summary>
        private void OpenVotingReview(List<VotingResult> results)
        {
            if (results == null || results.Count == 0) return;

            var currentMap = new Dictionary<string, string>();
            foreach (var en in _viewModel.Entries)
            {
                if (results.Any(r => r.EntryKey == en.Key))
                    currentMap[en.Key] = en.Translation ?? "";
            }

            var window = new VotingReviewWindow(results, currentMap) { Owner = this };
            if (window.ShowDialog() == true)
            {
                var selections = window.GetSelections();
                if (selections.Count > 0)
                {
                    var applied = _viewModel.ApplyVotingSelections(selections);
                    if (applied > 0)
                    {
                        EntriesGrid.Items.Refresh();
                        _viewModel.SyncScoresToCache(_viewModel.Entries);
                        _viewModel.SaveScoreCache();
                        AddLog($"✅ {LocalizationManager.GetString("VoteAppliedBest", applied)}");
                    }
                }
            }
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

        private void RenderConsistencyScan(List<ConsistencyIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                AddLog($"✅ {LocalizationManager.GetString("ConsistencyNoIssues")}");
                MessageBox.Show(LocalizationManager.GetString("ConsistencyNoIssues"),
                    LocalizationManager.GetString("ConsistencyScanTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new StringBuilder();
            foreach (var issue in issues)
            {
                var translations = issue.Translations;
                for (int i = 0; i < translations.Count - 1; i++)
                {
                    for (int j = i + 1; j < translations.Count; j++)
                    {
                        sb.AppendLine(LocalizationManager.GetString("ConsistencyIssueDesc",
                            issue.Source, translations[i], translations[j]));
                    }
                }
            }
            AddLog($"⚠ {LocalizationManager.GetString("LogConsistencyScan", issues.Count, _viewModel.Entries.Count)}");
            AddLog(sb.ToString());

            // 询问是否导出报告，方便对照修改
            if (MessageBox.Show(
                LocalizationManager.GetString("ConsistencyExportPrompt", issues.Count),
                LocalizationManager.GetString("ConsistencyScanTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"consistency_report_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (saveDialog.ShowDialog() != true) return;

            try
            {
                _reviewExporter.ExportConsistency(saveDialog.FileName, issues);
                AddLog($"✅ {LocalizationManager.GetString("ConsistencyExported", saveDialog.FileName)}");
                MessageBox.Show(LocalizationManager.GetString("ConsistencyExported", saveDialog.FileName),
                    LocalizationManager.GetString("ReviewReport"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"❌ {LocalizationManager.GetString("ExportFailed", ex.Message)}");
                MessageBox.Show(LocalizationManager.GetString("ExportFailed", ex.Message),
                    LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowConflictResults(List<GlossaryConflict> conflicts)
        {
            if (conflicts == null || conflicts.Count == 0)
            {
                MessageBox.Show(LocalizationManager.GetString("GlossaryNoConflicts"),
                    LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new ConflictDialog(conflicts);
            dialog.Owner = this;
            dialog.ShowDialog();
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
