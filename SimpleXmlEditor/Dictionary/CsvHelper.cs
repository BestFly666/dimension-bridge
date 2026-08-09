using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleXmlEditor.Dictionary
{
    public static class CsvHelper
    {
        public static bool IsHeaderLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            var lower = line.ToLowerInvariant();
            return lower.Contains("source") || lower.Contains("original") ||
                   lower.Contains("key") || lower.Contains("english") ||
                   lower.Contains("原文") || lower.Contains("源文本") ||
                   lower.Contains("英文") || lower.Contains("键") ||
                   lower == "en,ch,cat" || lower == "\"en\",\"ch\",\"cat\"";
        }

        public static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                        inQuotes = true;
                    else if (c == ',' || c == '\t')
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }
            result.Add(current.ToString());

            return result;
        }

        public static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            // 防 CSV 公式注入（OWASP）：= + - @ \t \r 开头前置 '，防 Excel 执行公式
            if (field[0] == '=' || field[0] == '+' || field[0] == '-' ||
                field[0] == '@' || field[0] == '\t' || field[0] == '\r')
            {
                field = "'" + field;
            }
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }
    }
}
