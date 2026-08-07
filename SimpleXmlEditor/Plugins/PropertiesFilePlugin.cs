using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Plugins
{
    /// <summary>
    /// Java Properties 支持（.properties，Java/Android 项目资源）。
    /// 格式：注释行（# !）、key=value / key:value / key value、行尾反斜杠续行。
    /// 转义：加载时解析 \\ \n \t \r \uXXXX \: \=；保存时转义 \ 与换行/制表（中文直接写，保持可读）。
    /// 行为：加载识别编码并原编码写回（中文项目通常直接 UTF-8，而非规范默认 ISO-8859-1），译文为空回退原文。
    /// Non-Goals（V1）：value 前导空格保留（仅分隔符本身空白被跳过）、逻辑行尾无多余转义空格处理。
    /// </summary>
    public class PropertiesFilePlugin : IFileFormatPlugin
    {
        public string FormatName => "Java Properties";
        public string[] FileExtensions => new[] { ".properties" };

        private Encoding _loadedEncoding = new UTF8Encoding(false);

        public List<LocalizationEntry> Load(string filePath)
        {
            _loadedEncoding = TextEncodingDetector.Detect(filePath);
            var lines = File.ReadAllLines(filePath, _loadedEncoding);

            var entries = new List<LocalizationEntry>();
            var logical = new StringBuilder();
            bool pendingContinuation = false;
            int rowNumber = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine;
                if (pendingContinuation)
                {
                    logical.Append(line);
                    pendingContinuation = false;
                }
                else
                {
                    logical.Clear();
                    logical.Append(line);
                }

                // 行尾单个反斜杠 = 续行（注意：\\ 结尾不算）
                bool endsWithSingleBackslash = EndsWithSingleBackslash(logical);
                if (endsWithSingleBackslash)
                {
                    pendingContinuation = true;
                    continue;
                }

                var parsed = ParseLine(logical.ToString());
                if (parsed == null) continue;

                rowNumber++;
                entries.Add(new LocalizationEntry
                {
                    RowNumber = rowNumber,
                    Key = parsed.Value.key,
                    Value = parsed.Value.value,
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
                sb.Append(EscapeKey(entry.Key)).Append('=').Append(EscapeValue(text)).AppendLine();
            }
            File.WriteAllText(filePath, sb.ToString(), _loadedEncoding);
        }

        /// <summary>解析单条逻辑行，返回 (key, value)；注释/空行/无分隔符返回 null。</summary>
        private static (string key, string value)? ParseLine(string line)
        {
            var s = line.TrimStart();
            if (s.Length == 0 || s[0] == '#' || s[0] == '!') return null;

            // 找第一个未转义分隔符（= : 或空白）
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\') { i += 2; continue; }
                if (c == '=' || c == ':' || char.IsWhiteSpace(c)) break;
                i++;
            }

            string key = Unescape(s[..i]);

            // 跳过分隔符与紧邻空白（规范：分隔符与 key 间空白全部跳过）
            int j = i;
            while (j < s.Length && (s[j] == '=' || s[j] == ':' || char.IsWhiteSpace(s[j]))) j++;
            string value = Unescape(s[j..]);

            if (key.Length == 0) return null;
            return (key, value);
        }

        /// <summary>逻辑行是否以单个反斜杠结尾（\\ 结尾不算续行）。</summary>
        private static bool EndsWithSingleBackslash(StringBuilder sb)
        {
            int backslashes = 0;
            for (int i = sb.Length - 1; i >= 0 && sb[i] == '\\'; i--)
                backslashes++;
            return backslashes % 2 == 1;
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }

                char n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 < s.Length &&
                            int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                        {
                            sb.Append((char)cp);
                            i += 4;
                        }
                        else
                        {
                            sb.Append('u');
                        }
                        break;
                    default: sb.Append(n); break; // \\ \= \: \ 空格 → 字面字符
                }
            }
            return sb.ToString();
        }

        private static string EscapeKey(string key)
        {
            var sb = new StringBuilder();
            foreach (char c in key)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '=': sb.Append("\\="); break;
                    case ':': sb.Append("\\:"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static string EscapeValue(string value)
        {
            var sb = new StringBuilder();
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\r': sb.Append("\\r"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
