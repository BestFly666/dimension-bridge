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

        private void MenuSmartPreTrans_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SmartPreTranslateCommand.Execute(GetSelectedEntries());
        }

        private void MenuConsistency_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ConsistencyScanCommand.Execute(null);
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
            var toClear = entries.Where(en => !string.IsNullOrEmpty(en.Translation)).ToList();
            if (toClear.Count == 0) return;
            _viewModel.PushUndoSnapshot(toClear);
            foreach (var entry in toClear)
            {
                entry.Translation = "";
            }
            AddLog($"🗑️ {LocalizationManager.GetString("LogClearedTranslation", toClear.Count)}");
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

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var cfg = _viewModel.ConfigService.Config;
            var settings = new SettingsWindow(
                _viewModel.AiTranslationService.ApiKey,
                _viewModel.AiTranslationService.Model,
                _viewModel.AiTranslationService.TargetLanguage,
                _viewModel.ProgramLanguage,
                _viewModel.CustomPrompt,
                _viewModel.ActiveExpertProfileName,
                _viewModel.AiProvider,
                this,
                _viewModel.ProfileManager,
                cfg.EvaluationAiProvider,
                _viewModel.ConfigService.GetEvaluationApiKey(),
                cfg.EvaluationModel);
            if (settings.ShowDialog() == true)
            {
                _viewModel.AiTranslationService.ApiKey = settings.ApiKey;
                _viewModel.AiTranslationService.Model = settings.Model;
                _viewModel.AiTranslationService.TargetLanguage = settings.TargetLanguage;
                _viewModel.AiProvider = settings.AiProvider;

                // 评估模型配置
                _viewModel.ConfigService.UpdateConfig(c =>
                {
                    c.EvaluationAiProvider = settings.EvalAiProvider;
                    c.EvaluationModel = settings.EvalModel;
                });
                _viewModel.ConfigService.SetEvaluationApiKey(settings.EvalApiKey);

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

                AddLog(LocalizationManager.GetString("LogConflictStart", entryList.Count));

                Task.Run(() => _viewModel.Glossary.DetectConflicts(entryList, (processed, total) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                        AddLog(LocalizationManager.GetString("LogConflictProgress", processed, total))));
                }))
                    .ContinueWith(t =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                var conflicts = t.Result;
                                AddLog(LocalizationManager.GetString("LogConflictDone", conflicts.Count));
                                ShowConflictResults(conflicts);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, ex.Message, "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }));
                    });
            };
            window.ShowDialog();
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
            if (restored.Count == 0)
            {
                AddLog($"↩️ {LocalizationManager.GetString("NothingToUndo")}");
                return;
            }

            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
            view?.Refresh();

            AddLog($"↩️ {LocalizationManager.GetString("UndoComplete", restored.Count)}");

            // 实时跳转到第一个被撤销的行（Excel 风格：撤销后立即定位）
            var firstEntry = restored[0];
            EntriesGrid.ScrollIntoView(firstEntry);
            EntriesGrid.SelectedItem = firstEntry;
            EntriesGrid.Focus();
        }

        /// <summary>
        /// 用户开始编辑单元格时，先保存撤销快照（仅 Translation 列）。
        /// 这样手动编辑翻译后也能用撤销按钮恢复。
        /// </summary>
        private void EntriesGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // 编辑开始时强制退出逻辑选择模式并清理高亮 cells，
            // 防止编辑过程中的自动滚动触发 ScrollChanged 补选，导致其他单元格全部变色
            if (_logicalSelectAll || _logicalSelectColumn != null)
            {
                _logicalSelectAll = false;
                _logicalSelectColumn = null;

                _suppressSelectionChanged = true;
                try
                {
                    EntriesGrid.SelectedCells.Clear();
                    if (e.Column != null && e.Row?.Item is LocalizationEntry editEntry)
                        EntriesGrid.SelectedCells.Add(new DataGridCellInfo(editEntry, e.Column));
                }
                finally
                {
                    _suppressSelectionChanged = false;
                }
            }

            // Translation 列的 DisplayIndex 是 4（✓/Status/Key/Original/Translation/Score）
            if (e.Column?.DisplayIndex == 4 && e.Row?.Item is LocalizationEntry entry)
            {
                _viewModel.PushUndoSnapshot(new[] { entry });
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (e.Key == Key.S && ctrl)
            {
                e.Handled = true;
                QuickSave();
            }
            else if (e.Key == Key.O && ctrl)
            {
                e.Handled = true;
                LoadBtn_Click(null, null);
            }
            else if (e.Key == Key.Z && ctrl)
            {
                e.Handled = true;
                UndoBtn_Click(null, null);
            }
            else if (e.Key == Key.T && ctrl)
            {
                e.Handled = true;
                var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                if (shift)
                    TranslateAllBtn_Click(null, null);
                else
                    TranslateSelectedBtn_Click(null, null);
            }
            else if (e.Key == Key.F5)
            {
                e.Handled = true;
                EvaluateBtn_Click(null, null);
            }
            else if (e.Key == Key.F6)
            {
                e.Handled = true;
                VoteBtn_Click(null, null);
            }
            else if (e.Key == Key.Escape)
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

        private void ToggleLogBtn_Click(object sender, RoutedEventArgs e)
        {
            _logCollapsed = !_logCollapsed;
            LogColumn.Width = _logCollapsed ? new GridLength(30) : new GridLength(LogPanelDefaultWidth);
            LogPanel.Visibility = _logCollapsed ? Visibility.Collapsed : Visibility.Visible;
            LogExpandBar.Visibility = _logCollapsed ? Visibility.Visible : Visibility.Collapsed;
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

            foreach (ComboBoxItem item in ExpertProfileCombo.Items)
            {
                if (item.Tag?.ToString() == _viewModel.ActiveExpertProfileName)
                {
                    item.IsSelected = true;
                    return;
                }
            }
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
        }

        private void MenuShowLog_Click(object sender, RoutedEventArgs e)
        {
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
}
