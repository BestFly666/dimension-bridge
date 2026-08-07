using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    /// <summary>
    /// MainWindow partial: DataGrid 列头交互与 Excel 式整列/全选
    /// （列头双击自适应宽度、列字母按钮选中整列、Ctrl+A 逻辑全选、可见行高亮）。
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// 重置排序：清除所有列的排序箭头与排序描述，恢复 XML 原始顺序（保留筛选）。
        /// 由左上角全选按钮点击触发（该按钮原为全选功能，已改为重置排序）。
        /// </summary>
        private void ResetSorting()
        {
            foreach (var column in EntriesGrid.Columns)
                column.SortDirection = null;

            var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
            view?.SortDescriptions.Clear();
            view?.Refresh();

            AddLog("已重置排序（恢复原始顺序）");
            EntriesGrid.Focus();
        }

        private void AttachColumnHeaderEvents()
        {
            if (VisualTreeHelper.GetChildrenCount(EntriesGrid) == 0) return;

            var scrollViewer = FindVisualChild<DataGridColumnHeadersPresenter>(EntriesGrid);
            if (scrollViewer == null) return;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(scrollViewer); i++)
            {
                if (VisualTreeHelper.GetChild(scrollViewer, i) is DataGridColumnHeader header)
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

        /// <summary>
        /// 拦截 DataGrid 内置 Ctrl+A（会 SelectAll 所有 cell，大数据量下卡死），
        /// 改为 Excel 式逻辑全选（毫秒级）。编辑模式下放行以保留文本框全选文本。
        /// 另支持 Excel 式 Del：清空选中行的译文。
        /// </summary>
        private void EntriesGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                // 编辑模式下放行，让 TextBox 自身的 Del 删除光标后字符/选中文本
                if (Keyboard.FocusedElement is TextBox) return;

                var entries = GetSelectedEntries();
                if (entries.Count == 0) return;

                e.Handled = true;
                _viewModel.PushUndoSnapshot(entries);
                // 静默清空 + 末尾 Refresh：全选时 entries 可能是全部行，逐条触发 PropertyChanged 会假死
                foreach (var entry in entries)
                    entry.SetTranslationSilent("");
                var view = CollectionViewSource.GetDefaultView(EntriesGrid.ItemsSource);
                view?.Refresh();
                EntriesGrid.Focus();
                return;
            }

            if (e.Key != Key.A) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
            if (Keyboard.FocusedElement is TextBox) return; // 编辑单元格时保留 Ctrl+A 全选文本

            e.Handled = true;
            SelectAllEntries();
        }

        private void ColumnLetterBtn_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is Button btn && btn.Tag is DataGridColumnHeader header && header.Column != null)
            {
                SelectEntireColumn(header.Column);
            }
        }

        /// <summary>
        /// 选中整列（Excel 式）：记录逻辑选择标志 + 数据驱动高亮全部行。
        /// 业务读取 GetSelectedEntries 时直接返回全部行，毫秒级完成。
        /// </summary>
        private void SelectEntireColumn(DataGridColumn column)
        {
            ApplyLogicalSelection(
                logicalSelectAll: false,
                logicalSelectColumn: column,
                logicalSelectColumnIndex: column.DisplayIndex);
        }

        /// <summary>
        /// 全选（Excel 式）：记录逻辑全选标志 + 数据驱动高亮全部行。
        /// </summary>
        private void SelectAllEntries()
        {
            ApplyLogicalSelection(
                logicalSelectAll: true,
                logicalSelectColumn: null,
                logicalSelectColumnIndex: -1);
        }

        /// <summary>
        /// 统一的逻辑选择执行方法：设置标志、清除 WPF 选择、同步勾选、高亮全部行。
        /// </summary>
        private void ApplyLogicalSelection(
            bool logicalSelectAll,
            DataGridColumn logicalSelectColumn,
            int logicalSelectColumnIndex)
        {
            var entries = EntriesGrid.Items.Cast<LocalizationEntry>().ToList();
            if (entries.Count == 0) return;

            _logicalSelectAll = logicalSelectAll;
            _logicalSelectColumn = logicalSelectColumn;
            _logicalSelectRangeLo = -1;
            _logicalSelectRangeHi = -1;
            LogicalSelectColumnIndex = logicalSelectColumnIndex;

            _suppressSelectionSync = true;
            _suppressSelectionChanged = true;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                EntriesGrid.SelectedCells.Clear();
                System.Diagnostics.Debug.WriteLine($"[perf] LogicalSelection.Clear = {sw.ElapsedMilliseconds}ms");

                SetAllEntriesSelectedSilent(true);

                sw.Restart();
                HighlightAllRows();
                System.Diagnostics.Debug.WriteLine($"[perf] LogicalSelection.Highlight = {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyLogicalSelection: {ex.Message}");
            }
            finally
            {
                _suppressSelectionSync = false;
                _suppressSelectionChanged = false;
            }

            StatusText.Text = $"{LocalizationManager.GetString("SelectedCount")}: {entries.Count}";
        }
    }
}
