using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Plugins
{
    /// <summary>
    /// 通用键值对 TXT 支持（通用能力，非特定游戏格式）。
    /// 格式：每行 "KEY = value" 或 "KEY: value"；# 或 ; 开头为注释行；空行跳过。
    /// 编码：加载时自动识别（UTF-8 BOM → 严格 UTF-8 → GBK 兜底），保存时按原编码写回，
    ///       避免改编码后游戏读不了。
    /// 行为：保存时写 "KEY = 译文"（译文为空则回退原文），与工具"导出替换原值"约定一致。
    /// Non-Goals（V1）：无 Key 的纯文本行跳过、行内多分隔符/引号包裹、编码自动检测仅覆盖
    ///       常见三种，特殊文件遇真样本再迭代。
    /// </summary>
    public class TxtFilePlugin : IFileFormatPlugin
    {
        public string FormatName => "Key-Value TXT";
        public string[] FileExtensions => new[] { ".txt" };

        /// <summary>加载时的编码，保存时复用（保持原编码写回）。</summary>
        private Encoding _loadedEncoding = new UTF8Encoding(false);

        public List<LocalizationEntry> Load(string filePath)
        {
            var encoding = TextEncodingDetector.Detect(filePath);
            _loadedEncoding = encoding;

            var entries = new List<LocalizationEntry>();
            var lines = File.ReadAllLines(filePath, encoding);
            int rowNumber = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#' || line[0] == ';') continue;

                int sep = FindSeparator(line);
                if (sep <= 0) continue; // 无分隔符的行跳过（V1 Non-Goal：无 Key 纯文本行）

                var key = line[..sep].Trim();
                var value = line[(sep + 1)..].Trim();
                if (key.Length == 0) continue;

                rowNumber++;
                entries.Add(new LocalizationEntry
                {
                    RowNumber = rowNumber,
                    Key = key,
                    Value = value,
                    Translation = "",
                    IsSelected = false
                });
            }
            return entries;
        }

        public void Save(string filePath, List<LocalizationEntry> entries)
        {
            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                var text = string.IsNullOrEmpty(entry.Translation) ? entry.Value : entry.Translation;
                sb.Append(entry.Key).Append(" = ").Append(text).AppendLine();
            }
            File.WriteAllText(filePath, sb.ToString(), _loadedEncoding);
        }

        /// <summary>行内第一个 '=' 或 ':' 的索引；均无则返回 -1。</summary>
        private static int FindSeparator(string line)
        {
            int eq = line.IndexOf('=');
            int colon = line.IndexOf(':');
            if (eq < 0) return colon;
            if (colon < 0) return eq;
            return Math.Min(eq, colon);
        }
    }
}
