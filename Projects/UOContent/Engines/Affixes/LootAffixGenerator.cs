using System;
using Server.Items;

namespace Server.Engines.Affixes;

public interface ILootAffixItem
{
    LootAffixes LootAffixes { get; set; }
}

public static class LootAffixGenerator
{
    public const int MaxAffixesPerSlot = 3;
    public const int MaxResistBonus = 15;
    public const double MaxRareChance = 0.75;

    private const int MaxAffixes = MaxAffixesPerSlot * 2;
    private const int SkillBonusSlots = 5;

    /// <summary>
    /// Rolls Magic or Rare affixes onto a freshly constructed loot item. Returns false for item
    /// types the affix system does not cover.
    /// </summary>
    public static bool Apply(Item item, Mobile creature, int luckChance, int minIntensity, int maxIntensity) =>
        Apply(
            item,
            ItemLevel.Compute(creature),
            GetRareChance(minIntensity, maxIntensity, LuckFromChance(luckChance))
        );

    public static bool Apply(Item item, int ilvl, double rareChance)
    {
        var mask = GetMask(item);

        if (mask == AffixItemMask.None || item is not ILootAffixItem target)
        {
            return false;
        }

        var rarity = Utility.RandomDouble() < rareChance ? ItemRarity.Rare : ItemRarity.Magic;

        int prefixTarget;
        int suffixTarget;

        if (rarity == ItemRarity.Rare)
        {
            var total = Utility.RandomMinMax(3, GetMaxRareAffixes(ilvl));
            prefixTarget = Utility.RandomMinMax(Math.Max(0, total - MaxAffixesPerSlot), Math.Min(MaxAffixesPerSlot, total));
            suffixTarget = total - prefixTarget;
        }
        else
        {
            (prefixTarget, suffixTarget) = Utility.Random(4) switch
            {
                0 => (1, 0),
                1 => (0, 1),
                _ => (1, 1)
            };
        }

        Span<int> prefixes = stackalloc int[MaxAffixesPerSlot];
        Span<int> suffixes = stackalloc int[MaxAffixesPerSlot];
        Span<int> families = stackalloc int[MaxAffixes];
        var state = new RollState(families);

        var prefixCount = Roll(item, mask, AffixSlot.Prefix, ilvl, prefixTarget, prefixes, ref state);
        var suffixCount = Roll(item, mask, AffixSlot.Suffix, ilvl, suffixTarget, suffixes, ref state);

        // A Magic item whose one slot had nothing eligible tries the other slot.
        if (prefixCount + suffixCount == 0)
        {
            prefixCount = Roll(item, mask, AffixSlot.Prefix, ilvl, 1, prefixes, ref state);

            if (prefixCount == 0)
            {
                suffixCount = Roll(item, mask, AffixSlot.Suffix, ilvl, 1, suffixes, ref state);
            }
        }

        if (prefixCount + suffixCount == 0)
        {
            return false;
        }

        // A rare that only found one affix per slot reads fine as a Magic name.
        if (rarity == ItemRarity.Rare && prefixCount <= 1 && suffixCount <= 1)
        {
            rarity = ItemRarity.Magic;
        }

        byte rareName1 = 0;
        byte rareName2 = 0;

        if (rarity == ItemRarity.Rare)
        {
            rareName1 = LootAffixNames.RandomFirst();
            rareName2 = LootAffixNames.RandomSecond(mask);
        }

        target.LootAffixes = new LootAffixes(
            item,
            rarity,
            prefixes[..prefixCount].ToArray(),
            suffixes[..suffixCount].ToArray(),
            rareName1,
            rareName2
        );

        if (item is BaseWeapon weapon && state.ChangedElement)
        {
            weapon.Hue = weapon.GetElementalDamageHue();
        }

        return true;
    }

    public static AffixItemMask GetMask(Item item) => item switch
    {
        BaseRanged => AffixItemMask.Ranged,
        BaseWeapon => AffixItemMask.Melee,
        BaseShield => AffixItemMask.Shield,
        BaseArmor  => AffixItemMask.Armor,
        BaseHat    => AffixItemMask.Hat,
        BaseJewel  => AffixItemMask.Jewelry,
        _          => AffixItemMask.None
    };

    /// <summary>
    /// Inverse of <see cref="LootPack.GetLuckChance"/>, so honor's perfection bonus carries through.
    /// </summary>
    public static int LuckFromChance(int luckChance) =>
        luckChance <= 0 ? 0 : (int)Math.Round(Math.Pow(luckChance / 100.0, 1.8));

    /// <summary>
    /// Base chance comes from the loot pack entry's old intensity band so boss packs stay better.
    /// Luck acts like Diablo's magic find against rares: luck * 600 / (luck + 600) percent.
    /// </summary>
    public static double GetRareChance(int minIntensity, int maxIntensity, int luck)
    {
        var chance = maxIntensity switch
        {
            >= 100 => 0.10,
            >= 75  => 0.06,
            _      => 0.03
        };

        if (minIntensity >= 25)
        {
            chance += 0.10;
        }

        if (luck > 0)
        {
            var magicFind = luck * 600.0 / (luck + 600);
            chance *= 1 + magicFind / 100.0;
        }

        return Math.Min(chance, MaxRareChance);
    }

    public static int GetMaxRareAffixes(int ilvl) => ilvl switch
    {
        < 30 => 4,
        < 60 => 5,
        _    => MaxAffixes
    };

    private ref struct RollState
    {
        public ulong UsedGroups;
        public Span<int> Families;
        public int FamilyCount;
        public bool ChangedElement;

        public RollState(Span<int> families) => Families = families;

        public readonly bool HasFamily(int family)
        {
            for (var i = 0; i < FamilyCount; i++)
            {
                if (Families[i] == family)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static int Roll(
        Item item, AffixItemMask mask, AffixSlot slot, int ilvl, int count, Span<int> picked, ref RollState state
    )
    {
        var rolled = 0;

        while (rolled < count && state.FamilyCount < state.Families.Length)
        {
            var def = Pick(item, mask, slot, ilvl, ref state);

            if (def == null)
            {
                break;
            }

            for (var i = 0; i < def.Mods.Length; i++)
            {
                ApplyMod(item, def.Mods[i], ref state);
            }

            if (def.Group != AffixGroup.None)
            {
                state.UsedGroups |= 1UL << (int)def.Group;
            }

            state.Families[state.FamilyCount++] = def.Family;
            picked[rolled++] = def.Id;
        }

        return rolled;
    }

    private static LootAffixDef Pick(Item item, AffixItemMask mask, AffixSlot slot, int ilvl, ref RollState state)
    {
        var all = LootAffixDefinitions.All;
        var total = 0;

        for (var i = 0; i < all.Count; i++)
        {
            var def = all[i];

            if (IsEligible(item, mask, slot, ilvl, def, ref state))
            {
                total += def.Frequency;
            }
        }

        if (total == 0)
        {
            return null;
        }

        var roll = Utility.Random(total);

        for (var i = 0; i < all.Count; i++)
        {
            var def = all[i];

            if (!IsEligible(item, mask, slot, ilvl, def, ref state))
            {
                continue;
            }

            if (roll < def.Frequency)
            {
                return def;
            }

            roll -= def.Frequency;
        }

        return null;
    }

    private static bool IsEligible(
        Item item, AffixItemMask mask, AffixSlot slot, int ilvl, LootAffixDef def, ref RollState state
    )
    {
        if (def.Slot != slot || (def.Mask & mask) == 0 || ilvl < def.MinIlvl || ilvl > def.MaxIlvl)
        {
            return false;
        }

        if (def.Group != AffixGroup.None && (state.UsedGroups & (1UL << (int)def.Group)) != 0)
        {
            return false;
        }

        if (state.HasFamily(def.Family))
        {
            return false;
        }

        for (var i = 0; i < def.Mods.Length; i++)
        {
            if (!CanApply(item, def.Mods[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanApply(Item item, AffixMod mod)
    {
        switch (mod.Kind)
        {
            case AffixStatKind.Attribute:
                {
                    // Elves see in the dark; elf-only armor never rolls night sight.
                    return mod.Stat != (int)AosAttribute.NightSight ||
                           item is not BaseArmor armor || armor.RequiredRaces != Race.AllowElvesOnly;
                }
            case AffixStatKind.WeaponAttribute:
                {
                    return item is BaseWeapon;
                }
            case AffixStatKind.ArmorAttribute:
                {
                    if (item is BaseClothing)
                    {
                        return mod.Stat != (int)AosArmorAttribute.MageArmor;
                    }

                    if (item is not BaseArmor armor)
                    {
                        return false;
                    }

                    var attr = (AosArmorAttribute)mod.Stat;

                    if (attr == AosArmorAttribute.MageArmor)
                    {
                        return armor is not BaseShield && armor.MeditationAllowance != ArmorMeditationAllowance.All;
                    }

                    // Leather can't be made lighter or tougher by loot.
                    var isLeather = armor.Resource is >= CraftResource.RegularLeather and <= CraftResource.BarbedLeather;
                    return !isLeather || (attr != AosArmorAttribute.LowerStatReq && attr != AosArmorAttribute.DurabilityBonus);
                }
            case AffixStatKind.Resist:
                {
                    return item is BaseWeapon or BaseArmor or BaseClothing or BaseJewel;
                }
            case AffixStatKind.ElementDamage:
                {
                    return item is BaseWeapon;
                }
            case AffixStatKind.Skill:
                {
                    var skill = (SkillName)mod.Stat;

                    if (!Core.SE && skill is SkillName.Bushido or SkillName.Ninjitsu)
                    {
                        return false;
                    }

                    var bonuses = GetSkillBonuses(item);
                    return bonuses != null && FindSkillSlot(bonuses, skill) >= 0;
                }
            case AffixStatKind.Slayer:
                {
                    return item is BaseWeapon { Slayer: SlayerName.None };
                }
        }

        return false;
    }

    private static int RollValue(AffixMod mod)
    {
        if (mod.Step <= 1)
        {
            return Utility.RandomMinMax(mod.Min, mod.Max);
        }

        return Utility.RandomMinMax(mod.Min / mod.Step, mod.Max / mod.Step) * mod.Step;
    }

    private static void ApplyMod(Item item, AffixMod mod, ref RollState state)
    {
        switch (mod.Kind)
        {
            case AffixStatKind.Attribute:
                {
                    if (item is IAosItem aos)
                    {
                        aos.Attributes[(AosAttribute)mod.Stat] += RollValue(mod);
                    }

                    break;
                }
            case AffixStatKind.WeaponAttribute:
                {
                    if (item is BaseWeapon weapon)
                    {
                        weapon.WeaponAttributes[(AosWeaponAttribute)mod.Stat] += RollValue(mod);
                    }

                    break;
                }
            case AffixStatKind.ArmorAttribute:
                {
                    var attrs = item switch
                    {
                        BaseArmor armor       => armor.ArmorAttributes,
                        BaseClothing clothing => clothing.ClothingAttributes,
                        _                     => null
                    };

                    if (attrs != null)
                    {
                        attrs[(AosArmorAttribute)mod.Stat] += RollValue(mod);
                    }

                    break;
                }
            case AffixStatKind.Resist:
                {
                    ApplyResist(item, (ResistanceType)mod.Stat, RollValue(mod));
                    break;
                }
            case AffixStatKind.ElementDamage:
                {
                    if (item is BaseWeapon weapon)
                    {
                        weapon.GetDamageTypes(null, out var phys, out _, out _, out _, out _, out _, out _);
                        var value = Math.Min(RollValue(mod), phys);

                        if (value > 0)
                        {
                            weapon.AosElementDamages[(AosElementAttribute)mod.Stat] += value;
                            state.ChangedElement = true;
                        }
                    }

                    break;
                }
            case AffixStatKind.Skill:
                {
                    var bonuses = GetSkillBonuses(item);
                    var skill = (SkillName)mod.Stat;
                    var slot = bonuses == null ? -1 : FindSkillSlot(bonuses, skill);

                    if (slot >= 0)
                    {
                        bonuses.SetValues(slot, skill, RollValue(mod));
                    }

                    break;
                }
            case AffixStatKind.Slayer:
                {
                    if (item is BaseWeapon weapon)
                    {
                        weapon.Slayer = (SlayerName)mod.Stat;
                    }

                    break;
                }
        }
    }

    private static void ApplyResist(Item item, ResistanceType type, int value)
    {
        switch (item)
        {
            case BaseWeapon weapon:
                {
                    var attr = type switch
                    {
                        ResistanceType.Physical => AosWeaponAttribute.ResistPhysicalBonus,
                        ResistanceType.Fire     => AosWeaponAttribute.ResistFireBonus,
                        ResistanceType.Cold     => AosWeaponAttribute.ResistColdBonus,
                        ResistanceType.Poison   => AosWeaponAttribute.ResistPoisonBonus,
                        _                       => AosWeaponAttribute.ResistEnergyBonus
                    };

                    weapon.WeaponAttributes[attr] = CapResist(weapon.WeaponAttributes[attr], value);
                    break;
                }
            case BaseArmor armor:
                {
                    switch (type)
                    {
                        case ResistanceType.Physical:
                            {
                                armor.PhysicalBonus = CapResist(armor.PhysicalBonus, value);
                                break;
                            }
                        case ResistanceType.Fire:
                            {
                                armor.FireBonus = CapResist(armor.FireBonus, value);
                                break;
                            }
                        case ResistanceType.Cold:
                            {
                                armor.ColdBonus = CapResist(armor.ColdBonus, value);
                                break;
                            }
                        case ResistanceType.Poison:
                            {
                                armor.PoisonBonus = CapResist(armor.PoisonBonus, value);
                                break;
                            }
                        default:
                            {
                                armor.EnergyBonus = CapResist(armor.EnergyBonus, value);
                                break;
                            }
                    }

                    break;
                }
            case BaseClothing clothing:
                {
                    ApplyElementResist(clothing.Resistances, type, value);
                    break;
                }
            case BaseJewel jewel:
                {
                    ApplyElementResist(jewel.Resistances, type, value);
                    break;
                }
        }
    }

    private static void ApplyElementResist(AosElementAttributes resists, ResistanceType type, int value)
    {
        var attr = type switch
        {
            ResistanceType.Physical => AosElementAttribute.Physical,
            ResistanceType.Fire     => AosElementAttribute.Fire,
            ResistanceType.Cold     => AosElementAttribute.Cold,
            ResistanceType.Poison   => AosElementAttribute.Poison,
            _                       => AosElementAttribute.Energy
        };

        resists[attr] = CapResist(resists[attr], value);
    }

    // A single-element affix plus an all-resist affix must not exceed what one old roll could give.
    private static int CapResist(int current, int value) => Math.Min(current + value, MaxResistBonus);

    private static AosSkillBonuses GetSkillBonuses(Item item) => item switch
    {
        BaseJewel jewel       => jewel.SkillBonuses,
        BaseWeapon weapon     => weapon.SkillBonuses,
        BaseArmor armor       => armor.SkillBonuses,
        BaseClothing clothing => clothing.SkillBonuses,
        _                     => null
    };

    // Returns the first free slot, or -1 if the skill is already present or every slot is taken.
    private static int FindSkillSlot(AosSkillBonuses bonuses, SkillName skill)
    {
        var free = -1;

        for (var i = 0; i < SkillBonusSlots; i++)
        {
            if (bonuses.GetValues(i, out var existing, out _))
            {
                if (existing == skill)
                {
                    return -1;
                }
            }
            else if (free < 0)
            {
                free = i;
            }
        }

        return free;
    }
}
