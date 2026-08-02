using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    public partial class MainWindow
    {
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
            ProgressText.Text = LocalizationManager.GetString("ProgressDisplay", _viewModel.ProgressPercentage, _viewModel.TranslatedCount, _viewModel.TotalCount);
            SpeedText.Text = _viewModel.TranslationSpeed > 0 ? $"⚡ {LocalizationManager.GetString("SpeedDisplay", _viewModel.TranslationSpeed)}" : "";
            EtaText.Text = !string.IsNullOrEmpty(_viewModel.EstimatedTimeRemaining) && _viewModel.EstimatedTimeRemaining != "..."
                ? $"⏱ {LocalizationManager.GetString("EtaDisplay", _viewModel.EstimatedTimeRemaining)}" : "";
            CostText.Text = _viewModel.TotalCost > 0 ? $"💰 {LocalizationManager.GetString("CostDisplay", _viewModel.TotalCost)}" : "";
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
                    EntriesGrid.Items.Refresh();
                }
            });
            window.Owner = this;
            window.Show();
        }
    }
}
