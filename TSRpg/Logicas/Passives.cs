using Terraria;
using Terraria.ID;
using TShockAPI;

namespace TSRpg.Logicas
{
    /// <summary>
    /// Always-on class effects, evaluated once per second: regeneration in and out of combat,
    /// buffs while at low life or standing still. Also tracks who is in combat.
    /// </summary>
    public static class Passives
    {
        private static readonly Random random = new();

        // Last game tick each player hit something or got hurt.
        private static readonly Dictionary<int, long> lastCombat = new();

        // Last game tick each player got a mana refund, to keep stars from piling up.
        private static readonly Dictionary<int, long> lastManaRefund = new();
        private const int ManaRefundCooldownTicks = 120;

        // Healing builds up and is applied every few seconds: each server heal shows a green
        // number over the player, and one per second would be noisy.
        private static readonly Dictionary<int, int> pendingHeal = new();
        private const int HealFlushSeconds = 3;

        public static void MarkCombat(int playerIndex, long tick) => lastCombat[playerIndex] = tick;

        public static void ForgetPlayer(int playerIndex)
        {
            lastCombat.Remove(playerIndex);
            lastManaRefund.Remove(playerIndex);
            pendingHeal.Remove(playerIndex);
        }

        public static bool InCombat(int playerIndex, long tick)
        {
            return lastCombat.TryGetValue(playerIndex, out long last)
                   && tick - last < RpgPlugin.Config.OutOfCombatSeconds * 60;
        }

        /// <summary>Runs every second for each living player with a class.</summary>
        public static void Tick(TSPlayer tsPlayer, RpgPlayer rpg, ClassDefinition cls, long tick)
        {
            var p = tsPlayer.TPlayer;
            bool inCombat = InCombat(tsPlayer.Index, tick);
            long second = tick / 60;
            int heal = 0;

            if (!inCombat)
                heal += RpgPlugin.Config.RestRegenPerSecond;

            foreach (var passive in (cls.Passives ?? new()).Where(x => x.Level <= rpg.Level))
            {
                bool due = second % Math.Max(1, passive.IntervalSeconds) == 0;
                switch (passive.Type)
                {
                    case "combat_regen" when inCombat && due:
                    case "rest_regen" when !inCombat && due:
                        heal += (int)passive.Value;
                        break;

                    case "minion_regen" when due:
                        heal += (int)(passive.Value * ClassSystem.MinionCount(tsPlayer.Index));
                        break;

                    case "low_life_buff" when p.statLifeMax2 > 0 && (double)p.statLife / p.statLifeMax2 <= passive.Threshold:
                    case "idle_buff" when p.velocity.LengthSquared() < 0.01f:
                        foreach (int buff in passive.BuffIds)
                            tsPlayer.SetBuff(buff, 3 * 60, true);
                        break;
                }
            }

            pendingHeal.TryGetValue(tsPlayer.Index, out int pending);
            pending += heal;

            if (p.statLife >= p.statLifeMax2)
                pending = 0;
            else if (pending > 0 && second % HealFlushSeconds == 0)
            {
                tsPlayer.Heal(Math.Min(pending, p.statLifeMax2 - p.statLife));
                pending = 0;
            }
            pendingHeal[tsPlayer.Index] = pending;
        }

        /// <summary>Called on each hit with the class weapon type.</summary>
        public static void OnClassHit(TSPlayer tsPlayer, RpgPlayer rpg, ClassDefinition cls, long tick)
        {
            var p = tsPlayer.TPlayer;
            if (p.statMana >= p.statManaMax2) return;
            if (lastManaRefund.TryGetValue(tsPlayer.Index, out long last) && tick - last < ManaRefundCooldownTicks) return;

            // Each mana_refund passive rolls on its own, so higher levels refund more often.
            foreach (var passive in (cls.Passives ?? new()).Where(x => x.Type == "mana_refund" && x.Level <= rpg.Level))
            {
                if (random.NextDouble() >= passive.Chance) continue;

                // The client owns its mana and ignores server updates to it; a mana star
                // dropped on the player is picked up at once and restores mana the vanilla way.
                tsPlayer.GiveItem(ItemID.Star, 1);
                lastManaRefund[tsPlayer.Index] = tick;
                break;
            }
        }
    }
}
