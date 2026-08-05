using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    public class EvaluationItem
    {
        public string EntryKey { get; set; } = "";
        public double Score { get; set; }
        public string ScoreDisplay => Score == 0 ? Localization.LocalizationManager.GetString("EvalNA") : $"{Score:F1}";
        public string Explanation { get; set; } = "";
        public string Improvement { get; set; } = "";
        public string ProviderName { get; set; } = "";

        public string ScoreColor => Score switch
        {
            >= 8 => "#2E7D32",
            >= 5 => "#F57F17",
            > 0 => "#C62828",
            _ => "#9E9E9E"
        };

        public bool HasSuggestion => !string.IsNullOrEmpty(Improvement);
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    public partial class EvaluationWindow : Window
    {
        private readonly ObservableCollection<EvaluationItem> _items = new();
        private readonly Action<string, string> _onApplySuggestion;

        public EvaluationWindow(IEnumerable<EvaluationResult> results, Dictionary<string, EvaluationResult> resultMap, Action<string, string> onApplySuggestion = null)
        {
            InitializeComponent();

            Resources["BoolToVis"] = new BoolToVisibilityConverter();
            _onApplySuggestion = onApplySuggestion;

            // Apply localization
            ApplyLocalization();

            foreach (var r in results)
            {
                var entryKey = r.TranslatedText; // TranslatedText holds the entry key
                _items.Add(new EvaluationItem
                {
                    EntryKey = entryKey,
                    Score = r.Score,
                    Explanation = r.Explanation,
                    Improvement = r.Improvement,
                    ProviderName = r.ProviderName
                });
            }

            ResultsList.ItemsSource = _items;
            UpdateSummary();
        }

        private void ApplyLocalization()
        {
            Title = Localization.LocalizationManager.GetString("EvaluationTitle");
            WindowTitle.Text = Localization.LocalizationManager.GetString("EvaluationTitle");
            ScoreDistLabel.Text = Localization.LocalizationManager.GetString("EvalScoreDist");
            MarkLowScoresBtn.Content = Localization.LocalizationManager.GetString("EvalMarkLowScores");
            CloseBtn.Content = Localization.LocalizationManager.GetString("EvalClose");
        }

        public void AddResult(EvaluationResult result, string entryKey)
        {
            _items.Add(new EvaluationItem
            {
                EntryKey = entryKey,
                Score = result.Score,
                Explanation = result.Explanation,
                Improvement = result.Improvement,
                ProviderName = result.ProviderName
            });
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var scored = _items.Where(i => i.Score > 0).ToList();
            var high = scored.Count(i => i.Score >= 8);
            var mid = scored.Count(i => i.Score >= 5 && i.Score < 8);
            var low = scored.Count(i => i.Score > 0 && i.Score < 5);
            var avg = scored.Any() ? scored.Average(i => i.Score) : 0;

            SummaryText.Text = Localization.LocalizationManager.GetString("EvalEvaluated", _items.Count);
            HighCount.Text = Localization.LocalizationManager.GetString("EvalHighCount", high);
            MidCount.Text = Localization.LocalizationManager.GetString("EvalMidCount", mid);
            LowCount.Text = Localization.LocalizationManager.GetString("EvalLowCount", low);
            AvgScoreText.Text = Localization.LocalizationManager.GetString("EvalAvgScore", avg);
        }

        private void ApplySuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is EvaluationItem item)
            {
                if (!string.IsNullOrEmpty(item.Improvement))
                {
                    _onApplySuggestion?.Invoke(item.EntryKey, item.Improvement);
                    AddLogMessage(Localization.LocalizationManager.GetString("EvalAppliedSuggestion", item.EntryKey));
                }
            }
        }

        private void MarkLowScores_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items.Where(i => i.Score > 0 && i.Score < 5))
            {
                AddLogMessage(Localization.LocalizationManager.GetString("EvalMarkAsLow", item.EntryKey, item.Score));
            }
            var count = _items.Count(i => i.Score > 0 && i.Score < 5);
            MessageBox.Show(Localization.LocalizationManager.GetString("EvalMarkedEntries", count),
                Localization.LocalizationManager.GetString("EvalMarkComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddLogMessage(string message)
        {
            // Log would be handled by the parent window
        }
    }
}
