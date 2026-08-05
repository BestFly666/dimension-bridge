using System;
using System.IO;
using System.Text;
using SimpleXmlEditor.Services;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    public class XmlRepositoryTests
    {
        [Fact]
        public void LoadXml_LocalisationDataRoot_SetsFormat()
        {
            var xml = @"<LocalisationData>
  <Localisation Key=""TEXT_SPEECH_001"">
    <Translation>Hello</Translation>
  </Localisation>
</LocalisationData>";

            using var temp = TempXml(xml);
            var repo = new XmlRepository();

            var entries = repo.LoadXml(temp.Path);

            Assert.Equal(XmlFormat.LocalisationData, repo.CurrentFormat);
            Assert.Single(entries);
            Assert.Equal("TEXT_SPEECH_001", entries[0].Key);
            Assert.Equal("Hello", entries[0].Value);
        }

        [Fact]
        public void LoadXml_ExcelSpreadsheet_SetsFormat()
        {
            var xml = @"<?xml version=""1.0""?>
<Workbook xmlns=""urn:schemas-microsoft-com:office:spreadsheet"">
  <Worksheet ss:Name=""Sheet1"" xmlns:ss=""urn:schemas-microsoft-com:office:spreadsheet"">
    <Table>
      <Row>
        <Cell><Data ss:Type=""String"">KEY_1</Data></Cell>
        <Cell><Data ss:Type=""String"">Hello</Data></Cell>
      </Row>
    </Table>
  </Worksheet>
</Workbook>";

            using var temp = TempXml(xml);
            var repo = new XmlRepository();

            var entries = repo.LoadXml(temp.Path);

            Assert.Equal(XmlFormat.ExcelSpreadsheet, repo.CurrentFormat);
            Assert.Single(entries);
            Assert.Equal("KEY_1", entries[0].Key);
            Assert.Equal("Hello", entries[0].Value);
        }

        [Fact]
        public void LoadXml_UnrecognizedRoot_ThrowsInvalidDataException()
        {
            var xml = @"<UnknownRoot><Data>hello</Data></UnknownRoot>";

            using var temp = TempXml(xml);
            var repo = new XmlRepository();

            var ex = Assert.Throws<InvalidDataException>(() => repo.LoadXml(temp.Path));
            Assert.Contains("UnknownRoot", ex.Message);
        }

        private static TempFile TempXml(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"xmlrepo_test_{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, content, Encoding.UTF8);
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
