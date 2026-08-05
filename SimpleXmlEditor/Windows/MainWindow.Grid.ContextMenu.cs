using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    /// <summary>
    /// MainWindow partial: DataGrid 右键菜单处理
    /// （全选 / 全不选 / 反选 / 标记审核状态）。
    /// </summary>
    public partial class MainWindow
    {
        private void CtxSelectAll_Click(object sender, RoutedEventArgs e)
        {
            SelectAllEntries();
        }

        private void CtxSelectNone_Click(object sender, RoutedEventArgs e)
        {
            UnselectAllEntries();
        }

        /// <summary>反选：先记录当前选中集合，再分片选中未选中的行（所有列）。</summary>
        private async void InvertSelectionAsync()
        {
            // 逻辑选择模式下反选无意义（全选反选=全不选），先退出逻辑模式
            _logicalSelectAll = false;
            _logicalSelectColumn = null;

            var entries = EntriesGrid.Items.Cast<LocalizationEntry>().ToList();
            if (entries.Count == 0) return;

            // 在 Clear 之前记录当前选中的 entry 集合
            var selectedSet = new HashSet<LocalizationEntry>();
            foreach (var item in EntriesGrid.SelectedItems)
            {
                if (GetEntryFromSelectionItem(item) is LocalizationEntry entry)
                    selectedSet.Add(entry);
            }

            StatusText.Text = $"⏳ {LocalizationManager.GetString("InvertingSelection")}...";

            int selectCount = 0;
            _suppressSelectionSync = true;
            _suppressSelectionChanged = true;
            try
            {
                EntriesGrid.SelectedCells.Clear();

                var columns = EntriesGrid.Columns.ToList();
                var cells = new List<DataGridCellInfo>();
                foreach (var entry in entries)
                {
                    bool newState = !selectedSet.Contains(entry);
                    entry.SetIsSelectedSilent(newState);
                    if (newState)
                    {
                        selectCount++;
                        _rowHeaderSelected.Add(entry);
                        foreach (var col in columns)
                            cells.Add(new DataGridCellInfo(entry, col));
                    }
                    else
                    {
                        _rowHeaderSelected.Remove(entry);
                    }
                }

                await AddCellsBatchedAsync(cells);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InvertSelectionAsync: {ex.Message}");
            }
            finally
            {
                _suppressSelectionSync = false;
                _suppressSelectionChanged = false;
            }

            StatusText.Text = $"{LocalizationManager.GetString("SelectedCount")}: {selectCount}";
        }

        private void CtxInvertSelection_Click(object sender, RoutedEventArgs e)
        {
            InvertSelectionAsync();
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
    }
}
