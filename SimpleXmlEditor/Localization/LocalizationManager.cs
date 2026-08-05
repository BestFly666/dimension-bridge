using System;
using System.Collections.Generic;
using System.Globalization;

namespace SimpleXmlEditor.Localization
{
    public static partial class LocalizationManager
    {
        private static Dictionary<string, Dictionary<string, string>> _translations = new();
        private static string _currentLanguage = "zh";

        static LocalizationManager()
        {
            InitializeTranslationsEn();
            InitializeTranslationsZh();
        }

        public static string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                _currentLanguage = value;
                LanguageChanged?.Invoke();
            }
        }

        public static event Action LanguageChanged;

        public static string GetString(string key)
        {
            if (_translations.ContainsKey(_currentLanguage) &&
                _translations[_currentLanguage].ContainsKey(key))
            {
                return _translations[_currentLanguage][key];
            }

            // Fallback to English
            if (_translations.ContainsKey("en") &&
                _translations["en"].ContainsKey(key))
            {
                return _translations["en"][key];
            }

            return key; // Return key if translation not found
        }

        /// <summary>
        /// Get a formatted string (supports {0}, {1}, ... placeholders).
        /// </summary>
        public static string GetString(string key, params object[] args)
        {
            var template = GetString(key);
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        public static List<(string Code, string Name)> GetAvailableLanguages()
        {
            return new List<(string, string)>
            {
                ("en", "English"),
                ("tr", "Türkçe"),
                ("es", "Español"),
                ("fr", "Français"),
                ("de", "Deutsch"),
                ("it", "Italiano"),
                ("pt", "Português"),
                ("ru", "Русский"),
                ("ja", "日本語"),
                ("ko", "한국어"),
                ("zh", "中文"),
                ("ar", "العربية"),
                ("hi", "हिन्दी"),
                ("nl", "Nederlands")
            };
        }

    }
}