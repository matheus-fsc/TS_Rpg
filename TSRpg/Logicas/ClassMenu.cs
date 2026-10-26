using Terraria;
using Terraria.ID;
using TShockAPI;

namespace TSRpg.Logicas
{
    /// <summary>
    /// First chest menu: the classes on the top row, and the unlocks of the player's class below.
    /// A class is picked by clicking its icon twice, so a stray click (or Loot All) never changes it.
    /// </summary>
    public static class ClassMenu
    {
        private const int ConfirmSeconds = 4;
        // Classes are centered on the top row.
        private const int FirstClassSlot = 3;
        private const int FirstUnlockSlot = 10;

        // Title colors. The chest title is drawn like chat text, so it takes [c/hex:...] and [i:id] tags.
        private const string NormalColor = "FFFFFF";
        private const string CurrentColor = "FFD700";
        private const string ConfirmColor = "FFA040";
        private const string UnlockedColor = "80FF80";
        private const string LockedColor = "A0A0A0";

        // Player index -> slot clicked once and when.
        private static readonly Dictionary<int, (int Slot, DateTime At)> pending = new();

        public static void Command(CommandArgs args)
        {
            var player = args.Player;
            var t = Translations.Instance;

            if (!ChestMenus.IsAvailable(player))
            {
                player.SendErrorMessage(t.Get("menu.ssc_required"));
                return;
            }

            // Admin-only stress test: /menu test <slots> fills every slot, so scrolling and clicks can be checked.
            if (args.Parameters.Count > 0 && args.Parameters[0].Equals("test", StringComparison.OrdinalIgnoreCase)
                && player.HasPermission("tsrpg.admin"))
            {
                int size = args.Parameters.Count > 1 && int.TryParse(args.Parameters[1], out int n) ? n : 80;
                if (!ChestMenus.Open(player, TestMenu(size)))
                    player.SendErrorMessage(t.Get("menu.no_spot"));
                return;
            }

            if (!RpgPlugin.Players.TryGetValue(player.Index, out var rpg))
            {
                player.SendErrorMessage(t.Get("cmd.class.not_ready"));
                return;
            }

            if (!ChestMenus.Open(player, Build(rpg)))
            {
                player.SendErrorMessage(t.Get("menu.no_spot"));
                return;
            }

            // Items keep their vanilla names on hover, so name the top row once in chat.
            var names = RpgPlugin.Config.Classes.Take(10 - FirstClassSlot).Select(c => $"[i:{ClassIcon(c)}] {t.ClassName(c.Id)}");
            player.SendInfoMessage(t.Get("menu.legend", string.Join(", ", names)));
        }

        public static void ForgetPlayer(int playerIndex) => pending.Remove(playerIndex);

        private static ChestMenu Build(RpgPlayer rpg)
        {
            var t = Translations.Instance;
            var config = RpgPlugin.Config;
            var menu = new ChestMenu { Title = t.Get("menu.title.classes") };

            for (int i = 0; i < config.Classes.Count && FirstClassSlot + i < 10; i++)
            {
                var cls = config.Classes[i];
                menu.Slots[FirstClassSlot + i] = new MenuIcon
                {
                    ItemId = ClassIcon(cls),
                    // The current class shows the player's level in the slot corner.
                    Stack = cls.Id == rpg.ClassId ? rpg.Level : 1,
                    Label = cls.Id == rpg.ClassId
                        ? Title(ClassIcon(cls), CurrentColor, t.Get("menu.label.class_current", t.ClassName(cls.Id)))
                        : Title(ClassIcon(cls), NormalColor, t.ClassName(cls.Id)),
                    OnClick = (p, slot) => OnClassClick(p, slot, cls)
                };
            }

            var current = config.FindClass(rpg.ClassId);
            if (current == null)
                return menu;

            var unlocks = current.Buffs.Select(b => (b.Level, Icon: BuffIcon(b.BuffId), Name: TShock.Utils.GetBuffName(b.BuffId), Desc: ""))
                .Concat(current.Perks.Select(p => (p.Level, Icon: PerkIcon(p), Name: t.Get($"perk.{p.Id}"), Desc: t.Get($"perk.{p.Id}.desc"))))
                .Concat((current.Passives ?? new()).Select(p => (p.Level, Icon: PassiveIcon(p), Name: t.Get($"passive.{p.Id}"), Desc: t.Get($"passive.{p.Id}.desc"))))
                .OrderBy(u => u.Level)
                .ToList();

            for (int i = 0; i < unlocks.Count && FirstUnlockSlot + i < menu.Size; i++)
            {
                var u = unlocks[i];
                bool unlocked = u.Level <= rpg.Level;
                menu.Slots[FirstUnlockSlot + i] = new MenuIcon
                {
                    ItemId = unlocked ? u.Icon : ItemID.GrayBrick,
                    Stack = u.Level,
                    Label = Title(u.Icon, unlocked ? UnlockedColor : LockedColor,
                        t.Get(unlocked ? "menu.label.unlocked" : "menu.label.locked", u.Level, u.Name)),
                    OnClick = (p, _) =>
                    {
                        string text = u.Desc.Length > 0 ? $"{u.Name} — {u.Desc}" : u.Name;
                        string key = unlocked ? "menu.unlock.unlocked" : "menu.unlock.locked";
                        p.SendInfoMessage(Translations.Instance.Get(key, u.Level, text));
                    }
                };
            }
            return menu;
        }

        private static void OnClassClick(TSPlayer player, int slot, ClassDefinition cls)
        {
            var t = Translations.Instance;
            if (!RpgPlugin.Players.TryGetValue(player.Index, out var rpg)) return;

            if (cls.Id == rpg.ClassId)
            {
                player.SendInfoMessage(t.Get("menu.class.current", t.ClassName(cls.Id), t.Get($"class.{cls.Id}.desc")));
                return;
            }

            if (pending.TryGetValue(player.Index, out var last) && last.Slot == slot
                && (DateTime.UtcNow - last.At).TotalSeconds <= ConfirmSeconds)
            {
                pending.Remove(player.Index);
                if (Comandos.TryChangeClass(player, rpg, cls))
                    ChestMenus.Open(player, Build(rpg));
                return;
            }

            pending[player.Index] = (slot, DateTime.UtcNow);
            ChestMenus.SetTitle(player, Title(ClassIcon(cls), ConfirmColor, t.Get("menu.label.confirm", t.ClassName(cls.Id))));
            player.SendInfoMessage(t.Get("menu.class.info", t.ClassName(cls.Id), t.Get($"class.{cls.Id}.desc")));
        }

        private static ChestMenu TestMenu(int size)
        {
            var menu = new ChestMenu(size) { Title = Translations.Instance.Get("menu.title.test", size) };
            for (int slot = 0; slot < menu.Size; slot++)
            {
                menu.Slots[slot] = new MenuIcon
                {
                    ItemId = ItemID.DirtBlock,
                    Stack = slot + 1,
                    Label = Translations.Instance.Get("menu.test.click", slot + 1),
                    OnClick = (p, s) => p.SendInfoMessage(Translations.Instance.Get("menu.test.click", s + 1))
                };
            }
            return menu;
        }

        private static int ClassIcon(ClassDefinition cls) => cls.DamageType switch
        {
            "melee"  => ItemID.IronBroadsword,
            "magic"  => ItemID.WandofSparking,
            "ranged" => ItemID.WoodenBow,
            "summon" => ItemID.SlimeStaff,
            _ => ItemID.Book
        };

        /// <summary>A title line with the item's icon in front of colored text.</summary>
        private static string Title(int itemId, string color, string text) =>
            // A ']' would end the color tag early.
            $"[i:{itemId}] [c/{color}:{text.Replace("]", ")")}]";

        // Buff id -> the potion that gives it, found once from the item data.
        private static Dictionary<int, int>? potions;

        /// <summary>The potion for the buff, so the slot shows the same name and look as the buff.</summary>
        private static int BuffIcon(int buffId)
        {
            potions ??= ContentSamples.ItemsByType.Values
                .Where(i => i.buffType > 0 && i.consumable)
                .GroupBy(i => i.buffType)
                .ToDictionary(g => g.Key, g => g.First().type);
            return potions.TryGetValue(buffId, out int item) ? item : ItemID.IronskinPotion;
        }

        private static int PerkIcon(ClassPerk perk) => perk.Type switch
        {
            "lifesteal" => ItemID.VampireKnives,
            "strike" => ItemID.EyeoftheGolem,
            "teamheal" => ItemID.SpectreHood,
            "debuff" => DebuffIcon(perk.BuffId),
            _ => ItemID.FallenStar
        };

        /// <summary>An item known for inflicting the debuff.</summary>
        private static int DebuffIcon(int buffId) => buffId switch
        {
            BuffID.Ichor => ItemID.FlaskofIchor,
            BuffID.CursedInferno => ItemID.FlaskofCursedFlames,
            BuffID.Poisoned => ItemID.FlaskofPoison,
            BuffID.Venom => ItemID.FlaskofVenom,
            BuffID.OnFire => ItemID.FlaskofFire,
            BuffID.Frostburn => ItemID.FrostburnArrow,
            BuffID.ShadowFlame => ItemID.ShadowFlameKnife,
            _ => ItemID.FlaskofPoison
        };

        private static int PassiveIcon(ClassPassive passive) => passive.Type switch
        {
            "combat_regen" => ItemID.BandofRegeneration,
            "rest_regen" => ItemID.Campfire,
            "low_life_buff" => ItemID.PanicNecklace,
            "mana_refund" => ItemID.ManaRegenerationBand,
            "idle_buff" => ItemID.SniperScope,
            "minion_regen" => ItemID.PygmyNecklace,
            _ => ItemID.LifeCrystal
        };
    }
}
