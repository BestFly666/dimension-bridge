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
    }
}
