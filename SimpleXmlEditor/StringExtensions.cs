using System;

namespace SimpleXmlEditor
{
    public static class StringExtensions
    {
        public static bool HasChineseChars(this string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) return true;
            }
            return false;
        }
    }
}
