using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Plugins
{
    /// <summary>
    /// RESX 支持（.NET 资源文件，WinForms/WPF 项目本地化标配）。
    /// 格式：&lt;root&gt; 下的 &lt;data name="key"&gt;&lt;value&gt;text&lt;/value&gt;&lt;/data&gt;。
    /// 行为：UTF-8 读写；保存时输出标准 resheader（resmimetype/version/reader/writer）保证资源编译器可用；
    ///       译文为空回退原文，与工具"导出替换原值"约定一致。
    /// 安全：XDocument.Load 默认禁止 DTD/外部实体（防 XXE），符合项目硬约束。
    /// Non-Goals（V1）：<data> 的 comment/type 等子元素不保留，仅保留 name/value。
    /// </summary>
    public class ResxFilePlugin : IFileFormatPlugin
    {
        public string FormatName => "RESX";
        public string[] FileExtensions => new[] { ".resx" };

        public List<LocalizationEntry> Load(string filePath)
        {
            var doc = XDocument.Load(filePath, LoadOptions.None);
            var entries = new List<LocalizationEntry>();
            int rowNumber = 0;

            if (doc.Root == null) return entries;

            foreach (var data in doc.Root.Elements("data"))
            {
                var name = (string)data.Attribute("name");
                if (string.IsNullOrEmpty(name)) continue;

                var value = data.Element("value")?.Value ?? "";
                rowNumber++;
                entries.Add(new LocalizationEntry
                {
                    RowNumber = rowNumber,
                    Key = name,
                    Value = value,
                    Translation = "",
                    IsSelected = false
                });
            }
            return entries;
        }

        public void Save(string filePath, List<LocalizationEntry> entries)
        {
            var root = new XElement("root",
                new XElement("resheader", new XAttribute("name", "resmimetype"),
                    new XElement("value", "text/microsoft-resx")),
                new XElement("resheader", new XAttribute("name", "version"),
                    new XElement("value", "2.0")),
                new XElement("resheader", new XAttribute("name", "reader"),
                    new XElement("value",
                        "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
                new XElement("resheader", new XAttribute("name", "writer"),
                    new XElement("value",
                        "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")));

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                var text = string.IsNullOrEmpty(entry.Translation) ? entry.Value : entry.Translation;
                root.Add(new XElement("data", new XAttribute("name", entry.Key),
                    new XElement("value", text)));
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            var settings = new System.Xml.XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            };
            using var writer = System.Xml.XmlWriter.Create(filePath, settings);
            doc.Save(writer);
        }
    }
}
