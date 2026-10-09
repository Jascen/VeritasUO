using System;
using Server.Items;

namespace Server.Engines.Affixes;

public enum AffixSlot : byte
{
    Prefix,
    Suffix
}

[Flags]
public enum AffixItemMask : byte
{
    None = 0,
    Melee = 0x01,
    Ranged = 0x02,
    Armor = 0x04,
    Shield = 0x08,
    Hat = 0x10,
    Jewelry = 0x20,

    Weapon = Melee | Ranged,
    Worn = Armor | Hat
}

/// <summary>
/// Affix ids are saved on items as <c>family * <see cref="LootAffixDefinitions.TiersPerFamily"/> + tier</c>.
/// Values are persisted: never renumber or reuse one, and only append tiers to a family.
/// Skill families are <see cref="SkillBase"/> + <see cref="SkillName"/>, slayer families are
/// <see cref="SlayerBase"/> + <see cref="SlayerName"/>.
/// </summary>
public enum AffixFamily
{
    WeaponDamage = 1,
    JewelDamage = 2,
    HitChance = 3,
    DefendChance = 4,
    SwingSpeed = 5,
    SpellDamage = 6,
    BonusMana = 7,
    ResistPhysical = 8,
    ResistFire = 9,
    ResistCold = 10,
    ResistPoison = 11,
    ResistEnergy = 12,
    AllResist = 13,
    Luck = 14,
    HitMagicArrow = 15,
    HitHarm = 16,
    HitFireball = 17,
    HitLightning = 18,
    HitDispel = 19,
    HitPhysicalArea = 20,
    HitFireArea = 21,
    HitColdArea = 22,
    HitPoisonArea = 23,
    HitEnergyArea = 24,
    LeechHits = 25,
    LeechMana = 26,
    LeechStam = 27,
    HitLowerAttack = 28,
    HitLowerDefend = 29,
    WeaponDurability = 30,
    ArmorDurability = 31,
    WeaponLowerStatReq = 32,
    ArmorLowerStatReq = 33,
    SelfRepair = 34,
    BonusStr = 35,
    BonusDex = 36,
    BonusInt = 37,
    BonusHits = 38,
    BonusStam = 39,
    RegenHits = 40,
    RegenMana = 41,
    RegenStam = 42,
    LowerManaCost = 43,
    LowerRegCost = 44,
    CastSpeed = 45,
    CastRecovery = 46,
    SpellChanneling = 47,
    MageWeapon = 48,
    UseBestSkill = 49,
    ReflectPhysical = 50,
    NightSight = 51,
    MageArmor = 52,
    EnhancePotions = 53,
    FireDamage = 54,
    ColdDamage = 55,
    PoisonDamage = 56,
    EnergyDamage = 57,

    SkillBase = 200,
    SlayerBase = 300
}

/// <summary>
/// Mutually exclusive affixes share a group. Not persisted, so it can be reorganized freely,
/// but it must stay under 64 members (tracked as a bitmask while rolling).
/// </summary>
public enum AffixGroup : byte
{
    None,
    WeaponDamage,
    HitChance,
    DefendChance,
    SwingSpeed,
    SpellDamage,
    BonusMana,
    ResistPhysical,
    ResistFire,
    ResistCold,
    ResistPoison,
    ResistEnergy,
    AllResist,
    Luck,
    HitSpell,
    HitDispel,
    HitArea,
    LeechHits,
    LeechMana,
    LeechStam,
    HitLowerAttack,
    HitLowerDefend,
    Durability,
    LowerStatReq,
    SelfRepair,
    BonusStr,
    BonusDex,
    BonusInt,
    BonusHits,
    BonusStam,
    RegenHits,
    RegenMana,
    RegenStam,
    LowerManaCost,
    LowerRegCost,
    CastSpeed,
    CastRecovery,
    SpellChanneling,
    WeaponSkill,
    ReflectPhysical,
    NightSight,
    MageArmor,
    EnhancePotions,
    Element,
    Slayer
}

public enum AffixStatKind : byte
{
    Attribute,
    WeaponAttribute,
    ArmorAttribute,
    Resist,
    ElementDamage,
    Skill,
    Slayer
}

public readonly record struct AffixMod(AffixStatKind Kind, int Stat, int Min, int Max, int Step = 1)
{
    public static AffixMod Attr(AosAttribute attr, int min, int max, int step = 1) =>
        new(AffixStatKind.Attribute, (int)attr, min, max, step);

    public static AffixMod Weapon(AosWeaponAttribute attr, int min, int max, int step = 1) =>
        new(AffixStatKind.WeaponAttribute, (int)attr, min, max, step);

    public static AffixMod Armor(AosArmorAttribute attr, int min, int max, int step = 1) =>
        new(AffixStatKind.ArmorAttribute, (int)attr, min, max, step);

    public static AffixMod Resist(ResistanceType type, int min, int max) =>
        new(AffixStatKind.Resist, (int)type, min, max);

    public static AffixMod Element(AosElementAttribute attr, int min, int max) =>
        new(AffixStatKind.ElementDamage, (int)attr, min, max, 10);

    public static AffixMod Skill(SkillName skill, int min, int max) =>
        new(AffixStatKind.Skill, (int)skill, min, max);

    public static AffixMod Slayer(SlayerName slayer) => new(AffixStatKind.Slayer, (int)slayer, 0, 0);
}

public sealed record LootAffixDef(
    int Id,
    int Family,
    string Name,
    AffixSlot Slot,
    AffixGroup Group,
    int MinIlvl,
    int MaxIlvl,
    int Frequency,
    AffixItemMask Mask,
    AffixMod[] Mods
);
