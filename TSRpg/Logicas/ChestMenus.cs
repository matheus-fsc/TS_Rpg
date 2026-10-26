using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using static TShockAPI.GetDataHandlers;

namespace TSRpg.Logicas
{
    /// <summary>One clickable icon in a chest menu.</summary>
    public class MenuIcon
    {
        public int ItemId { get; set; }
        /// <summary>Shown as the number in the slot corner when above 1 (ranks, levels).</summary>
        public int Stack { get; set; } = 1;
        /// <summary>
        /// Shown as the window title when the icon is clicked. Items keep their vanilla names
        /// (packet 32 carries no name), so the title is the only text the menu can change.
        /// </summary>
        public string? Label { get; set; }
        public Action<TSPlayer, int>? OnClick { get; set; }
    }

    public class ChestMenu
    {
        /// <summary>Chest name, shown above the slots. Keep it short: long names run over the window buttons.</summary>
        public string Title { get; set; } = "";
        /// <summary>Slot count. Above 40 the chest window scrolls with the mouse wheel (Terraria 1.4.5+).</summary>
        public int Size { get; }
        public MenuIcon?[] Slots { get; }

        public ChestMenu(int size = ChestMenus.VisibleSlots)
        {
            Size = Math.Clamp(size, 1, ChestMenus.MaxSlots);
            Slots = new MenuIcon?[Size];
        }
    }

    /// <summary>
    /// Menus drawn with the vanilla chest window, for server side characters (SSC) only.
    ///
    /// The server never creates a chest. Only the target client is told that a chest exists:
    /// a chest tile is sent to that client alone next to its player (the client closes chest
    /// windows that are not near a chest tile). The tile is echo coated, so the player does not
    /// see it, and gets fake ground under it when the spot has none. Packet 69 creates the client-side chest,
    /// packet 155 sizes it, packet 32 fills the slots and packet 33 opens the window.
    ///
    /// A click is the client reporting that a slot changed. The change is refused, the icon is
    /// sent back, and every inventory slot the icon (or an item the player dropped on the menu)
    /// lands in is reverted to the server copy. Clients only accept their own inventory from the
    /// server with SSC on, which is why the menu needs it.
    /// </summary>
    public static class ChestMenus
    {
        public const int VisibleSlots = 40;
        // Packet 32 sends the slot as a byte.
        public const int MaxSlots = 250;

        // Item types involved in a click stay guarded this long, so late inventory packets are still reverted.
        private const int GuardTicks = 180;
        // Loot All and Deposit All report every slot in the same frame: only the first counts as a click.
        private const int ClickCooldownTicks = 10;
        // Wooden chest frames.
        private const int ChestStyle = 0;
        // The 2x2 chest plus the row under it, which the client needs solid or the chest breaks.
        private const int SpotHeight = 3;

        private class Session
        {
            public ChestMenu Menu = null!;
            public int ChestId;
            public int X;
            public int Y;
            public long LastClickTick = -ClickCooldownTicks;
        }

        private static readonly Dictionary<int, Session> sessions = new();
        // Per player: item type -> tick until which inventory changes involving it are reverted.
        private static readonly Dictionary<int, Dictionary<int, long>> guarded = new();
        private static long tick;

        public static void Register(TerrariaPlugin plugin)
        {
            // Highest runs before TShock's Bouncer, which would reject packets for a chest the server does not know.
            GetDataHandlers.ChestItemChange.Register(OnChestItemChange, HandlerPriority.Highest);
            GetDataHandlers.PlayerSlot.Register(OnPlayerSlot, HandlerPriority.Highest);
            GetDataHandlers.ItemDrop.Register(OnItemDrop, HandlerPriority.Highest);
            // Above TShock's own NetGetData (order 0): the server must never adopt the client-only chest id.
            ServerApi.Hooks.NetGetData.Register(plugin, OnGetData, 10);
        }

        public static void Deregister(TerrariaPlugin plugin)
        {
            foreach (int index in sessions.Keys.ToList())
                Close(index, notifyClient: true);

            GetDataHandlers.ChestItemChange.UnRegister(OnChestItemChange);
            GetDataHandlers.PlayerSlot.UnRegister(OnPlayerSlot);
            GetDataHandlers.ItemDrop.UnRegister(OnItemDrop);
            ServerApi.Hooks.NetGetData.Deregister(plugin, OnGetData);
        }

        public static bool IsAvailable(TSPlayer player) =>
            Main.ServerSideCharacter && player.IsLoggedIn && !player.HasPermission(Permissions.bypassssc);

        public static bool IsOpen(int playerIndex) => sessions.ContainsKey(playerIndex);

        /// <summary>Changes the window title of the open menu without resending the slots.</summary>
        public static void SetTitle(TSPlayer player, string title)
        {
            if (sessions.TryGetValue(player.Index, out var session))
                SendName(player, session, title);
        }

        /// <summary>Opens the menu, or redraws it in place when this player already has one open.</summary>
        public static bool Open(TSPlayer player, ChestMenu menu)
        {
            if (sessions.TryGetValue(player.Index, out var open))
            {
                open.Menu = menu;
                SendContents(player, open);
                return true;
            }

            if (!FindChestSpot(player.TPlayer, out int x, out int y))
                return false;

            int chestId = FreeChestId();
            if (chestId < 0)
                return false;

            var session = new Session { Menu = menu, ChestId = chestId, X = x, Y = y };
            sessions[player.Index] = session;

            SendFakeChestTiles(player.Index, x, y);
            SendContents(player, session);
            player.SendRawData(Packet(PacketTypes.ChestOpen, w =>
            {
                w.Write((short)chestId);
                w.Write((short)x);
                w.Write((short)y);
                w.Write((byte)0);
            }));
            return true;
        }

        public static void Close(int playerIndex, bool notifyClient)
        {
            if (!sessions.Remove(playerIndex, out var session))
                return;

            var player = TShock.Players[playerIndex];
            if (player == null || !player.Active)
                return;

            if (notifyClient)
                player.SendRawData(Packet(PacketTypes.ChestOpen, w =>
                {
                    w.Write((short)-1);
                    w.Write((short)0);
                    w.Write((short)0);
                    w.Write((byte)0);
                }));

            // Give the client back the real tiles where the fake chest stood.
            NetMessage.SendTileSquare(playerIndex, session.X, session.Y, 2, SpotHeight);
        }

        public static void ForgetPlayer(int playerIndex)
        {
            sessions.Remove(playerIndex);
            guarded.Remove(playerIndex);
        }

        /// <summary>Called every frame.</summary>
        public static void Update()
        {
            tick++;
            if (tick % 30 != 0) return;

            foreach (var (index, session) in sessions.ToList())
            {
                var player = TShock.Players[index];
                if (player == null || !player.Active || player.Dead)
                {
                    Close(index, notifyClient: true);
                    continue;
                }

                // The client closes the window by itself when its player walks away; this covers a lost packet.
                var chestCenter = new Microsoft.Xna.Framework.Vector2(session.X * 16 + 16, session.Y * 16 + 16);
                if (Microsoft.Xna.Framework.Vector2.Distance(player.TPlayer.Center, chestCenter) > 16 * 12)
                    Close(index, notifyClient: true);
            }

            foreach (var types in guarded.Values)
            {
                foreach (var (type, until) in types.ToList())
                    if (until < tick) types.Remove(type);
            }
        }

        // ── Packets from the client ──────────────────────────────────────────

        private static void OnChestItemChange(object? sender, ChestItemEventArgs args)
        {
            if (args.Player == null || !sessions.TryGetValue(args.Player.Index, out var session) || args.ID != session.ChestId)
                return;

            args.Handled = true;
            if (args.Slot >= session.Menu.Size)
                return;

            var icon = session.Menu.Slots[args.Slot];
            int iconType = icon?.ItemId ?? 0;
            int iconStack = iconType > 0 ? icon!.Stack : 0;
            if (args.Type == iconType && args.Stacks == iconStack)
                return;

            // The icon went to the cursor or the inventory, and whatever the player put here left it.
            Guard(args.Player.Index, iconType);
            Guard(args.Player.Index, args.Type);
            SendSlot(args.Player, session, args.Slot);
            // Slot 58 is the mouse cursor: empty it now instead of waiting for the client to report it.
            args.Player.SendData(PacketTypes.PlayerSlot, "", args.Player.Index, 58);

            if (icon?.OnClick == null || tick - session.LastClickTick < ClickCooldownTicks)
                return;

            session.LastClickTick = tick;
            // Before OnClick, so the handler can still set a title of its own.
            if (icon.Label != null)
                SendName(args.Player, session, icon.Label);
            icon.OnClick(args.Player, args.Slot);
        }

        private static void OnPlayerSlot(object? sender, PlayerSlotEventArgs args)
        {
            if (args.Player == null || args.PlayerId != args.Player.Index) return;
            if (!guarded.TryGetValue(args.Player.Index, out var types) || types.Count == 0) return;
            if (args.Slot < 0 || args.Slot >= PlayerItemSlotID.Count) return;

            var current = new PlayerItemSlotID.SlotReference(args.Player.TPlayer, args.Slot).Item;
            int currentType = current?.type ?? 0;
            int currentStack = current?.stack ?? 0;
            if (args.Type == currentType && args.Stack == currentStack) return;
            if (!types.ContainsKey(args.Type) && !types.ContainsKey(currentType)) return;

            // Refuse the change and send the server copy back to the client.
            args.Handled = true;
            args.Player.SendData(PacketTypes.PlayerSlot, "", args.Player.Index, args.Slot);
        }

        private static void OnItemDrop(object? sender, ItemDropEventArgs args)
        {
            if (args.Player == null) return;
            if (!guarded.TryGetValue(args.Player.Index, out var types) || !types.ContainsKey(args.Type)) return;

            // An icon thrown out of the window: never let it reach the world, and remove the client's copy.
            args.Handled = true;
            if (args.ID >= 0 && args.ID < Main.item.Length)
                args.Player.SendData(PacketTypes.ItemDrop, "", args.ID);
        }

        private static void OnGetData(GetDataEventArgs args)
        {
            if (args.Handled || args.MsgID != PacketTypes.ChestOpen) return;

            int index = args.Msg.whoAmI;
            if (!sessions.TryGetValue(index, out var session)) return;

            short chestId = BitConverter.ToInt16(args.Msg.readBuffer, args.Index);
            if (chestId == session.ChestId)
            {
                // The client confirming the open (or renaming the chest).
                args.Handled = true;
                return;
            }

            // Closed, or switched to another chest: the packet goes on to TShock as usual.
            Close(index, notifyClient: false);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void Guard(int playerIndex, int itemType)
        {
            if (itemType <= 0) return;
            if (!guarded.TryGetValue(playerIndex, out var types))
                guarded[playerIndex] = types = new Dictionary<int, long>();
            types[itemType] = tick + GuardTicks;
        }

        private static void SendContents(TSPlayer player, Session session)
        {
            var menu = session.Menu;
            SendName(player, session, menu.Title);
            player.SendRawData(Packet((PacketTypes)155, w =>
            {
                w.Write((short)session.ChestId);
                w.Write((short)menu.Size);
            }));
            for (int slot = 0; slot < menu.Size; slot++)
                SendSlot(player, session, slot);
        }

        /// <summary>Creates the chest on the client if needed, and sets its name. The open window shows the new name at once.</summary>
        private static void SendName(TSPlayer player, Session session, string name)
        {
            player.SendRawData(Packet(PacketTypes.ChestName, w =>
            {
                w.Write((short)session.ChestId);
                w.Write((short)session.X);
                w.Write((short)session.Y);
                w.Write(name);
            }));
        }

        private static void SendSlot(TSPlayer player, Session session, int slot)
        {
            var icon = session.Menu.Slots[slot];
            player.SendRawData(Packet(PacketTypes.ChestItem, w =>
            {
                w.Write((short)session.ChestId);
                w.Write((byte)slot);
                w.Write((short)(icon?.ItemId > 0 ? icon.Stack : 0));
                w.Write((byte)0);
                w.Write((short)(icon?.ItemId ?? 0));
            }));
        }

        /// <summary>
        /// Sends an invisible chest tile to one client only: the server tiles are swapped in just long
        /// enough to build the tile packet, then restored. Ground the spot lacks is sent as invisible stone.
        /// </summary>
        private static void SendFakeChestTiles(int playerIndex, int x, int y)
        {
            var saved = new Tile[2 * SpotHeight];
            for (int i = 0; i < saved.Length; i++)
            {
                var tile = Main.tile[x + i % 2, y + i / 2];
                saved[i] = new Tile();
                saved[i].CopyFrom(tile);
            }

            for (int i = 0; i < saved.Length; i++)
            {
                int tx = x + i % 2, ty = y + i / 2;
                var tile = Main.tile[tx, ty];
                if (i < 4)
                {
                    tile.ClearTile();
                    tile.liquid = 0;
                    tile.active(true);
                    tile.type = TileID.Containers;
                    tile.frameX = (short)(ChestStyle * 36 + i % 2 * 18);
                    tile.frameY = (short)(i / 2 * 18);
                }
                else if (!WorldGen.SolidTileAllowBottomSlope(tx, ty))
                {
                    tile.ClearTile();
                    tile.liquid = 0;
                    tile.active(true);
                    tile.type = TileID.Stone;
                }
                else continue;
                // Echo coating: the player only sees it with echo sight.
                tile.invisibleBlock(true);
            }

            NetMessage.SendTileSquare(playerIndex, x, y, 2, SpotHeight);

            for (int i = 0; i < saved.Length; i++)
                Main.tile[x + i % 2, y + i / 2].CopyFrom(saved[i]);
        }

        /// <summary>
        /// Top-left tile of a 2x2 spot within reach of the player. A free spot on solid ground is
        /// best, then any free spot; failing both, the spot where the player stands is used, since
        /// the client only sees fake tiles there until the menu closes.
        /// </summary>
        private static bool FindChestSpot(Player player, out int x, out int y)
        {
            int px = (int)(player.Center.X / 16);
            int feet = (int)((player.position.Y + player.height) / 16);

            foreach (bool needGround in new[] { true, false })
            {
                foreach (int dy in new[] { 0, -1, 1 })
                {
                    foreach (int dx in new[] { 0, -1, 1, -2, 2, -3, 3 })
                    {
                        x = px + dx;
                        y = feet - 2 + dy;
                        if (IsFreeSpot(x, y, needGround)) return true;
                    }
                }
            }

            x = px;
            y = feet - 2;
            return WorldGen.InWorld(x, y, 10);
        }

        private static bool IsFreeSpot(int x, int y, bool needGround)
        {
            if (!WorldGen.InWorld(x, y, 10)) return false;

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    var tile = Main.tile[x + i, y + j];
                    if (tile.active() || tile.liquid > 0) return false;
                }

                // Unsupported chests break as soon as the client frames them, unless fake ground is sent.
                if (needGround && !WorldGen.SolidTileAllowBottomSlope(x + i, y + 2)) return false;
            }
            return true;
        }

        /// <summary>A chest id the server does not use, picked from the top so new world chests rarely take it.</summary>
        private static int FreeChestId()
        {
            for (int id = Main.chest.Length - 1; id > 0; id--)
            {
                if (Main.chest[id] == null && sessions.Values.All(s => s.ChestId != id))
                    return id;
            }
            return -1;
        }

        private static byte[] Packet(PacketTypes type, Action<BinaryWriter> body)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((ushort)0);
                writer.Write((byte)type);
                body(writer);
                writer.Seek(0, SeekOrigin.Begin);
                writer.Write((ushort)stream.Length);
            }
            return stream.ToArray();
        }
    }
}
