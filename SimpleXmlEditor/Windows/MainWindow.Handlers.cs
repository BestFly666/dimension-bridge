using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;
using SimpleXmlEditor.ViewModels;

namespace SimpleXmlEditor
{
    /// <summary>
    /// MainWindow partial: ViewModel 事件订阅与结果渲染回调
    /// （评估 / 投票 / 预翻译 / 一致性扫描 / 词汇冲突展示）。
    /// </summary>
    public partial class MainWindow
    {
        private void SubscribeViewModelEvents()
        {
            // 高频事件（日志/状态/进度）→ 只入队/存最新值，由 _uiFlushTimer 合并渲染。
            // 此前每个事件都 Dispatcher.BeginInvoke，无合并无积压上限，批次多时 UI 线程
            // 处理不过来 → 队列积压 → 越来越卡。
            _viewModel.LogMessage += msg => _pendingLogs.Enqueue(msg);
            _viewModel.StatusMessageChanged += msg => _pendingStatusText = msg;

            // TranslationProgressChanged 只记录最新值，flush 时渲染一次
            _viewModel.TranslationProgressChanged += (translated, total) =>
            {
                _pendingProgressTranslated = translated;
                _pendingProgressTotal = total;
            };

            // 低频/关键事件仍立即处理（BeginInvoke 保证后台线程不阻塞）

            _viewModel.TranslationStarted += total => Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowControlButtons(true);
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Maximum = Math.Max(total, 1);
                ProgressBar.Value = 0;
                StatusIndicator.Text = _viewModel.GetTranslationStatusIndicator();
            }));

            _viewModel.TranslationFinished += () => Dispatcher.BeginInvoke(new Action(() =>
            {
                // 翻译结束：先同步 flush 残留的日志/进度，再恢复 UI
                FlushPendingUi();
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
            }));

            _viewModel.TranslationErrorOccurred += msg => Dispatcher.BeginInvoke(new Action(() =>
                MessageBox.Show(msg, LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error)));

            _viewModel.EvaluationStatusText += msg => Dispatcher.BeginInvoke(new Action(() => EvalResult.Text = msg));
            _viewModel.VotingStatusText += msg => Dispatcher.BeginInvoke(new Action(() => EvalResult.Text = msg));

            _viewModel.EvaluationCompleted += outcome => Dispatcher.BeginInvoke(new Action(() => RenderEvaluationOutcome(outcome)));
            _viewModel.VotingCompleted += outcome => Dispatcher.BeginInvoke(new Action(() => RenderVotingOutcome(outcome)));
            _viewModel.PreTranslateCompleted += outcome => Dispatcher.BeginInvoke(new Action(() => RenderPreTranslateOutcome(outcome)));
            _viewModel.ConsistencyScanCompleted += issues => Dispatcher.BeginInvoke(new Action(() => RenderConsistencyScan(issues)));

            // ConfirmationRequested 需要返回值 → 保留 Invoke（同步等待用户确认）
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

            _viewModel.MessageRequested += (message, title) => Dispatcher.BeginInvoke(new Action(() =>
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information)));
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
                        SafeRefreshDataGrid();
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

            SafeRefreshDataGrid();
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
    }
}
