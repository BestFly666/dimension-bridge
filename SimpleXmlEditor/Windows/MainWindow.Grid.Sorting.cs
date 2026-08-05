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
    /// MainWindow partial: DataGrid 列头交互与 Excel 式整列/全选
    /// （列头双击自适应宽度、列字母按钮选中整列、Ctrl+A 逻辑全选、可见行高亮）。
    /// </summary>
    public partial class MainWindow
    {
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
        /// </summary>
        private void EntriesGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
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
        /// 选中整列（Excel 式）：记录逻辑选择标志，只高亮当前可见行的该列 cell。
        /// 滚动时通过 EntriesGrid_ScrollChanged 动态补选新进入视野的行。
        /// 业务读取 GetSelectedEntries 时直接返回全部行，毫秒级完成。
        /// </summary>
        private void SelectEntireColumn(DataGridColumn column)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var entries = EntriesGrid.Items.Cast<LocalizationEntry>().ToList();
            if (entries.Count == 0) return;

            // 逻辑选择：整列 → 所有行选中（只记标志，不逐个操作）
            _logicalSelectColumn = column;
            _logicalSelectAll = false;

            _suppressSelectionSync = true;
            _suppressSelectionChanged = true;
            try
            {
                sw.Restart();
                EntriesGrid.SelectedCells.Clear();
                System.Diagnostics.Debug.WriteLine($"[perf] SelectEntireColumn.Clear = {sw.ElapsedMilliseconds}ms, count={EntriesGrid.SelectedCells.Count}");

                // 同步勾选状态：让"翻译选中"等按 IsSelected 读取的入口拿到整列所有行
                SetAllEntriesSelectedSilent(true);

                // 只高亮可见行（虚拟化下数量很少，毫秒级）
                sw.Restart();
                HighlightVisibleCells(column, selectAll: false);
                System.Diagnostics.Debug.WriteLine($"[perf] SelectEntireColumn.Highlight = {sw.ElapsedMilliseconds}ms, cells={EntriesGrid.SelectedCells.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SelectEntireColumn: {ex.Message}");
            }
            finally
            {
                _suppressSelectionSync = false;
                _suppressSelectionChanged = false;
            }

            StatusText.Text = $"{LocalizationManager.GetString("SelectedCount")}: {entries.Count}";
        }

        /// <summary>
        /// 全选（Excel 式）：记录逻辑全选标志，只高亮可见行，滚动时动态补选。
        /// </summary>
        private void SelectAllEntries()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var entries = EntriesGrid.Items.Cast<LocalizationEntry>().ToList();
            if (entries.Count == 0) return;

            _logicalSelectAll = true;
            _logicalSelectColumn = null;

            _suppressSelectionSync = true;
            _suppressSelectionChanged = true;
            try
            {
                sw.Restart();
                EntriesGrid.SelectedCells.Clear();
                System.Diagnostics.Debug.WriteLine($"[perf] SelectAll.Clear = {sw.ElapsedMilliseconds}ms, count={EntriesGrid.SelectedCells.Count}");

                // 同步勾选状态：让"翻译选中"等按 IsSelected 读取的入口拿到全部行
                SetAllEntriesSelectedSilent(true);

                sw.Restart();
                HighlightVisibleCells(null, selectAll: true);
                System.Diagnostics.Debug.WriteLine($"[perf] SelectAll.Highlight = {sw.ElapsedMilliseconds}ms, cells={EntriesGrid.SelectedCells.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SelectAllEntries: {ex.Message}");
            }
            finally
            {
                _suppressSelectionSync = false;
                _suppressSelectionChanged = false;
            }

            StatusText.Text = $"{LocalizationManager.GetString("SelectedCount")}: {entries.Count}";
        }

        /// <summary>
        /// 只把当前可见行的 cell 添加到 SelectedCells（视觉高亮）。
        /// 直接遍历虚拟化已生成的容器（仅可见行），避免 ContainerFromIndex 全量遍历。
        /// 整列模式传 column，全选模式传 null + selectAll=true。
        /// </summary>
        private void HighlightVisibleCells(DataGridColumn column, bool selectAll)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var panel = GetRowsPanel();
            if (panel == null) return;

            var columns = EntriesGrid.Columns;
            var addCount = 0;
            foreach (var child in panel.Children)
            {
                if (child is not DataGridRow row || row.Item is not LocalizationEntry entry) continue;

                if (selectAll)
                {
                    for (int c = 0; c < columns.Count; c++)
                    {
                        var info = new DataGridCellInfo(entry, columns[c]);
                        if (!EntriesGrid.SelectedCells.Contains(info))
                        {
                            EntriesGrid.SelectedCells.Add(info);
                            addCount++;
                        }
                    }
                }
                else if (column != null)
                {
                    var info = new DataGridCellInfo(entry, column);
                    if (!EntriesGrid.SelectedCells.Contains(info))
                    {
                        EntriesGrid.SelectedCells.Add(info);
                        addCount++;
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine($"[perf] Highlight: {sw.ElapsedMilliseconds}ms, containers={panel.Children.Count}, added={addCount}, totalCells={EntriesGrid.SelectedCells.Count}");
        }
    }
}
