using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Plugins
{
    /// <summary>
    /// INI 支持（游戏配置文件常用）。
    /// 格式：注释行（; #）、[Section] 段、key=value 键值对。
    /// 带段的 Key 存为 "[Section]key"，保存时按 [Section] 前缀还原段结构（同段连续输出，换段自动补段头）。
    /// 行为：加载识别编码并原编码写回，译文为空回退原文，与工具"导出替换原值"约定一致。
    /// Non-Goals（V1）：无 Key 的键值对（如布尔 flag）跳过、注释/空行不保留、key 同时含 '=' 与 ':' 以 '=' 优先。
    /// </summary>
    public class IniFilePlugin : IFileFormatPlugin
    {
        public string FormatName => "INI";
        public string[] FileExtensions => new[] { ".ini" };

        private Encoding _loadedEncoding = new UTF8Encoding(false);

        public List<LocalizationEntry> Load(string filePath)
        {
            _loadedEncoding = TextEncodingDetector.Detect(filePath);
            var lines = File.ReadAllLines(filePath, _loadedEncoding);

            var entries = new List<LocalizationEntry>();
            string section = "";
            int rowNumber = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line[0] == ';' || line[0] == '#') continue;

                if (line[0] == '[' && line.EndsWith("]"))
                {
                    section = line[1..^1].Trim();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();
                if (key.Length == 0) continue;

                rowNumber++;
                entries.Add(new LocalizationEntry
                {
                    RowNumber = rowNumber,
                    Key = FormatKey(section, key),
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
            string currentSection = null; // 当前已输出的段；null 表示刚输出过"无段条目"

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                var text = string.IsNullOrEmpty(entry.Translation) ? entry.Value : entry.Translation;

                var (section, key) = SplitKey(entry.Key);
                if (section != null)
                {
                    if (section != currentSection)
                    {
                        // 无段条目之后进入新段：补空行分隔
                        if (currentSection != null && sb.Length > 0 && !sb.ToString().EndsWith("\n\n"))
                            sb.AppendLine();
                        sb.Append('[').Append(section).Append(']').AppendLine();
                        currentSection = section;
                    }
                }
                else if (currentSection != null)
                {
                    sb.AppendLine();
                    currentSection = null;
                }

                sb.Append(key).Append('=').Append(text).AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), _loadedEncoding);
        }

        /// <summary>带段前缀的 Key："[Section]key"；无段则原样。</summary>
        private static string FormatKey(string section, string key) =>
            section.Length > 0 ? $"[{section}]{key}" : key;

        /// <summary>解析 "[Section]key" 为 (section, key)；无段返回 (null, key)。</summary>
        private static (string section, string key) SplitKey(string key)
        {
            if (key.Length > 2 && key[0] == '[')
            {
                int close = key.IndexOf(']');
                if (close > 1)
                    return (key[1..close], key[(close + 1)..]);
            }
            return (null, key);
        }
    }
}
