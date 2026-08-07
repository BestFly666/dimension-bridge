using System.Collections.Generic;
using System.Linq;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Services;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    public class GlossaryManagerTests
    {
        [Fact]
        public void TryGetValue_ExactMatch_ReturnsTrue()
        {
            var manager = new GlossaryManager();
            manager.SetEntry("Jedi", "绝地");
            
            Assert.True(manager.TryGetValue("Jedi", out var result));
            Assert.Equal("绝地", result);
        }

        [Fact]
        public void TryGetValue_NoMatch_ReturnsFalse()
        {
            var manager = new GlossaryManager();
            Assert.False(manager.TryGetValue("NonExistent", out _));
        }

        [Fact]
        public void SetEntry_UpdatesExistingTerm()
        {
            var manager = new GlossaryManager();
            manager.SetEntry("Test", "测试1");
            manager.SetEntry("Test", "测试2");
            
            Assert.True(manager.TryGetValue("Test", out var result));
            Assert.Equal("测试2", result);
        }

        [Fact]
        public void RemoveEntry_RemovesExistingTerm()
        {
            var manager = new GlossaryManager();
            manager.SetEntry("Test", "测试");
            Assert.True(manager.RemoveEntry("Test"));
            Assert.False(manager.TryGetValue("Test", out _));
        }

        [Fact]
        public void Count_ReflectsActualCount()
        {
            var manager = new GlossaryManager();
            manager.Clear();
            Assert.Equal(0, manager.Count);
            
            manager.SetEntry("A", "甲");
            manager.SetEntry("B", "乙");
            Assert.Equal(2, manager.Count);
            
            manager.Clear();
        }

        [Fact]
        public void ContainsWholeWord_PluralAndUnderscoreVariants_Matches()
        {
            // 复数/所有格/下划线变体应命中
            Assert.True(GlossaryManager.ContainsWholeWord("The Jedis attack", "Jedi"));
            Assert.True(GlossaryManager.ContainsWholeWord("Jedi's lightsaber", "Jedi"));
            Assert.True(GlossaryManager.ContainsWholeWord("dark_jedi", "Jedi"));
            Assert.True(GlossaryManager.ContainsWholeWord("Stormtroopers marching", "Stormtrooper"));
            Assert.True(GlossaryManager.ContainsWholeWord("three boxes", "box"));

            // 词内拼接不命中，避免误伤
            Assert.False(GlossaryManager.ContainsWholeWord("JediMaster", "Jedi"));
            Assert.False(GlossaryManager.ContainsWholeWord("JedisX", "Jedi"));
            Assert.False(GlossaryManager.ContainsWholeWord("xJedi", "Jedi"));
        }

        [Fact]
        public void ContainsWholeWord_SpacePunctuationVariants_Matches()
        {
            // 术语值差一个空格/标点时也应命中（空格 ↔ 连字符/下划线/斜杠/句点 互换）
            Assert.True(GlossaryManager.ContainsWholeWord("Star-Destroyer approaches", "Star Destroyer"));
            Assert.True(GlossaryManager.ContainsWholeWord("Star_Destroyer approaches", "Star Destroyer"));
            Assert.True(GlossaryManager.ContainsWholeWord("The Star Destroyer approaches", "Star-Destroyer"));
            Assert.True(GlossaryManager.ContainsWholeWord("TIE/Fighter squadron", "TIE Fighter"));
            Assert.True(GlossaryManager.ContainsWholeWord("attack on the Death Star.", "Death Star"));
            Assert.True(GlossaryManager.ContainsWholeWord("Attack  of  the  Clones", "Attack of the Clones"));

            // 撇号差异（Hutt's ↔ Hutts）
            Assert.True(GlossaryManager.ContainsWholeWord("The Hutts control the sector", "Hutt's"));
            Assert.True(GlossaryManager.ContainsWholeWord("Hutt's palace", "Hutts"));

            // 词内拼接仍不命中（保持原语义）
            Assert.False(GlossaryManager.ContainsWholeWord("StarDestroyer", "Star Destroyer"));
        }

        [Fact]
        public void ContainsWholeWord_InsertedModifierWord_Matches()
        {
            // 差一个修饰词（class/mk/type 等）也应命中
            Assert.True(GlossaryManager.ContainsWholeWord("Procursator-class Star Destroyer", "Procursator Star Destroyer"));
            Assert.True(GlossaryManager.ContainsWholeWord("Procursator-class Star Destroyer", "Procursator-class Star Destroyer"));
            Assert.True(GlossaryManager.ContainsWholeWord("Executor Star Dreadnought", "Executor-class Star Dreadnought"));
            Assert.True(GlossaryManager.ContainsWholeWord("TIE-class Fighter", "TIE Fighter"));
            Assert.True(GlossaryManager.ContainsWholeWord("Acclamator-type assault ship", "Acclamator assault ship"));
            Assert.True(GlossaryManager.ContainsWholeWord("Nebula-class Star Destroyer", "Nebula Star Destroyer"));

            // 插入非修饰词不算（避免过度宽松）：High 不在修饰词白名单
            Assert.False(GlossaryManager.ContainsWholeWord("Jedi High Council", "Jedi Council"));
        }

        [Fact]
        public void GetGlossaryContextTerms_PluralAndUnderscore_FindsTerms()
        {
            var manager = new GlossaryManager();
            manager.Clear();
            manager.SetEntry("Stormtrooper", "暴风兵");
            manager.SetEntry("Jedi", "绝地");
            manager.SetEntry("Box", "箱子");

            var entries = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "K1", Value = "The Stormtroopers and dark_jedi carry three boxes" }
            };

            var terms = manager.GetGlossaryContextTerms(entries);

            Assert.Contains("Stormtrooper", terms.Keys);
            Assert.Contains("Jedi", terms.Keys);
            Assert.Contains("Box", terms.Keys);
            Assert.Equal("暴风兵", terms["Stormtrooper"]);
            manager.Clear();
        }

        // ─── 宽松相关判定（AI 提示词注入，覆盖核心词省略变体） ──────

        [Fact]
        public void IsTermRelated_FirstCoreWordPlusClassModifier_Matches()
        {
            // 原文 "Xyston-class" 只含术语首核心词 + class 修饰词
            Assert.True(GlossaryManager.IsTermRelated("Xyston-class", "Xyston-class Star Destroyer"));
            Assert.True(GlossaryManager.IsTermRelated("Quasar Fire-class", "Quasar Fire-class cruiser-carrier"));
        }

        [Fact]
        public void IsTermRelated_CoreWordSubsetInOrder_Matches()
        {
            // 原文省略术语的部分核心词（Xyston Siege Destroyer Upkeep 缺 Star）
            Assert.True(GlossaryManager.IsTermRelated(
                "Xyston Siege Destroyer Upkeep", "Xyston-class Star Destroyer"));
            // 原文省略术语的部分核心词（Quasar Fire-class Carrier 缺 cruiser）
            Assert.True(GlossaryManager.IsTermRelated(
                "Quasar Fire-class Carrier", "Quasar Fire-class cruiser-carrier"));
        }

        [Fact]
        public void IsTermRelated_UnrelatedText_DoesNotMatch()
        {
            // 缺 Destroyer/Star 且无 class 修饰词：不相关
            Assert.False(GlossaryManager.IsTermRelated(
                "Xyston Siege Upkeep Battery", "Xyston-class Star Destroyer"));
            Assert.False(GlossaryManager.IsTermRelated(
                "The Quasar Nebula", "Quasar Fire-class cruiser-carrier"));
            Assert.False(GlossaryManager.IsTermRelated("", "Xyston-class Star Destroyer"));
        }

        [Fact]
        public void IsTermRelated_SingleCoreWordTerm_Matches()
        {
            // 单核心词术语（"Executor-class" 只剩 executor）：含首核心词即宽松命中
            Assert.True(GlossaryManager.IsTermRelated("Executor Dreadnought", "Executor-class"));
            Assert.True(GlossaryManager.IsTermRelated("Executor's bridge", "Executor-class"));

            // 不含首核心词仍不命中
            Assert.False(GlossaryManager.IsTermRelated("Dreadnought", "Executor-class"));
        }

        [Fact]
        public void Skipray_Variants_Match()
        {
            // 标准写法（大小写差异）严格命中
            Assert.True(GlossaryManager.ContainsWholeWord("Skipray Blastboat", "Skipray blastboat"));
            Assert.True(GlossaryManager.ContainsWholeWord("The Skipray Blastboat squadron", "Skipray blastboat"));

            // 单独短名 "Skipray"（2 核心词术语只命中首核心词）应宽松命中提示注入
            Assert.True(GlossaryManager.IsTermRelated("2 Skipray (6)", "Skipray blastboat"));
            Assert.True(GlossaryManager.IsTermRelated("The GAT-12 Skipray is durable", "Skipray blastboat"));

            // 空格拆分 "Blast Boat(s)" 应宽松命中（首核心词 Skipray 命中即相关）
            Assert.True(GlossaryManager.IsTermRelated("Skipray Blast Boat here", "Skipray blastboat"));
            Assert.True(GlossaryManager.IsTermRelated("Skipray Blast Boats can be used", "Skipray blastboat"));

            // 不含 Skipray 不命中
            Assert.False(GlossaryManager.IsTermRelated("Blastboat", "Skipray blastboat"));
        }

        [Fact]
        public void GetGlossaryContextTerms_LooseVariant_FindsTerm()
        {
            var manager = new GlossaryManager();
            manager.Clear();
            manager.SetEntry("Xyston-class Star Destroyer", "长矛级歼星舰");
            manager.SetEntry("Quasar Fire-class cruiser-carrier", "“类星体火级”载机巡洋舰");

            var entries = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "K1", Value = "Xyston-class" },
                new LocalizationEntry { Key = "K2", Value = "Xyston Siege Destroyer Upkeep" },
                new LocalizationEntry { Key = "K3", Value = "Quasar Fire-class Carrier." }
            };

            var terms = manager.GetGlossaryContextTerms(entries);

            Assert.Contains("Xyston-class Star Destroyer", terms.Keys);
            Assert.Contains("Quasar Fire-class cruiser-carrier", terms.Keys);
            Assert.Equal("长矛级歼星舰", terms["Xyston-class Star Destroyer"]);
            manager.Clear();
        }
    }
}
