using TShockAPI;

namespace TSRpg.Logicas
{
    public static class Comandos
    {
        public static void Register()
        {
            // Player commands need no permission so the default group can use them.
            Commands.ChatCommands.Add(new Command(ClassCommand, "class", "classe")
            {
                AllowServer = false,
                HelpText = "Lists the classes or picks one: /class [name]"
            });
            Commands.ChatCommands.Add(new Command(StatsCommand, "rpg", "nivel", "level")
            {
                AllowServer = false,
                HelpText = "Shows your level, XP, class bonuses and perks."
            });
            Commands.ChatCommands.Add(new Command(ClassMenu.Command, "menu", "rpgmenu")
            {
                AllowServer = false,
                HelpText = "Opens the class menu in a chest window (needs server side characters)."
            });
            Commands.ChatCommands.Add(new Command(TopCommand, "rpgtop")
            {
                HelpText = "Shows the highest level players."
            });

            Commands.ChatCommands.Add(new Command("tsrpg.admin", AdminCommand, "rpgadmin")
            {
                HelpText = "/rpgadmin <setlevel|givexp|setclass|reset> <player> [value]"
            });
            Commands.ChatCommands.Add(new Command("tsrpg.admin", ReloadCommand, "rpgreload")
            {
                HelpText = "Reloads the TSRpg config and translations."
            });
        }

        private static string BuffList(IEnumerable<ClassBuff> buffs)
        {
            var names = buffs.Select(b => TShock.Utils.GetBuffName(b.BuffId)).ToList();
            return names.Count > 0 ? string.Join(", ", names) : Translations.Instance.Get("cmd.stats.no_buffs");
        }

        public static void ClassCommand(CommandArgs args)
        {
            var player = args.Player;
            var t = Translations.Instance;
            var config = RpgPlugin.Config;

            if (!RpgPlugin.Players.TryGetValue(player.Index, out var rpg))
            {
                player.SendErrorMessage(t.Get("cmd.class.not_ready"));
                return;
            }

            if (args.Parameters.Count == 0)
            {
                player.SendInfoMessage(t.Get("cmd.class.current", rpg.ClassId == null ? t.Get("class.none") : t.ClassName(rpg.ClassId)));
                player.SendInfoMessage(t.Get("cmd.class.list_header"));
                foreach (var c in config.Classes)
                {
                    string names = string.Join("/", new[] { c.Id }.Concat(c.Aliases));
                    player.SendInfoMessage(t.Get("cmd.class.list_entry", t.ClassName(c.Id), names, t.Get($"class.{c.Id}.desc")));
                }
                return;
            }

            var cls = config.FindClass(string.Join(" ", args.Parameters));
            if (cls == null)
            {
                player.SendErrorMessage(t.Get("cmd.class.unknown", string.Join(" ", args.Parameters)));
                return;
            }

            TryChangeClass(player, rpg, cls);
        }

        /// <summary>Picks a class for the player, with the same rules for /class and the class menu.</summary>
        internal static bool TryChangeClass(TSPlayer player, RpgPlayer rpg, ClassDefinition cls)
        {
            var t = Translations.Instance;

            if (cls.Id == rpg.ClassId)
            {
                player.SendErrorMessage(t.Get("cmd.class.same", t.ClassName(cls.Id)));
                return false;
            }

            // The first pick is free; switching afterwards has a cooldown.
            if (rpg.ClassId != null && rpg.ClassChangedAt.HasValue)
            {
                var ready = rpg.ClassChangedAt.Value.AddMinutes(RpgPlugin.Config.ClassChangeCooldownMinutes);
                if (DateTime.UtcNow < ready)
                {
                    int minutes = (int)Math.Ceiling((ready - DateTime.UtcNow).TotalMinutes);
                    player.SendErrorMessage(t.Get("cmd.class.cooldown", minutes));
                    return false;
                }
            }

            rpg.ClassId = cls.Id;
            rpg.ClassChangedAt = DateTime.UtcNow;
            rpg.Dirty = true;
            RpgDatabase.Save(rpg);

            ClassSystem.ApplyClassBuffs(player, rpg);
            player.SendSuccessMessage(t.Get("cmd.class.changed", t.ClassName(cls.Id)));
            return true;
        }

        public static void StatsCommand(CommandArgs args)
        {
            var player = args.Player;
            var t = Translations.Instance;
            var config = RpgPlugin.Config;

            if (!RpgPlugin.Players.TryGetValue(player.Index, out var rpg))
            {
                player.SendErrorMessage(t.Get("cmd.class.not_ready"));
                return;
            }

            string className = rpg.ClassId == null ? t.Get("class.none") : t.ClassName(rpg.ClassId);
            player.SendInfoMessage(t.Get("cmd.stats.line1", player.Name, rpg.Level, className));
            player.SendInfoMessage(rpg.Level >= config.MaxLevel
                ? t.Get("cmd.stats.max")
                : t.Get("cmd.stats.line2", rpg.Xp, config.XpToNextLevel(rpg.Level)));

            var cls = config.FindClass(rpg.ClassId);
            if (cls == null)
            {
                player.SendInfoMessage(t.Get("join.choose_class"));
                return;
            }

            int damageBonus = (int)Math.Round(cls.DamagePerLevel * (rpg.Level - 1) * 100);
            player.SendInfoMessage(t.Get("cmd.stats.damage", damageBonus, t.Get($"damage.{cls.DamageType}")));
            player.SendInfoMessage(t.Get("cmd.stats.buffs", BuffList(cls.Buffs.Where(b => b.Level <= rpg.Level))));

            var perks = cls.Perks.Where(p => p.Level <= rpg.Level).Select(p => t.Get($"perk.{p.Id}")).ToList();
            player.SendInfoMessage(t.Get("cmd.stats.perks", perks.Count > 0 ? string.Join(", ", perks) : t.Get("cmd.stats.no_buffs")));

            var passives = (cls.Passives ?? new()).Where(p => p.Level <= rpg.Level).Select(p => t.Get($"passive.{p.Id}")).ToList();
            player.SendInfoMessage(t.Get("cmd.stats.passives", passives.Count > 0 ? string.Join(", ", passives) : t.Get("cmd.stats.no_buffs")));

            // What the next unlock is, to give the player something to chase.
            var nextBuff = cls.Buffs.Where(b => b.Level > rpg.Level).OrderBy(b => b.Level).FirstOrDefault();
            var nextPerk = cls.Perks.Where(p => p.Level > rpg.Level).OrderBy(p => p.Level).FirstOrDefault();
            var nextPassive = (cls.Passives ?? new()).Where(p => p.Level > rpg.Level).OrderBy(p => p.Level).FirstOrDefault();
            if (nextPassive != null
                && (nextPerk == null || nextPassive.Level < nextPerk.Level)
                && (nextBuff == null || nextPassive.Level <= nextBuff.Level))
                player.SendInfoMessage(t.Get("cmd.stats.next", nextPassive.Level, $"{t.Get($"passive.{nextPassive.Id}")} — {t.Get($"passive.{nextPassive.Id}.desc")}"));
            else if (nextPerk != null && (nextBuff == null || nextPerk.Level <= nextBuff.Level))
                player.SendInfoMessage(t.Get("cmd.stats.next", nextPerk.Level, $"{t.Get($"perk.{nextPerk.Id}")} — {t.Get($"perk.{nextPerk.Id}.desc")}"));
            else if (nextBuff != null)
                player.SendInfoMessage(t.Get("cmd.stats.next", nextBuff.Level, TShock.Utils.GetBuffName(nextBuff.BuffId)));
        }

        public static void TopCommand(CommandArgs args)
        {
            var t = Translations.Instance;

            // Online players may have unsaved progress.
            RpgPlugin.SaveAll(onlyDirty: true);

            args.Player.SendInfoMessage(t.Get("cmd.top.header"));
            int rank = 1;
            foreach (var p in RpgDatabase.Top(10))
            {
                string className = p.ClassId == null ? t.Get("class.none") : t.ClassName(p.ClassId);
                args.Player.SendInfoMessage(t.Get("cmd.top.entry", rank++, p.Name, p.Level, className));
            }
        }

        public static void AdminCommand(CommandArgs args)
        {
            var player = args.Player;
            var t = Translations.Instance;

            if (args.Parameters.Count < 2)
            {
                player.SendErrorMessage(t.Get("cmd.admin.usage"));
                return;
            }

            var found = TSPlayer.FindByNameOrID(args.Parameters[1]);
            if (found.Count != 1 || !RpgPlugin.Players.TryGetValue(found[0].Index, out var rpg))
            {
                player.SendErrorMessage(t.Get("cmd.admin.player_not_found", args.Parameters[1]));
                return;
            }
            var target = found[0];
            string value = args.Parameters.Count > 2 ? args.Parameters[2] : "";

            switch (args.Parameters[0].ToLowerInvariant())
            {
                case "setlevel" when int.TryParse(value, out int level):
                    Progression.SetLevel(target, rpg, level);
                    break;

                case "givexp" when long.TryParse(value, out long xp):
                    Progression.GiveXp(target, rpg, xp);
                    break;

                case "setclass":
                    var cls = RpgPlugin.Config.FindClass(value);
                    if (cls == null)
                    {
                        player.SendErrorMessage(t.Get("cmd.class.unknown", value));
                        return;
                    }
                    rpg.ClassId = cls.Id;
                    rpg.Dirty = true;
                    ClassSystem.ApplyClassBuffs(target, rpg);
                    break;

                case "reset":
                    rpg.ClassId = null;
                    rpg.ClassChangedAt = null;
                    Progression.SetLevel(target, rpg, 1);
                    break;

                default:
                    player.SendErrorMessage(t.Get("cmd.admin.usage"));
                    return;
            }

            RpgDatabase.Save(rpg);
            string className = rpg.ClassId == null ? t.Get("class.none") : t.ClassName(rpg.ClassId);
            player.SendSuccessMessage(t.Get("cmd.admin.done", target.Name, rpg.Level, className));
        }

        public static void ReloadCommand(CommandArgs args)
        {
            RpgPlugin.Config = Config.Load();
            Translations.Initialize(RpgPlugin.Config.Language);
            args.Player.SendSuccessMessage(Translations.Instance.Get("cmd.reload.success"));
        }
    }
}
