using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
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

        // ─── Data Refresh ────────────────────────────────────────────

        private void RefreshAll()
        {
            // 增量更新同一 ObservableCollection：DataGrid 复用同一个 ItemsSource，
            // 避免每次重建列表导致卡顿与滚动位置丢失
            _displayedTerms.Clear();
            foreach (var term in GetFilteredTerms())
                _displayedTerms.Add(term);
            UpdateStats();
        }

        private List<GlossaryTerm> GetFilteredTerms()
        {
            var terms = _glossary.Search(SearchTxt.Text ?? "");

            var catFilter = (CategoryFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            if (catFilter == LocalizationManager.GetString("GlossaryFilterAll") || string.IsNullOrEmpty(catFilter))
                catFilter = "";

            var statusFilterItem = StatusFilter.SelectedItem as ComboBoxItem;
            var statusFilter = statusFilterItem?.Tag?.ToString() ?? "";

            if (!string.IsNullOrEmpty(catFilter))
                terms = terms.Where(t => t.Category == catFilter).ToList();
            if (!string.IsNullOrEmpty(statusFilter))
                terms = terms.Where(t => t.Status == statusFilter).ToList();

            return terms;
        }

        private void UpdateStats()
        {
            var total = _glossary.Count;
            var confirmed = _glossary.Terms.Values.Count(t => t.Status == "confirmed");
            var pending = _glossary.Terms.Values.Count(t => t.Status == "pending");
            var rejected = _glossary.Terms.Values.Count(t => t.Status == "rejected");

            StatsTxt.Text = string.Format(LocalizationManager.GetString("GlossaryTermCount"), total, _displayedTerms.Count);
            BottomStatsTxt.Text = string.Format(LocalizationManager.GetString("GlossaryStatusSummary"),
                confirmed, pending, rejected);

            // Category summary
            var categories = _glossary.GetAllCategories();
            BottomCategoryTxt.Text = categories.Count > 0
                ? string.Format(LocalizationManager.GetString("GlossaryCategoryCount"), categories.Count)
                : "";

            // Populate filter combos
            PopulateFilterCombos();
        }

        private void PopulateFilterCombos()
        {
            var cats = _glossary.GetAllCategories();

            // 分类集合未变化时跳过重建（搜索/过滤不改变分类，避免每次刷新重建下拉框）
            var currentCats = CategoryFilter.Items
                .Cast<ComboBoxItem>()
                .Skip(1) // 跳过"全部"项
                .Select(i => i.Content?.ToString() ?? "")
                .ToList();
            if (currentCats.SequenceEqual(cats))
                return;

            _suppressFilterEvents = true;
            try
            {
                var allLabel = LocalizationManager.GetString("GlossaryFilterAll");

                // Category filter
                var prevCat = (CategoryFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? allLabel;
                CategoryFilter.Items.Clear();
                CategoryFilter.Items.Add(new ComboBoxItem { Content = allLabel, IsSelected = prevCat == allLabel });
                foreach (var cat in cats)
                {
                    CategoryFilter.Items.Add(new ComboBoxItem { Content = cat, IsSelected = prevCat == cat });
                }

                // Status filter
                var prevStatus = (StatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? allLabel;
                StatusFilter.Items.Clear();
                StatusFilter.Items.Add(new ComboBoxItem { Content = allLabel, IsSelected = prevStatus == allLabel });
                foreach (var (english, localized) in new[] { 
                    ("confirmed", LocalizationManager.GetString("GlossaryStatusConfirmed")),
                    ("pending", LocalizationManager.GetString("GlossaryStatusPending")),
                    ("rejected", LocalizationManager.GetString("GlossaryStatusRejected"))
                })
                {
                    StatusFilter.Items.Add(new ComboBoxItem { Content = localized, Tag = english, IsSelected = prevStatus == localized });
                }
            }
            finally
            {
                _suppressFilterEvents = false;
            }
        }

        // ─── Event Handlers ──────────────────────────────────────────

        private void SearchTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 防抖：停止上一个定时器重新计时，输入停顿后才刷新
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressFilterEvents) return;
            RefreshAll();
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            _glossary.Load();
            RefreshAll();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ─── CRUD ─────────────────────────────────────────────────────

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TermEditDialog(null);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _glossary.SetTerm(dialog.Result);
                RefreshAll();
            }
        }

        private void EditBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = TermsGrid.SelectedItem as GlossaryTerm;
            if (selected == null)
            {
                MessageBox.Show(LocalizationManager.GetString("GlossarySelectHint"), 
                    LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new TermEditDialog(selected);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _glossary.SetTerm(dialog.Result);
                RefreshAll();
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = TermsGrid.SelectedItem as GlossaryTerm;
            if (selected == null) return;

            var msg = string.Format(LocalizationManager.GetString("GlossaryDeleteConfirm"), selected.English);
            if (MessageBox.Show(msg, LocalizationManager.GetString("MsgConfirm"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _glossary.RemoveEntry(selected.English);
                RefreshAll();
            }
        }

        private void TermsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditBtn_Click(sender, e);
        }

        // ─── Import / Export ─────────────────────────────────────────

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = LocalizationManager.GetString("GlossaryImportTitle"),
                Filter = "All supported|*.csv;*.json|CSV files|*.csv|JSON files|*.json",
                FilterIndex = 1
            };

            if (dlg.ShowDialog() == true)
            {
                (int added, int updated, int skipped) result;

                if (dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var r = _glossary.ImportJson(dlg.FileName);
                    result = (r.added, r.updated, 0);
                }
                else
                {
                    result = _glossary.ImportCsv(dlg.FileName);
                }

                MessageBox.Show(
                    string.Format(LocalizationManager.GetString("GlossaryImportResult"), result.added, result.updated, result.skipped),
                    LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);

                RefreshAll();
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = LocalizationManager.GetString("GlossaryExportTitle"),
                Filter = "CSV files|*.csv|JSON files|*.json",
                FilterIndex = 1,
                FileName = "glossary_export.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                if (dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    _glossary.ExportJson(dlg.FileName);
                else
                    _glossary.ExportCsv(dlg.FileName);

                MessageBox.Show(
                    string.Format(LocalizationManager.GetString("GlossaryExportResult"), _glossary.Count),
                    LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShareBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = LocalizationManager.GetString("GlossaryShareTitle"),
                Filter = "JSON files|*.json",
                FilterIndex = 1,
                FileName = "shared_glossary.json"
            };

            if (dlg.ShowDialog() == true)
            {
                var termList = _glossary.Terms.Values.ToList();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"source\": \"XML AI Translator Community\",");
                sb.AppendLine("  \"game\": \"Unknown\",");
                sb.AppendLine("  \"version\": \"1.0\",");
                sb.AppendLine("  \"author\": \"\",");
                sb.AppendLine("  \"description\": \"Shared glossary exported from XML AI Translator.\",");
                sb.AppendLine($"  \"date\": \"{DateTime.Now:yyyy-MM-dd}\",");
                sb.AppendLine("  \"terms\": [");
                for (int i = 0; i < termList.Count; i++)
                {
                    var comma = i < termList.Count - 1 ? "," : "";
                    string eng = EscapeJson(termList[i].English);
                    string chn = EscapeJson(termList[i].Chinese);
                    sb.AppendLine($"    {{\"english\": \"{eng}\", \"chinese\": \"{chn}\"}}{comma}");
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");

                System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                MessageBox.Show(LocalizationManager.GetString("GlossaryShareResult", termList.Count),
                    LocalizationManager.GetString("GlossaryShareResultTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        // ─── Merge Profile ───────────────────────────────────────────

        private void MergeProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            var profileManager = new ExpertProfiles.ExpertProfileManager();
            if (profileManager.Profiles.Count == 0)
            {
                MessageBox.Show(LocalizationManager.GetString("GlossaryNoProfiles"),
                    LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Simple selection dialog: pick a profile to merge
            var dialog = new ProfileSelectDialog(profileManager.Profiles);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.SelectedProfile != null)
            {
                var result = _glossary.MergeFromProfile(
                    dialog.SelectedProfile.Name, dialog.SelectedProfile.Glossary);
                MessageBox.Show(
                    string.Format(LocalizationManager.GetString("GlossaryMergeResult"), result.added, result.updated),
                    LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAll();
            }
        }

        // ─── Conflict Detection ──────────────────────────────────────

        private void DetectConflictsBtn_Click(object sender, RoutedEventArgs e)
        {
            ConflictsDetected?.Invoke(null); // Trigger MainWindow to send entries
            this.Close();
        }

        public void ShowConflicts(List<GlossaryConflict> conflicts)
        {
            if (conflicts == null || conflicts.Count == 0)
            {
                MessageBox.Show(LocalizationManager.GetString("GlossaryNoConflicts"),
                    LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new ConflictDialog(conflicts);
            dialog.Owner = this;
            dialog.ShowDialog();
        }
    }

    // ─── Converters ──────────────────────────────────────────────────

    public class StatusColorConverter : IValueConverter
    {
        public static readonly StatusColorConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "confirmed" => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), // green
                "pending" => new SolidColorBrush(Color.FromRgb(0xE6, 0x5C, 0x00)),   // orange
                "rejected" => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),  // red
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ─── Term Edit Dialog ────────────────────────────────────────────

    public class TermEditDialog : Window
    {
        public GlossaryTerm Result { get; private set; }
        private readonly GlossaryTerm _original;
        private readonly TextBox _englishTxt, _chineseTxt, _categoryTxt, _tagsTxt;
        private readonly ComboBox _statusCombo;

        public TermEditDialog(GlossaryTerm existing)
        {
            _original = existing;
            Title = existing == null ? LocalizationManager.GetString("TermAddTitle") : LocalizationManager.GetString("TermEditTitle");
            Width = 520;
            Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));

            var grid = new Grid();
            grid.Margin = new Thickness(20);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int row = 0;

            void AddRow(string label, out TextBox tb, string value = "")
            {
                grid.RowDefinitions.Insert(row, new RowDefinition { Height = GridLength.Auto });
                var lbl = new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 2), FontWeight = FontWeights.Medium, FontSize = 12 };
                Grid.SetRow(lbl, row);
                grid.Children.Add(lbl);
                row++;

                tb = new TextBox { Text = value, Height = 28, FontSize = 13, Margin = new Thickness(0, 0, 0, 4) };
                Grid.SetRow(tb, row);
                grid.Children.Add(tb);
                row++;
            }

            AddRow(LocalizationManager.GetString("GlossaryColEnglish") + ":", out _englishTxt, existing?.English ?? "");
            AddRow(LocalizationManager.GetString("GlossaryColChinese") + ":", out _chineseTxt, existing?.Chinese ?? "");
            AddRow(LocalizationManager.GetString("GlossaryColCategory") + ":", out _categoryTxt, existing?.Category ?? "");
            AddRow(LocalizationManager.GetString("GlossaryColTags") + ":", out _tagsTxt, existing?.Tags ?? "");

            // Status
            {
                var lbl = new TextBlock { Text = LocalizationManager.GetString("GlossaryColStatus") + ":", Margin = new Thickness(0, 8, 0, 2), FontWeight = FontWeights.Medium, FontSize = 12 };
                Grid.SetRow(lbl, row);
                grid.Children.Add(lbl);
                row++;

                _statusCombo = new ComboBox { Height = 28, FontSize = 13, Margin = new Thickness(0, 0, 0, 4) };
                var statuses = new[] { "confirmed", "pending", "rejected" };
                foreach (var s in statuses)
                {
                    var item = new ComboBoxItem { Content = s };
                    if (s == (existing?.Status ?? "confirmed")) item.IsSelected = true;
                    _statusCombo.Items.Add(item);
                }
                Grid.SetRow(_statusCombo, row);
                grid.Children.Add(_statusCombo);
                row++;
            }

            // Buttons
            {
                var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
                Grid.SetRow(btnPanel, row);
                grid.Children.Add(btnPanel);

                var cancelBtn = new Button { Content = LocalizationManager.GetString("GlossaryCancel"), Width = 80, Height = 30, Margin = new Thickness(4, 0, 0, 0) };
                cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
                btnPanel.Children.Add(cancelBtn);

                var saveBtn = new Button { Content = LocalizationManager.GetString("GlossarySave"), Width = 80, Height = 30, Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), Foreground = Brushes.White, FontWeight = FontWeights.Medium, BorderThickness = new Thickness(0) };
                saveBtn.Click += (_, _) => SaveAndClose();
                btnPanel.Children.Add(saveBtn);
            }

            Content = grid;
        }

        private void SaveAndClose()
        {
            var english = _englishTxt.Text.Trim();
            var chinese = _chineseTxt.Text.Trim();

            if (string.IsNullOrEmpty(english) || string.IsNullOrEmpty(chinese))
            {
                MessageBox.Show(LocalizationManager.GetString("GlossaryRequiredFields"),
                    LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var status = (_statusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "confirmed";

            Result = new GlossaryTerm
            {
                English = english,
                Chinese = chinese,
                Category = _categoryTxt.Text.Trim(),
                Tags = _tagsTxt.Text.Trim(),
                Status = status,
                CreatedAt = _original?.CreatedAt ?? DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            DialogResult = true;
            Close();
        }
    }

    // ─── Profile Select Dialog ──────────────────────────────────────

    public class ProfileSelectDialog : Window
    {
        public ExpertProfiles.ExpertProfile SelectedProfile { get; private set; }

        public ProfileSelectDialog(List<ExpertProfiles.ExpertProfile> profiles)
        {
            Title = LocalizationManager.GetString("GlossaryMergeProfile");
            Width = 400;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));

            var grid = new Grid();
            grid.Margin = new Thickness(20);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock { Text = LocalizationManager.GetString("GlossaryMergeProfileHelp"), Margin = new Thickness(0, 0, 0, 8), FontSize = 12 };
            Grid.SetRow(lbl, 0);
            grid.Children.Add(lbl);

            var listBox = new ListBox { FontSize = 13 };
            foreach (var p in profiles)
                listBox.Items.Add(p);
            Grid.SetRow(listBox, 1);
            grid.Children.Add(listBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            var cancelBtn = new Button { Content = LocalizationManager.GetString("GlossaryCancel"), Width = 80, Height = 30 };
            cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
            btnPanel.Children.Add(cancelBtn);

            var mergeBtn = new Button { Content = LocalizationManager.GetString("GlossaryMerge"), Width = 80, Height = 30, Margin = new Thickness(8, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)), Foreground = Brushes.White, FontWeight = FontWeights.Medium, BorderThickness = new Thickness(0) };
            mergeBtn.Click += (_, _) =>
            {
                SelectedProfile = listBox.SelectedItem as ExpertProfiles.ExpertProfile;
                if (SelectedProfile != null)
                {
                    DialogResult = true;
                    Close();
                }
                else
                    MessageBox.Show(LocalizationManager.GetString("GlossarySelectProfileHint"), 
                        LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
            };
            btnPanel.Children.Add(mergeBtn);

            Content = grid;
        }
    }

    // ─── Conflict Dialog ─────────────────────────────────────────────

    public class ConflictDialog : Window
    {
        public ConflictDialog(List<GlossaryConflict> conflicts)
        {
            Title = LocalizationManager.GetString("GlossaryConflictsTitle");
            Width = 800;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));

            var grid = new Grid();
            grid.Margin = new Thickness(12);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var topText = new TextBlock
            {
                Text = string.Format(LocalizationManager.GetString("GlossaryConflictCount"), conflicts.Count),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(topText, 0);
            grid.Children.Add(topText);

            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                ItemsSource = conflicts,
                FontSize = 12,
                RowHeight = 28
            };
            dataGrid.Columns.Add(new DataGridTextColumn { Header = LocalizationManager.GetString("GlossaryColEnglish"), Binding = new Binding("TermEnglish"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = LocalizationManager.GetString("GlossaryExpectedTranslation"), Binding = new Binding("TermChinese"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = LocalizationManager.GetString("GlossaryActualTranslation"), Binding = new Binding("Translation"), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = LocalizationManager.GetString("GlossaryEntryKey"), Binding = new Binding("EntryKey"), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
            Grid.SetRow(dataGrid, 1);
            grid.Children.Add(dataGrid);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            var exportBtn = new Button
            {
                Content = LocalizationManager.GetString("GlossaryExportConflicts"),
                Width = 110,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x00, 0xAC, 0xC1)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Medium
            };
            exportBtn.Click += (_, _) => ExportConflicts(conflicts);
            btnPanel.Children.Add(exportBtn);

            var closeBtn = new Button
            {
                Content = LocalizationManager.GetString("GlossaryClose"),
                Width = 80,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Medium
            };
            closeBtn.Click += (_, _) => Close();
            btnPanel.Children.Add(closeBtn);

            Content = grid;
        }

        private void ExportConflicts(List<GlossaryConflict> conflicts)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = LocalizationManager.GetString("GlossaryExportConflictsTitle"),
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"conflict_report_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                new ReviewExporter().ExportConflicts(dlg.FileName, conflicts);
                MessageBox.Show(
                    LocalizationManager.GetString("GlossaryExportConflictsDone", dlg.FileName),
                    LocalizationManager.GetString("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(LocalizationManager.GetString("ExportFailed", ex.Message),
                    LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
