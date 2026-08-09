using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ExpertProfiles
{
    /// <summary>
    /// Manages the lifecycle of expert profiles: load, save, CRUD, and active profile tracking.
    /// Profiles are stored in expert_profiles.json.
    /// </summary>
    public class ExpertProfileManager : IExpertProfileManager
    {
        private static readonly string ProfilesFile = Path.Combine(
            AppContext.BaseDirectory,
            "expert_profiles.json");

        public List<ExpertProfile> Profiles { get; private set; } = new();
        public string ActiveProfileName { get; set; } = "";

        /// <summary>
        /// Gets the currently active profile, or null if none is selected.
        /// </summary>
        public ExpertProfile ActiveProfile
        {
            get
            {
                if (string.IsNullOrEmpty(ActiveProfileName))
                    return null;
                return Profiles.FirstOrDefault(p => p.Name == ActiveProfileName);
            }
        }

        public ExpertProfileManager()
        {
            LoadProfiles();
        }

        public void LoadProfiles()
        {
            try
            {
                if (File.Exists(ProfilesFile))
                {
                    var json = File.ReadAllText(ProfilesFile);
                    var data = JsonConvert.DeserializeObject<ProfileStorageData>(json);
                    if (data != null)
                    {
                        Profiles = data.Profiles ?? new List<ExpertProfile>();
                        ActiveProfileName = data.ActiveProfileName ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                Profiles = new List<ExpertProfile>();
                ActiveProfileName = "";
                System.Diagnostics.Debug.WriteLine($"Expert profiles load error: {ex.Message}");
            }
        }

        public void SaveProfiles()
        {
            try
            {
                var data = new ProfileStorageData
                {
                    Profiles = Profiles,
                    ActiveProfileName = ActiveProfileName
                };
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(ProfilesFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Expert profiles save error: {ex.Message}");
            }
        }

        public void AddProfile(ExpertProfile profile)
        {
            // Remove existing with same name
            Profiles.RemoveAll(p => p.Name == profile.Name);
            Profiles.Add(profile);
            SaveProfiles();
        }

        public void DeleteProfile(string name)
        {
            Profiles.RemoveAll(p => p.Name == name);
            if (ActiveProfileName == name)
                ActiveProfileName = "";
            SaveProfiles();
        }

        public ExpertProfile GetProfile(string name)
        {
            return Profiles.FirstOrDefault(p => p.Name == name);
        }

        /// <summary>
        /// Creates default example profiles if no profiles exist yet.
        /// </summary>
        public void EnsureDefaultsExist()
        {
            if (Profiles.Count == 0)
            {
                AddProfile(CreateStarWarsProfile());
                AddProfile(CreateProofreadingProfile());
                AddProfile(CreateMarvelProfile());
            }
        }

        private ExpertProfile CreateStarWarsProfile()
        {
            return new ExpertProfile
            {
                Name = "星球大战 (Star Wars)",
                Description = "星球大战：帝国战争（Empire at War）游戏及 mod 汉化专家",
                Context = @"You are a senior localization expert for Star Wars: Empire at War (EaW) and its mods, translating game text into {LANGUAGE}.
Think like an EaW player and a Star Wars lore expert.
- Use the established Chinese translations from official Star Wars media, games, and the EaW Chinese localization community.
- Maintain the epic, mythic tone appropriate for the Star Wars galaxy (military, grand fleet battles, space opera).
- Ship, unit, faction, and character names have canonical Chinese translations - use them consistently; do NOT translate literally or invent new ones.
- Preserve all numbers, statistics, and structural formatting exactly as in the source (e.g. '最高速度：2.5；护盾值：15000；' with '；' separators).
- CRITICAL - Squadron composition entries: a list like '2 A-Wing (2), 1 Elite A-Wing (2), 2 PT-1 (2)' describes fighter SQUADRONS, displayed in-game squadron by squadron. Translate the count unit as '队' (squadron), e.g. '2 队 A翼（2），1 队精英A翼（2），2 队 PT-1（2）'. NEVER render them as individual aircraft like '1 架 XX（1）'.
- Example: '2 X-Wing (4), 1 Y-Wing (2), 2 B-Wing (4)' -> '2 队 X翼（4），1 队 Y翼（2），2 队 B翼（4）'.
- Do NOT invent new translations for well-known terms; always use the canonical ones.",
                Glossary = new Dictionary<string, string>()  // 术语由独立的术语注入功能提供
            };
        }

        private ExpertProfile CreateMarvelProfile()
        {
            return new ExpertProfile
            {
                Name = "漫威 (Marvel)",
                Description = "漫威超级英雄宇宙游戏本地化",
                Context = @"You are translating content from the Marvel universe.
Think like a Marvel comics and MCU lore expert.
- Use the established Chinese translations from official Marvel movies, comics, and games.
- Hero names have specific, widely recognized Chinese translations — do NOT translate literally.
- Maintain the superhero genre tone: dramatic, punchy, and larger-than-life.
- Team names and event names have specific translations that fans recognize.",
                Glossary = new Dictionary<string, string>
                {
                    { "Iron Man", "钢铁侠" },
                    { "Captain America", "美国队长" },
                    { "Spider-Man", "蜘蛛侠" },
                    { "Black Widow", "黑寡妇" },
                    { "Thor", "雷神" },
                    { "Hulk", "绿巨人" },
                    { "Hawkeye", "鹰眼" },
                    { "Doctor Strange", "奇异博士" },
                    { "Black Panther", "黑豹" },
                    { "Ant-Man", "蚁人" },
                    { "Thanos", "灭霸" },
                    { "Avengers", "复仇者联盟" },
                    { "Guardians of the Galaxy", "银河护卫队" },
                    { "Infinity Stones", "无限宝石" },
                    { "Vibranium", "振金" },
                    { "S.H.I.E.L.D.", "神盾局" },
                    { "HYDRA", "九头蛇" },
                    { "Stark Tower", "斯塔克大厦" },
                    { "Wakanda", "瓦坎达" }
                }
            };
        }

        private ExpertProfile CreateProofreadingProfile()
        {
            return new ExpertProfile
            {
                Name = "校对专家 (Proofreading)",
                Description = "游戏本地化校对：检查译文准确性、术语一致性与语境适配",
                Context = @"You are a professional game localization proofreading expert. Your job is to review and correct existing AI-translated game text.
- Provided terms are the DEFAULT preferred translation, NOT absolute rules. If a term is clearly wrong or unnatural in the specific context (different meaning, figurative use, part of a proper name, or a different sense), correct it to a natural contextual translation instead of forcing the term.
- Check terminology consistency across entries: the same source term should use the same translation unless context clearly demands otherwise.
- Preserve all numbers, statistics, and formatting exactly (e.g. '；' separators, squadron lists like '2 队 A翼（2）').
- Provide concise corrections with clear reasons.",
                Glossary = new Dictionary<string, string>()
            };
        }

        private class ProfileStorageData
        {
            public List<ExpertProfile> Profiles { get; set; } = new();
            public string ActiveProfileName { get; set; } = "";
        }
    }
}
