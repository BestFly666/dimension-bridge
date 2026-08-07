using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;
using SimpleXmlEditor.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleXmlEditor
{
    /// <summary>
    /// Pure UI layer. All business logic (translation, evaluation, voting, caching,
    /// consistency scanning) lives in MainViewModel / services. This class only:
    ///  - forwards UI events to ViewModel commands/methods
    ///  - renders ViewModel state via events (status, progress, evaluation results)
    ///  - manages window lifecycle, theme, and localization
    ///
    /// Split into partial classes:
    ///   MainWindow.Localization.cs  — ApplyLocalization, UpdateInfoLabels
    ///   MainWindow.Theme.cs        — ApplyTheme
    ///   MainWindow.Grid.cs         — DataGrid interaction, selection, column/row resize
    ///   MainWindow.Helpers.cs      — AddLog, UpdateCacheInfo, ShowControlButtons, ShowEvaluationWindow
    ///   MainWindow.Events.cs       — All UI event handlers (clicks, filters, menus, shortcuts)
    ///   MainWindow.Handlers.cs     — ViewModel event subscription & outcome rendering (evaluation, voting, pre-translate, consistency, conflicts)
    ///   MainWindow.FileOps.cs      — LoadXml/SaveXml, config initialization, model auto-load, window lifecycle & external API
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private ReviewExporter _reviewExporter = new ReviewExporter();
        private System.Windows.Threading.DispatcherTimer _filterTimer;
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        private System.Windows.Threading.DispatcherTimer _uiFlushTimer;
        private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(5);

        // ── UI 更新合并 ─────────────────────────────────────────────
        // 后台线程的高频事件（日志/状态/进度）只写入队列与最新值，由 _uiFlushTimer
        // 在 UI 线程合并渲染（每 250ms 一次）。此前每批完成产生 6+ 个 BeginInvoke
        // 回调且无合并，UI 线程处理速度跟不上时队列无限积压 → 批次越多越卡。
        private const int UiFlushIntervalMs = 250;
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _pendingLogs = new();
        private volatile string _pendingStatusText;
        private volatile int _pendingProgressTranslated = -1;
        private volatile int _pendingProgressTotal = -1;

        private bool _isDarkMode = false;
        private bool _showUntranslatedOnly = false;
        private bool _hideBlacklisted = true;
        private bool _suppressSelectionSync = false;
        private bool _suppressSelectionChanged = false;
        private bool _logCollapsed = false;
        private const double LogPanelDefaultWidth = 380;

        /// <summary>
        /// 当前逻辑整列选择的列 DisplayIndex（-1 表示非整列模式）。
        /// CellStyle 的 MultiBinding 依赖此值在整列/全选切换时重新求值高亮。
        /// </summary>
        public static readonly DependencyProperty LogicalSelectColumnIndexProperty =
            DependencyProperty.Register(
                nameof(LogicalSelectColumnIndex), typeof(int), typeof(MainWindow), new PropertyMetadata(-1));

        public int LogicalSelectColumnIndex
        {
            get => (int)GetValue(LogicalSelectColumnIndexProperty);
            set => SetValue(LogicalSelectColumnIndexProperty, value);
        }

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = App.Services?.GetService<MainViewModel>() ?? new MainViewModel();
            SubscribeViewModelEvents();

            EntriesGrid.ItemsSource = _viewModel.Entries;

            EntriesGrid.SelectionChanged += EntriesGrid_SelectionChanged;
            EntriesGrid.Loaded += EntriesGrid_Loaded;

            _filterTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _filterTimer.Tick += FilterTimer_Tick;

            // Excel 式自动保存：每 5 分钟自动保存缓存与配置（不直接写 XML，防止覆盖源文件）
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = AutoSaveInterval
            };
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            _autoSaveTimer.Start();

            // UI 合并刷新定时器：高频日志/状态/进度事件合并渲染，防止 BeginInvoke 积压
            _uiFlushTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(UiFlushIntervalMs)
            };
            _uiFlushTimer.Tick += UiFlushTimer_Tick;
            _uiFlushTimer.Start();

            this.KeyDown += MainWindow_KeyDown;

            InitializeFromConfig();
        }
    }

    public class IndexToLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index && index >= 0 && index < 26)
            {
                return ((char)('A' + index)).ToString();
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 单元格高亮判定：IsHighlighted 为 true 且（无整列模式，或单元格属于被选中的整列）。
    /// 使点击列字母只高亮该列，而非所有列（与 Ctrl+A 全选区分）。
    /// 输入：values[0]=IsHighlighted, values[1]=单元格 Column.DisplayIndex, values[2]=LogicalSelectColumnIndex
    /// </summary>
    public class CellHighlightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3
                && values[0] is bool highlighted && highlighted
                && values[2] is int columnIndex)
            {
                if (columnIndex < 0) return true;          // 非整列模式：全部列高亮
                return values[1] is int cellIndex && cellIndex == columnIndex;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
