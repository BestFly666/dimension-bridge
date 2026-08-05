using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    // ─── Import / Export ─────────────────────────────────────────

    public partial class GlossaryWindow
    {
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
