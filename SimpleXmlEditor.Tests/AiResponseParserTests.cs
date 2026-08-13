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

        // ─── CleanTranslationEcho（AI 回显污染清洗） ──────────────

        [Fact]
        public void CleanTranslationEcho_WrappedKey_ExtractsQuotedContent()
        {
            // 用户实测案例：模型把 [KEY] "译文" 连同 KEY 与英文原文一起回显
            var polluted = "[TEXT_UPGRADE_RL_HEAVY_ARMOR_L2] \"复合材料装甲板 II\"TEXT_UPGRADE_RL_HEAVY_ARMOR_L2  Composite Armor Plates II";
            var cleaned = AiResponseParser.CleanTranslationEcho(
                polluted, "TEXT_UPGRADE_RL_HEAVY_ARMOR_L2", "Composite Armor Plates II");

            Assert.Equal("复合材料装甲板 II", cleaned);
        }

        [Fact]
        public void CleanTranslationEcho_BareKeyAndOriginal_RemovesBoth()
        {
            // 无 [KEY] 包装，但含裸 KEY 与末尾英文原文
            var polluted = "复合材料装甲板 II TEXT_UPGRADE_RL_HEAVY_ARMOR_L2 Composite Armor Plates II";
            var cleaned = AiResponseParser.CleanTranslationEcho(
                polluted, "TEXT_UPGRADE_RL_HEAVY_ARMOR_L2", "Composite Armor Plates II");

            Assert.Equal("复合材料装甲板 II", cleaned);
        }

        [Fact]
        public void CleanTranslationEcho_NoKey_Untouched()
        {
            // 合法译文（含英文原名）不含 KEY → 不清洗，防止误伤
            var legit = "复合材料装甲板 II (Composite Armor Plates II)";
            var cleaned = AiResponseParser.CleanTranslationEcho(
                legit, "TEXT_UPGRADE_RL_HEAVY_ARMOR_L2", "Composite Armor Plates II");

            Assert.Equal(legit, cleaned);
        }

        [Fact]
        public void CleanTranslationEcho_OnlyKey_ReturnsEmpty()
        {
            // 整条都是 KEY 回显 → 清洗后为空，调用方按"缺失"处理
            var cleaned = AiResponseParser.CleanTranslationEcho(
                "TEXT_UPGRADE_RL_HEAVY_ARMOR_L2", "TEXT_UPGRADE_RL_HEAVY_ARMOR_L2", "Composite Armor Plates II");

            Assert.Equal(string.Empty, cleaned);
        }

        [Fact]
        public void CleanTranslationEcho_NullOrEmpty_ReturnsAsIs()
        {
            Assert.Null(AiResponseParser.CleanTranslationEcho(null, "K", "V"));
            Assert.Equal(string.Empty, AiResponseParser.CleanTranslationEcho("", "K", "V"));
        }
    }
}
