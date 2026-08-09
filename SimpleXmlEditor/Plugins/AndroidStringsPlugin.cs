using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Plugins
{
    /// <summary>
    /// Android strings.xml format:
    /// <resources><string name="key">value</string></resources>
    /// </summary>
    public class AndroidStringsPlugin : IFileFormatPlugin
    {
        public string FormatName => "Android strings.xml";
        public string[] FileExtensions => new[] { ".android.xml" };

        public List<LocalizationEntry> Load(string filePath)
        {
            // 安全：XDocument.Load 在 .NET Core 3.0+ 默认禁止 DTD/外部实体（DtdProcessing.Prohibit），防 XXE；
            // 与 XmlRepository 的显式 XmlReaderSettings 防护一致（纵深防御）
            var doc = XDocument.Load(filePath);
            if (doc.Root?.Name.LocalName != "resources")
                return new List<LocalizationEntry>(); // Not an Android file

            var entries = new List<LocalizationEntry>();
            var index = 0;

            foreach (var el in doc.Root.Elements("string"))
            {
                var name = el.Attribute("name")?.Value ?? "";
                var value = el.Value ?? "";
                index++;
                entries.Add(new LocalizationEntry
                {
                    RowNumber = index,
                    Key = name,
                    Value = value,
                    Translation = ""
                });
            }

            // Also handle string-array items
            foreach (var array in doc.Root.Elements("string-array"))
            {
                var arrayName = array.Attribute("name")?.Value ?? "";
                var itemIdx = 0;
                foreach (var item in array.Elements("item"))
                {
                    var value = item.Value ?? "";
                    index++;
                    itemIdx++;
                    entries.Add(new LocalizationEntry
                    {
                        RowNumber = index,
                        Key = $"{arrayName}[{itemIdx}]",
                        Value = value,
                        Translation = ""
                    });
                }
            }

            return entries;
        }

        public void Save(string filePath, List<LocalizationEntry> entries)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
                new XElement("resources"));

            // Group entries: string-array items go back to their arrays
            var stringItems = new List<LocalizationEntry>();
            var arrayGroups = new Dictionary<string, List<string>>();

            foreach (var entry in entries)
            {
                if (entry.Key.Contains("["))
                {
                    var bracketIdx = entry.Key.IndexOf('[');
                    var arrayName = entry.Key[..bracketIdx];
                    if (!arrayGroups.ContainsKey(arrayName))
                        arrayGroups[arrayName] = new List<string>();
                    // Use the translation if available, otherwise original
                    arrayGroups[arrayName].Add(string.IsNullOrEmpty(entry.Translation) ? entry.Value : entry.Translation);
                }
                else
                {
                    stringItems.Add(entry);
                }
            }

            foreach (var entry in stringItems)
            {
                var el = new XElement("string",
                    new XAttribute("name", entry.Key),
                    string.IsNullOrEmpty(entry.Translation) ? entry.Value : entry.Translation);
                doc.Root!.Add(el);
            }

            foreach (var kvp in arrayGroups)
            {
                var arrayEl = new XElement("string-array", new XAttribute("name", kvp.Key));
                foreach (var item in kvp.Value)
                {
                    arrayEl.Add(new XElement("item", item));
                }
                doc.Root!.Add(arrayEl);
            }

            var settings = new System.Xml.XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                OmitXmlDeclaration = false
            };

            using var writer = System.Xml.XmlWriter.Create(filePath, settings);
            doc.Save(writer);
        }
    }
}
