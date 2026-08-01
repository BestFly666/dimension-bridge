using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Plugins
{
    /// <summary>
    /// JSON i18n format: flat or nested key-value pairs.
    /// </summary>
    public class JsonI18nPlugin : IFileFormatPlugin
    {
        public string FormatName => "JSON i18n";
        public string[] FileExtensions => new[] { ".json", ".i18n.json" };

        public List<LocalizationEntry> Load(string filePath)
        {
            var text = File.ReadAllText(filePath, Encoding.UTF8);
            var json = JToken.Parse(text);
            var entries = new List<LocalizationEntry>();

            if (json is JObject obj)
            {
                FlattenJson(obj, "", entries);
            }
            else if (json is JArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JObject item)
                        FlattenJson(item, $"[{i}].", entries);
                }
            }

            for (int i = 0; i < entries.Count; i++)
                entries[i].RowNumber = i + 1;

            return entries;
        }

        private void FlattenJson(JObject obj, string prefix, List<LocalizationEntry> entries)
        {
            foreach (var prop in obj.Properties())
            {
                var fullKey = prefix + prop.Name;
                if (prop.Value is JObject child)
                {
                    FlattenJson(child, fullKey + ".", entries);
                }
                else if (prop.Value.Type == JTokenType.String || prop.Value.Type == JTokenType.Integer ||
                         prop.Value.Type == JTokenType.Float || prop.Value.Type == JTokenType.Boolean)
                {
                    entries.Add(new LocalizationEntry
                    {
                        Key = fullKey,
                        Value = prop.Value.ToString(),
                        Translation = ""
                    });
                }
            }
        }

        public void Save(string filePath, List<LocalizationEntry> entries)
        {
            JObject root = new JObject();
            var dict = new SortedDictionary<string, string>();

            foreach (var entry in entries)
            {
                var value = string.IsNullOrEmpty(entry.Translation) ? entry.Value : entry.Translation;
                dict[entry.Key] = value;
            }

            foreach (var kvp in dict)
            {
                SetNestedValue(root, kvp.Key, kvp.Value);
            }

            var json = root.ToString(Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        private void SetNestedValue(JObject parent, string key, string value)
        {
            var parts = key.Split('.');
            var current = parent;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (current[parts[i]] is JObject child)
                {
                    current = child;
                }
                else
                {
                    var newChild = new JObject();
                    current[parts[i]] = newChild;
                    current = newChild;
                }
            }
            current[parts[^1]] = value;
        }
    }
}
