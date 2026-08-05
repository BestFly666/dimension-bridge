using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimpleXmlEditor.Plugins;
using SimpleXmlEditor.Services;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    public class TxtFilePluginTests
    {
        static TxtFilePluginTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void Load_KeyValueEquals_ParsesKeyAndValue()
        {
            using var temp = TempTxt("TEXT_SPEECH_1 = Hello\nUNIT_2=World\n");
            var plugin = new TxtFilePlugin();

            var entries = plugin.Load(temp.Path);

            Assert.Equal(2, entries.Count);
            Assert.Equal("TEXT_SPEECH_1", entries[0].Key);
            Assert.Equal("Hello", entries[0].Value);
            Assert.Equal("UNIT_2", entries[1].Key);
            Assert.Equal("World", entries[1].Value);
        }

        [Fact]
        public void Load_ColonSeparator_Parses()
        {
            using var temp = TempTxt("KEY_A: Hello\nKEY_B :World\n");
            var plugin = new TxtFilePlugin();

            var entries = plugin.Load(temp.Path);

            Assert.Equal(2, entries.Count);
            Assert.Equal("KEY_A", entries[0].Key);
            Assert.Equal("Hello", entries[0].Value);
            Assert.Equal("KEY_B", entries[1].Key);
            Assert.Equal("World", entries[1].Value);
        }

        [Fact]
        public void Load_SkipsCommentsAndBlankLines()
        {
            using var temp = TempTxt("# comment\n; another comment\n\nKEY_1 = Hello\n   \n");
            var plugin = new TxtFilePlugin();

            var entries = plugin.Load(temp.Path);

            Assert.Single(entries);
            Assert.Equal("KEY_1", entries[0].Key);
        }

        [Fact]
        public void Load_ValueContainsSeparator_KeepsFullValue()
        {
            using var temp = TempTxt("KEY_1 = A=B\n");
            var plugin = new TxtFilePlugin();

            var entries = plugin.Load(temp.Path);

            Assert.Equal("A=B", entries[0].Value);
        }

        [Fact]
        public void Load_GbkEncoded_ReadsChineseCorrectly()
        {
            var gbk = Encoding.GetEncoding(936);
            var bytes = gbk.GetBytes("TEXT_1 = 你好世界\n");
            var path = Path.Combine(Path.GetTempPath(), $"txtplugin_gbk_{Guid.NewGuid():N}.txt");
            File.WriteAllBytes(path, bytes);

            try
            {
                var plugin = new TxtFilePlugin();
                var entries = plugin.Load(path);
                Assert.Single(entries);
                Assert.Equal("你好世界", entries[0].Value);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Save_PrefersTranslation_FallsBackToValue()
        {
            using var temp = TempTxt("KEY_1 = Hello\nKEY_2 = World\n");
            var plugin = new TxtFilePlugin();
            var entries = plugin.Load(temp.Path);
            entries[0].Translation = "你好";

            var outPath = Path.Combine(Path.GetTempPath(), $"txtplugin_out_{Guid.NewGuid():N}.txt");
            try
            {
                plugin.Save(outPath, entries);
                var text = File.ReadAllText(outPath);
                Assert.Contains("KEY_1 = 你好", text);
                Assert.Contains("KEY_2 = World", text);
            }
            finally
            {
                File.Delete(outPath);
            }
        }

        [Fact]
        public void Save_PreservesGbkEncoding()
        {
            var gbk = Encoding.GetEncoding(936);
            var bytes = gbk.GetBytes("KEY_1 = 你好\n");
            var path = Path.Combine(Path.GetTempPath(), $"txtplugin_gbk_{Guid.NewGuid():N}.txt");
            File.WriteAllBytes(path, bytes);

            var outPath = Path.Combine(Path.GetTempPath(), $"txtplugin_gbkout_{Guid.NewGuid():N}.txt");
            try
            {
                var plugin = new TxtFilePlugin();
                var entries = plugin.Load(path);
                entries[0].Translation = "世界";
                plugin.Save(outPath, entries);

                var savedBytes = File.ReadAllBytes(outPath);
                var expected = gbk.GetBytes($"KEY_1 = 世界{Environment.NewLine}");
                Assert.Equal(expected, savedBytes);
            }
            finally
            {
                File.Delete(path);
                File.Delete(outPath);
            }
        }

        private static TempFile TempTxt(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"txtplugin_{Guid.NewGuid():N}.txt");
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return new TempFile(path);
        }

        private sealed class TempFile : IDisposable
        {
            public string Path { get; }

            public TempFile(string path)
            {
                Path = path;
            }

            public void Dispose()
            {
                if (File.Exists(Path))
                    File.Delete(Path);
            }
        }
    }
}
