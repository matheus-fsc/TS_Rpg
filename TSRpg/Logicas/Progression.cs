using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace TSRpg.Logicas
{
    /// <summary>XP from kills (shared by damage and proximity), levels and the death penalty.</summary>
    public static class Progression
    {
        // Damage dealt to each living NPC (by NPC index), per player index.
        private static readonly Dictionary<int, Dictionary<int, long>> npcDamage = new();

        public static void RecordDamage(int npcIndex, int playerIndex, long damage)
        {
            // Worm bodies share the head's life pool: credit the damage to the head.
            var npc = Main.npc[npcIndex];
            int index = npc.realLife >= 0 ? npc.realLife : npcIndex;

            if (!npcDamage.TryGetValue(index, out var byPlayer))
                npcDamage[index] = byPlayer = new Dictionary<int, long>();

            byPlayer.TryGetValue(playerIndex, out long dealt);
            byPlayer[playerIndex] = dealt + damage;
        }

        public static void Forget(int npcIndex) => npcDamage.Remove(npcIndex);

        public static void ForgetPlayer(int playerIndex)
        {
            foreach (var byPlayer in npcDamage.Values)
                byPlayer.Remove(playerIndex);
        }

        public static void OnNpcKilled(NpcKilledEventArgs args)
        {
            var npc = args.npc;
            int index = npc.whoAmI;
            bool elite = MobScaling.IsElite(index);
            MobScaling.OnNpcGone(index);

            // Body segments of a worm die with the head; only the head pays out.
            if (npc.realLife >= 0 && npc.realLife != index) return;

            if (!npcDamage.Remove(index, out var byPlayer) || byPlayer.Count == 0) return;
            if (!GivesXp(npc)) return;

            var config = RpgPlugin.Config;
            double totalXp = XpForKill(npc);
            if (elite) totalXp *= config.EliteXpMultiplier;

            // Participants: everyone who hit it, plus anyone alive close enough to the kill.
            float range = config.PartyRangeTiles * 16f;
            var participants = new HashSet<int>(byPlayer.Keys.Where(i => RpgPlugin.Players.ContainsKey(i)));
            foreach (var i in RpgPlugin.Players.Keys)
            {
                var p = Main.player[i];
                if (p.active && !p.dead && Vector2.Distance(p.Center, npc.Center) <= range)
                    participants.Add(i);
            }
            if (participants.Count == 0) return;

            totalXp *= 1 + config.PartyBonusPerPlayer * (participants.Count - 1);

            double evenPool = totalXp * config.PartyEvenShare;
            double damagePool = totalXp - evenPool;
            long totalDamage = byPlayer.Values.Sum();

            foreach (int i in participants)
            {
                byPlayer.TryGetValue(i, out long dealt);
                double share = evenPool / participants.Count
                               + (totalDamage > 0 ? damagePool * dealt / totalDamage : 0);

                long xp = Math.Max(config.MinXpPerKill, (long)Math.Round(share));
                GiveXp(TShock.Players[i], RpgPlugin.Players[i], xp);
            }
        }

        private static bool GivesXp(NPC npc)
        {
            if (RpgPlugin.Config.NpcXpOverrides.ContainsKey(npc.type)) return true;

            return !npc.friendly
                   && !npc.townNPC
                   && !npc.SpawnedFromStatue
                   && npc.lifeMax > 5
                   && npc.type != NPCID.TargetDummy
                   && !NPCID.Sets.CountsAsCritter[npc.type];
        }

        private static double XpForKill(NPC npc)
        {
            var config = RpgPlugin.Config;
            if (config.NpcXpOverrides.TryGetValue(npc.type, out int fixedXp))
                return fixedXp;

            double xp = npc.lifeMax * config.XpPerLifePoint;
            if (RpgPlugin.IsBossPart(npc))
                xp *= config.BossXpMultiplier;
            return xp;
        }

        public static void GiveXp(TSPlayer? tsPlayer, RpgPlayer rpg, long amount)
        {
            var config = RpgPlugin.Config;
            if (amount <= 0 || rpg.Level >= config.MaxLevel) return;

            rpg.Xp += amount;
            rpg.Dirty = true;

            if (tsPlayer != null && config.ShowXpPopup)
            {
                var pos = tsPlayer.TPlayer.Top;
                tsPlayer.SendData(PacketTypes.CreateCombatTextExtended,
                    Translations.Instance.Get("xp.popup", amount), (int)new Color(120, 200, 255).PackedValue, pos.X, pos.Y - 16);
            }

            while (rpg.Level < config.MaxLevel && rpg.Xp >= config.XpToNextLevel(rpg.Level))
            {
                rpg.Xp -= config.XpToNextLevel(rpg.Level);
                rpg.Level++;
                OnLevelUp(tsPlayer, rpg);
            }

            if (rpg.Level >= config.MaxLevel)
                rpg.Xp = 0;
        }

        public static void SetLevel(TSPlayer? tsPlayer, RpgPlayer rpg, int level)
        {
            rpg.Level = Math.Clamp(level, 1, RpgPlugin.Config.MaxLevel);
            rpg.Xp = 0;
            rpg.Dirty = true;
            if (tsPlayer != null)
                ClassSystem.ApplyClassBuffs(tsPlayer, rpg);
        }

        private static void OnLevelUp(TSPlayer? tsPlayer, RpgPlayer rpg)
        {
            if (tsPlayer == null) return;
            var t = Translations.Instance;

            if (RpgPlugin.Config.AnnounceLevelUp)
                TSPlayer.All.SendMessage(t.Get("level.up.broadcast", tsPlayer.Name, rpg.Level), new Color(255, 215, 0));
            else
                tsPlayer.SendSuccessMessage(t.Get("level.up.self", rpg.Level));

            var cls = RpgPlugin.Config.FindClass(rpg.ClassId);
            if (cls != null)
            {
                foreach (var buff in cls.Buffs.Where(b => b.Level == rpg.Level))
                    tsPlayer.SendSuccessMessage(t.Get("level.up.buff", TShock.Utils.GetBuffName(buff.BuffId)));
                foreach (var perk in cls.Perks.Where(p => p.Level == rpg.Level))
                    tsPlayer.SendSuccessMessage(t.Get("level.up.perk", t.Get($"perk.{perk.Id}"), t.Get($"perk.{perk.Id}.desc")));
            }

            ClassSystem.ApplyClassBuffs(tsPlayer, rpg);
        }

        public static void OnPlayerDeath(TSPlayer tsPlayer, RpgPlayer rpg)
        {
            double percent = RpgPlugin.Config.XpLossOnDeathPercent;
            if (percent <= 0 || rpg.Xp <= 0) return;

            long lost = (long)Math.Round(rpg.Xp * percent / 100.0);
            if (lost <= 0) return;

            rpg.Xp -= lost;
            rpg.Dirty = true;
            tsPlayer.SendWarningMessage(Translations.Instance.Get("death.xp_lost", lost));
        }
    }
}
