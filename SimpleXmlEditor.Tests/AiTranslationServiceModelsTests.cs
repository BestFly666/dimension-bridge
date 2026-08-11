using System.Collections.Generic;
using SimpleXmlEditor.Services;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    /// <summary>验证 Gemini 模型列表排序：3.x 优先于 2.x，旧系列与 gemma 靠后，类内倒序（新版本在前）。</summary>
    public class AiTranslationServiceModelsTests
    {
        [Fact]
        public void SortGeminiModels_MixedInput_OrdersByVersionThenDescending()
        {
            var input = new List<string>
            {
                "gemini-2.0-flash",
                "gemma-2-9b-it",
                "gemini-3-pro-preview",
                "gemini-2.5-flash",
                "gemini-pro",
                "gemini-1.5-flash"
            };

            var sorted = AiTranslationService.SortGeminiModels(input);

            // ① gemini-3 优先
            Assert.Equal("gemini-3-pro-preview", sorted[0]);
            // ② gemini-2 其次，类内倒序（2.5 在 2.0 前）
            Assert.Equal("gemini-2.5-flash", sorted[1]);
            Assert.Equal("gemini-2.0-flash", sorted[2]);
            // ③ 无版本号旧系列（gemini-pro / gemini-1.x）在后
            Assert.Equal("gemini-pro", sorted[3]);
            Assert.Equal("gemini-1.5-flash", sorted[4]);
            // ④ 其他对话模型（gemma-*）最后
            Assert.Equal("gemma-2-9b-it", sorted[5]);

            Assert.Equal(input.Count, sorted.Count);
        }

        [Fact]
        public void SortGeminiModels_EmptyInput_ReturnsEmptyList()
        {
            var sorted = AiTranslationService.SortGeminiModels(new List<string>());
            Assert.Empty(sorted);
        }

        [Fact]
        public void SortGeminiModels_NullInput_ReturnsEmptyList()
        {
            var sorted = AiTranslationService.SortGeminiModels(null);
            Assert.Empty(sorted);
        }

        [Fact]
        public void SortGeminiModels_NewerVersionWithinCategory_ComesFirst()
        {
            // gemini-3-flash 与 gemini-3-pro：同一类别内按名称倒序，flash < pro 字典序（f < p），故 pro 在前
            var input = new List<string> { "gemini-3-flash-preview", "gemini-3-pro-preview" };
            var sorted = AiTranslationService.SortGeminiModels(input);

            Assert.Equal("gemini-3-pro-preview", sorted[0]);
            Assert.Equal("gemini-3-flash-preview", sorted[1]);
        }
    }
}
