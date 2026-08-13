using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor
{
    // ─── CRUD / Merge Profile / Conflict Detection ──────────────────

    public partial class GlossaryWindow
    {
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
            Height = 400;
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

            // Buttons（不放入 grid 最后一行：固定高度窗口下字段多时按钮会被挤出可视区，
            // 改为 DockPanel 固定在窗口底部，内容区用 ScrollViewer 滚动，确定按钮始终可点）
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var cancelBtn = new Button { Content = LocalizationManager.GetString("GlossaryCancel"), Width = 80, Height = 30, Margin = new Thickness(4, 0, 0, 0) };
            cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
            btnPanel.Children.Add(cancelBtn);

            var saveBtn = new Button { Content = LocalizationManager.GetString("GlossarySave"), Width = 80, Height = 30, Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), Foreground = Brushes.White, FontWeight = FontWeights.Medium, BorderThickness = new Thickness(0) };
            saveBtn.Click += (_, _) => SaveAndClose();
            btnPanel.Children.Add(saveBtn);

            var root = new DockPanel();
            DockPanel.SetDock(btnPanel, Dock.Bottom);
            root.Children.Add(btnPanel);
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = grid };
            root.Children.Add(scroll);

            Content = root;
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
}
