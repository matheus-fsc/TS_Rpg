using Newtonsoft.Json;
using TShockAPI;

namespace TSRpg.Logicas
{
    public class Translations
    {
        private static readonly string TranslationsPath = Path.Combine(TShock.SavePath, "TSRpg", "Translations");
        private Dictionary<string, string> _translations = new();

        public static Translations Instance { get; private set; } = new Translations();

        public static void Initialize(string language)
        {
            Instance = new Translations();
            Instance.LoadTranslations(language);
        }

        private void LoadTranslations(string language)
        {
            try
            {
                if (!Directory.Exists(TranslationsPath))
                    Directory.CreateDirectory(TranslationsPath);

                // Always (re)write the bundled languages so new keys reach existing installs;
                // an edited copy can live under any other file name and be selected via Language.
                CreateDefaultTranslation("en-US");
                CreateDefaultTranslation("pt-BR");

                string translationFile = Path.Combine(TranslationsPath, $"{language}.json");
                if (!File.Exists(translationFile))
                    translationFile = Path.Combine(TranslationsPath, "en-US.json");

                var defaults = Defaults("en-US");
                var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(translationFile))
                             ?? new Dictionary<string, string>();

                // Fall back to English for any key missing from the selected file.
                foreach (var kv in loaded)
                    defaults[kv.Key] = kv.Value;
                _translations = defaults;

                TShock.Log.ConsoleInfo($"TSRpg: translations loaded for language: {language}");
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"TSRpg: error loading translations: {ex.Message}");
                _translations = Defaults("en-US");
            }
        }

        public string Get(string key)
        {
            return _translations.TryGetValue(key, out string? value) ? value : key;
        }

        public string Get(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        public string ClassName(string classId)
        {
            string key = $"class.{classId}";
            string name = Get(key);
            return name == key ? classId : name;
        }

        private static Dictionary<string, string> Defaults(string language)
        {
            if (language == "pt-BR")
            {
                return new Dictionary<string, string>
                {
                    { "class.melee", "Guerreiro" },
                    { "class.mage", "Mago" },
                    { "class.ranged", "Atirador" },
                    { "class.summoner", "Invocador" },
                    { "class.none", "Sem classe" },
                    { "class.melee.desc", "dano corpo a corpo, defesa, vida e roubo de vida" },
                    { "class.mage.desc", "dano mágico, mana, debuffs elementais e explosões arcanas" },
                    { "class.ranged.desc", "dano à distância, velocidade, munição e tiros críticos" },
                    { "class.summoner.desc", "dano de lacaios, mais lacaios e venenos" },
                    { "damage.melee", "corpo a corpo" },
                    { "damage.magic", "mágico" },
                    { "damage.ranged", "à distância" },
                    { "damage.summon", "de invocação" },

                    { "perk.lifesteal", "Roubo de Vida" }, { "perk.lifesteal.desc", "cura 3% do dano causado" },
                    { "perk.brutal", "Golpe Brutal!" }, { "perk.brutal.desc", "10% de chance de +75% de dano" },
                    { "perk.sunder", "Quebra-Armadura" }, { "perk.sunder.desc", "15% de chance de aplicar Icor (reduz defesa)" },
                    { "perk.bloodlust", "Sede de Sangue" }, { "perk.bloodlust.desc", "cura mais 2% do dano causado" },
                    { "perk.frost", "Toque Gélido" }, { "perk.frost.desc", "20% de chance de congelar (Queimadura de Gelo)" },
                    { "perk.arcane", "Explosão Arcana!" }, { "perk.arcane.desc", "10% de chance de +150% de dano" },
                    { "perk.shadowflame", "Chama Sombria" }, { "perk.shadowflame.desc", "20% de chance de aplicar Chama Sombria" },
                    { "perk.siphon", "Sifão" }, { "perk.siphon.desc", "cura 2% do dano mágico causado" },
                    { "perk.headshot", "Headshot!" }, { "perk.headshot.desc", "12% de chance de +100% de dano" },
                    { "perk.pierce", "Perfuração" }, { "perk.pierce.desc", "15% de chance de aplicar Icor (reduz defesa)" },
                    { "perk.incendiary", "Munição Incendiária" }, { "perk.incendiary.desc", "20% de chance de aplicar Chama Amaldiçoada" },
                    { "perk.deadeye", "Olho de Águia!" }, { "perk.deadeye.desc", "8% de chance de +200% de dano" },
                    { "perk.poison", "Peçonha" }, { "perk.poison.desc", "25% de chance de envenenar" },
                    { "perk.venom", "Veneno" }, { "perk.venom.desc", "20% de chance de aplicar Veneno forte" },
                    { "perk.pack", "Instinto de Matilha!" }, { "perk.pack.desc", "8% de chance de +100% de dano" },
                    { "perk.bond", "Elo Vital" }, { "perk.bond.desc", "cura 1% do dano dos lacaios" },

                    { "cmd.stats.damage", "Bônus de dano {1}: +{0}%" },
                    { "cmd.stats.perks", "Perks: {0}" },
                    { "cmd.top.header", "Ranking de níveis:" },
                    { "cmd.top.entry", "{0}. {1} — nível {2} ({3})" },
                    { "level.up.perk", "Novo perk: {0} ({1})." },
                    { "elite.spawn", "★ Elite ★" },
                    { "death.xp_lost", "Você morreu e perdeu {0} XP." },

                    { "cmd.class.list_header", "Classes disponíveis (use /classe <nome>):" },
                    { "cmd.class.list_entry", "{0} ({1}) — {2}" },
                    { "cmd.class.current", "Sua classe atual: {0}." },
                    { "cmd.class.unknown", "Classe desconhecida: {0}." },
                    { "cmd.class.same", "Você já é {0}." },
                    { "cmd.class.cooldown", "Você só pode trocar de classe daqui a {0} minuto(s)." },
                    { "cmd.class.changed", "Agora você é {0}!" },
                    { "cmd.class.not_ready", "Seus dados de RPG ainda não foram carregados. Tente de novo em instantes." },

                    { "cmd.stats.line1", "{0} — Nível {1} ({2})" },
                    { "cmd.stats.line2", "XP: {0}/{1}" },
                    { "cmd.stats.max", "XP: nível máximo alcançado" },
                    { "cmd.stats.buffs", "Bônus ativos: {0}" },
                    { "cmd.stats.next", "Próximo bônus no nível {0}: {1}" },
                    { "cmd.stats.no_buffs", "nenhum" },

                    { "cmd.admin.usage", "Uso: /rpgadmin <setlevel|givexp|setclass|reset> <jogador> [valor]" },
                    { "cmd.admin.player_not_found", "Jogador não encontrado: {0}." },
                    { "cmd.admin.done", "Feito: {0} agora está no nível {1} ({2})." },
                    { "cmd.reload.success", "Config e traduções do TSRpg recarregadas!" },

                    { "xp.popup", "+{0} XP" },
                    { "level.up.self", "Você subiu para o nível {0}!" },
                    { "level.up.broadcast", "{0} chegou ao nível {1}!" },
                    { "level.up.buff", "Novo bônus desbloqueado: {0}." },
                    { "join.choose_class", "Escolha uma classe com /classe para ganhar bônus ao subir de nível." },
                    { "join.welcome", "Bem-vindo de volta, {0}! Nível {1} ({2})." }
                };
            }

            return new Dictionary<string, string>
            {
                { "class.melee", "Warrior" },
                { "class.mage", "Mage" },
                { "class.ranged", "Ranger" },
                { "class.summoner", "Summoner" },
                { "class.none", "No class" },
                { "class.melee.desc", "melee damage, defense, life and lifesteal" },
                { "class.mage.desc", "magic damage, mana, elemental debuffs and arcane bursts" },
                { "class.ranged.desc", "ranged damage, speed, ammo and critical shots" },
                { "class.summoner.desc", "minion damage, more minions and poisons" },
                { "damage.melee", "melee" },
                { "damage.magic", "magic" },
                { "damage.ranged", "ranged" },
                { "damage.summon", "summon" },

                { "perk.lifesteal", "Lifesteal" }, { "perk.lifesteal.desc", "heals 3% of damage dealt" },
                { "perk.brutal", "Brutal Strike!" }, { "perk.brutal.desc", "10% chance for +75% damage" },
                { "perk.sunder", "Sunder" }, { "perk.sunder.desc", "15% chance to apply Ichor (lowers defense)" },
                { "perk.bloodlust", "Bloodlust" }, { "perk.bloodlust.desc", "heals another 2% of damage dealt" },
                { "perk.frost", "Frost Touch" }, { "perk.frost.desc", "20% chance to apply Frostburn" },
                { "perk.arcane", "Arcane Burst!" }, { "perk.arcane.desc", "10% chance for +150% damage" },
                { "perk.shadowflame", "Shadowflame" }, { "perk.shadowflame.desc", "20% chance to apply Shadowflame" },
                { "perk.siphon", "Siphon" }, { "perk.siphon.desc", "heals 2% of magic damage dealt" },
                { "perk.headshot", "Headshot!" }, { "perk.headshot.desc", "12% chance for +100% damage" },
                { "perk.pierce", "Armor Pierce" }, { "perk.pierce.desc", "15% chance to apply Ichor (lowers defense)" },
                { "perk.incendiary", "Incendiary Rounds" }, { "perk.incendiary.desc", "20% chance to apply Cursed Inferno" },
                { "perk.deadeye", "Deadeye!" }, { "perk.deadeye.desc", "8% chance for +200% damage" },
                { "perk.poison", "Toxin" }, { "perk.poison.desc", "25% chance to poison" },
                { "perk.venom", "Venom" }, { "perk.venom.desc", "20% chance to apply Venom" },
                { "perk.pack", "Pack Instinct!" }, { "perk.pack.desc", "8% chance for +100% damage" },
                { "perk.bond", "Life Bond" }, { "perk.bond.desc", "heals 1% of minion damage" },

                { "cmd.stats.damage", "{1} damage bonus: +{0}%" },
                { "cmd.stats.perks", "Perks: {0}" },
                { "cmd.top.header", "Level ranking:" },
                { "cmd.top.entry", "{0}. {1} — level {2} ({3})" },
                { "level.up.perk", "New perk: {0} ({1})." },
                { "elite.spawn", "★ Elite ★" },
                { "death.xp_lost", "You died and lost {0} XP." },

                { "cmd.class.list_header", "Available classes (use /class <name>):" },
                { "cmd.class.list_entry", "{0} ({1}) — {2}" },
                { "cmd.class.current", "Your current class: {0}." },
                { "cmd.class.unknown", "Unknown class: {0}." },
                { "cmd.class.same", "You already are a {0}." },
                { "cmd.class.cooldown", "You can change class again in {0} minute(s)." },
                { "cmd.class.changed", "You are now a {0}!" },
                { "cmd.class.not_ready", "Your RPG data is not loaded yet. Try again in a moment." },

                { "cmd.stats.line1", "{0} — Level {1} ({2})" },
                { "cmd.stats.line2", "XP: {0}/{1}" },
                { "cmd.stats.max", "XP: max level reached" },
                { "cmd.stats.buffs", "Active bonuses: {0}" },
                { "cmd.stats.next", "Next bonus at level {0}: {1}" },
                { "cmd.stats.no_buffs", "none" },

                { "cmd.admin.usage", "Usage: /rpgadmin <setlevel|givexp|setclass|reset> <player> [value]" },
                { "cmd.admin.player_not_found", "Player not found: {0}." },
                { "cmd.admin.done", "Done: {0} is now level {1} ({2})." },
                { "cmd.reload.success", "TSRpg config and translations reloaded!" },

                { "xp.popup", "+{0} XP" },
                { "level.up.self", "You reached level {0}!" },
                { "level.up.broadcast", "{0} reached level {1}!" },
                { "level.up.buff", "New bonus unlocked: {0}." },
                { "join.choose_class", "Pick a class with /class to earn bonuses as you level up." },
                { "join.welcome", "Welcome back, {0}! Level {1} ({2})." }
            };
        }

        private static void CreateDefaultTranslation(string language)
        {
            string filePath = Path.Combine(TranslationsPath, $"{language}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(Defaults(language), Formatting.Indented));
        }
    }
}
