using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    public partial class GlossaryWindow : Window
    {
        private readonly IGlossaryManager _glossary;
        private readonly ObservableCollection<GlossaryTerm> _displayedTerms = new();
        private bool _suppressFilterEvents = false;
        private readonly DispatcherTimer _searchTimer;
        public event Action<List<GlossaryConflict>> ConflictsDetected;

        public GlossaryWindow(IGlossaryManager glossary)
        {
            InitializeComponent();
            _glossary = glossary;

            // 搜索防抖：停止输入 250ms 后才执行搜索，避免每次按键全量刷新卡顿
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _searchTimer.Tick += (_, _) =>
            {
                _searchTimer.Stop();
                RefreshAll();
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLocalization();
            TermsGrid.ItemsSource = _displayedTerms;
            RefreshAll();
        }

        private void ApplyLocalization()
        {
            Title = LocalizationManager.GetString("GlossaryWindowTitle");
            AddBtn.Content = $"+ {LocalizationManager.GetString("GlossaryAdd")}";
            EditBtn.Content = LocalizationManager.GetString("GlossaryEdit");
            DeleteBtn.Content = LocalizationManager.GetString("GlossaryDelete");
            ImportBtn.Content = LocalizationManager.GetString("GlossaryImport");
            ExportBtn.Content = LocalizationManager.GetString("GlossaryExport");
            MergeProfileBtn.Content = LocalizationManager.GetString("GlossaryMergeProfile");
            DetectConflictsBtn.Content = LocalizationManager.GetString("GlossaryDetectConflicts");
            RefreshBtn.Content = LocalizationManager.GetString("GlossaryRefresh");
            CloseBtn.Content = LocalizationManager.GetString("GlossaryClose");

            // Column headers
            TermsGrid.Columns[0].Header = LocalizationManager.GetString("GlossaryColEnglish");
            TermsGrid.Columns[1].Header = LocalizationManager.GetString("GlossaryColChinese");
            TermsGrid.Columns[2].Header = LocalizationManager.GetString("GlossaryColCategory");
            TermsGrid.Columns[3].Header = LocalizationManager.GetString("GlossaryColStatus");
            TermsGrid.Columns[4].Header = LocalizationManager.GetString("GlossaryColTags");
        }
    }
}
