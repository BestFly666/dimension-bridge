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
    /// MainWindow partial: DataGrid 选择核心模型与行头 Excel 式整行选择
    /// （列头交互见 MainWindow.Grid.Sorting.cs，右键菜单见 MainWindow.Grid.ContextMenu.cs，
    /// 选中同步与行编辑见 MainWindow.Grid.Editing.cs）。
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Excel 式逻辑选择模型：全选/整列/行范围 只记录标志 + 仅高亮可见行，
        /// 业务读取时按标志返回选中行（毫秒级，不受数据量影响）。
        /// 行范围模式（Shift 拖拽/Shift+点击）记录 [lo, hi] 索引，避免逐个 Add cell 的 O(n×m) 卡顿。
        /// </summary>
        private bool _logicalSelectAll = false;
        private DataGridColumn _logicalSelectColumn = null;
        private int _logicalSelectRangeLo = -1;
        private int _logicalSelectRangeHi = -1;
        /// <summary>当前被静默勾选（IsSelected=true）的行，用于清理时快速还原，始终与勾选状态同步。</summary>
        private readonly List<LocalizationEntry> _rowHeaderSelected = new();

        private List<LocalizationEntry> GetSelectedEntries()
        {
            // 逻辑选择模式：全选或选中整列 → 所有行都被选中（Excel Range 语义）
            if (_logicalSelectAll || _logicalSelectColumn != null)
            {
                return EntriesGrid.Items.Cast<LocalizationEntry>()
                    .Where(e => !string.IsNullOrEmpty(e.Key))
                    .ToList();
            }

            // 行范围模式：Shift 拖拽/Shift+点击选中的连续行
            if (_logicalSelectRangeLo >= 0)
            {
                var result = new List<LocalizationEntry>(_logicalSelectRangeHi - _logicalSelectRangeLo + 1);
                for (int i = _logicalSelectRangeLo; i <= _logicalSelectRangeHi; i++)
                {
                    if (EntriesGrid.Items[i] is LocalizationEntry e && !string.IsNullOrEmpty(e.Key))
                        result.Add(e);
                }
                return result;
            }

            var set = new HashSet<LocalizationEntry>();
            foreach (var cellInfo in EntriesGrid.SelectedCells)
            {
                if (cellInfo.Item is LocalizationEntry entry)
                    set.Add(entry);
            }
            foreach (var item in EntriesGrid.SelectedItems)
            {
                if (GetEntryFromSelectionItem(item) is LocalizationEntry entry)
                    set.Add(entry);
            }
            return set.ToList();
        }

        private static LocalizationEntry GetEntryFromSelectionItem(object item)
        {
            return item switch
            {
                LocalizationEntry entry => entry,
                DataGridCell cell => cell.DataContext as LocalizationEntry,
                _ => null
            };
        }

        private void EntriesGrid_Loaded(object sender, RoutedEventArgs e)
        {
            AttachColumnHeaderEvents();
            GetRowsPanel(); // 预热虚拟化面板引用（需在加载完成后获取）
        }

        // ===== 行头 Excel 式整行选择 =====
        // WPF 原生 SelectionUnit=CellOrRowHeader 下行头 Shift/拖拽选择的是"单元格矩形"而非整行，
        // 导致用行号选中多行时实际只选中了部分列，看起来"不算选中"。
        // 这里拦截行头鼠标事件，自行实现：单击选整行 / Shift 选连续多行 / 拖拽连续选 / Ctrl 加选。
        private bool _rowHeaderSelecting = false;
        private int _selectionAnchorRow = -1;

        private void EntriesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 左上角全选按钮（行头与列头交汇处，x 在行头宽度内、命中 Button）→ 改为重置排序
            var pt = e.GetPosition(EntriesGrid);
            if (pt.X <= EntriesGrid.RowHeaderWidth && pt.Y <= 60 &&
                FindVisualAncestor<Button>(e.OriginalSource as DependencyObject) != null)
            {
                ResetSorting();
                e.Handled = true; // 阻止 DataGrid 默认的全选行为
                return;
            }

            var header = FindVisualAncestor<DataGridRowHeader>(e.OriginalSource as DependencyObject);
            if (header == null)
            {
                // 用户手动点击单元格（非行头）→ 退出逻辑选择模式，清理遗留高亮
                ExitLogicalSelection();
                return;
            }

            var row = ItemsControl.ContainerFromElement(EntriesGrid, header) as DataGridRow;
            if (row?.Item is not LocalizationEntry)
                return;

            var index = EntriesGrid.Items.IndexOf(row.Item);
            if (index < 0) return;

            e.Handled = true; // 接管行头选择，阻止 WPF 默认的矩形单元格选择
            EntriesGrid.Focus();

            _rowHeaderSelecting = true;
            var wasLogical = _logicalSelectAll || _logicalSelectColumn != null || _logicalSelectRangeLo >= 0;
            _logicalSelectAll = false;
            _logicalSelectColumn = null;
            _logicalSelectRangeLo = -1;
            _logicalSelectRangeHi = -1;
            // 从逻辑全选/整列转入行头选择：清掉之前静默勾选的所有行
            if (wasLogical)
            {
                foreach (var item in EntriesGrid.Items)
                {
                    if (item is LocalizationEntry en)
                        en.SetIsSelectedSilent(false);
                }
                _rowHeaderSelected.Clear();
            }

            var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isShift && _selectionAnchorRow >= 0)
            {
                SelectRowRange(_selectionAnchorRow, index);
            }
            else if (isCtrl)
            {
                ToggleRowSelection(index);
            }
            else
            {
                _selectionAnchorRow = index;
                SelectRowRange(index, index);
            }
        }

        private void EntriesGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_rowHeaderSelecting || e.LeftButton != MouseButtonState.Pressed) return;

            var header = FindVisualAncestor<DataGridRowHeader>(e.OriginalSource as DependencyObject);
            if (header == null) return;

            var row = ItemsControl.ContainerFromElement(EntriesGrid, header) as DataGridRow;
            if (row?.Item is not LocalizationEntry)
                return;

            var index = EntriesGrid.Items.IndexOf(row.Item);
            if (index >= 0)
                SelectRowRange(_selectionAnchorRow, index);
        }

        private void EntriesGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _rowHeaderSelecting = false;
        }

        /// <summary>
        /// 退出逻辑选择模式（全选/整列/范围），清理遗留高亮，只保留当前单元格。
        /// 仅在用户真正手动点击单元格时调用（PreviewMouseLeftButtonDown 非行头分支）。
        /// </summary>
        private void ExitLogicalSelection()
        {
            if (!(_logicalSelectAll || _logicalSelectColumn != null || _logicalSelectRangeLo >= 0)) return;

            _logicalSelectAll = false;
            _logicalSelectColumn = null;
            _logicalSelectRangeLo = -1;
            _logicalSelectRangeHi = -1;

            ClearAllHighlight();

            var current = EntriesGrid.CurrentCell;
            _suppressSelectionChanged = true;
            try
            {
                EntriesGrid.SelectedCells.Clear();
                if (current.IsValid && current.Item is LocalizationEntry)
                    EntriesGrid.SelectedCells.Add(current);
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        /// <summary>选中 [lo, hi] 行区间（整行所有列），Excel 行头拖拽/Shift 语义。</summary>
        private void SelectRowRange(int a, int b)
        {
            var lo = Math.Min(a, b);
            var hi = Math.Max(a, b);
            if (lo < 0 || hi >= EntriesGrid.Items.Count) return;

            // Excel 式范围选择：只记录 [lo, hi] 标志 + 数据驱动高亮范围内行
            _logicalSelectRangeLo = lo;
            _logicalSelectRangeHi = hi;
            _logicalSelectAll = false;
            _logicalSelectColumn = null;
            LogicalSelectColumnIndex = -1;

            _suppressSelectionChanged = true;
            _suppressSelectionSync = true;
            try
            {
                EntriesGrid.SelectedCells.Clear();
                SetHighlightRange(lo, hi);
            }
            finally
            {
                _suppressSelectionChanged = false;
                _suppressSelectionSync = false;
            }

            EntriesGrid.ScrollIntoView(EntriesGrid.Items[hi]);
            StatusText.Text = $"{LocalizationManager.GetString("SelectedCount")}: {hi - lo + 1}";
        }

        // ===== 数据驱动逻辑高亮 =====
        // 视觉高亮走 LocalizationEntry.IsHighlighted + RowStyle DataTrigger：
        // 高亮是数据属性，虚拟化滚动时行容器重建，绑定自动恢复，无需滚动补选。
        private int _highlightLo = -1;
        private int _highlightHi = -1;
        private bool _highlightAll = false;

        /// <summary>清除所有逻辑高亮标志（全选/整列/范围）。</summary>
        private void ClearAllHighlight()
        {
            LogicalSelectColumnIndex = -1;
            if (_highlightAll)
            {
                foreach (var item in EntriesGrid.Items)
                    if (item is LocalizationEntry en && en.IsHighlighted)
                        en.IsHighlighted = false;
                _highlightAll = false;
            }
            if (_highlightLo >= 0)
            {
                for (int i = _highlightLo; i <= _highlightHi; i++)
                    SetEntryHighlight(i, false);
                _highlightLo = -1;
                _highlightHi = -1;
            }
        }

        /// <summary>高亮所有行（全选/整列）。</summary>
        private void HighlightAllRows()
        {
            if (_highlightAll) return;
            if (_highlightLo >= 0)
            {
                for (int i = _highlightLo; i <= _highlightHi; i++)
                    SetEntryHighlight(i, false);
                _highlightLo = -1;
                _highlightHi = -1;
            }
            foreach (var item in EntriesGrid.Items)
                if (item is LocalizationEntry en)
                    en.IsHighlighted = true;
            _highlightAll = true;
        }

        /// <summary>增量设置 [lo, hi] 行高亮（拖拽扩展时只更新变化部分，性能优化）。</summary>
        private void SetHighlightRange(int lo, int hi)
        {
            if (_highlightAll)
            {
                foreach (var item in EntriesGrid.Items)
                    if (item is LocalizationEntry en && en.IsHighlighted)
                        en.IsHighlighted = false;
                _highlightAll = false;
            }
            if (_highlightLo >= 0)
            {
                for (int i = _highlightLo; i <= _highlightHi; i++)
                    if (i < lo || i > hi)
                        SetEntryHighlight(i, false);
            }
            for (int i = lo; i <= hi; i++)
            {
                if (_highlightLo >= 0 && i >= _highlightLo && i <= _highlightHi) continue;
                SetEntryHighlight(i, true);
            }
            _highlightLo = lo;
            _highlightHi = hi;
        }

        private void SetEntryHighlight(int index, bool value)
        {
            if (index < 0 || index >= EntriesGrid.Items.Count) return;
            if (EntriesGrid.Items[index] is LocalizationEntry en && en.IsHighlighted != value)
                en.IsHighlighted = value;
        }

        /// <summary>重置选择与高亮状态（加载/清空文件时调用，防止旧数据残留）。</summary>
        private void ResetSelectionState()
        {
            _logicalSelectAll = false;
            _logicalSelectColumn = null;
            _logicalSelectRangeLo = -1;
            _logicalSelectRangeHi = -1;
            _highlightLo = -1;
            _highlightHi = -1;
            _highlightAll = false;
            LogicalSelectColumnIndex = -1;
        }

        /// <summary>把所有行的勾选状态（IsSelected）静默设置为 selected，并同步维护 _rowHeaderSelected。</summary>
        private void SetAllEntriesSelectedSilent(bool selected)
        {
            _rowHeaderSelected.Clear();
            foreach (var item in EntriesGrid.Items)
            {
                if (item is LocalizationEntry en)
                {
                    en.SetIsSelectedSilent(selected);
                    if (selected) _rowHeaderSelected.Add(en);
                }
            }
        }

        /// <summary>Ctrl+点击行头：该行已在选中集则取消，否则加选整行。</summary>
        private void ToggleRowSelection(int index)
        {
            var entry = EntriesGrid.Items[index];
            if (entry == null || EntriesGrid.Columns.Count == 0) return;

            // Ctrl+点击的结果是不规则集合，退出范围模式转为显式 cell 集合
            _logicalSelectAll = false;
            _logicalSelectColumn = null;
            _logicalSelectRangeLo = -1;
            _logicalSelectRangeHi = -1;

            ClearAllHighlight();

            var columns = EntriesGrid.Columns.ToList();
            var probe = new DataGridCellInfo(entry, columns[0]);
            var isSelected = EntriesGrid.SelectedCells.Contains(probe);

            _suppressSelectionChanged = true;
            try
            {
                if (isSelected)
                {
                    var toRemove = EntriesGrid.SelectedCells.Where(c => ReferenceEquals(c.Item, entry)).ToList();
                    foreach (var cell in toRemove)
                        EntriesGrid.SelectedCells.Remove(cell);
                }
                else
                {
                    foreach (var col in columns)
                        EntriesGrid.SelectedCells.Add(new DataGridCellInfo(entry, col));
                }
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            // Ctrl 加选/取消时同步 IsSelected
            if (entry is LocalizationEntry en)
            {
                if (isSelected)
                {
                    en.SetIsSelectedSilent(false);
                    _rowHeaderSelected.Remove(en);
                }
                else
                {
                    en.SetIsSelectedSilent(true);
                    _rowHeaderSelected.Add(en);
                }
            }

            StatusText.Text = $"{LocalizationManager.GetString("SelectedCount")}: {EntriesGrid.SelectedCells.Select(c => c.Item).Distinct().Count()}";
        }

        private VirtualizingStackPanel _rowsPanel;

        /// <summary>获取 DataGrid 行虚拟化面板（只含已生成的可见行容器），缓存复用。</summary>
        private VirtualizingStackPanel GetRowsPanel()
        {
            if (_rowsPanel != null) return _rowsPanel;
            _rowsPanel = FindVisualChild<VirtualizingStackPanel>(EntriesGrid);
            return _rowsPanel;
        }

        /// <summary>
        /// 分片添加 cell 到 SelectedCells，每片后让出 UI 线程。
        /// 用于反选等不规则选择（结果无法用逻辑标志表示）。
        /// </summary>
        private async Task AddCellsBatchedAsync(List<DataGridCellInfo> cells, int batchSize = 400)
        {
            for (int i = 0; i < cells.Count; i += batchSize)
            {
                int end = Math.Min(i + batchSize, cells.Count);
                for (int j = i; j < end; j++)
                    EntriesGrid.SelectedCells.Add(cells[j]);
                await Task.Yield();
            }
        }

        /// <summary>
        /// 滚动时保持选择不变（不补选新进入视野的行），避免滚轮滚动触发高亮变化。
        /// 逻辑选择标志已保证业务上全选/整列生效，视觉高亮只需在全选/整列那一刻标记。
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T typed) return typed;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private static T FindVisualAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var current = child;
            while (current != null)
            {
                if (current is T typed) return typed;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>全不选：清空选择 + 清除逻辑选择标志 + 清除逻辑高亮。</summary>
        private void UnselectAllEntries()
        {
            _logicalSelectAll = false;
            _logicalSelectColumn = null;
            _logicalSelectRangeLo = -1;
            _logicalSelectRangeHi = -1;

            _suppressSelectionSync = true;
            _suppressSelectionChanged = true;
            try
            {
                EntriesGrid.SelectedCells.Clear();
                SetAllEntriesSelectedSilent(false);
                ClearAllHighlight();
            }
            finally
            {
                _suppressSelectionSync = false;
                _suppressSelectionChanged = false;
            }

            StatusText.Text = $"{LocalizationManager.GetString("SelectedCount")}: 0";
        }
    }
}
