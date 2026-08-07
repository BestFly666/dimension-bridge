using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Plugins
{
    /// <summary>
    /// CSV 支持（Excel 直开，本地化交付最主流格式）。
    /// 列结构自动识别：
    ///   - 3 列（Key, Original, Translation）：有表头关键词（key/original/translation 等）或至少 3 列
    ///   - 2 列（Key, Value）：表头为 Key,Value 或恰好 2 列
    /// 行为：加载识别编码（UTF-8 BOM → UTF-8 → GBK），保存按原编码写回，译文为空回退原文，
    ///       与工具"导出替换原值"约定一致；支持引号包裹与 "" 转义（引号内逗号/换行安全）。
    /// Non-Goals（V1）：不带引号的分隔符不规范文件、表头缺失但第一列恰好叫 Key 的数据行（按表头处理）。
    /// </summary>
    public class CsvFilePlugin : IFileFormatPlugin
    {
        public string FormatName => "CSV";
        public string[] FileExtensions => new[] { ".csv" };

        /// <summary>加载时的编码，保存时复用（保持原编码写回，国内 Excel 打开不乱码）。</summary>
        private Encoding _loadedEncoding = new UTF8Encoding(false);

        /// <summary>是否有表头（保存时回写表头）。</summary>
        private bool _hasHeader;

        /// <summary>两列模式（Key/Value）还是三列模式（Key/Original/Translation）。</summary>
        private bool _isTwoColumn;

        public List<LocalizationEntry> Load(string filePath)
        {
            _loadedEncoding = TextEncodingDetector.Detect(filePath);
            var text = File.ReadAllText(filePath, _loadedEncoding);
            var rows = ParseCsv(text);
            if (rows.Count == 0) return new List<LocalizationEntry>();

            // 列结构识别：首行是否表头 + 两列/三列
            var first = rows[0];
            bool headerDetected = IsHeaderRow(first);
            _hasHeader = headerDetected;
            _isTwoColumn = !headerDetected
                ? first.Length == 2
                : !IsThreeColumnHeader(first);

            var entries = new List<LocalizationEntry>();
            int rowNumber = 0;
            int start = headerDetected ? 1 : 0;

            for (int r = start; r < rows.Count; r++)
            {
                var cells = rows[r];
                if (cells.Length == 0) continue;

                string key = cells.Length > 0 ? cells[0] : "";
                string original = _isTwoColumn
                    ? (cells.Length > 1 ? cells[1] : "")
                    : (cells.Length > 1 ? cells[1] : "");
                string translation = (!_isTwoColumn && cells.Length > 2) ? cells[2] : "";

                if (string.IsNullOrEmpty(key)) continue;

                rowNumber++;
                entries.Add(new LocalizationEntry
                {
                    RowNumber = rowNumber,
                    Key = key,
                    Value = original,
                    Translation = translation,
                    IsSelected = false
                });
            }
            return entries;
        }

        public void Save(string filePath, List<LocalizationEntry> entries)
        {
            var sb = new StringBuilder();
            if (_hasHeader)
            {
                sb.AppendLine(_isTwoColumn ? "Key,Value" : "Key,Original,Translation");
            }

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                var original = entry.Value ?? "";
                var translation = string.IsNullOrEmpty(entry.Translation) ? original : entry.Translation;

                var cells = _isTwoColumn
                    ? new[] { entry.Key, translation }
                    : new[] { entry.Key, original, translation };
                sb.AppendLine(string.Join(",", cells.Select(EscapeField)));
            }

            File.WriteAllText(filePath, sb.ToString(), _loadedEncoding);
        }

        /// <summary>首行是否表头：第一列为 key/id 且其余列是常见列名（key/source/value/translation 等）。</summary>
        private static bool IsHeaderRow(string[] cells)
        {
            if (cells.Length == 0) return false;
            var c0 = cells[0].Trim().ToLowerInvariant();
            if (c0 != "key" && c0 != "id") return false;

            if (cells.Length == 1) return true;
            var c1 = cells[1].Trim().ToLowerInvariant();
            return c1 is "original" or "source" or "value" or "text" or "english"
                or "translation" or "target" or "chinese" or "zh";
        }

        /// <summary>表头是否三列：第 3 列（若存在）是翻译列关键词。</summary>
        private static bool IsThreeColumnHeader(string[] cells)
        {
            if (cells.Length < 3) return false;
            var c2 = cells[2].Trim().ToLowerInvariant();
            return c2 is "translation" or "target" or "chinese" or "zh";
        }

        /// <summary>字段转义：含逗号/引号/换行时用引号包裹，内部引号双写。</summary>
        private static string EscapeField(string field)
        {
            if (field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return field;
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>字符级 CSV 解析：支持引号包裹、"" 转义、引号内逗号/换行。</summary>
        private static List<string[]> ParseCsv(string text)
        {
            var rows = new List<string[]>();
            var field = new StringBuilder();
            var row = new List<string>();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    switch (c)
                    {
                        case '"':
                            inQuotes = true;
                            break;
                        case ',':
                            row.Add(field.ToString());
                            field.Clear();
                            break;
                        case '\r':
                            break;
                        case '\n':
                            row.Add(field.ToString());
                            field.Clear();
                            rows.Add(row.ToArray());
                            row = new List<string>();
                            break;
                        default:
                            field.Append(c);
                            break;
                    }
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }
    }
}
