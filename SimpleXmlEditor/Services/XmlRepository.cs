using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor.Services
{
    public partial class XmlRepository : IXmlRepository
    {
        public XmlFormat CurrentFormat { get; private set; } = XmlFormat.ExcelSpreadsheet;

        public event Action<string> LogMessage;

        private void RaiseLog(string message)
        {
            LogMessage?.Invoke(message);
        }

        public List<LocalizationEntry> LoadXml(string fileName, bool isTranslationFile = false)
        {
            var entries = new List<LocalizationEntry>();

            try
            {
                if (!File.Exists(fileName))
                {
                    RaiseLog(LocalizationManager.GetString("LogFileNotFound", fileName));
                    return entries;
                }

                // Security: Use XmlReaderSettings to disable DTD processing and external entities (XXE protection)
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    IgnoreComments = true
                };

                using (var reader = XmlReader.Create(fileName, settings))
                {
                    var doc = XDocument.Load(reader);
                    var root = doc.Root;
                    if (root == null) return entries;

                    if (root.Name.LocalName == "LocalisationData")
                    {
                        CurrentFormat = XmlFormat.LocalisationData;
                        entries = ParseLocalisationDataXml(doc, isTranslationFile);
                    }
                    else if (doc.Descendants(XNamespace.Get("urn:schemas-microsoft-com:office:spreadsheet") + "Row").Any())
                    {
                        CurrentFormat = XmlFormat.ExcelSpreadsheet;
                        entries = ParseExcelSpreadsheetXml(doc);
                    }
                    else
                    {
                        // 无法识别的 XML 方言：明确报错，避免静默按 Excel 解析出空条目
                        var msg = LocalizationManager.GetString("UnsupportedXmlFormat", root.Name.LocalName);
                        RaiseLog(msg);
                        throw new InvalidDataException(msg);
                    }
                }

                return entries;
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("ErrorLoadingXml", ex.Message));
                throw;
            }
        }

        private List<LocalizationEntry> ParseLocalisationDataXml(XDocument doc, bool isTranslationFile)
        {
            var entries = new List<LocalizationEntry>();
            int rowNumber = 0;

            foreach (var localisation in doc.Descendants("Localisation"))
            {
                var key = localisation.Attribute("Key")?.Value ?? "";
                var translationElem = localisation.Descendants("Translation").FirstOrDefault();
                var value = translationElem?.Value ?? "";

                if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(value))
                    continue;

                rowNumber++;
                entries.Add(new LocalizationEntry
                {
                    RowNumber = rowNumber,
                    Key = key,
                    Value = value,
                    Translation = isTranslationFile ? value : "",
                    IsSelected = false
                });
            }

            return entries;
        }

        private List<LocalizationEntry> ParseExcelSpreadsheetXml(XDocument doc)
        {
            var entries = new List<LocalizationEntry>();
            var ns = XNamespace.Get("urn:schemas-microsoft-com:office:spreadsheet");
            var rows = doc.Descendants(ns + "Row");
            int rowNumber = 0;

            foreach (var row in rows)
            {
                var cells = row.Elements(ns + "Cell").ToList();
                if (cells.Count >= 2)
                {
                    rowNumber++;
                    var keyData = cells[0].Element(ns + "Data");
                    var valueData = cells[1].Element(ns + "Data");
                    var translationData = cells.Count >= 3 ? cells[2].Element(ns + "Data") : null;

                    var key = keyData?.Value ?? "";
                    var value = valueData?.Value ?? "";
                    var savedTranslation = translationData?.Value ?? "";

                    entries.Add(new LocalizationEntry
                    {
                        RowNumber = rowNumber,
                        Key = key,
                        Value = value,
                        Translation = savedTranslation,
                        IsSelected = false
                    });
                }
            }

            return entries;
        }

        public void SaveXml(string fileName, List<LocalizationEntry> entries)
        {
            try
            {
                if (CurrentFormat == XmlFormat.LocalisationData)
                {
                    SaveLocalisationDataXml(fileName, entries);
                }
                else
                {
                    SaveExcelSpreadsheetXml(fileName, entries);
                }
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("ErrorSavingXml", ex.Message));
                throw;
            }
        }

        private void SaveLocalisationDataXml(string fileName, List<LocalizationEntry> entries)
        {
            var xsiNs = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");
            var xsdNs = XNamespace.Get("http://www.w3.org/2001/XMLSchema");

            var root = new XElement("LocalisationData",
                new XAttribute(XNamespace.Xmlns + "xsi", xsiNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xsd", xsdNs.NamespaceName)
            );

            foreach (var entry in entries)
            {
                var localisation = new XElement("Localisation",
                    new XAttribute("Key", entry.Key)
                );

                var translationData = new XElement("TranslationData");

                var translation = new XElement("Translation",
                    new XAttribute("Language", "ENGLISH")
                );

                // LocalisationData 格式的特殊设计：<Translation> 元素同时存储原文和译文
                // 译文为空时必须保留原文，否则下次加载时原文会丢失
                var textToWrite = !string.IsNullOrEmpty(entry.Translation) ? entry.Translation : entry.Value;
                translation.Add(new XCData(textToWrite));
                translationData.Add(translation);

                localisation.Add(translationData);
                root.Add(localisation);
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                root
            );

            doc.Save(fileName);
        }

        private void SaveExcelSpreadsheetXml(string fileName, List<LocalizationEntry> entries)
        {
            var ns = XNamespace.Get("urn:schemas-microsoft-com:office:spreadsheet");

            var workbook = new XElement(ns + "Workbook",
                new XAttribute(XNamespace.Xmlns + "ss", ns.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "o", "urn:schemas-microsoft-com:office:office"),
                new XAttribute(XNamespace.Xmlns + "x", "urn:schemas-microsoft-com:office:excel"),
                new XAttribute(XNamespace.Xmlns + "html", "http://www.w3.org/TR/REC-html40")
            );

            var worksheet = new XElement(ns + "Worksheet",
                new XAttribute(ns + "Name", "Metro localization")
            );

            var table = new XElement(ns + "Table");

            table.Add(new XElement(ns + "Column",
                new XAttribute(ns + "AutoFitWidth", "0"),
                new XAttribute(ns + "Width", "480")
            ));

            table.Add(new XElement(ns + "Column",
                new XAttribute(ns + "AutoFitWidth", "0"),
                new XAttribute(ns + "Width", "650")
            ));

            table.Add(new XElement(ns + "Column",
                new XAttribute(ns + "AutoFitWidth", "0"),
                new XAttribute(ns + "Width", "650")
            ));

            foreach (var entry in entries)
            {
                var row = new XElement(ns + "Row");

                var cell1 = new XElement(ns + "Cell",
                    new XElement(ns + "Data",
                        new XAttribute(ns + "Type", "String"),
                        entry.Key
                    )
                );

                var cell2 = new XElement(ns + "Cell",
                    new XElement(ns + "Data",
                        new XAttribute(ns + "Type", "String"),
                        entry.Value
                    )
                );

                var cell3 = new XElement(ns + "Cell",
                    new XElement(ns + "Data",
                        new XAttribute(ns + "Type", "String"),
                        entry.Translation ?? ""
                    )
                );

                row.Add(cell1, cell2, cell3);
                table.Add(row);
            }

            worksheet.Add(table);
            workbook.Add(worksheet);

            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XProcessingInstruction("mso-application", "progid=\"Excel.Sheet\""),
                workbook
            );

            doc.Save(fileName);
        }
    }
}
