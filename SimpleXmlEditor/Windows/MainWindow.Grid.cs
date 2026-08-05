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
        /// Excel 式逻辑选择模型：全选/选中整列时只记录标志 + 仅高亮可见行，
        /// 业务读取时按标志返回全部行（毫秒级，不受数据量影响）。
        /// </summary>
        private bool _logicalSelectAll = false;
        private DataGridColumn _logicalSelectColumn = null;
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
            var header = FindVisualAncestor<DataGridRowHeader>(e.OriginalSource as DependencyObject);
            if (header == null) return;

            var row = ItemsControl.ContainerFromElement(EntriesGrid, header) as DataGridRow;
            if (row?.Item is not LocalizationEntry)
                return;

            var index = EntriesGrid.Items.IndexOf(row.Item);
            if (index < 0) return;

            e.Handled = true; // 接管行头选择，阻止 WPF 默认的矩形单元格选择
            EntriesGrid.Focus();

            _rowHeaderSelecting = true;
            var wasLogical = _logicalSelectAll || _logicalSelectColumn != null;
            _logicalSelectAll = false;
            _logicalSelectColumn = null;
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

        /// <summary>选中 [lo, hi] 行区间（整行所有列），Excel 行头拖拽/Shift 语义。</summary>
        private void SelectRowRange(int a, int b)
        {
            var lo = Math.Min(a, b);
            var hi = Math.Max(a, b);
            if (lo < 0 || hi >= EntriesGrid.Items.Count) return;

            var columns = EntriesGrid.Columns.ToList();
            if (columns.Count == 0) return;

            _suppressSelectionChanged = true;
            try
            {
                EntriesGrid.SelectedCells.Clear();
                for (int i = lo; i <= hi; i++)
                {
                    var entry = EntriesGrid.Items[i];
                    foreach (var col in columns)
                        EntriesGrid.SelectedCells.Add(new DataGridCellInfo(entry, col));
                }
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            // 同步 IsSelected（静默，不触发 UI 联动）：让"翻译选中"等按复选框读取的入口拿到选中行
            foreach (var en in _rowHeaderSelected)
                en.SetIsSelectedSilent(false);
            _rowHeaderSelected.Clear();
            for (int i = lo; i <= hi; i++)
            {
                if (EntriesGrid.Items[i] is LocalizationEntry en)
                {
                    en.SetIsSelectedSilent(true);
                    _rowHeaderSelected.Add(en);
                }
            }

            EntriesGrid.ScrollIntoView(EntriesGrid.Items[hi]);
            StatusText.Text = $"{LocalizationManager.GetString("SelectedCount")}: {hi - lo + 1}";
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

        /// <summary>全不选：清空选择 + 清除逻辑选择标志。</summary>
        private void UnselectAllEntries()
        {
            _logicalSelectAll = false;
            _logicalSelectColumn = null;

            _suppressSelectionSync = true;
            _suppressSelectionChanged = true;
            try
            {
                EntriesGrid.SelectedCells.Clear();
                SetAllEntriesSelectedSilent(false);
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
