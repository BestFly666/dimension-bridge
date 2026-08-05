using System.Collections.Generic;

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
        /// </summary>
        public string BuildExpertContextBlock(string targetLanguage)
        {
            if (string.IsNullOrEmpty(Context) && (Glossary == null || Glossary.Count == 0))
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
                    sb.AppendLine($"  • \"{kvp.Key}\" → \"{kvp.Value}\"");
                }
                sb.AppendLine();
                sb.AppendLine("Use these translations by default for terminology consistency. If a term is clearly used with a different meaning in the specific context (figurative use, part of a proper name, or a different sense), translate it naturally instead; when in doubt, prefer the glossary translation.");
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
