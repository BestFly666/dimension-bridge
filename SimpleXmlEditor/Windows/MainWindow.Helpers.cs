using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    public partial class MainWindow
    {
        // 日志文本框长度上限：无限制时每次 Text += 都会重建整个字符串并全文重排，
        // 大批量翻译（数百条日志）会导致 UI 线程越来越卡。达到上限后丢弃最旧内容。
        private const int MaxLogChars = 30000;

        /// <summary>
        /// 日志入队（不再立即渲染）。由 _uiFlushTimer 每 250ms 合并写入文本框，
        /// 避免每批多个 BeginInvoke 触发 N 次全量字符串重建与重排。
        /// </summary>
        private void AddLog(string message) => _pendingLogs.Enqueue(message);

        /// <summary>
        /// UI 合并渲染：把积压的日志/状态/进度一次性刷新到控件。
        /// 在 UI 线程调用（DispatcherTimer.Tick 或翻译结束时兜底）。
        /// </summary>
        private void UiFlushTimer_Tick(object sender, EventArgs e) => FlushPendingUi();

        private void FlushPendingUi()
        {
            // 合并渲染积压日志：一次 Text 更新（含截断 + 滚动），而非每条一次
            if (!_pendingLogs.IsEmpty)
            {
                var sb = new StringBuilder(LogTextBox.Text.Length + 256);
                sb.Append(LogTextBox.Text);
                while (_pendingLogs.TryDequeue(out var msg))
                    sb.Append($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                if (sb.Length > MaxLogChars)
                    sb.Remove(0, sb.Length - MaxLogChars); // 丢弃最旧内容，控制重建/渲染成本有上界
                LogTextBox.Text = sb.ToString();
                LogTextBox.ScrollToEnd();
            }

            // 状态栏用最新值（中间值丢弃）
            var status = _pendingStatusText;
            if (status != null)
            {
                StatusText.Text = status;
                _pendingStatusText = null;
            }

            // 进度用最新值渲染一次
            if (_pendingProgressTranslated >= 0)
            {
                var total = _pendingProgressTotal;
                if (total > 0) ProgressBar.Maximum = total;
                ProgressBar.Value = _pendingProgressTranslated;
                UpdateProgressDisplay();
                _pendingProgressTranslated = -1;
                _pendingProgressTotal = -1;
            }
        }

        private void UpdateCacheInfo()
        {
            var costText = _viewModel.TotalCost > 0 ? $" | {LocalizationManager.GetString("CostLabel")}: ¥{_viewModel.TotalCost * _viewModel.CostExchangeRate:F2}" : "";
            CacheInfo.Text = $"💾 {LocalizationManager.GetString("CacheInfo", _viewModel.ConfigService.Cache.Count, _viewModel.CacheHits, _viewModel.ApiCalls, costText)}";
        }

        private void UpdateGlossaryInfo()
        {
            GlossaryInfo.Text = LocalizationManager.GetString("GlossaryInfo", _viewModel.Glossary.Count, _viewModel.GlossaryHits);
        }

        /// <summary>
        /// 安全刷新 DataGrid：先强制提交未完成的单元格/行编辑，再 Refresh。
        /// DataGrid 处于编辑状态（单元格文本被选中/正在编辑）时，直接 Refresh 或修改
        /// CollectionView.Filter 会抛 InvalidOperationException（数据源在编辑中被重新评估），
        /// 表现为 Ctrl+Z、批量替换、筛选等操作在"选中单元格内容"后崩溃。
        /// </summary>
        private void SafeRefreshDataGrid()
        {
            ExitDataGridEditing();
            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
            view?.Refresh();
        }

        /// <summary>
        /// 提交 DataGrid 未完成的编辑（单元格 + 行）。在设置筛选 / Refresh / 排序
        /// 等会重新评估视图的操作之前调用，异常静默忽略（无当前编辑项时 CommitEdit 返回 false）。
        /// </summary>
        private void ExitDataGridEditing()
        {
            try
            {
                EntriesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                EntriesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch
            {
                // 无当前编辑项 / 提交异常时忽略：DataGrid 编辑状态已被清除
            }
        }

        private void UpdateProgressDisplay()
        {
            StatusIndicator.Text = _viewModel.GetTranslationStatusIndicator();
            ProgressText.Text = LocalizationManager.GetString("ProgressDisplay", _viewModel.ProgressPercentage, _viewModel.TranslatedCount, _viewModel.TotalCount);
            SpeedText.Text = _viewModel.TranslationSpeed > 0 ? $"⚡ {LocalizationManager.GetString("SpeedDisplay", _viewModel.TranslationSpeed)}" : "";
            EtaText.Text = !string.IsNullOrEmpty(_viewModel.EstimatedTimeRemaining) && _viewModel.EstimatedTimeRemaining != "..."
                ? $"⏱ {LocalizationManager.GetString("EtaDisplay", _viewModel.EstimatedTimeRemaining)}" : "";
            CostText.Text = _viewModel.TotalCost > 0 ? $"💰 {LocalizationManager.GetString("CostDisplay", _viewModel.TotalCost * _viewModel.CostExchangeRate)}" : "";
        }

        private void DeleteProgressFile()
        {
            _viewModel.ConfigService.DeleteProgressFile();
        }

        private void ShowControlButtons(bool show)
        {
            PauseBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            StopBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            TranslateSelectedBtn.IsEnabled = !show;
            TranslateAllBtn.IsEnabled = !show;
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
                    AddLog($"📝 {LocalizationManager.GetString("LogAppliedSuggestion", key)}");
                    SafeRefreshDataGrid();
                }
            });
            window.Owner = this;
            window.Show();
        }
    }
}
