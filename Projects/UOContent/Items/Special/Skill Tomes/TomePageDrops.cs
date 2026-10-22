using Server.Engines.Affixes;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Rare tome page drops from tough creatures, scaled by item level.
/// Champions and the Harrower award their pages directly and are excluded here.
/// </summary>
public static class TomePageDrops
{
    // Item level is bard difficulty / 2 + dungeon bonus, so 50 is roughly lich-tier and up.
    public const int MinItemLevel = 50;

    // A dragon in Destard (~ilvl 75) drops one ~1.25% of the time; Doom monsters (ilvl 99) ~2.5%.
    public const double ChancePerItemLevel = 0.0005;

    public const double ParagonMultiplier = 2.0;

    public static double GetChance(BaseCreature creature)
    {
        var excess = ItemLevel.Compute(creature) - MinItemLevel;

        if (excess <= 0)
        {
            return 0.0;
        }

        var chance = excess * ChancePerItemLevel;
        return creature.IsParagon ? chance * ParagonMultiplier : chance;
    }

    public static void TryPack(BaseCreature creature)
    {
        if (creature is BaseChampion or Harrower || creature.Controlled || !IsKilledByPlayer(creature))
        {
            return;
        }

        if (Utility.RandomDouble() < GetChance(creature))
        {
            creature.PackItem(TomePage.CreateRandom());
        }
    }

    private static bool IsKilledByPlayer(BaseCreature creature)
    {
        var killer = creature.LastKiller;

        if (killer is BaseCreature bc)
        {
            killer = bc.GetMaster();
        }

        return killer is PlayerMobile;
    }
}
