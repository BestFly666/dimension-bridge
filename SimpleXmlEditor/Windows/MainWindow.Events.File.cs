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
        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            var allExt = new List<string> { "*.xml" };
            allExt.AddRange(_viewModel.PluginLoader.GetAllSupportedExtensions().Select(ext => $"*{ext}"));
            var filterExts = string.Join(";", allExt);

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationManager.GetString("SelectXmlFile"),
                Filter = $"{LocalizationManager.GetString("FileFilterAllSupported")} ({filterExts})|{filterExts}|{LocalizationManager.GetString("FileFilterXml")} (*.xml)|*.xml|{LocalizationManager.GetString("FileFilterPo")} (*.po)|*.po|{LocalizationManager.GetString("FileFilterJson")} (*.json)|*.json|{LocalizationManager.GetString("FileFilterAll")} (*.*)|*.*",
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
                    var entries = plugin.Load(openFileDialog.FileName);
                    if (entries.Count > 0)
                    {
                        _viewModel.Entries = new System.Collections.ObjectModel.ObservableCollection<LocalizationEntry>(entries);
                        EntriesGrid.ItemsSource = _viewModel.Entries;
                        _viewModel.LastLoadedFilePath = openFileDialog.FileName;
                        StatusText.Text = LocalizationManager.GetString("LoadedEntries", entries.Count, plugin.FormatName);
                        AddLog($"📂 {LocalizationManager.GetString("LogLoadedFile", openFileDialog.FileName, entries.Count, plugin.FormatName)}");
                        UpdateCacheInfo();
                        UpdateGlossaryInfo();
                        return;
                    }
                }

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
                Filter = $"{LocalizationManager.GetString("FileFilterAllSupported")} ({filterExts})|{filterExts}|{LocalizationManager.GetString("FileFilterXml")} (*.xml)|*.xml|{LocalizationManager.GetString("FileFilterPo")} (*.po)|*.po|{LocalizationManager.GetString("FileFilterJson")} (*.json)|*.json|{LocalizationManager.GetString("FileFilterAll")} (*.*)|*.*",
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
                    AddLog($"💾 {LocalizationManager.GetString("LogSavedFile", _viewModel.Entries.Count, plugin.FormatName, saveFileDialog.FileName)}");
                    StatusText.Text = LocalizationManager.GetString("StatusSavedPlugin", _viewModel.Entries.Count, plugin.FormatName);
                }
                else
                {
                    SaveXml(saveFileDialog.FileName);
                }
            }
        }

        /// <summary>
        /// Excel 式自动保存定时器回调：每隔 AutoSaveInterval 自动执行 QuickSave。
        /// 未加载文件时不执行（避免无意义的空保存）。
        /// </summary>
        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            if (_viewModel == null || _viewModel.Entries.Count == 0) return;
            if (string.IsNullOrEmpty(_viewModel.LastLoadedFilePath)) return;
            QuickSave();
        }

        private void QuickSave()
        {
            try
            {
                EntriesGrid.CommitEdit(DataGridEditingUnit.Row, true);

                _viewModel.SyncEntriesToCache(_viewModel.Entries);
                SaveCache();
                _viewModel.SyncScoresToCache(_viewModel.Entries);
                _viewModel.SaveScoreCache();
                _viewModel.SaveConfig();
                // 快速保存代表用户确认当前状态：主缓存已是最新快照，
                // 删除崩溃恢复进度文件，防止下次加载时旧译文被恢复（"删除后重开又出现"）
                _viewModel.ConfigService.DeleteProgressFile();
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
                AddLog($"📋 {LocalizationManager.GetString("ExportReviewLog", result.Total, result.Reviewed, result.NeedsFix, result.NotReviewed)}");
                MessageBox.Show(LocalizationManager.GetString("ExportReviewMsg", result.Total, result.Reviewed, result.NeedsFix, result.NotReviewed),
                    LocalizationManager.GetString("ReviewReport"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"❌ {LocalizationManager.GetString("ExportFailed", ex.Message)}");
                MessageBox.Show(LocalizationManager.GetString("ExportFailed", ex.Message), LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseFileBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Entries.Clear();
            ResetSelectionState();
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
}
