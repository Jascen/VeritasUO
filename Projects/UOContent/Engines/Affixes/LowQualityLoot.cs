using System;
using Server.Items;

namespace Server.Engines.Affixes;

/// <summary>
/// Weak monsters sometimes drop low quality weapons and armor that new characters can actually wield:
/// half the strength requirement, paid for with less damage/resists and durability.
/// </summary>
public static class LowQualityLoot
{
    /// <summary>Low quality chance is (this - item level)%, so nothing at or above this level drops low quality.</summary>
    public const int FalloffItemLevel = 40;

    public const int StrRequirementPercent = 50;
    public const int DurabilityPenalty = 25;
    public const int ResistPenalty = 1;

    public static int ReduceStrRequirement(int req) => (req * StrRequirementPercent + 99) / 100;

    public static int GetChance(int itemLevel) => Math.Max(0, FalloffItemLevel - itemLevel);

    public static void TryApply(Item item, Mobile from)
    {
        if (from == null || item is not (BaseWeapon or BaseArmor))
        {
            return;
        }

        if (Utility.Random(100) >= GetChance(ItemLevel.Compute(from)))
        {
            return;
        }

        if (item is BaseWeapon weapon)
        {
            weapon.Quality = WeaponQuality.Low;
        }
        else if (item is BaseArmor armor)
        {
            armor.Quality = ArmorQuality.Low;
        }
    }
}
