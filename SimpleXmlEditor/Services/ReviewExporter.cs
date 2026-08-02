using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor.Services
{
    /// <summary>Summary statistics of an exported review report.</summary>
    public class ReviewExportResult
    {
        public int Total { get; set; }
        public int Reviewed { get; set; }
        public int NeedsFix { get; set; }
        public int NotReviewed { get; set; }
        public string FilePath { get; set; } = "";
    }

    /// <summary>一条一致性检测问题：同一原文被翻译成了不同译文。</summary>
    public class ConsistencyIssue
    {
        public string Source { get; set; } = "";
        public List<string> Translations { get; set; } = new();
        public List<string> Keys { get; set; } = new();
    }

    /// <summary>
    /// Exports review status of localization entries to a CSV report.
    /// Extracted from MainWindow to separate UI from business logic.
    /// </summary>
    public class ReviewExporter
    {
        /// <summary>
        /// Writes all entries (Key/Original/Translation + localized review status) to a CSV file.
        /// </summary>
        public ReviewExportResult Export(string filePath, IEnumerable<LocalizationEntry> entries)
        {
            var entryList = (entries ?? new List<LocalizationEntry>()).ToList();
            var result = new ReviewExportResult
            {
                Total = entryList.Count,
                Reviewed = CountByStatus(entryList, ReviewStatus.Reviewed),
                NeedsFix = CountByStatus(entryList, ReviewStatus.NeedsFix),
                NotReviewed = CountByStatus(entryList, ReviewStatus.NotReviewed),
                FilePath = filePath
            };

            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("Status,Key,Original,Translation");

            foreach (var entry in entryList)
            {
                var status = entry.ReviewStatus switch
                {
                    ReviewStatus.Reviewed => LocalizationManager.GetString("ReviewStatusReviewed"),
                    ReviewStatus.NeedsFix => LocalizationManager.GetString("ReviewStatusNeedsFix"),
                    _ => LocalizationManager.GetString("ReviewStatusNotReviewed")
                };
                writer.WriteLine($"{status},{EscapeCsv(entry.Key)},{EscapeCsv(entry.Value)},{EscapeCsv(entry.Translation ?? "")}");
            }

            return result;
        }

        /// <summary>
        /// Exports glossary conflict detection results to a CSV file.
        /// Columns: EntryKey, Source, Translation, TermEnglish, Expected, Category.
        /// </summary>
        public void ExportConflicts(string filePath, IEnumerable<GlossaryConflict> conflicts)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("EntryKey,Source,Translation,TermEnglish,Expected,Category");

            foreach (var c in conflicts ?? new List<GlossaryConflict>())
            {
                writer.WriteLine(
                    $"{EscapeCsv(c.EntryKey)},{EscapeCsv(c.SourceText)},{EscapeCsv(c.Translation)}," +
                    $"{EscapeCsv(c.TermEnglish)},{EscapeCsv(c.TermChinese)},{EscapeCsv(c.Category)}");
            }
        }

        /// <summary>
        /// Exports consistency scan issues to a CSV file.
        /// Columns: Original, Translations (pipe-separated), EntryKeys (pipe-separated).
        /// </summary>
        public void ExportConsistency(string filePath, IEnumerable<ConsistencyIssue> issues)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("Original,Translations,EntryKeys");

            foreach (var issue in issues ?? new List<ConsistencyIssue>())
            {
                writer.WriteLine(
                    $"{EscapeCsv(issue.Source)},{EscapeCsv(string.Join(" | ", issue.Translations))}," +
                    $"{EscapeCsv(string.Join(" | ", issue.Keys))}");
            }
        }

        private static int CountByStatus(IEnumerable<LocalizationEntry> entries, ReviewStatus status)
        {
            var count = 0;
            foreach (var entry in entries)
            {
                if (entry.ReviewStatus == status) count++;
            }
            return count;
        }

        private static string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n"))
                return $"\"{text.Replace("\"", "\"\"")}\"";
            return text;
        }
    }
}
