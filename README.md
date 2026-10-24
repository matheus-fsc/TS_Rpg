<h1 align="center">TSRpg</h1>
<p align="center"><strong>RPG classes, XP and levels for TShock servers, with mobs and bosses that grow with the players.</strong></p>

<p align="center">
  <img src="https://img.shields.io/badge/Terraria-1.4.5.8-green" alt="Terraria 1.4.5.8">
  <img src="https://img.shields.io/badge/TShock-6.2.1-green" alt="TShock 6.2.1">
  <img src="https://img.shields.io/badge/.NET-9.0-blue" alt=".NET 9">
  <img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License">
</p>

---

## Overview

TSRpg adds a light RPG layer on top of vanilla Terraria. It works with the unmodified game client: everything is done server-side.

- **XP and levels.** Killing hostile mobs gives XP based on their max life. Bosses give much more. Critters, town NPCs, statue spawns and target dummies give nothing.
- **Four classes.** Warrior, Mage, Ranger and Summoner. Each one gets permanent buffs, a damage bonus that grows with level for its own weapon type, and on-hit perks.
- **Group play pays off.** Kill XP is shared among everyone who hit the NPC and everyone close to it, with a bonus for each extra player.
- **Scaling difficulty.** New mobs get tougher with the level of the players around them, and bosses with the average level of everyone online. Some mobs spawn as **elites**: they glitter gold, hit harder, and give more XP and coins.

## Installation

1. Download `TSRpg.dll` from the [Releases page](https://github.com/matheus-fsc/TS_Rpg/releases).
2. Place it into your TShock `ServerPlugins` folder.
3. Restart the server. On first launch the plugin creates `tshock/TSRpg/` containing the config, the translation files and the SQLite database.

## Commands

| Command | Permission | Description |
| --- | --- | --- |
| `/class [name]` (`/classe`) | none | Lists the classes or picks one. The first pick is free; switching again has a cooldown. |
| `/rpg` (`/nivel`, `/level`) | none | Shows your level, XP, damage bonus, active buffs and perks, and the next unlock. |
| `/rpgtop` | none | Highest level players. |
| `/rpgadmin <setlevel\|givexp\|setclass\|reset> <player> [value]` | `tsrpg.admin` | Admin tools. |
| `/rpgreload` | `tsrpg.admin` | Reloads the config and translations. |

## Classes

All classes gain **+1.5% damage per level** (up to +73.5% at level 50) on hits with their own weapon type. Summoners also get it while they have a minion or sentry out.

| Level | Warrior (melee) | Mage (magic) | Ranger (ranged) | Summoner (summon) |
| --- | --- | --- | --- | --- |
| 1 | Ironskin | Mana Regeneration | Archery | Summoning |
| 5 | Regeneration | Magic Power | Swiftness | Regeneration |
| 10 | *Lifesteal* 3% | *Frost Touch* (Frostburn) | *Headshot* +100% | *Toxin* (Poisoned) |
| 15 | Endurance | Clairvoyance, *Spectral Mend* (team heal) | Ammo Reservation | Bewitched |
| 20 | *Brutal Strike* +75% | *Arcane Burst* +150% | *Armor Pierce* (Ichor) | *Venom* |
| 25 | Lifeforce | Regeneration | Hunter | Swiftness |
| 30 | *Sunder* (Ichor) | *Shadowflame* | *Incendiary* (Cursed Inferno) | *Pack Instinct* +100% |
| 35 | Titan | Rage | Rage | Ironskin |
| 40 | *Bloodlust* +2% lifesteal | *Spectral Mastery* (more team heal) | *Deadeye* +200% | *Life Bond* 1% lifesteal |
| 45 | Thorns | Wrath | Wrath | Wrath |

*Italic* entries are perks, effects that trigger on hit. The mage's heals go to the most hurt player nearby (the mage included), like the Spectre hood. Buffs, perks, chances and values can all be changed in the config.

## Difficulty scaling

Terraria computes enemy contact damage on the client, so changing an NPC's damage on the server has no effect. TSRpg uses the game's own per-NPC **difficulty** value instead, the same one Journey mode's difficulty slider uses (Classic 1, Expert 2, Master 3, Legendary 4). When an NPC spawns, the plugin raises that value before the NPC is first sent to clients. The client then builds the NPC with the same life and damage as the server, and health bars stay correct.

| Setting | Default | Effect |
| --- | --- | --- |
| `MobDifficultyPerLevel` | `0.015` | Mobs: +0.015 difficulty per level of the nearby players (Master → ~3.74 at level 50). |
| `BossDifficultyBonus` / `BossDifficultyPerLevel` | `0.25` / `0.015` | Bosses: flat bonus plus the average online level. |
| `MaxNpcDifficulty` | `4.0` | Cap for regular mobs and bosses. |
| `EliteChance` / `EliteDifficultyBonus` | `0.04` / `1.0` | Elites: 4% of mobs once players reach level 5; they always may reach 4.0. |

From Master (3) to Legendary (4) vanilla curves give enemies ×1.33 life and ×1.78 contact damage, and they drop more coins. Nothing grows past 4.

## Configuration

File: `tshock/TSRpg/RpgConfig.json`

| Key | Default | Description |
| --- | --- | --- |
| `Language` | `"en-US"` | Translation file from `tshock/TSRpg/Translations/` (`en-US` or `pt-BR` bundled). |
| `XpPerLifePoint` | `0.1` | XP per point of NPC max life. |
| `BossXpMultiplier` / `EliteXpMultiplier` | `3.0` / `4.0` | Extra XP for bosses and elites. |
| `NpcXpOverrides` | `{}` | Fixed XP per NPC id, e.g. `{ "4": 2000 }`. |
| `PartyRangeTiles` | `60` | Players this close to a kill take part in it. |
| `PartyBonusPerPlayer` | `0.1` | +10% kill XP per extra participant. |
| `PartyEvenShare` | `0.3` | 30% of kill XP is split evenly; the rest by damage dealt. |
| `XpLossOnDeathPercent` | `10` | Share of the current level's progress lost on death (never de-levels). |
| `LevelBaseXp` / `LevelExponent` / `MaxLevel` | `100` / `1.5` / `50` | XP to next level = 100 × level^1.5. |
| `ClassChangeCooldownMinutes` | `60` | Cooldown between class changes. |
| `Classes` | see above | Full class definitions: damage type, damage per level, buffs and perks. |
| `ExtraBossNpcIds` | boss parts | NPCs treated as bosses (hands, segments...). |

## License

[MIT](LICENSE)
