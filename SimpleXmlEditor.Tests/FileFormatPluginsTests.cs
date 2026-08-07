using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SimpleXmlEditor.Plugins;
using SimpleXmlEditor.Services;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    /// <summary>
    /// 文件格式插件测试：CSV / INI / YAML / RESX / PROPERTIES 的加载、保存往返与编码检测。
    /// </summary>
    public class FileFormatPluginsTests
    {
        private static string WriteTemp(string content, Encoding encoding)
        {
            var path = Path.Combine(Path.GetTempPath(), $"i18n_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(path, content, encoding);
            return path;
        }

        private static void DeleteTemp(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* 测试清理失败忽略 */ }
        }

        // ─── CSV ────────────────────────────────────────────────

        [Fact]
        public void Csv_ThreeColumn_LoadAndSave_RoundTrips()
        {
            var plugin = new CsvFilePlugin();
            var path = WriteTemp("Key,Original,Translation\nSTORM,Stormtrooper,暴风兵\nTIE,TIE Fighter,\n", new UTF8Encoding(true));

            try
            {
                var entries = plugin.Load(path);
                Assert.Equal(2, entries.Count);
                Assert.Equal("STORM", entries[0].Key);
                Assert.Equal("Stormtrooper", entries[0].Value);
                Assert.Equal("暴风兵", entries[0].Translation);
                Assert.Equal("", entries[1].Translation); // 空译文

                entries[1].Translation = "钛战机";
                var savePath = Path.Combine(Path.GetTempPath(), $"i18n_save_{Guid.NewGuid():N}.csv");
                plugin.Save(savePath, entries);

                var reloaded = new CsvFilePlugin().Load(savePath);
                Assert.Equal(2, reloaded.Count);
                // 三列模型：译文写入 Translation 列
                Assert.Equal("钛战机", reloaded[1].Translation);
                Assert.Equal("暴风兵", reloaded[0].Translation);
                DeleteTemp(savePath);
            }
            finally
            {
                DeleteTemp(path);
            }
        }

        [Fact]
        public void Csv_QuotedFields_HandlesCommaAndQuotes()
        {
            var plugin = new CsvFilePlugin();
            var path = WriteTemp("Key,Original\nA,\"Hello, world\"\nB,\"Say \"\"hi\"\"\"\n", new UTF8Encoding(true));

            try
            {
                var entries = plugin.Load(path);
                Assert.Equal(2, entries.Count);
                Assert.Equal("Hello, world", entries[0].Value);
                Assert.Equal("Say \"hi\"", entries[1].Value);
            }
            finally
            {
                DeleteTemp(path);
            }
        }

        [Fact]
        public void Csv_TwoColumn_NoHeader_LoadsKeyValue()
        {
            var plugin = new CsvFilePlugin();
            var path = WriteTemp("Alpha,Alpha text\nBeta,Beta text\n", new UTF8Encoding(true));

            try
            {
                var entries = plugin.Load(path);
                Assert.Equal(2, entries.Count);
                Assert.Equal("Alpha", entries[0].Key);
                Assert.Equal("Alpha text", entries[0].Value);
                Assert.Equal("", entries[0].Translation);
            }
            finally
            {
                DeleteTemp(path);
            }
        }

        [Fact]
        public void Csv_GbkEncoding_DetectedAndPreserved()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var gbk = Encoding.GetEncoding(936);
            var path = WriteTemp("Key,Original,Translation\nK1,你好世界,\n", gbk);

            try
            {
                var plugin = new CsvFilePlugin();
                var entries = plugin.Load(path);
                Assert.Equal("你好世界", entries[0].Value);

                entries[0].Translation = "Hello World";
                var savePath = Path.Combine(Path.GetTempPath(), $"i18n_save_{Guid.NewGuid():N}.csv");
                plugin.Save(savePath, entries);

                // 保存后应仍为 GBK（可被 gbk 解码且 utf8 严格解码失败）
                var bytes = File.ReadAllBytes(savePath);
                Assert.Throws<DecoderFallbackException>(() => new UTF8Encoding(false, true).GetString(bytes));
                Assert.Equal("Hello World", new CsvFilePlugin().Load(savePath)[0].Translation);
                DeleteTemp(savePath);
            }
            finally
            {
                DeleteTemp(path);
            }
        }

        // ─── INI ────────────────────────────────────────────────

        [Fact]
        public void Ini_SectionKey_LoadAndSave_RoundTrips()
        {
            var plugin = new IniFilePlugin();
            var path = WriteTemp("; comment\n[General]\nName=Empire\n# hash comment\n[Fleet]\nShips=12\n", new UTF8Encoding(true));

            try
            {
                var entries = plugin.Load(path);
                Assert.Equal(2, entries.Count);
                Assert.Equal("[General]Name", entries[0].Key);
                Assert.Equal("Empire", entries[0].Value);
                Assert.Equal("[Fleet]Ships", entries[1].Key);

                entries[1].Translation = "舰队";
                var savePath = Path.Combine(Path.GetTempPath(), $"i18n_save_{Guid.NewGuid():N}.ini");
                plugin.Save(savePath, entries);

                var text = File.ReadAllText(savePath, new UTF8Encoding(true));
                Assert.Contains("[General]\r\nName=Empire", text.Replace("\r\n", "\r\n"));
                Assert.Contains("[Fleet]", text);
                Assert.Contains("Ships=舰队", text);
                DeleteTemp(savePath);
            }
            finally
            {
                DeleteTemp(path);
            }
        }

        [Fact]
        public void Ini_NoSection_KeyStoredAsIs()
        {
            var plugin = new IniFilePlugin();
            var path = WriteTemp("Volume=5\nMusic=on\n", new UTF8Encoding(true));

            try
            {
                var entries = plugin.Load(path);
                Assert.Equal(2, entries.Count);
                Assert.Equal("Volume", entries[0].Key);
                Assert.Equal("5", entries[0].Value);
            }
            finally
            {
                DeleteTemp(path);
            }
        }

        // ─── YAML ───────────────────────────────────────────────

        [Fact]
        public void Yaml_FlatAndNested_LoadAndSave_RoundTrips()
        {
            var plugin = new YamlFilePlugin();
            var path = WriteTemp("title: Empire\nintro:\n  line1: First line\n  line2: Second line\n", new UTF8Encoding(true));

            try
            {
                var entries = plugin.Load(path);
                Assert.Equal(3, entries.Count);
                Assert.Equal("title", entries[0].Key);
                Assert.Equal("Empire", entries[0].Value);
                Assert.Equal("intro.line1", entries[1].Key);
                Assert.Equal("First line", entries[1].Value);

                entries[1].Translation = "第一行";
                var savePath = Path.Combine(Path.GetTempPath(), $"i18n_save_{Guid.NewGuid():N}.yaml");
                plugin.Save(savePath, entries);

                var reloaded = new YamlFilePlugin().Load(savePath);
                // 两列模型：译文写入 Value（导出替换原值约定）
                Assert.Equal("第一行", reloaded.Single(e => e.Key == "intro.line1").Value);
                Assert.Equal("Second line", reloaded.Single(e => e.Key == "intro.line2").Value);
                DeleteTemp(savePath);
            }
            finally
            {
                DeleteTemp(path);
            }
        }

        // ─── RESX ───────────────────────────────────────────────

        [Fact]
        public void Resx_LoadAndSave_RoundTrips()
        {
            var plugin = new ResxFilePlugin();
            var path = WriteTemp(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>" +
                "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>" +
                "<data name=\"Menu.File\"><value>文件</value></data>" +
                "<data name=\"Menu.Open\"><value>打开</value></data>" +
                "</root>", new UTF8Encoding(false));

            try
            {
                var entries = plugin.Load(path);
                Assert.Equal(2, entries.Count);
                Assert.Equal("Menu.File", entries[0].Key);
                Assert.Equal("文件", entries[0].Value);

                entries[0].Translation = "File";
                var savePath = Path.Combine(Path.GetTempPath(), $"i18n_save_{Guid.NewGuid():N}.resx");
                plugin.Save(savePath, entries);

                var reloaded = new ResxFilePlugin().Load(savePath);
                Assert.Equal(2, reloaded.Count);
                // 两列模型：译文写入 Value（导出替换原值约定）
                Assert.Equal("File", reloaded.Single(e => e.Key == "Menu.File").Value);
                Assert.Equal("打开", reloaded.Single(e => e.Key == "Menu.Open").Value);
                DeleteTemp(savePath);
            }
            finally
            {
                DeleteTemp(path);
            }
        }

        // ─── PROPERTIES ─────────────────────────────────────────

        [Fact]
        public void Properties_LoadUnescapes_AndSaveRoundTrips()
        {
            var plugin = new PropertiesFilePlugin();
            var path = WriteTemp(
                "# comment\n! another\nkey1=value1\nkey2=line\\nbreak\nkey\\:colon=has colon\nunicode=\\u4F60\\u597D\n",
                new UTF8Encoding(true));

            try
            {
                var entries = plugin.Load(path);
                Assert.Equal(4, entries.Count);
                Assert.Equal("value1", entries[0].Value);
                Assert.Equal("line\nbreak", entries[1].Value);
                Assert.Equal("key:colon", entries[2].Key);
                Assert.Equal("has colon", entries[2].Value);
                Assert.Equal("你好", entries[3].Value);

                entries[0].Translation = "译文一";
                var savePath = Path.Combine(Path.GetTempPath(), $"i18n_save_{Guid.NewGuid():N}.properties");
                plugin.Save(savePath, entries);

                var reloaded = new PropertiesFilePlugin().Load(savePath);
                Assert.Equal(4, reloaded.Count);
                // 两列模型：译文写入 Value（导出替换原值约定）
                Assert.Equal("译文一", reloaded.Single(e => e.Key == "key1").Value);
                Assert.Equal("line\nbreak", reloaded.Single(e => e.Key == "key2").Value);
                Assert.Equal("你好", reloaded.Single(e => e.Key == "unicode").Value);
                DeleteTemp(savePath);
            }
            finally
            {
                DeleteTemp(path);
            }
        }

        // ─── 编码检测 ───────────────────────────────────────────

        [Fact]
        public void EncodingDetector_DetectsUtf8BomUtf8AndGbk()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var bom = WriteTemp("测试", new UTF8Encoding(true));
            var utf8 = WriteTemp("测试", new UTF8Encoding(false));
            var gbk = WriteTemp("测试", Encoding.GetEncoding(936));

            try
            {
                Assert.True(TextEncodingDetector.Detect(bom).GetPreamble().Length > 0);
                Assert.False(TextEncodingDetector.Detect(utf8).GetPreamble().Length > 0);
                Assert.Equal(936, TextEncodingDetector.Detect(gbk).CodePage);
            }
            finally
            {
                DeleteTemp(bom);
                DeleteTemp(utf8);
                DeleteTemp(gbk);
            }
        }
    }
}
