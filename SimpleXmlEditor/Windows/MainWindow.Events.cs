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

        private void HideBlacklistToggle_Click(object sender, RoutedEventArgs e)
        {
            _hideBlacklisted = HideBlacklistToggle.IsChecked == true;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
            if (view == null) return;

            var keyFilter = FilterKeyBox.Text.Trim();
            var filter = FilterBox.Text.Trim();
            var translationFilter = FilterTranslationBox.Text.Trim();

            if (string.IsNullOrEmpty(keyFilter) && string.IsNullOrEmpty(filter) && string.IsNullOrEmpty(translationFilter) && !_showUntranslatedOnly && !_hideBlacklisted)
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
                        // 黑名单条目默认隐藏：不显示、不会被全选/整列/行头选中
                        if (_hideBlacklisted && entry.IsBlacklisted)
                            return false;

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
                // 静默批量赋值：避免逐条触发 PropertyChanged（大文件下 DataGrid 逐行重绘导致假死），
                // 统一由末尾 view.Refresh() 一次刷新。
                foreach (var entry in _viewModel.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.Translation) &&
                        entry.Translation.Contains(searchText))
                    {
                        entry.SetTranslationSilent(entry.Translation.Replace(searchText, replaceText));
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
            // 编辑开始时强制退出逻辑选择模式并清理高亮，
            // 防止编辑过程中其他选中行残留高亮干扰视觉
            if (_logicalSelectAll || _logicalSelectColumn != null || _logicalSelectRangeLo >= 0)
            {
                _logicalSelectAll = false;
                _logicalSelectColumn = null;
                _logicalSelectRangeLo = -1;
                _logicalSelectRangeHi = -1;

                ClearAllHighlight();

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
    }
}
