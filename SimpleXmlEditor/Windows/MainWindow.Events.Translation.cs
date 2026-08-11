using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    public partial class MainWindow
    {
        private void TranslateSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            // 防重入：翻译运行中不允许再次启动/清空译文，避免覆盖流水线状态
            if (_viewModel.IsTranslationRunning) return;

            // 统一走 GetSelectedEntries（支持逻辑选择标志），不依赖 IsSelected
            var entries = GetSelectedEntries();
            if (entries.Count == 0)
            {
                MessageBox.Show(LocalizationManager.GetString("SelectEntriesFirst"), LocalizationManager.GetString("MsgTip"));
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

        private void TranslateAllBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TranslateAllCommand.Execute(null);
        }

        private void CtxTranslateSelected_Click(object sender, RoutedEventArgs e)
        {
            // 防重入：翻译运行中不允许再次启动/清空译文
            if (_viewModel.IsTranslationRunning) return;

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

        private void ConcurrentBatchesTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel == null) return;

            if (int.TryParse(ConcurrentBatchesTxt.Text, out int value) && value >= 1 && value <= 50)
            {
                _viewModel.MaxConcurrentBatches = value;
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

        private void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(LocalizationManager.GetString("ConfirmClearCache", _viewModel.ConfigService.Cache.Count),
                LocalizationManager.GetString("MsgConfirm"), MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.ConfigService.Cache.Clear();
                SaveCache();
                DeleteProgressFile();

                // 同步清空评分缓存（score_cache.json），避免重新加载后恢复旧评分
                _viewModel.ConfigService.ClearScoreCache();

                _viewModel.Entries.Clear();
                ResetSelectionState();
                _viewModel.LastLoadedFilePath = null;
                _viewModel.ConfigService.Config.LastLoadedFilePath = null;
                // 清空缓存时同步清零统计（API 调用/命中），随 SaveConfig 落盘防止重启后旧值恢复
                _viewModel.CacheHits = 0;
                _viewModel.ApiCalls = 0;
                _viewModel.GlossaryHits = 0;
                _viewModel.ConfigService.Config.TotalCacheHits = 0;
                _viewModel.ConfigService.Config.TotalApiCalls = 0;
                _viewModel.ConfigService.Config.TotalGlossaryHits = 0;
                _viewModel.SaveConfig();

                FilterKeyBox.Text = "";
                FilterBox.Text = "";
                FilterTranslationBox.Text = "";
                FilterCountText.Text = LocalizationManager.GetString("TotalCount", 0);
                StatusText.Text = LocalizationManager.GetString("Ready");
                CurrentFileTab.Text = LocalizationManager.GetString("NoFileLoaded");

                UpdateCacheInfo();
                UpdateGlossaryInfo();

                SafeRefreshDataGrid();

                AddLog($"🗑️ {LocalizationManager.GetString("LogCacheCleared")}");
            }
        }
    }
}
