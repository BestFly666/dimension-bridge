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
        private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(5);

        private bool _isDarkMode = false;
        private bool _showUntranslatedOnly = false;
        private bool _hideBlacklisted = true;
        private bool _suppressSelectionSync = false;
        private bool _suppressSelectionChanged = false;
        private bool _logCollapsed = false;
        private const double LogPanelDefaultWidth = 380;

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
}
