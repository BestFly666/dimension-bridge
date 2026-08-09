using System;
using SimpleXmlEditor.Services;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    /// <summary>验证统一 AI 响应解析：code fence 清理、标准 JSON、对象格式、截断检测与三级回退。</summary>
    public class AiResponseParserTests
    {
        [Fact]
        public void StripCodeFence_RemovesMarkdownFence()
        {
            var raw = "```json\n{\"translations\":[]}\n```";
            Assert.Equal("{\"translations\":[]}", AiResponseParser.StripCodeFence(raw));
        }

        [Fact]
        public void ParseTranslations_EmptyResponse_ReturnsEmpty()
        {
            var result = AiResponseParser.ParseTranslations(null, 5);
            Assert.Empty(result);

            result = AiResponseParser.ParseTranslations("", 5);
            Assert.Empty(result);
        }

        [Fact]
        public void ParseTranslations_StandardJsonArray_ReturnsIndexed()
        {
            var response = "{\"translations\":[{\"index\":1,\"translation\":\"甲\"},{\"index\":2,\"translation\":\"乙\"}]}";
            var result = AiResponseParser.ParseTranslations(response, 2);

            Assert.Equal(2, result.Count);
            Assert.Equal("甲", result[1]);
            Assert.Equal("乙", result[2]);
        }

        [Fact]
        public void ParseTranslations_ObjectFormat_ReturnsIndexed()
        {
            var response = "{\"translations\":{\"1\":\"甲\",\"2\":\"乙\"}}";
            var result = AiResponseParser.ParseTranslations(response, 2);

            Assert.Equal(2, result.Count);
            Assert.Equal("甲", result[1]);
            Assert.Equal("乙", result[2]);
        }

        [Fact]
        public void ParseTranslations_IndexOutOfRange_Ignored()
        {
            // index=5 超出 expectedCount=2，应被忽略
            var response = "{\"translations\":[{\"index\":1,\"translation\":\"甲\"},{\"index\":5,\"translation\":\"越界\"}]}";
            var result = AiResponseParser.ParseTranslations(response, 2);

            Assert.Single(result);
            Assert.Equal("甲", result[1]);
        }

        [Fact]
        public void ParseTranslations_TruncatedJson_Throws()
        {
            // 'translations' 数组未闭合 → 视为截断，抛异常交给拆半重试
            var response = "{\"translations\":[{\"index\":1,\"translation\":\"甲\"},";
            Assert.Throws<InvalidOperationException>(() => AiResponseParser.ParseTranslations(response, 5));
        }

        [Fact]
        public void ParseTranslations_FallbackRegex_NumberedQuoted()
        {
            // 非标准 JSON，但符合 "N. \"译文\"" 行格式 → 三级回退策略 2
            var response = "Here are the translations:\n1. \"甲\"\n2. \"乙\"\nDone.";
            var result = AiResponseParser.ParseTranslations(response, 2);

            Assert.Equal(2, result.Count);
            Assert.Equal("甲", result[1]);
            Assert.Equal("乙", result[2]);
        }

        [Fact]
        public void ParseTranslations_FallbackLineByLine()
        {
            // 无引号的行格式 → 三级回退策略 3
            var response = "甲\n乙\n丙";
            var result = AiResponseParser.ParseTranslations(response, 3);

            Assert.Equal(3, result.Count);
            Assert.Equal("甲", result[1]);
            Assert.Equal("丙", result[3]);
        }
    }
}
