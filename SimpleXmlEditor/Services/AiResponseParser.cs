using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SimpleXmlEditor.Services
{
    /// <summary>
    /// 统一的 AI 响应解析工具：code fence 清理、标准 translations JSON 提取、
    /// 以及三级回退策略。供 TranslationOrchestrator / TranslationEvaluator 复用，
    /// 消除各处重复的解析逻辑。
    /// </summary>
    public static class AiResponseParser
    {
        private static readonly object _logLock = new();

        /// <summary>去除 markdown code fence（```json ... ```）包装。</summary>
        public static string StripCodeFence(string response)
        {
            var clean = response.Trim();
            if (clean.StartsWith("```json"))
                clean = clean[7..];
            else if (clean.StartsWith("```"))
                clean = clean[3..];
            if (clean.EndsWith("```"))
                clean = clean[..^3];
            return clean.Trim();
        }

        /// <summary>
        /// 解析批量翻译响应（index → 译文文本）。
        /// 先尝试标准 JSON（{"translations":[{index,translation}]}），失败走三级回退。
        /// </summary>
        public static Dictionary<int, string> ParseTranslations(string response, int expectedCount)
        {
            var results = new Dictionary<int, string>();

            if (string.IsNullOrEmpty(response))
                return results;

            var clean = StripCodeFence(response);

            // 截断检测：JSON 明显不完整时直接抛异常，让拆半重试处理
            if (clean.Contains("\"translations\"") && !clean.Contains("\"translations\": []"))
            {
                var hasOpenBracket = clean.Contains("\"translations\": [") || clean.Contains("\"translations\":[");
                var hasCloseBracket = clean.Contains("]");
                if (hasOpenBracket && !hasCloseBracket)
                {
                    LogParseError("Response truncated: 'translations' array not closed", clean);
                    throw new InvalidOperationException("Response truncated: 'translations' array not closed");
                }
            }

            try
            {
                var json = JObject.Parse(clean);
                var translations = json["translations"];

                // 兼容数组格式
                if (translations is JArray arr)
                {
                    foreach (var t in arr)
                    {
                        var idx = t["index"]?.ToObject<int>() ?? 0;
                        var text = t["translation"]?.ToString()?.Trim();
                        if (idx > 0 && idx <= expectedCount && IsValidTranslation(text))
                            results[idx] = text;
                    }
                    return results;
                }

                // 兼容对象格式：{ "translations": { "1": "译文1", "2": "译文2" } }
                if (translations is JObject obj)
                {
                    foreach (var prop in obj.Properties())
                    {
                        if (int.TryParse(prop.Name, out var idx) && idx > 0 && idx <= expectedCount)
                        {
                            var text = prop.Value?.ToString()?.Trim();
                            if (IsValidTranslation(text))
                                results[idx] = text;
                        }
                    }
                    return results;
                }
            }
            catch (Exception ex)
            {
                LogParseError($"JSON parse failed: {ex.Message}", clean);
            }

            ParseTranslationsFallback(response, expectedCount, results);
            return results;
        }

        /// <summary>
        /// 检查译文是否有效（过滤 JSON 结构片段等无效内容）。
        /// </summary>
        private static bool IsValidTranslation(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // 过滤明显的 JSON 结构片段
            if (text.Contains("\"translations\"") || 
                text.Contains("{\"") || 
                text.Contains("\"index\"") || 
                text.Contains("\"translation\""))
                return false;

            // 过滤纯 JSON 结构字符（只有 [ { } ] : , 等，没有实际内容）
            var trimmed = text.Trim();
            if (trimmed.Length <= 5 && 
                (trimmed.Contains("{") || trimmed.Contains("[") || trimmed.Contains("}") || trimmed.Contains("]")))
                return false;

            return true;
        }

        /// <summary>
        /// 三级回退策略：提取 JSON 片段 → 正则 "N. 译文" → 逐行解析。
        /// </summary>
        private static void ParseTranslationsFallback(string response, int expectedCount, Dictionary<int, string> results)
        {
            var clean = StripCodeFence(response);

            // Strategy 1: Extract JSON fragment
            var jsonStart = clean.IndexOf('{');
            var jsonEnd = clean.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                try
                {
                    var jsonStr = clean[jsonStart..(jsonEnd + 1)];
                    var jsonResponse = JObject.Parse(jsonStr);
                    var translations = jsonResponse["translations"];

                    // 兼容数组
                    if (translations is JArray arr)
                    {
                        foreach (var t in arr)
                        {
                            var idx = t["index"]?.ToObject<int>() ?? 0;
                            var text = t["translation"]?.ToString()?.Trim();
                            if (idx > 0 && idx <= expectedCount && IsValidTranslation(text))
                                results[idx] = text;
                        }
                        return;
                    }

                    // 兼容对象
                    if (translations is JObject obj)
                    {
                        foreach (var prop in obj.Properties())
                        {
                            if (int.TryParse(prop.Name, out var idx) && idx > 0 && idx <= expectedCount)
                            {
                                var text = prop.Value?.ToString()?.Trim();
                                if (IsValidTranslation(text))
                                    results[idx] = text;
                            }
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogParseError($"Fallback JSON fragment parse failed: {ex.Message}", clean);
                }
            }

            // Strategy 2: Regex for "N. \"translation\"" pattern
            var lines = clean.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var regex = new Regex(@"(\d+)[\.\s]\s*[""""'](.+?)[""""']");
            foreach (var line in lines)
            {
                var match = regex.Match(line.Trim());
                if (match.Success && int.TryParse(match.Groups[1].Value, out var idx))
                {
                    var text = match.Groups[2].Value.Trim();
                    if (idx > 0 && idx <= expectedCount && IsValidTranslation(text))
                        results[idx] = text;
                }
            }

            // Strategy 3: Line-by-line parsing
            if (results.Count == 0)
            {
                for (int i = 0; i < Math.Min(lines.Length, expectedCount); i++)
                {
                    var line = lines[i].Trim();
                    line = line.Replace($"{i + 1}.", "").Replace("-", "").Trim();
                    if (line.StartsWith("\"") && line.EndsWith("\""))
                        line = line[1..^1];
                    if (line.StartsWith("\u201C") && line.EndsWith("\u201D"))
                        line = line[1..^1];
                    if (!string.IsNullOrEmpty(line) && !line.Contains("{") && !line.Contains("}") && !line.Contains("index") && IsValidTranslation(line))
                        results[i + 1] = line;
                }
            }
        }

        private static void LogParseError(string message, string responseSnippet)
        {
            try
            {
                lock (_logLock)
                {
                    // 写到 AppData（与缓存/进度文件一致），避免污染程序目录（bin 随构建变化）
                    var logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SimpleXmlEditor");
                    Directory.CreateDirectory(logDir);
                    var logPath = Path.Combine(logDir, "parse_errors.log");
                    var snippet = responseSnippet.Length > 300 ? responseSnippet[..300] + "..." : responseSnippet;
                    File.AppendAllText(logPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n" +
                        $"Response: {snippet}\n\n");
                }
            }
            catch
            {
                // 日志失败不影响主流程
            }
        }
    }
}