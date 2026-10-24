using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace TSRpg.Logicas
{
    /// <summary>
    /// Makes hostile NPCs tougher as players level up.
    ///
    /// Instead of editing life/damage directly (contact damage is computed client-side and the
    /// client keeps its own lifeMax), each NPC gets its own vanilla "difficulty" value, the same one
    /// Journey's difficulty slider uses. The NPC is re-initialised with it in the NpcSpawn hook,
    /// which runs before the NPC is first sent to clients, so the client builds it with the same
    /// difficulty and life, damage and health bars match on both sides.
    /// </summary>
    public static class MobScaling
    {
        // Vanilla stat curves stop growing past Legendary (4.0).
        private const float MaxVanillaDifficulty = 4f;

        private static readonly HashSet<int> elites = new();
        private static readonly Random random = new();

        public static bool IsElite(int npcIndex) => elites.Contains(npcIndex);

        public static void OnNpcSpawn(NpcSpawnEventArgs args)
        {
            int index = args.NpcId;
            elites.Remove(index);

            if (index < 0 || index >= Main.maxNPCs) return;
            var npc = Main.npc[index];
            var config = RpgPlugin.Config;

            bool boss = RpgPlugin.IsBossPart(npc);
            if (!IsScalable(npc)) return;
            if (boss ? !config.ScaleBosses : !config.ScaleMobs) return;

            float world = Main.Difficulty;
            float bonus;
            if (boss)
            {
                double level = AverageOnlineLevel();
                bonus = (float)(config.BossDifficultyBonus + config.BossDifficultyPerLevel * (level - 1));
            }
            else
            {
                double level = AverageLevelAround(npc.Center);
                bonus = (float)(config.MobDifficultyPerLevel * (level - 1));

                // Only the head of a worm rolls for elite; its segments are separate NPCs.
                if (level >= config.EliteMinLevel && npc.realLife < 0 && random.NextDouble() < config.EliteChance)
                {
                    bonus += (float)config.EliteDifficultyBonus;
                    elites.Add(index);
                }
            }

            // Elites may always reach the top of the vanilla curve; everything else respects the config cap.
            float cap = elites.Contains(index) ? MaxVanillaDifficulty : Math.Max(world, (float)config.MaxNpcDifficulty);
            float difficulty = Math.Min(cap, world + Math.Max(0f, bonus));
            if (difficulty <= world + 0.001f) return;

            Reinitialize(npc, difficulty);

            if (elites.Contains(index))
            {
                npc.AddBuff(BuffID.Midas, 60 * 60);
                TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended,
                    Translations.Instance.Get("elite.spawn"), (int)new Color(255, 200, 50).PackedValue, npc.Center.X, npc.Top.Y);
            }
        }

        public static void OnNpcGone(int npcIndex) => elites.Remove(npcIndex);

        /// <summary>Keeps elites glittering: Midas also makes them drop more coins.</summary>
        public static void RefreshEliteVisuals()
        {
            foreach (int index in elites.ToList())
            {
                var npc = Main.npc[index];
                if (!npc.active)
                {
                    elites.Remove(index);
                    continue;
                }
                npc.AddBuff(BuffID.Midas, 60 * 60);
                NetMessage.SendData((int)PacketTypes.NpcAddBuff, -1, -1, null, index, BuffID.Midas, 60 * 60);
            }
        }

        private static bool IsScalable(NPC npc)
        {
            return npc.active
                   && !npc.friendly
                   && !npc.townNPC
                   && npc.lifeMax > 5
                   && npc.damage > 0
                   && !NPCID.Sets.CountsAsCritter[npc.type]
                   && npc.type != NPCID.TargetDummy;
        }

        /// <summary>Runs SetDefaults again with a custom difficulty, keeping spawn position and AI arguments.</summary>
        private static void Reinitialize(NPC npc, float difficulty)
        {
            var bottom = npc.Bottom;
            var ai = (float[])npc.ai.Clone();
            int target = npc.target;
            int timeLeft = npc.timeLeft;

            npc.SetDefaults(npc.type, new NPCSpawnParams { difficultyOverride = difficulty });

            npc.Bottom = bottom;
            Array.Copy(ai, npc.ai, ai.Length);
            npc.target = target;
            npc.timeLeft = timeLeft;
            npc.active = true;
        }

        private static double AverageLevelAround(Vector2 position)
        {
            float range = RpgPlugin.Config.ScalingRangeTiles * 16f;
            var levels = RpgPlugin.Players
                .Where(kv => Main.player[kv.Key].active && Vector2.Distance(Main.player[kv.Key].Center, position) <= range)
                .Select(kv => kv.Value.Level)
                .ToList();
            return levels.Count > 0 ? levels.Average() : 1;
        }

        private static double AverageOnlineLevel()
        {
            var levels = RpgPlugin.Players
                .Where(kv => Main.player[kv.Key].active)
                .Select(kv => kv.Value.Level)
                .ToList();
            return levels.Count > 0 ? levels.Average() : 1;
        }
    }
}
