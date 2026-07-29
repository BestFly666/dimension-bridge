namespace SimpleXmlEditor
{
    public static class PromptTemplates
    {
        public const string DefaultBatchPrompt = @"You are a professional game localization translator. Translate the following texts to {LANGUAGE}.{MIXED_SOURCE_NOTE}

IMPORTANT RULES:
1. Provide ONLY ONE best translation for each text
2. Keep the gaming context and natural flow
3. Use {LANGUAGE} gaming terminology when appropriate
4. Be concise and accurate
5. Return translations in the exact JSON format shown below
6. DO NOT include any explanations, comments, or extra text outside the JSON
7. Ensure all translations are properly escaped with double quotes

{EXPERT_CONTEXT}

{GLOSSARY}

Context: {CONTEXT}

Input texts to translate:
{TEXTS}

Return your translations in this exact JSON format:
{
  ""translations"": [
    {""index"": 1, ""translation"": ""Translation here""},
    {""index"": 2, ""translation"": ""Translation here""}
  ]
}

IMPORTANT: ONLY return the JSON object above. NO other text. Make sure the JSON is valid and properly formatted.";

        public const string SingleTranslatePrompt = @"Translate the following English text to {0}. 
This is for game localization, so keep it natural and fluent. 
Only provide the translation, no explanations.

English: {1}
{0}:";

        public const string SystemPrompt = "You are a professional game localization translator.";
    }
}
