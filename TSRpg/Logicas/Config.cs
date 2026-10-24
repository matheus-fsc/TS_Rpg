using Newtonsoft.Json;
using Terraria.ID;
using TShockAPI;

namespace TSRpg.Logicas
{
    public class ClassBuff
    {
        /// <summary>Level at which the buff is unlocked.</summary>
        public int Level { get; set; }
        public int BuffId { get; set; }

        public ClassBuff() { }
        public ClassBuff(int level, int buffId) { Level = level; BuffId = buffId; }
    }

    /// <summary>
    /// On-hit effect triggered by a hit with the class damage type.
    /// Types: "lifesteal" (heal Value × damage, capped by Max), "strike" (Chance to deal
    /// Value × damage as extra damage), "debuff" (Chance to apply BuffId to the NPC).
    /// </summary>
    public class ClassPerk
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public int Level { get; set; }
        public double Chance { get; set; } = 1.0;
        public double Value { get; set; }
        public int Max { get; set; }
        public int BuffId { get; set; }
        public int DurationSeconds { get; set; }
    }

    /// <summary>
    /// Always-on effect checked once per second. Types:
    /// "combat_regen" / "rest_regen" heal Value every IntervalSeconds while in / out of combat;
    /// "minion_regen" heals Value per active minion every IntervalSeconds;
    /// "low_life_buff" applies BuffIds while life is at or below Threshold (0.3 = 30%);
    /// "idle_buff" applies BuffIds while standing still;
    /// "mana_refund" has Chance on each class hit to restore mana (a mana star is dropped on the player).
    /// </summary>
    public class ClassPassive
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public int Level { get; set; }
        public double Value { get; set; }
        public double Chance { get; set; } = 1.0;
        public int IntervalSeconds { get; set; } = 1;
        public double Threshold { get; set; }
        public List<int> BuffIds { get; set; } = new();
    }

    public class ClassDefinition
    {
        /// <summary>Internal id, also the translation key suffix (class.&lt;id&gt;).</summary>
        public string Id { get; set; } = "";
        /// <summary>Extra names accepted by /class, e.g. "guerreiro".</summary>
        public List<string> Aliases { get; set; } = new();
        /// <summary>Weapon type that benefits from the class: melee, ranged, magic or summon.</summary>
        public string DamageType { get; set; } = "";
        /// <summary>Extra damage per level above 1 on hits with the class damage type (0.015 = +1.5%).</summary>
        public double DamagePerLevel { get; set; } = 0.015;
        /// <summary>Buffs kept permanently active once the player reaches each level.</summary>
        public List<ClassBuff> Buffs { get; set; } = new();
        public List<ClassPerk> Perks { get; set; } = new();
        /// <summary>Null when missing from an older config file; filled from the defaults on load.</summary>
        public List<ClassPassive>? Passives { get; set; }
    }

    public class Config
    {
        private static readonly string ConfigPath = Path.Combine(TShock.SavePath, "TSRpg", "RpgConfig.json");

        // Replace (instead of append to) the default lists when reading the file.
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Formatting = Formatting.Indented
        };

        public string Language { get; set; } = "en-US";

        // ── XP ───────────────────────────────────────────────────────────────
        /// <summary>XP for a kill = NPC max life (after scaling) × this value.</summary>
        public double XpPerLifePoint { get; set; } = 0.1;
        public double BossXpMultiplier { get; set; } = 3.0;
        public double EliteXpMultiplier { get; set; } = 4.0;
        public int MinXpPerKill { get; set; } = 1;
        /// <summary>Fixed XP per NPC id, replacing the life-based formula.</summary>
        public Dictionary<int, int> NpcXpOverrides { get; set; } = new();
        /// <summary>Players this close (in tiles) to a kill take part in it even without hitting.</summary>
        public int PartyRangeTiles { get; set; } = 60;
        /// <summary>Extra XP per additional participant (0.1 = +10% for each extra player).</summary>
        public double PartyBonusPerPlayer { get; set; } = 0.1;
        /// <summary>Part of the kill XP split evenly among all participants; the rest goes by damage dealt.</summary>
        public double PartyEvenShare { get; set; } = 0.3;
        /// <summary>Percent of the current level progress lost on death.</summary>
        public double XpLossOnDeathPercent { get; set; } = 10;
        public bool ShowXpPopup { get; set; } = true;

        // ── Levels ───────────────────────────────────────────────────────────
        /// <summary>XP needed to go from level N to N+1 = LevelBaseXp × N^LevelExponent.</summary>
        public int LevelBaseXp { get; set; } = 100;
        public double LevelExponent { get; set; } = 1.5;
        public int MaxLevel { get; set; } = 50;
        public bool AnnounceLevelUp { get; set; } = true;

        // ── Classes ──────────────────────────────────────────────────────────
        public int ClassChangeCooldownMinutes { get; set; } = 60;
        public int BuffRefreshSeconds { get; set; } = 5;
        public bool ShowPerkPopup { get; set; } = true;
        /// <summary>Seconds without hitting or being hit before a player counts as out of combat.</summary>
        public int OutOfCombatSeconds { get; set; } = 5;
        /// <summary>Life healed per second out of combat, for every player with a class.</summary>
        public int RestRegenPerSecond { get; set; } = 2;
        public List<ClassDefinition> Classes { get; set; } = DefaultClasses();

        // ── Mob & boss scaling ───────────────────────────────────────────────
        // Scaling raises each NPC's vanilla difficulty value above the world's (Classic 1,
        // Expert 2, Master 3, Legendary 4). Vanilla curves: from Master to Legendary enemy
        // life goes ×1.33 and contact damage ×1.78; nothing grows past 4.

        /// <summary>Hostile mobs scale with the average level of the players around them.</summary>
        public bool ScaleMobs { get; set; } = true;
        /// <summary>Difficulty added per player level above 1 (0.015 → +0.735 at level 50).</summary>
        public double MobDifficultyPerLevel { get; set; } = 0.015;
        /// <summary>Highest difficulty a regular mob or boss can reach (elites ignore it).</summary>
        public double MaxNpcDifficulty { get; set; } = 4.0;
        /// <summary>Radius (in tiles) used to find the players a new mob scales against.</summary>
        public int ScalingRangeTiles { get; set; } = 150;

        /// <summary>Chance for a new mob to be an elite: tougher, glittering gold, more XP and coins.</summary>
        public double EliteChance { get; set; } = 0.04;
        public int EliteMinLevel { get; set; } = 5;
        public double EliteDifficultyBonus { get; set; } = 1.0;

        /// <summary>Bosses scale with the average level of everyone online, plus a flat bonus.</summary>
        public bool ScaleBosses { get; set; } = true;
        public double BossDifficultyBonus { get; set; } = 0.25;
        public double BossDifficultyPerLevel { get; set; } = 0.015;
        /// <summary>Boss parts that are separate NPCs without the boss flag (hands, segments...).</summary>
        public List<int> ExtraBossNpcIds { get; set; } = new()
        {
            NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail,
            NPCID.Creeper,
            NPCID.SkeletronHand,
            NPCID.WallofFleshEye,
            NPCID.TheDestroyerBody, NPCID.TheDestroyerTail,
            NPCID.PrimeCannon, NPCID.PrimeSaw, NPCID.PrimeVice, NPCID.PrimeLaser,
            NPCID.PlanterasTentacle,
            NPCID.GolemHead, NPCID.GolemFistLeft, NPCID.GolemFistRight,
            NPCID.MoonLordHead, NPCID.MoonLordHand, NPCID.MoonLordCore
        };

        public static List<ClassDefinition> DefaultClasses() => new()
        {
            new ClassDefinition
            {
                Id = "melee",
                Aliases = new() { "guerreiro", "warrior" },
                DamageType = "melee",
                Buffs = new()
                {
                    new(1, BuffID.Ironskin), new(5, BuffID.Regeneration), new(15, BuffID.Endurance),
                    new(25, BuffID.Lifeforce), new(35, BuffID.Titan), new(45, BuffID.Thorns)
                },
                Perks = new()
                {
                    new ClassPerk { Id = "lifesteal", Type = "lifesteal", Level = 10, Value = 0.03, Max = 8 },
                    new ClassPerk { Id = "brutal", Type = "strike", Level = 20, Chance = 0.10, Value = 0.75 },
                    new ClassPerk { Id = "sunder", Type = "debuff", Level = 30, Chance = 0.15, BuffId = BuffID.Ichor, DurationSeconds = 5 },
                    new ClassPerk { Id = "bloodlust", Type = "lifesteal", Level = 40, Value = 0.02, Max = 6 }
                },
                Passives = new()
                {
                    new ClassPassive { Id = "second_wind", Type = "combat_regen", Level = 5, Value = 2, IntervalSeconds = 2 },
                    new ClassPassive { Id = "berserk", Type = "low_life_buff", Level = 20, Threshold = 0.3, BuffIds = new() { BuffID.Rage, BuffID.Wrath } },
                    new ClassPassive { Id = "iron_will", Type = "combat_regen", Level = 35, Value = 3, IntervalSeconds = 2 }
                }
            },
            new ClassDefinition
            {
                Id = "mage",
                Aliases = new() { "mago", "wizard" },
                DamageType = "magic",
                Buffs = new()
                {
                    new(1, BuffID.ManaRegeneration), new(5, BuffID.MagicPower), new(15, BuffID.Clairvoyance),
                    new(25, BuffID.Regeneration), new(35, BuffID.Rage), new(45, BuffID.Wrath)
                },
                Perks = new()
                {
                    new ClassPerk { Id = "frost", Type = "debuff", Level = 10, Chance = 0.20, BuffId = BuffID.Frostburn, DurationSeconds = 4 },
                    new ClassPerk { Id = "arcane", Type = "strike", Level = 20, Chance = 0.10, Value = 1.5 },
                    new ClassPerk { Id = "shadowflame", Type = "debuff", Level = 30, Chance = 0.20, BuffId = BuffID.ShadowFlame, DurationSeconds = 4 },
                    new ClassPerk { Id = "siphon", Type = "lifesteal", Level = 40, Value = 0.02, Max = 6 }
                },
                Passives = new()
                {
                    new ClassPassive { Id = "mana_flow", Type = "mana_refund", Level = 1, Chance = 0.05 },
                    new ClassPassive { Id = "arcane_shield", Type = "low_life_buff", Level = 15, Threshold = 0.3, BuffIds = new() { BuffID.Endurance, BuffID.Regeneration } },
                    new ClassPassive { Id = "mana_tide", Type = "mana_refund", Level = 30, Chance = 0.05 }
                }
            },
            new ClassDefinition
            {
                Id = "ranged",
                Aliases = new() { "atirador", "ranger" },
                DamageType = "ranged",
                Buffs = new()
                {
                    new(1, BuffID.Archery), new(5, BuffID.Swiftness), new(15, BuffID.AmmoReservation),
                    new(25, BuffID.Hunter), new(35, BuffID.Rage), new(45, BuffID.Wrath)
                },
                Perks = new()
                {
                    new ClassPerk { Id = "headshot", Type = "strike", Level = 10, Chance = 0.12, Value = 1.0 },
                    new ClassPerk { Id = "pierce", Type = "debuff", Level = 20, Chance = 0.15, BuffId = BuffID.Ichor, DurationSeconds = 5 },
                    new ClassPerk { Id = "incendiary", Type = "debuff", Level = 30, Chance = 0.20, BuffId = BuffID.CursedInferno, DurationSeconds = 4 },
                    new ClassPerk { Id = "deadeye", Type = "strike", Level = 40, Chance = 0.08, Value = 2.0 }
                },
                Passives = new()
                {
                    new ClassPassive { Id = "steady_aim", Type = "idle_buff", Level = 10, BuffIds = new() { BuffID.Rage } },
                    new ClassPassive { Id = "survival", Type = "low_life_buff", Level = 20, Threshold = 0.3, BuffIds = new() { BuffID.Endurance, BuffID.Regeneration } },
                    new ClassPassive { Id = "field_medic", Type = "rest_regen", Level = 30, Value = 3, IntervalSeconds = 1 }
                }
            },
            new ClassDefinition
            {
                Id = "summoner",
                Aliases = new() { "invocador", "summon" },
                DamageType = "summon",
                Buffs = new()
                {
                    new(1, BuffID.Summoning), new(5, BuffID.Regeneration), new(15, BuffID.Bewitched),
                    new(25, BuffID.Swiftness), new(35, BuffID.Ironskin), new(45, BuffID.Wrath)
                },
                Perks = new()
                {
                    new ClassPerk { Id = "poison", Type = "debuff", Level = 10, Chance = 0.25, BuffId = BuffID.Poisoned, DurationSeconds = 5 },
                    new ClassPerk { Id = "venom", Type = "debuff", Level = 20, Chance = 0.20, BuffId = BuffID.Venom, DurationSeconds = 5 },
                    new ClassPerk { Id = "pack", Type = "strike", Level = 30, Chance = 0.08, Value = 1.0 },
                    new ClassPerk { Id = "bond", Type = "lifesteal", Level = 40, Value = 0.01, Max = 4 }
                },
                Passives = new()
                {
                    new ClassPassive { Id = "spirit_link", Type = "minion_regen", Level = 5, Value = 1, IntervalSeconds = 3 },
                    new ClassPassive { Id = "guardian", Type = "low_life_buff", Level = 20, Threshold = 0.3, BuffIds = new() { BuffID.Endurance, BuffID.Regeneration } },
                    new ClassPassive { Id = "swarm", Type = "minion_regen", Level = 35, Value = 1, IntervalSeconds = 3 }
                }
            }
        };

        public ClassDefinition? FindClass(string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return Classes.FirstOrDefault(c =>
                c.Id.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                c.Aliases.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }

        public long XpToNextLevel(int level)
        {
            return Math.Max(1, (long)Math.Round(LevelBaseXp * Math.Pow(level, LevelExponent)));
        }

        public static Config Load()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (!File.Exists(ConfigPath))
                {
                    var defaultConfig = new Config();
                    defaultConfig.Save();
                    TShock.Log.ConsoleInfo("TSRpg: default config file created.");
                    return defaultConfig;
                }

                var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigPath), JsonSettings) ?? new Config();

                // Configs written before passives existed: give the bundled classes their default passives.
                var defaults = DefaultClasses();
                bool upgraded = false;
                foreach (var cls in config.Classes.Where(c => c.Passives == null))
                {
                    cls.Passives = defaults.FirstOrDefault(d => d.Id == cls.Id)?.Passives ?? new();
                    upgraded = true;
                }
                if (upgraded)
                    config.Save();

                TShock.Log.ConsoleInfo("TSRpg: config loaded successfully.");
                return config;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"TSRpg: error loading config: {ex.Message}");
                return new Config();
            }
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, JsonSettings));
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"TSRpg: error saving config: {ex.Message}");
            }
        }
    }
}
