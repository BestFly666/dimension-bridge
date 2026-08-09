using System.Linq;

namespace SimpleXmlEditor.Utils
{
    /// <summary>
    /// 提示词文本净化：转义引号/反斜杠、剥离控制字符、截断超长文本，
    /// 防止用户输入（原文/术语/专家 Context/评估文本）逃逸出提示词结构或注入恶意指令。
    /// 所有把动态文本拼进 AI 提示词的路径都应经过此净化。
    /// </summary>
    public static class PromptTextSanitizer
    {
        /// <summary>
        /// 净化文本。空文本返回空串；超长截断；转义 \ 和 "；剥离 JSON 结构可能破坏的控制字符。
        /// </summary>
        public static string Sanitize(string text, int maxLength = 4000)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Limit individual text length to prevent prompt overflow
            if (text.Length > maxLength)
                text = text.Substring(0, maxLength) + "...[truncated]";

            // Escape quotes to break out of the "..." wrapper
            text = text.Replace("\\", "\\\\");
            text = text.Replace("\"", "\\\"");

            // Strip control characters that could break JSON parsing
            var cleanChars = text.Where(c => c >= 32 || c == '\n' || c == '\t').ToArray();
            return new string(cleanChars);
        }
    }
}
