using System.Collections.Generic;
using SimpleXmlEditor.Utils;

namespace SimpleXmlEditor.ExpertProfiles
{
    /// <summary>
    /// Represents a domain-specific expert profile for personalized translation.
    /// Contains context instructions and a terminology glossary to guide AI translation.
    /// </summary>
    public class ExpertProfile
    {
        /// <summary>Display name, e.g. "星球大战", "漫威"</summary>
        public string Name { get; set; } = "";

        /// <summary>Short description of the domain</summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Thinking instructions for the AI translator.
        /// Tells the AI how to approach this domain, what tone to use, etc.
        /// </summary>
        public string Context { get; set; } = "";

        /// <summary>
        /// Terminology glossary: source term -> correct target language translation.
        /// E.g., "Jedi" -> "绝地", "Iron Man" -> "钢铁侠"
        /// </summary>
        public Dictionary<string, string> Glossary { get; set; } = new();

        /// <summary>
        /// Builds the full expert context block that gets injected into the translation prompt.
        /// The optional glossary block (already matched against the batch) is appended
        /// as part of the expert knowledge, so terminology guidance travels with the expert.
        /// </summary>
        public string BuildExpertContextBlock(string targetLanguage, string glossary = "")
        {
            if (string.IsNullOrEmpty(Context) && (Glossary == null || Glossary.Count == 0)
                && string.IsNullOrEmpty(glossary))
                return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"\n=== SPECIAL DOMAIN KNOWLEDGE: {Name} ===");
            sb.AppendLine($"You are translating content from the \"{Name}\" domain.");

            if (!string.IsNullOrEmpty(Context))
            {
                // Replace {LANGUAGE} placeholder in expert context too
                var context = Context.Replace("{LANGUAGE}", targetLanguage);
                sb.AppendLine();
                sb.AppendLine(context);
            }

            if (Glossary != null && Glossary.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"IMPORTANT - {Name} Terminology Glossary (preferred translations):");
                foreach (var kvp in Glossary)
                {
                    // 术语转义：防术语值含引号/控制字符时逃逸出提示词结构
                    var safeKey = PromptTextSanitizer.Sanitize(kvp.Key);
                    var safeValue = PromptTextSanitizer.Sanitize(kvp.Value);
                    sb.AppendLine($"  • \"{safeKey}\" → \"{safeValue}\"");
                }
                sb.AppendLine();
                sb.AppendLine("Use these translations by default for terminology consistency. If a term is clearly used with a different meaning in the specific context (figurative use, part of a proper name, or a different sense), translate it naturally instead; when in doubt, prefer the glossary translation.");
            }

            // 术语注入：把对当前批次匹配到的术语并入专家知识块，
            // 术语与专家 Context 一同进入 API，保证术语指导一定生效。
            if (!string.IsNullOrEmpty(glossary))
            {
                sb.AppendLine(glossary);
            }

            sb.AppendLine("=========================================");
            return sb.ToString();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
