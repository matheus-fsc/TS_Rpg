"""Generates the README images (light and dark). Usage: python docs/images/generate.py docs/images"""
import sys
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch

OUT = Path(sys.argv[1])
OUT.mkdir(parents=True, exist_ok=True)

THEMES = {
    "light": dict(surface="#fcfcfb", card="#f3f2ee", primary="#0b0b0b", secondary="#52514e",
                  muted="#8a8984", grid="#e4e3de",
                  series=["#2a78d6", "#eb6834", "#1baf7a", "#eda100"]),
    "dark": dict(surface="#1a1a19", card="#242423", primary="#f0efec", secondary="#c3c2b7",
                 muted="#8f8e86", grid="#33332f",
                 series=["#3987e5", "#d95926", "#199e70", "#c98500"]),
}

plt.rcParams["font.family"] = ["Segoe UI", "DejaVu Sans"]

# (level, kind, name) - kind: b = buff, k = perk, p = passive. Same data as the default config.
CLASSES = [
    ("Warrior", "melee", [
        (1, "b", "Ironskin"), (5, "b", "Regeneration"), (5, "p", "Second Wind"),
        (10, "k", "Lifesteal 3%"), (15, "b", "Endurance"), (20, "k", "Brutal Strike +75%"),
        (20, "p", "Berserk"), (25, "b", "Lifeforce"), (30, "k", "Sunder (Ichor)"),
        (35, "b", "Titan"), (35, "p", "Iron Will"), (40, "k", "Bloodlust +2%"), (45, "b", "Thorns")]),
    ("Mage", "magic", [
        (1, "b", "Mana Regeneration"), (1, "p", "Mana Flow"), (5, "b", "Magic Power"),
        (10, "k", "Frost Touch"), (15, "b", "Clairvoyance"), (15, "k", "Spectral Mend"),
        (15, "p", "Arcane Shield"), (20, "k", "Arcane Burst +150%"), (25, "b", "Regeneration"),
        (30, "k", "Shadowflame"), (30, "p", "Mana Tide"), (35, "b", "Rage"),
        (40, "k", "Spectral Mastery"), (45, "b", "Wrath")]),
    ("Ranger", "ranged", [
        (1, "b", "Archery"), (5, "b", "Swiftness"), (10, "k", "Headshot +100%"),
        (10, "p", "Steady Aim"), (15, "b", "Ammo Reservation"), (20, "k", "Armor Pierce (Ichor)"),
        (20, "p", "Survival Instinct"), (25, "b", "Hunter"), (30, "k", "Incendiary"),
        (30, "p", "Field Medic"), (35, "b", "Rage"), (40, "k", "Deadeye +200%"), (45, "b", "Wrath")]),
    ("Summoner", "summon", [
        (1, "b", "Summoning"), (5, "b", "Regeneration"), (5, "p", "Spirit Link"),
        (10, "k", "Toxin (Poisoned)"), (15, "b", "Bewitched"), (20, "k", "Venom"),
        (20, "p", "Guardian"), (25, "b", "Swiftness"), (30, "k", "Pack Instinct +100%"),
        (35, "b", "Ironskin"), (35, "p", "Swarm"), (40, "k", "Life Bond 1%"), (45, "b", "Wrath")]),
]
ROWS = max(len(c[2]) for c in CLASSES)
MARKERS = {"b": ("o", "Buff"), "k": ("D", "Perk (on hit)"), "p": ("^", "Passive")}


def classes_image(name, th):
    fig = plt.figure(figsize=(16, 9.4), dpi=150, facecolor=th["surface"])
    fig.text(0.04, 0.95, "TSRpg classes", fontsize=24, fontweight="bold", color=th["primary"], va="top")
    fig.text(0.04, 0.905, "+1.5% damage per level with the class weapon type, plus buffs, perks and passives",
             fontsize=13, color=th["secondary"], va="top")

    width, gap, left = 0.215, 0.025, 0.04
    for i, (cls, dmg, rows) in enumerate(CLASSES):
        x0 = left + i * (width + gap)
        ax = fig.add_axes([x0, 0.1, width, 0.76])
        ax.set_xlim(0, 1)
        ax.set_ylim(ROWS + 2.2, -0.3)
        ax.axis("off")
        ax.add_patch(FancyBboxPatch((0, -0.3), 1, ROWS + 2.5, boxstyle="round,pad=0,rounding_size=0.04",
                                    transform=ax.transData, facecolor=th["card"], edgecolor="none",
                                    mutation_aspect=12))
        color = th["series"][i]
        ax.add_patch(plt.Rectangle((0, -0.3), 1, 0.22, facecolor=color, edgecolor="none"))
        ax.text(0.07, 0.75, cls, fontsize=18, fontweight="bold", color=th["primary"], va="center")
        ax.text(0.93, 0.75, dmg, fontsize=12, color=th["secondary"], va="center", ha="right")
        for r, (lvl, kind, label) in enumerate(rows):
            y = r + 2
            ax.text(0.07, y, f"Lv {lvl}", fontsize=11, color=th["muted"], va="center")
            ax.plot(0.27, y, marker=MARKERS[kind][0], markersize={"b": 7, "k": 7, "p": 9}[kind], color=color,
                    markeredgewidth=0)
            ax.text(0.34, y, label, fontsize=12, color=th["primary"], va="center",
                    fontweight="semibold" if kind == "k" else "normal")

    lx = 0.04
    for kind in ("b", "k", "p"):
        marker, label = MARKERS[kind]
        fig.lines.append(plt.Line2D([lx + 0.006], [0.05], marker=marker, markersize=8, color=th["secondary"],
                                    transform=fig.transFigure, linestyle="none"))
        fig.text(lx + 0.016, 0.05, label, fontsize=12, color=th["secondary"], va="center")
        lx += 0.13
    fig.savefig(OUT / f"classes-{name}.png", facecolor=th["surface"])
    plt.close(fig)


def xp_image(name, th):
    levels = list(range(1, 50))
    need = [100 * lvl ** 1.5 for lvl in levels]
    fig, ax = plt.subplots(figsize=(12, 5.2), dpi=150, facecolor=th["surface"])
    fig.subplots_adjust(left=0.09, right=0.97, top=0.8, bottom=0.14)
    ax.set_facecolor(th["surface"])
    color = th["series"][0]
    ax.plot(levels, need, color=color, linewidth=2, solid_capstyle="round")
    for lvl, label in [(10, "Specialization"), (25, "Tier 2"), (40, "Tier 3")]:
        y = 100 * lvl ** 1.5
        ax.plot(lvl, y, marker="o", markersize=8, color=color, markeredgecolor=th["surface"], markeredgewidth=2)
        ax.annotate(f"Lv {lvl} · {label}\n{y:,.0f} XP", (lvl, y), xytext=(-12, 14), textcoords="offset points",
                    ha="right", fontsize=11, color=th["primary"])
    ax.set_xlim(0, 50)
    ax.set_ylim(0, 37000)
    ax.grid(axis="y", color=th["grid"], linewidth=1)
    ax.set_axisbelow(True)
    for side in ("top", "right", "left"):
        ax.spines[side].set_visible(False)
    ax.spines["bottom"].set_color(th["grid"])
    ax.tick_params(colors=th["secondary"], labelsize=11, length=0)
    ax.yaxis.set_major_formatter(matplotlib.ticker.FuncFormatter(lambda v, _: (f"{v / 1000:.0f}k" if v else "0")))
    ax.set_xlabel("Level", color=th["secondary"], fontsize=12)
    fig.text(0.09, 0.93, "XP needed for the next level", fontsize=20, fontweight="bold", color=th["primary"])
    fig.text(0.09, 0.86, "100 × level^1.5 · max level 50 · kill XP = NPC max life × 0.1 (bosses ×3, elites ×4)",
             fontsize=12, color=th["secondary"])
    fig.savefig(OUT / f"xp-curve-{name}.png", facecolor=th["surface"])
    plt.close(fig)


for theme_name, theme in THEMES.items():
    classes_image(theme_name, theme)
    xp_image(theme_name, theme)
print("ok", sorted(p.name for p in OUT.iterdir()))
