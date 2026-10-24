using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using static TShockAPI.GetDataHandlers;

namespace TSRpg.Logicas
{
    [ApiVersion(2, 1)]
    public class RpgPlugin : TerrariaPlugin
    {
        public static Config Config { get; internal set; } = new Config();

        // RPG data of each online player, by player index.
        internal static readonly Dictionary<int, RpgPlayer> Players = new();

        private long tick;

        public override string Name        => "TSRpg";
        public override Version Version    => new Version(0, 1);
        public override string Author      => "matheus-fsc";
        public override string Description => "RPG classes, XP and levels, with mobs and bosses that scale with the players.";

        public RpgPlugin(Main game) : base(game) { }

        public override void Initialize()
        {
            Config = Config.Load();
            Translations.Initialize(Config.Language);
            RpgDatabase.InitializeDatabase();

            GetDataHandlers.NPCStrike += OnNpcStrike;
            GetDataHandlers.KillMe += OnKillMe;
            ServerApi.Hooks.NpcKilled.Register(this, Progression.OnNpcKilled);
            ServerApi.Hooks.NpcSpawn.Register(this, OnNpcSpawn);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
            ServerApi.Hooks.WorldSave.Register(this, OnWorldSave);

            Comandos.Register();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SaveAll(onlyDirty: false);

                GetDataHandlers.NPCStrike -= OnNpcStrike;
                GetDataHandlers.KillMe -= OnKillMe;
                ServerApi.Hooks.NpcKilled.Deregister(this, Progression.OnNpcKilled);
                ServerApi.Hooks.NpcSpawn.Deregister(this, OnNpcSpawn);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
                ServerApi.Hooks.WorldSave.Deregister(this, OnWorldSave);
            }
            base.Dispose(disposing);
        }

        internal static bool IsBossPart(NPC npc)
        {
            if (npc.boss || Config.ExtraBossNpcIds.Contains(npc.type)) return true;
            return npc.realLife >= 0 && npc.realLife != npc.whoAmI && Main.npc[npc.realLife].boss;
        }

        // ── Player sessions ──────────────────────────────────────────────────

        private void OnGreetPlayer(GreetPlayerEventArgs args)
        {
            var tsPlayer = TShock.Players[args.Who];
            if (tsPlayer == null) return;

            var rpg = RpgDatabase.Load(tsPlayer.Name);
            Players[args.Who] = rpg;

            var t = Translations.Instance;
            if (rpg.ClassId == null)
                tsPlayer.SendInfoMessage(t.Get("join.choose_class"));
            else
                tsPlayer.SendInfoMessage(t.Get("join.welcome", tsPlayer.Name, rpg.Level, t.ClassName(rpg.ClassId)));
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            if (Players.Remove(args.Who, out var rpg))
                RpgDatabase.Save(rpg);

            Progression.ForgetPlayer(args.Who);
            ClassSystem.ForgetPlayer(args.Who);
        }

        private void OnWorldSave(WorldSaveEventArgs args)
        {
            SaveAll(onlyDirty: true);
        }

        internal static void SaveAll(bool onlyDirty)
        {
            foreach (var rpg in Players.Values.Where(p => !onlyDirty || p.Dirty).ToList())
                RpgDatabase.Save(rpg);
        }

        // ── Combat ───────────────────────────────────────────────────────────

        private void OnNpcStrike(object? sender, NPCStrikeEventArgs args)
        {
            if (args.Player == null || args.ID < 0 || args.ID >= Main.maxNPCs || args.Damage <= 0) return;
            if (!Players.TryGetValue(args.Player.Index, out var rpg)) return;

            // Record the reported hit first so a kill by the class bonus below still credits it.
            Progression.RecordDamage(args.ID, args.Player.Index, args.Damage);

            long extra = ClassSystem.OnPlayerHit(args.Player, rpg, args, tick);
            if (extra > 0)
                Progression.RecordDamage(args.ID, args.Player.Index, extra);
        }

        private void OnNpcSpawn(NpcSpawnEventArgs args)
        {
            // The slot now holds a fresh NPC: forget whatever the previous occupant left behind.
            Progression.Forget(args.NpcId);
            MobScaling.OnNpcSpawn(args);
        }

        private void OnKillMe(object? sender, KillMeEventArgs args)
        {
            if (args.Player != null && Players.TryGetValue(args.Player.Index, out var rpg))
                Progression.OnPlayerDeath(args.Player, rpg);
        }

        // ── Periodic work ────────────────────────────────────────────────────

        private void OnGameUpdate(EventArgs args)
        {
            tick++;

            if (tick % 30 == 0)
                ClassSystem.RefreshMinionOwners();

            if (tick % (Math.Max(1, Config.BuffRefreshSeconds) * 60) == 0)
            {
                foreach (var (index, rpg) in Players)
                {
                    var tsPlayer = TShock.Players[index];
                    if (tsPlayer != null && tsPlayer.Active && !tsPlayer.Dead)
                        ClassSystem.ApplyClassBuffs(tsPlayer, rpg);
                }
            }

            if (tick % 300 == 0)
                MobScaling.RefreshEliteVisuals();

            // Persist progress every two minutes in case of a crash.
            if (tick % (120 * 60) == 0)
                SaveAll(onlyDirty: true);
        }
    }
}
