using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    /// <summary>一条待人工确认的投票候选条目（窗口内展示模型）。</summary>
    public class VotingReviewItem
    {
        public string EntryKey { get; set; } = "";
        public string OriginalText { get; set; } = "";
        public string CurrentTranslation { get; set; } = "";
        public List<string> Options { get; set; } = new();
        public int SelectedIndex { get; set; }
    }

    /// <summary>
    /// 投票候选对比窗口：列出 AI 建议改动的条目及其候选译文（带评分），
    /// 由用户选定要应用的译文，确认后返回选择结果（key → 译文）。
    /// </summary>
    public partial class VotingReviewWindow : Window
    {
        private const char Separator = '│';
        private readonly List<VotingReviewItem> _items = new();
        private readonly Dictionary<string, string> _currentMap;

        public VotingReviewWindow(List<VotingResult> results, Dictionary<string, string> currentTranslations)
        {
            InitializeComponent();
            _currentMap = currentTranslations ?? new Dictionary<string, string>();

            var keepCurrent = LocalizationManager.GetString("VotingKeepCurrent");

            foreach (var vr in results)
            {
                // 将各代理评分按译文分组，计算平均分并降序排列
                var candidates = vr.AgentResults
                    .Where(r => !string.IsNullOrEmpty(r.TranslatedText))
                    .GroupBy(r => r.TranslatedText)
                    .Select(g => new { Text = g.Key, Avg = g.Average(r => r.Score) })
                    .OrderByDescending(c => c.Avg)
                    .ToList();

                var current = _currentMap.TryGetValue(vr.EntryKey, out var c) ? c : "";

                var options = new List<string> { keepCurrent };
                foreach (var cand in candidates)
                {
                    var candIndex = options.Count; // 1-based 序号
                    options.Add($"{LocalizationManager.GetString("VotingCandidateFormat", candIndex, cand.Avg)} {Separator} {cand.Text}");
                }

                // 默认选中 AI 认为最佳的候选（即 vr.BestTranslation）
                var selected = 0;
                if (!string.IsNullOrEmpty(vr.BestTranslation))
                {
                    var bestIdx = candidates.FindIndex(c => c.Text == vr.BestTranslation);
                    if (bestIdx >= 0)
                        selected = bestIdx + 1;
                }

                _items.Add(new VotingReviewItem
                {
                    EntryKey = vr.EntryKey,
                    OriginalText = vr.OriginalText,
                    CurrentTranslation = current,
                    Options = options,
                    SelectedIndex = selected
                });
            }

            ReviewList.ItemsSource = _items;
            ApplyLocalization(results.Count);
        }

        private void ApplyLocalization(int count)
        {
            Title = LocalizationManager.GetString("VotingReviewTitle");
            WindowTitle.Text = LocalizationManager.GetString("VotingReviewTitle");
            HintText.Text = LocalizationManager.GetString("VotingReviewHint", count);
            ApplyBtn.Content = LocalizationManager.GetString("VotingApplySelected");
            CancelBtn.Content = LocalizationManager.GetString("VotingCancel");
        }

        /// <summary>
        /// 返回用户选择结果（EntryKey → 选中的译文文本）。
        /// 选择"保持当前译文"（索引 0）的条目不包含在结果中。
        /// </summary>
        public Dictionary<string, string> GetSelections()
        {
            var selections = new Dictionary<string, string>();
            foreach (var item in _items)
            {
                if (item.SelectedIndex <= 0 || item.SelectedIndex >= item.Options.Count)
                    continue;

                var translation = ExtractTranslation(item.Options[item.SelectedIndex]);
                if (!string.IsNullOrEmpty(translation))
                    selections[item.EntryKey] = translation;
            }
            return selections;
        }

        /// <summary>从选项文本中提取译文（分隔符后部分）。</summary>
        private static string ExtractTranslation(string option)
        {
            var idx = option.IndexOf(Separator);
            return idx >= 0 ? option.Substring(idx + 1).Trim() : "";
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
