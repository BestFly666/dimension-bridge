using SimpleXmlEditor;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    public class StringExtensionsTests
    {
        [Fact]
        public void HasChineseChars_NullOrEmpty_ReturnsFalse()
        {
            string nullStr = null;
            Assert.False(nullStr.HasChineseChars());
            Assert.False("".HasChineseChars());
        }

        [Fact]
        public void HasChineseChars_EnglishText_ReturnsFalse()
        {
            Assert.False("Hello World".HasChineseChars());
            Assert.False("UPGRADE_TECH".HasChineseChars());
        }

        [Fact]
        public void HasChineseChars_ChineseText_ReturnsTrue()
        {
            Assert.True("你好世界".HasChineseChars());
            Assert.True("这是中文测试".HasChineseChars());
        }

        [Fact]
        public void HasChineseChars_MixedText_ReturnsTrue()
        {
            Assert.True("这是English混合文本".HasChineseChars());
        }
    }
}
