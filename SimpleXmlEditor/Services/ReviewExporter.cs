using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
