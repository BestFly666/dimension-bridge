using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor
{
    // ─── Data Refresh / Filtering ────────────────────────────────────

    public partial class GlossaryWindow
    {
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
}
