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

        private DataGridRow _resizingRow;

        private void RowResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            var thumb = (Thumb)sender;
            var rowHeader = FindVisualAncestor<DataGridRowHeader>(thumb);
            _resizingRow = FindVisualAncestor<DataGridRow>(thumb);
            if (_resizingRow != null)
                _resizingRow.Height = _resizingRow.ActualHeight;
            e.Handled = true;
        }

        private void RowResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_resizingRow == null) return;
            double newHeight = _resizingRow.ActualHeight + e.VerticalChange;
            if (newHeight < 24) newHeight = 24;
            _resizingRow.Height = newHeight;
            e.Handled = true;
        }

        private void RowResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _resizingRow = null;
            e.Handled = true;
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

        private void EntriesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;

            // 用户手动交互选择 → 退出 Excel 式逻辑选择模式，并清理其遗留的高亮 cells，
            // 只保留用户当前点击的单元格（否则全选/整列后点单格编辑时其他单元格仍保持高亮）
            if (_logicalSelectAll || _logicalSelectColumn != null)
            {
                _logicalSelectAll = false;
                _logicalSelectColumn = null;

                var current = EntriesGrid.CurrentCell;
                EntriesGrid.SelectedCells.Clear();
                if (current.IsValid && current.Item is LocalizationEntry)
                    EntriesGrid.SelectedCells.Add(current);
            }

            // 注意：不再在此处同步 entry.IsSelected —— 点击单元格只应选中格子（Excel 行为），
            // 不该联动勾选整行复选框；复选框状态由用户点击 checkbox（双向绑定）或 SetIsSelectedSilent 维护。
        }

        private void OnEntrySelectionChanged(LocalizationEntry entry, bool isSelected)
        {
            if (_suppressSelectionSync) return;

            if (EntriesGrid.ItemContainerGenerator.ContainerFromItem(entry) is DataGridRow row)
            {
                row.IsSelected = isSelected;
            }
        }

        private void OnEntryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalizationEntry.IsSelected) && sender is LocalizationEntry changedEntry)
            {
                OnEntrySelectionChanged(changedEntry, changedEntry.IsSelected);
            }
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
