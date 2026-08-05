using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    /// <summary>
    /// 黑名单规则管理界面（Key 前缀 + 原文精确匹配两组规则）。纯 UI 层：
    /// 读写均委托给 IBlacklistManager，关闭后由调用方（MainWindow）刷新条目黑名单标记。
    /// </summary>
    public partial class BlacklistWindow : Window
    {
        private readonly IBlacklistManager _blacklistManager;
        private readonly ObservableCollection<PrefixItem> _displayedPrefixes = new();
        private readonly ObservableCollection<ExactItem> _displayedExactOriginals = new();

        private class PrefixItem
        {
            public string Prefix { get; set; } = "";
        }

        private class ExactItem
        {
            public string OriginalText { get; set; } = "";
        }

        public BlacklistWindow(IBlacklistManager blacklistManager)
        {
            InitializeComponent();
            _blacklistManager = blacklistManager;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLocalization();
            DeleteBtn.IsEnabled = false;
            ExactDeleteBtn.IsEnabled = false;
            PrefixGrid.ItemsSource = _displayedPrefixes;
            ExactGrid.ItemsSource = _displayedExactOriginals;
            RefreshAll();
        }

        private void ApplyLocalization()
        {
            Title = LocalizationManager.GetString("BlacklistWindowTitle");
            AddBtn.Content = $"+ {LocalizationManager.GetString("BlacklistAdd")}";
            DeleteBtn.Content = LocalizationManager.GetString("BlacklistDelete");
            ExactAddBtn.Content = $"+ {LocalizationManager.GetString("BlacklistAdd")}";
            ExactDeleteBtn.Content = LocalizationManager.GetString("BlacklistDelete");
            CloseBtn.Content = LocalizationManager.GetString("BlacklistClose");
            PrefixGrid.Columns[0].Header = LocalizationManager.GetString("BlacklistColPrefix");
            ExactGrid.Columns[0].Header = LocalizationManager.GetString("BlacklistColExact");
            HintTxt.Text = LocalizationManager.GetString("BlacklistHint");
        }

        private void RefreshAll()
        {
            _displayedPrefixes.Clear();
            foreach (var prefix in _blacklistManager.Prefixes)
                _displayedPrefixes.Add(new PrefixItem { Prefix = prefix });
            StatsTxt.Text = LocalizationManager.GetString("BlacklistCount", _blacklistManager.Prefixes.Count);

            _displayedExactOriginals.Clear();
            foreach (var text in _blacklistManager.ExactOriginalTexts)
                _displayedExactOriginals.Add(new ExactItem { OriginalText = text });
            ExactStatsTxt.Text = LocalizationManager.GetString("BlacklistCount", _blacklistManager.ExactOriginalTexts.Count);
        }

        // ─── Key prefix ───────────────────────────────────────────

        private void PrefixTxt_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AddBtn_Click(sender, e);
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            var prefix = PrefixTxt.Text?.Trim();
            if (string.IsNullOrEmpty(prefix))
                return;

            if (_blacklistManager.AddPrefix(prefix))
            {
                RefreshAll();
                PrefixTxt.Text = "";
                PrefixTxt.Focus();
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = PrefixGrid.SelectedItem as PrefixItem;
            if (selected == null)
                return;

            _blacklistManager.RemovePrefix(selected.Prefix);
            RefreshAll();
        }

        private void PrefixGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            DeleteBtn.IsEnabled = PrefixGrid.SelectedItem != null;
        }

        // ─── Exact original text ──────────────────────────────────

        private void ExactTxt_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ExactAddBtn_Click(sender, e);
        }

        private void ExactAddBtn_Click(object sender, RoutedEventArgs e)
        {
            var text = ExactTxt.Text?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            if (_blacklistManager.AddExactOriginalText(text))
            {
                RefreshAll();
                ExactTxt.Text = "";
                ExactTxt.Focus();
            }
        }

        private void ExactDeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ExactGrid.SelectedItem as ExactItem;
            if (selected == null)
                return;

            _blacklistManager.RemoveExactOriginalText(selected.OriginalText);
            RefreshAll();
        }

        private void ExactGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ExactDeleteBtn.IsEnabled = ExactGrid.SelectedItem != null;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
