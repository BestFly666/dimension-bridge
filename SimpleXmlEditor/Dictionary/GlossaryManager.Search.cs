using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleXmlEditor.Dictionary
{
    /// <summary>
    /// GlossaryManager: 搜索与过滤辅助方法。
    /// 与 GlossaryManager.Index.cs 拆分，保持单文件 ≤ 400 行。
    /// </summary>
    public partial class GlossaryManager
    {
        // ─── Search & Filter ─────────────────────────────────────────

        /// <summary>Filter terms by search text (matches English, Chinese, or Tags)</summary>
        public List<GlossaryTerm> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Terms.Values.OrderBy(t => t.English).ToList();

            // 支持多关键词：空格/全角空格分隔，所有关键词命中才显示（AND）
            var keywords = query.Trim()
                .Split(new[] { ' ', '\u3000' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToList();

            return Terms.Values
                .Where(t => keywords.All(k =>
                    (t.English ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0
                    || (t.Chinese ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0
                    || (t.Tags ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0
                    || (t.Category ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(t => t.English)
                .ToList();
        }

        /// <summary>Get all unique categories</summary>
        public List<string> GetAllCategories()
        {
            return Terms.Values
                .Where(t => !string.IsNullOrEmpty(t.Category))
                .Select(t => t.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        /// <summary>Get all unique tags</summary>
        public List<string> GetAllTags()
        {
            return Terms.Values
                .SelectMany(t => t.TagList)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        /// <summary>Get terms filtered by status</summary>
        public List<GlossaryTerm> FilterByStatus(string status)
        {
            return Terms.Values
                .Where(t => t.Status == status)
                .OrderBy(t => t.English)
                .ToList();
        }

        /// <summary>Get terms filtered by category</summary>
        public List<GlossaryTerm> FilterByCategory(string category)
        {
            return Terms.Values
                .Where(t => t.Category == category)
                .OrderBy(t => t.English)
                .ToList();
        }
    }
}
