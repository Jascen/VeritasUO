using System;
using Server.Text;
using Server.Items;

namespace Server.Engines.FoodBuffs;

public enum FoodBuffSlot
{
    Meal,
    Drink
}

/// <summary>
/// Immutable description of the bonuses a crafted food or drink grants at Regular quality.
/// Quality scales every value via <see cref="Scale"/>; negative values are drawbacks and scale too.
/// </summary>
public sealed class FoodBuffProfile
{
    private readonly string[] _descriptions = new string[3];

    public FoodBuffSlot Slot { get; init; }

    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(30);

    public (AosAttribute Attribute, int Value)[] Attributes { get; init; } = [];

    public (AosWeaponAttribute Attribute, int Value)[] WeaponAttributes { get; init; } = [];

    public int Str { get; init; }

    public int Dex { get; init; }

    public int Int { get; init; }

    public static int Scale(int value, FoodQuality quality)
    {
        if (value == 0)
        {
            return 0;
        }

        var scaled = quality switch
        {
            FoodQuality.Low         => (int)Math.Round(value * 0.5, MidpointRounding.AwayFromZero),
            FoodQuality.Exceptional => (int)Math.Round(value * 1.5, MidpointRounding.AwayFromZero),
            _                       => value
        };

        // A bonus never rounds away entirely.
        return scaled == 0 ? Math.Sign(value) : scaled;
    }

    /// <summary>
    /// Human-readable bonus list for tooltips and the buff bar, cached per quality.
    /// </summary>
    public string GetDescription(FoodQuality quality)
    {
        var index = (int)quality;
        return _descriptions[index] ??= BuildDescription(quality);
    }

    private string BuildDescription(FoodQuality quality)
    {
        var sb = ValueStringBuilder.Create();

        try
        {
            Append(ref sb, Scale(Str, quality), "Str", false);
            Append(ref sb, Scale(Dex, quality), "Dex", false);
            Append(ref sb, Scale(Int, quality), "Int", false);

            for (var i = 0; i < Attributes.Length; i++)
            {
                var (attr, value) = Attributes[i];
                Append(ref sb, Scale(value, quality), GetLabel(attr), IsPercent(attr));
            }

            for (var i = 0; i < WeaponAttributes.Length; i++)
            {
                var (attr, value) = WeaponAttributes[i];
                Append(ref sb, Scale(value, quality), GetLabel(attr), true);
            }

            return sb.ToString();
        }
        finally
        {
            sb.Dispose();
        }
    }

    private static void Append(ref ValueStringBuilder sb, int value, string label, bool percent)
    {
        if (value == 0)
        {
            return;
        }

        if (sb.Length > 0)
        {
            sb.Append(", ");
        }

        sb.Append(value > 0 ? '+' : '-');
        sb.Append(Math.Abs(value));

        if (percent)
        {
            sb.Append('%');
        }

        sb.Append(' ');
        sb.Append(label);
    }

    private static bool IsPercent(AosAttribute attr) =>
        attr is not (AosAttribute.RegenHits or AosAttribute.RegenStam or AosAttribute.RegenMana
            or AosAttribute.CastSpeed or AosAttribute.CastRecovery or AosAttribute.Luck);

    private static string GetLabel(AosAttribute attr) =>
        attr switch
        {
            AosAttribute.RegenHits          => "Hit Point Regeneration",
            AosAttribute.RegenStam          => "Stamina Regeneration",
            AosAttribute.RegenMana          => "Mana Regeneration",
            AosAttribute.DefendChance       => "Defense Chance",
            AosAttribute.AttackChance       => "Hit Chance",
            AosAttribute.WeaponDamage       => "Damage Increase",
            AosAttribute.WeaponSpeed        => "Swing Speed",
            AosAttribute.SpellDamage        => "Spell Damage",
            AosAttribute.CastRecovery       => "Faster Cast Recovery",
            AosAttribute.CastSpeed          => "Faster Casting",
            AosAttribute.LowerManaCost      => "Lower Mana Cost",
            AosAttribute.LowerRegCost       => "Lower Reagent Cost",
            AosAttribute.Luck               => "Luck",
            _                               => attr.ToString()
        };

    private static string GetLabel(AosWeaponAttribute attr) =>
        attr switch
        {
            AosWeaponAttribute.HitFireArea    => "Hit Fire Area",
            AosWeaponAttribute.HitColdArea    => "Hit Cold Area",
            AosWeaponAttribute.HitPoisonArea  => "Hit Poison Area",
            AosWeaponAttribute.HitEnergyArea  => "Hit Energy Area",
            AosWeaponAttribute.HitMagicArrow  => "Hit Magic Arrow",
            AosWeaponAttribute.HitHarm        => "Hit Harm",
            AosWeaponAttribute.HitFireball    => "Hit Fireball",
            AosWeaponAttribute.HitLightning   => "Hit Lightning",
            AosWeaponAttribute.HitLeechHits   => "Hit Life Leech",
            AosWeaponAttribute.HitLeechStam   => "Hit Stamina Leech",
            AosWeaponAttribute.HitLeechMana   => "Hit Mana Leech",
            AosWeaponAttribute.HitLowerAttack => "Hit Lower Attack",
            AosWeaponAttribute.HitLowerDefend => "Hit Lower Defense",
            _                                 => attr.ToString()
        };
}
