using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleXmlEditor.Utils;

namespace SimpleXmlEditor.Services
{
    public partial class TranslationEvaluator
    {
        /// <summary>净化提示词插值文本（转义引号/控制字符，防注入）。</summary>
        private static string Safe(string text) => PromptTextSanitizer.Sanitize(text);

        /// <summary>净化可空 Context（空值时用通用游戏 UI 文案兜底）。</summary>
        private static string SafeContext(string context)
            => string.IsNullOrEmpty(context) ? "General gaming UI text" : Safe(context);

        private string BuildBatchedVotingPrompt(string original, string[] candidates, string targetLang, string context)
        {
            var candidateList = string.Join("\n", candidates.Select((c, i) => $"{i + 1}. \"{Safe(c)}\""));
            return $@"You are a multi-agent translation review panel. Evaluate translation candidates from 3 perspectives.

Original ({targetLang}): ""{Safe(original)}""

Context: {SafeContext(context)}

Candidates:
{candidateList}

For EACH candidate, evaluate from ALL 3 perspectives:
- Fluency: naturalness, flow, readability
- Accuracy: whether meaning is preserved exactly
- Style: tone, register, gaming context fit

Return JSON with ALL evaluations:
{{
  ""evaluations"": [
    {{
      ""candidate"": 1,
      ""agent"": ""Fluency"",
      ""score"": 9.0,
      ""explanation"": ""Brief reason""
    }},
    {{
      ""candidate"": 1,
      ""agent"": ""Accuracy"",
      ""score"": 8.5,
      ""explanation"": ""Brief reason""
    }},
    {{
      ""candidate"": 1,
      ""agent"": ""Style"",
      ""score"": 9.0,
      ""explanation"": ""Brief reason""
    }},
    {{
      ""candidate"": 2,
      ""agent"": ""Fluency"",
      ""score"": 8.0,
      ""explanation"": ""Brief reason""
    }},
    ...
  ]
}}

You MUST include all {candidates.Length} candidates × 3 agents = {candidates.Length * 3} evaluations.
Only return the JSON, no other text.";
        }

        private string BuildEvaluationPrompt(string original, string translated, string targetLang, string context)
        {
            return $@"You are a professional game localization quality evaluator. Evaluate the following translation.

Original ({targetLang}): {Safe(original)}

Translation: {Safe(translated)}

Context: {SafeContext(context)}

Rate the translation on a 0-10 scale and provide:
1. Score (0-10, where 10 is perfect)
2. Brief explanation of the rating
3. Suggested improvement (if score < 8)

Return in this exact JSON format:
{{
  ""score"": 8.5,
  ""explanation"": ""Brief explanation of strengths and weaknesses"",
  ""improvement"": ""Better translation suggestion here""
}}

Only return the JSON, no other text.";
        }

        private string BuildCandidatePrompt(string original, string targetLang, string context, int count)
        {
            return $@"You are a professional game localization translator. Translate the following English text to {targetLang} and generate {count} DIFFERENT translation candidates.

Original (English): {Safe(original)}
Target language: {targetLang}

Context: {SafeContext(context)}

All candidates MUST be in {targetLang}, NOT in English. The candidates should differ in wording/style but ALL preserve the exact meaning and fit gaming UI tone.

Return in this exact JSON format:
{{
  ""candidates"": [
    ""first candidate translation in {targetLang}"",
    ""second candidate translation in {targetLang}""
  ]
}}

Only return the JSON, no other text.";
        }

        private string BuildBatchEvaluationPrompt(List<(string Key, string Original, string Translated)> items, string targetLang, string context)
        {
            var lines = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                lines.AppendLine($"### Entry {i + 1}");
                lines.AppendLine($"Original ({targetLang}): {Safe(items[i].Original)}");
                lines.AppendLine($"Translation: {Safe(items[i].Translated)}");
                lines.AppendLine();
            }

            return $@"You are a professional game localization quality evaluator. Evaluate ALL {items.Count} translations below.

Context: {SafeContext(context)}

{lines}

For EACH entry, rate 0-10 and provide brief explanation + improvement (if score < 8).

Return in this exact JSON format:
{{
  ""evaluations"": [
    {{ ""index"": 1, ""score"": 8.5, ""explanation"": ""brief reason"", ""improvement"": ""suggestion or empty"" }},
    {{ ""index"": 2, ""score"": 6.0, ""explanation"": ""brief reason"", ""improvement"": ""suggestion or empty"" }}
  ]
}}

Include ALL {items.Count} entries. Only return the JSON, no other text.";
        }

        private string BuildBatchVotingPrompt(List<(string Key, string Original, string[] Candidates)> items, string targetLang, string context)
        {
            var lines = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                lines.AppendLine($"### Entry {i + 1}");
                lines.AppendLine($"Original (English): {Safe(items[i].Original)}");
                lines.AppendLine($"Target language: {targetLang}");
                for (int c = 0; c < items[i].Candidates.Length; c++)
                    lines.AppendLine($"  Candidate {c + 1}: \"{Safe(items[i].Candidates[c])}\"");
                lines.AppendLine();
            }

            return $@"You are a multi-agent translation review panel. For EACH entry below, evaluate its candidates from 3 perspectives (Fluency, Accuracy, Style), then pick the BEST candidate. All candidates are {targetLang} translations of the English original.

Context: {SafeContext(context)}

{lines}

Return in this exact JSON format:
{{
  ""votes"": [
    {{
      ""index"": 1,
      ""scores"": [ {{ ""candidate"": 1, ""agent"": ""Fluency"", ""score"": 9.0, ""explanation"": ""brief"" }}, {{ ""candidate"": 1, ""agent"": ""Accuracy"", ""score"": 8.0 }} ],
      ""best"": 1
    }}
  ]
}}

Include ALL {items.Count} entries. Only return the JSON, no other text.";
        }
    }
}
