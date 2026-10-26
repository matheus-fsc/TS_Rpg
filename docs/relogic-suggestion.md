**Forum:** Game Suggestions — https://forums.terraria.org/index.php?forums/game-suggestions.41/
**Prefix:** Game Mechanics

**Title:** [UI][Multiplayer] Let servers set a display name and tooltip for items in chests they send

---

**What:** An optional way for a server to set the *display* name (and optionally the tooltip) of an item in a chest slot it sends to a client.

**Why:** On multiplayer servers (for example TShock), the chest window is the only UI a server can open on an unmodded client. Server owners use it for menus: class selection, shops, quests and so on. 1.4.5 made this much better with resizable chests and scrolling. Thank you for that!

The remaining problem is text. Each icon shows the vanilla item name, so a "Lifesteal" skill shows up as *Vampire Knives* on hover. Right now a server can only change the chest title or send chat messages, which makes these menus hard to read.

**How it could work:**

- A new optional message, e.g. `ItemDisplayOverride (chest id, slot, name, tooltip)`, or an optional field in the chest item message.
- The client only calls the existing `Item.SetNameOverride` on that slot.
- The override is already cleared whenever the slot changes (`SetDefaults`), so it never sticks to real items.

**Safety:**

- It only applies to chest slots sent by the server, never to items in a player's inventory.
- It could be shown in a different color, or with a small marker, so nobody can pass off a fake "Zenith" in a trade.
- Servers can already show any text in chest titles and chat, so this adds no new kind of risk.

**Compatibility:** Old clients would just ignore it, and nothing changes in single player.

*(Screenshot of the current menu here.)*

---

*Disclaimer: I wrote the original text in Portuguese and used an AI assistant to translate and format it.*
