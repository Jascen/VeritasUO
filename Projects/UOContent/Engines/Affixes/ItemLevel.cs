using System;
using Server.Items;

namespace Server.Engines.Affixes;

public static class ItemLevel
{
    public const int Min = 1;
    public const int Max = 99;

    /// <summary>
    /// Bard difficulty (0-160 under SE, paragons +40) halved, plus the dungeon's bonus.
    /// </summary>
    public static int Compute(Mobile creature)
    {
        if (creature == null)
        {
            return Min;
        }

        var monster = (int)Math.Round(BaseInstrument.GetBaseDifficulty(creature) / 2.0);
        return Compute(monster, DungeonDifficulty.GetBonus(creature.Region));
    }

    public static int Compute(int monsterLevel, int dungeonBonus) =>
        Math.Clamp(monsterLevel + dungeonBonus, Min, Max);
}

public static class DungeonDifficulty
{
    // Regions not listed add nothing.
    private static readonly (string Region, int Bonus)[] _bonuses =
    [
        ("Shame", 5),
        ("Deceit", 5),
        ("Wrong", 5),
        ("The Painted Caves", 5),
        ("Destard", 10),
        ("Hythloth", 10),
        ("Ice", 10),
        ("Sanctuary", 10),
        ("Blighted Grove", 10),
        ("Fire", 15),
        ("Terathan Keep", 15),
        ("Khaldun", 15),
        ("The Prism of Light", 15),
        ("Labyrinth", 20),
        ("The Citadel", 20),
        ("The Palace of Paroxysmus", 20),
        ("Doom", 25)
    ];

    public static int GetBonus(Region region)
    {
        if (region == null)
        {
            return 0;
        }

        var best = 0;

        foreach (var (name, bonus) in _bonuses)
        {
            if (bonus > best && region.IsPartOf(name))
            {
                best = bonus;
            }
        }

        return best;
    }
}
