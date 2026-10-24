using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;
using static TShockAPI.GetDataHandlers;

namespace TSRpg.Logicas
{
    /// <summary>
    /// What a class actually does: permanent buffs, extra damage per level on hits with the
    /// class weapon type, and on-hit perks (lifesteal, extra strikes, debuffs on the target).
    /// Everything is applied server-side, so it works with the vanilla client.
    /// </summary>
    public static class ClassSystem
    {
        private static readonly Random random = new();

        // Active minions and sentries per player, refreshed periodically.
        private static readonly Dictionary<int, int> minionCounts = new();

        public static int MinionCount(int playerIndex) => minionCounts.TryGetValue(playerIndex, out int n) ? n : 0;

        // Last game tick each player was healed by lifesteal.
        private static readonly Dictionary<int, long> lastHeal = new();
        private const int LifestealCooldownTicks = 15;

        public static void ForgetPlayer(int playerIndex)
        {
            minionCounts.Remove(playerIndex);
            lastHeal.Remove(playerIndex);
        }

        // ── Buffs ────────────────────────────────────────────────────────────

        public static void ApplyClassBuffs(TSPlayer tsPlayer, RpgPlayer rpg)
        {
            var cls = RpgPlugin.Config.FindClass(rpg.ClassId);
            if (cls == null) return;

            // Slightly longer than the refresh interval so the buff never blinks off.
            int duration = (Math.Max(1, RpgPlugin.Config.BuffRefreshSeconds) + 5) * 60;
            foreach (var buff in cls.Buffs.Where(b => b.Level <= rpg.Level))
                tsPlayer.SetBuff(buff.BuffId, duration, true);
        }

        public static void RefreshMinionOwners()
        {
            minionCounts.Clear();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                var proj = Main.projectile[i];
                if (proj.active && (proj.minion || proj.sentry) && proj.owner >= 0 && proj.owner < Main.maxPlayers)
                    minionCounts[proj.owner] = MinionCount(proj.owner) + 1;
            }
        }

        // ── On-hit effects ───────────────────────────────────────────────────

        /// <summary>
        /// Called for every hit a player reports on an NPC, before the server applies it.
        /// Returns the extra damage dealt by the class, so the caller can credit it for XP.
        /// </summary>
        public static long OnPlayerHit(TSPlayer tsPlayer, RpgPlayer rpg, NPCStrikeEventArgs args, long tick)
        {
            var cls = RpgPlugin.Config.FindClass(rpg.ClassId);
            if (cls == null || !UsesClassWeapon(tsPlayer, cls)) return 0;

            var npc = Main.npc[args.ID];
            if (!npc.active || npc.dontTakeDamage || npc.immortal || npc.friendly) return 0;

            Passives.OnClassHit(tsPlayer, rpg, cls, tick);

            var t = Translations.Instance;
            double extra = args.Damage * cls.DamagePerLevel * (rpg.Level - 1);

            foreach (var perk in cls.Perks.Where(p => p.Level <= rpg.Level))
            {
                if (perk.Chance < 1.0 && random.NextDouble() >= perk.Chance) continue;

                switch (perk.Type)
                {
                    case "strike":
                        extra += args.Damage * perk.Value;
                        Popup(npc, t.Get($"perk.{perk.Id}"), new Color(255, 120, 60));
                        break;

                    case "debuff":
                        int ticks = perk.DurationSeconds * 60;
                        npc.AddBuff(perk.BuffId, ticks);
                        NetMessage.SendData((int)PacketTypes.NpcAddBuff, -1, -1, null, npc.whoAmI, perk.BuffId, ticks);
                        break;

                    case "lifesteal":
                        if (lastHeal.TryGetValue(tsPlayer.Index, out long last) && tick - last < LifestealCooldownTicks) break;
                        int heal = (int)Math.Min(perk.Max > 0 ? perk.Max : int.MaxValue, Math.Round(args.Damage * perk.Value));
                        if (heal > 0 && tsPlayer.TPlayer.statLife < tsPlayer.TPlayer.statLifeMax2)
                        {
                            tsPlayer.Heal(heal);
                            lastHeal[tsPlayer.Index] = tick;
                        }
                        break;
                }
            }

            int bonus = (int)Math.Round(extra);
            if (bonus <= 0) return 0;

            // StrikeNPC subtracts half the defense again; add it back so the bonus lands as intended.
            // The packet encodes the hit direction as 0/2 for -1/+1.
            TSPlayer.Server.StrikeNPC(npc.whoAmI, bonus + npc.defense / 2, 0f, args.Direction > 1 ? 1 : -1);
            return bonus;
        }

        private static bool UsesClassWeapon(TSPlayer tsPlayer, ClassDefinition cls)
        {
            var item = tsPlayer.SelectedItem;
            return cls.DamageType switch
            {
                "melee"  => item.melee,
                "ranged" => item.ranged,
                "magic"  => item.magic,
                // Minions hit no matter what the player is holding.
                "summon" => item.summon || MinionCount(tsPlayer.Index) > 0,
                _ => false
            };
        }

        private static void Popup(NPC npc, string text, Color color)
        {
            if (!RpgPlugin.Config.ShowPerkPopup) return;
            TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, text, (int)color.PackedValue, npc.Center.X, npc.Top.Y);
        }
    }
}
