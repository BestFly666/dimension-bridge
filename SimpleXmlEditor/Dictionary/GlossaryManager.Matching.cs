using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SimpleXmlEditor.Dictionary
{
    /// <summary>
    /// GlossaryManager: 宽松术语相关判定（AI 提示词注入用）与严格整词匹配。
    /// 与 GlossaryManager.Index.cs 拆分，保持单文件 ≤ 400 行。
    /// </summary>
    public partial class GlossaryManager
    {
        // ─── 宽松相关判定（仅用于 AI 提示词注入） ──────────────────

        /// <summary>
        /// 宽松术语相关判定：用于 AI 提示词注入的候选验证。
        /// 原文无需包含术语的完整核心词序列——命中术语首核心词，且满足以下任一即视为相关：
        ///   1) 按序命中 ≥ ceil(核心词数/2) 个核心词（2 核心词时 ≥1，即命中首核心词即相关，
        ///      覆盖 "Skipray" 单独短名、"Xyston Siege Destroyer Upkeep" 缺 Star 这类省略变体）；
        ///   2) 首核心词后紧邻类名修饰词（class/mk/type 等），覆盖单位名 "Xyston-class"、"Quasar Fire-class"。
        /// 不影响 ContainsWholeWord 的严格语义（替换、冲突检测仍走严格路径）。
        /// </summary>
        public static bool IsTermRelated(string text, string term)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
                return false;
            if (ContainsWholeWord(text, term))
                return true;

            // 术语核心词（去修饰词、规范化小写）
            var coreWords = GetCoreWords(term);
            if (coreWords.Count == 0)
                return false;

            // 原文词（规范化小写，保留复数形态用于复数宽容比较）
            var textWords = TokenizeNormalized(text);
            if (textWords.Count == 0)
                return false;

            // 找首核心词位置（词干/复数宽容）
            int firstIdx = textWords.FindIndex(w => WordMatches(w, coreWords[0]));
            if (firstIdx < 0)
                return false;

            // 条件2：首核心词后紧邻类名修饰词（"Xyston-class"、"Quasar Fire-class"）
            if (firstIdx + 1 < textWords.Count && IsModifierToken(textWords[firstIdx + 1], ModifierTokenPattern))
                return true;

            // 条件1：从首核心词后按序命中后续核心词。
            // 阈值 = ceil(核心词数/2)，封顶到核心词数：
            //   2 核心词 → 1（覆盖 "Skipray" 单独短名，术语 "Skipray blastboat" 原文只出现 Skipray）
            //   3 核心词 → 2，4+ 核心词 → 半数以上（保持原语义）
            int minHits = Math.Min((int)Math.Ceiling(coreWords.Count / 2.0), coreWords.Count);
            int hits = 1; // 首核心词已命中
            int scan = firstIdx + 1;
            for (int ci = 1; ci < coreWords.Count && hits < coreWords.Count; ci++)
            {
                int found = textWords.FindIndex(scan, w => WordMatches(w, coreWords[ci]));
                if (found >= 0)
                {
                    hits++;
                    scan = found + 1;
                }
            }
            return hits >= minHits;
        }

        /// <summary>提取术语的核心词：去掉修饰词与分隔符，规范化为小写（去撇号）。</summary>
        private static List<string> GetCoreWords(string term)
        {
            var words = term.Split(new[] { ' ', '-', '_', '/', '.' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            foreach (var w in words)
            {
                if (IsModifierToken(w, ModifierTokenPattern)) continue;
                result.Add(NormalizeWord(w));
            }
            return result;
        }

        /// <summary>原文分词并规范化小写（去撇号、保留复数形态）。</summary>
        private static List<string> TokenizeNormalized(string text)
        {
            var words = text.Split(new[] { ' ', '-', '_', '/', '.', ',', ':', ';', '!', '?', '"',
                '\'', '(', ')', '[', ']', '{', '}', '<', '>', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>(words.Length);
            foreach (var w in words)
                result.Add(NormalizeWord(w));
            return result;
        }

        /// <summary>规范化：去撇号（直/弯）并转小写。</summary>
        private static string NormalizeWord(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (char ch in word)
            {
                if (ch == '\'' || ch == '\u2019') continue;
                sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        /// <summary>词匹配（复数/所有格宽容）：textWord 与核心词相同，或为其复数/所有格形态。</summary>
        private static bool WordMatches(string textWord, string coreWord)
        {
            if (textWord == coreWord) return true;
            if (textWord.Length == coreWord.Length + 1 &&
                textWord.EndsWith("s", StringComparison.Ordinal) &&
                textWord.AsSpan(0, textWord.Length - 1).SequenceEqual(coreWord))
                return true;
            if (textWord.Length == coreWord.Length + 2 &&
                textWord.EndsWith("es", StringComparison.Ordinal) &&
                textWord.AsSpan(0, textWord.Length - 2).SequenceEqual(coreWord))
                return true;
            return false;
        }

        /// <summary>
        /// Check if text contains a whole-word match for the given term.
        /// Uses cached Regex for performance.
        /// </summary>
        public static bool ContainsWholeWord(string text, string term)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
                return false;
            // 注意：不做 term.Length > text.Length 的预检——宽容匹配下
            // 术语可能因含修饰词（如 "Executor-class"）而比原文长，仍可能语义匹配。

            var regex = GetOrCreateRegex(term);
            return regex.IsMatch(text);
        }
    }
}
