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
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
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
                AddProfile(CreateMarvelProfile());
            }
        }

        private ExpertProfile CreateStarWarsProfile()
        {
            return new ExpertProfile
            {
                Name = "星球大战 (Star Wars)",
                Description = "星球大战宇宙游戏本地化",
                Context = @"You are translating content from the Star Wars universe.
Think like a Star Wars lore expert. 
- Use the established Chinese translations from official Star Wars media and games.
- Maintain the epic, mythic tone appropriate for the Star Wars galaxy.
- Character names should use widely accepted Chinese translations.
- Faction names and Force-related concepts have specific established translations — use them consistently.
- Do NOT invent new translations for well-known terms; always use the canonical ones.",
                Glossary = new Dictionary<string, string>
                {
                    { "Jedi", "绝地" },
                    { "Sith", "西斯" },
                    { "lightsaber", "光剑" },
                    { "The Force", "原力" },
                    { "Padawan", "学徒" },
                    { "Darth Vader", "达斯·维达" },
                    { "Luke Skywalker", "卢克·天行者" },
                    { "Stormtrooper", "暴风兵" },
                    { "Death Star", "死星" },
                    { "Millennium Falcon", "千年隼号" },
                    { "Wookiee", "伍基人" },
                    { "X-wing", "X翼战机" },
                    { "TIE fighter", "钛战机" },
                    { "Galactic Empire", "银河帝国" },
                    { "Rebel Alliance", "义军同盟" },
                    { "Bounty hunter", "赏金猎人" }
                }
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

        private class ProfileStorageData
        {
            public List<ExpertProfile> Profiles { get; set; } = new();
            public string ActiveProfileName { get; set; } = "";
        }
    }
}
