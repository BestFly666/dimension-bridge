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
    /// MainWindow partial: DataGrid 选中同步与行编辑
    /// （用户手动选择退出逻辑选择模式、复选框 ↔ 行选中联动、行高拖拽调整）。
    /// </summary>
    public partial class MainWindow
    {
        private void EntriesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;

            // 注意：不能在此处退出逻辑选择模式（全选/整列/范围）——
            // WPF DataGrid 的程序化 SelectedCells 修改会延迟触发本事件（在 _suppressSelectionChanged
            // 恢复之后才进来），若在此清标志会把刚设置的范围/全选标志误清除，导致选中回退为仅可见行。
            // 逻辑模式的退出统一由用户交互点处理：
            //   单元格点击 → PreviewMouseLeftButtonDown 非行头分支（ExitLogicalSelection）
            //   行头 Ctrl 加选 → ToggleRowSelection
            //   编辑单元格 → EntriesGrid_BeginningEdit

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

        /// <summary>
        /// 编辑提交/取消时，若译文值未变化（没改就提交、或 Esc 取消），丢弃 BeginningEdit 时压入的
        /// 单条撤销快照——避免无意义快照挤占 Undo 栈上限，导致批量操作快照被挤出、Ctrl+Z 无法撤销批量操作。
        /// </summary>
        private void EntriesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Translation 列的 DisplayIndex 是 4（✓/Status/Key/Original/Translation/Score）
            if (e.Column?.DisplayIndex == 4 && e.EditingElement is TextBox tb)
            {
                var editedKey = (e.Row.Item as LocalizationEntry)?.Key;
                if (!string.IsNullOrEmpty(editedKey))
                    _viewModel.DiscardUndoSnapshotIfUnchanged(editedKey, tb.Text);
            }
        }

        private DataGridRow _resizingRow;

        private void RowResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            var thumb = (Thumb)sender;
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
    }
}
