using System.Collections.Generic;
using Server.Items;
using static Server.Engines.Affixes.AffixMod;

namespace Server.Engines.Affixes;

/// <summary>
/// The affix table. Value ranges stay within what random loot could already roll per item type,
/// so a top-tier affix is never stronger than the old system's best roll.
/// </summary>
public static class LootAffixDefinitions
{
    public const int TiersPerFamily = 64;

    // A tier stops dropping once the tier two steps above it unlocks, so low tiers phase out.
    private const int TierFadeDistance = 2;

    private static readonly LootAffixDef[] _all;
    private static readonly Dictionary<int, LootAffixDef> _byId;

    public static IReadOnlyList<LootAffixDef> All => _all;

    public static LootAffixDef Get(int id) => _byId.GetValueOrDefault(id);

    private readonly record struct Tier(string Name, int MinIlvl, AffixMod[] Mods);

    private static Tier T(string name, int minIlvl, params AffixMod[] mods) => new(name, minIlvl, mods);

    static LootAffixDefinitions()
    {
        var list = new List<LootAffixDef>(256);

        AddPrefixes(list);
        AddSuffixes(list);
        AddSkills(list);
        AddSlayers(list);

        _all = list.ToArray();
        _byId = new Dictionary<int, LootAffixDef>(_all.Length);

        foreach (var def in _all)
        {
            _byId.Add(def.Id, def);
        }
    }

    private static void Family(
        List<LootAffixDef> list, int family, AffixGroup group, AffixSlot slot, AffixItemMask mask, int frequency,
        params Tier[] tiers
    )
    {
        for (var i = 0; i < tiers.Length; i++)
        {
            var tier = tiers[i];
            var fadeIndex = i + TierFadeDistance;
            var maxIlvl = fadeIndex < tiers.Length ? tiers[fadeIndex].MinIlvl - 1 : ItemLevel.Max;

            list.Add(
                new LootAffixDef(
                    family * TiersPerFamily + i,
                    family,
                    tier.Name,
                    slot,
                    group,
                    tier.MinIlvl,
                    maxIlvl,
                    frequency,
                    mask,
                    tier.Mods
                )
            );
        }
    }

    private static void Prefix(
        List<LootAffixDef> list, AffixFamily family, AffixGroup group, AffixItemMask mask, int frequency,
        params Tier[] tiers
    ) => Family(list, (int)family, group, AffixSlot.Prefix, mask, frequency, tiers);

    private static void Suffix(
        List<LootAffixDef> list, AffixFamily family, AffixGroup group, AffixItemMask mask, int frequency,
        params Tier[] tiers
    ) => Family(list, (int)family, group, AffixSlot.Suffix, mask, frequency, tiers);

    private static void AddPrefixes(List<LootAffixDef> l)
    {
        const AffixItemMask weapon = AffixItemMask.Weapon;
        const AffixItemMask resistItems = AffixItemMask.Weapon | AffixItemMask.Worn | AffixItemMask.Jewelry;
        const AffixItemMask casterWorn = AffixItemMask.Worn | AffixItemMask.Jewelry;

        Prefix(l, AffixFamily.WeaponDamage, AffixGroup.WeaponDamage, weapon, 12,
            T("Jagged", 1, Attr(AosAttribute.WeaponDamage, 5, 10)),
            T("Deadly", 10, Attr(AosAttribute.WeaponDamage, 11, 20)),
            T("Vicious", 20, Attr(AosAttribute.WeaponDamage, 21, 30)),
            T("Brutal", 35, Attr(AosAttribute.WeaponDamage, 31, 40)),
            T("Massive", 50, Attr(AosAttribute.WeaponDamage, 41, 45)),
            T("Merciless", 65, Attr(AosAttribute.WeaponDamage, 46, 50))
        );

        Prefix(l, AffixFamily.JewelDamage, AffixGroup.WeaponDamage, AffixItemMask.Jewelry, 8,
            T("Sharp", 1, Attr(AosAttribute.WeaponDamage, 3, 8)),
            T("Fine", 15, Attr(AosAttribute.WeaponDamage, 9, 14)),
            T("Warrior's", 30, Attr(AosAttribute.WeaponDamage, 15, 20)),
            T("Soldier's", 50, Attr(AosAttribute.WeaponDamage, 21, 25))
        );

        const AffixItemMask combatItems = AffixItemMask.Weapon | AffixItemMask.Shield | AffixItemMask.Jewelry;

        Prefix(l, AffixFamily.HitChance, AffixGroup.HitChance, combatItems, 10,
            T("Bronze", 1, Attr(AosAttribute.AttackChance, 1, 4)),
            T("Iron", 12, Attr(AosAttribute.AttackChance, 5, 8)),
            T("Steel", 25, Attr(AosAttribute.AttackChance, 9, 12)),
            T("Mithril", 45, Attr(AosAttribute.AttackChance, 13, 15))
        );

        Prefix(l, AffixFamily.DefendChance, AffixGroup.DefendChance, combatItems, 10,
            T("Sturdy", 1, Attr(AosAttribute.DefendChance, 1, 4)),
            T("Strong", 12, Attr(AosAttribute.DefendChance, 5, 8)),
            T("Glorious", 25, Attr(AosAttribute.DefendChance, 9, 12)),
            T("Blessed", 45, Attr(AosAttribute.DefendChance, 13, 15))
        );

        Prefix(l, AffixFamily.SpellDamage, AffixGroup.SpellDamage, AffixItemMask.Jewelry, 8,
            T("Shimmering", 5, Attr(AosAttribute.SpellDamage, 1, 4)),
            T("Arcane", 20, Attr(AosAttribute.SpellDamage, 5, 8)),
            T("Sorcerous", 40, Attr(AosAttribute.SpellDamage, 9, 12))
        );

        Prefix(l, AffixFamily.BonusMana, AffixGroup.BonusMana, AffixItemMask.Worn, 8,
            T("Lizard's", 1, Attr(AosAttribute.BonusMana, 1, 3)),
            T("Snake's", 15, Attr(AosAttribute.BonusMana, 4, 6)),
            T("Serpent's", 35, Attr(AosAttribute.BonusMana, 7, 8))
        );

        Prefix(l, AffixFamily.ResistPhysical, AffixGroup.ResistPhysical, resistItems, 8,
            T("Tough", 1, Resist(ResistanceType.Physical, 1, 5)),
            T("Hardened", 15, Resist(ResistanceType.Physical, 6, 10)),
            T("Adamant", 35, Resist(ResistanceType.Physical, 11, 15))
        );

        Prefix(l, AffixFamily.ResistFire, AffixGroup.ResistFire, resistItems, 8,
            T("Crimson", 1, Resist(ResistanceType.Fire, 1, 5)),
            T("Garnet", 15, Resist(ResistanceType.Fire, 6, 10)),
            T("Ruby", 35, Resist(ResistanceType.Fire, 11, 15))
        );

        Prefix(l, AffixFamily.ResistCold, AffixGroup.ResistCold, resistItems, 8,
            T("Azure", 1, Resist(ResistanceType.Cold, 1, 5)),
            T("Cobalt", 15, Resist(ResistanceType.Cold, 6, 10)),
            T("Sapphire", 35, Resist(ResistanceType.Cold, 11, 15))
        );

        Prefix(l, AffixFamily.ResistPoison, AffixGroup.ResistPoison, resistItems, 8,
            T("Beryl", 1, Resist(ResistanceType.Poison, 1, 5)),
            T("Viridian", 15, Resist(ResistanceType.Poison, 6, 10)),
            T("Emerald", 35, Resist(ResistanceType.Poison, 11, 15))
        );

        Prefix(l, AffixFamily.ResistEnergy, AffixGroup.ResistEnergy, resistItems, 8,
            T("Ocher", 1, Resist(ResistanceType.Energy, 1, 5)),
            T("Coral", 15, Resist(ResistanceType.Energy, 6, 10)),
            T("Amber", 35, Resist(ResistanceType.Energy, 11, 15))
        );

        Prefix(l, AffixFamily.LowerManaCost, AffixGroup.LowerManaCost, casterWorn, 8,
            T("Mage's", 1, Attr(AosAttribute.LowerManaCost, 1, 3)),
            T("Sorcerer's", 20, Attr(AosAttribute.LowerManaCost, 4, 6)),
            T("Archmage's", 40, Attr(AosAttribute.LowerManaCost, 7, 8))
        );

        Prefix(l, AffixFamily.LowerRegCost, AffixGroup.LowerRegCost, casterWorn, 8,
            T("Thrifty", 1, Attr(AosAttribute.LowerRegCost, 1, 7)),
            T("Frugal", 15, Attr(AosAttribute.LowerRegCost, 8, 14)),
            T("Economical", 35, Attr(AosAttribute.LowerRegCost, 15, 20))
        );

        Prefix(l, AffixFamily.CastSpeed, AffixGroup.CastSpeed,
            AffixItemMask.Weapon | AffixItemMask.Shield | AffixItemMask.Jewelry, 4,
            T("Apprentice's", 20, Attr(AosAttribute.CastSpeed, 1, 1))
        );

        Prefix(l, AffixFamily.CastRecovery, AffixGroup.CastRecovery, AffixItemMask.Jewelry, 6,
            T("Adept's", 10, Attr(AosAttribute.CastRecovery, 1, 1)),
            T("Magus's", 35, Attr(AosAttribute.CastRecovery, 2, 3))
        );

        Prefix(l, AffixFamily.SpellChanneling, AffixGroup.SpellChanneling,
            AffixItemMask.Weapon | AffixItemMask.Shield, 5,
            T("Runed", 10, Attr(AosAttribute.SpellChanneling, 1, 1))
        );

        Prefix(l, AffixFamily.MageWeapon, AffixGroup.WeaponSkill, AffixItemMask.Melee, 4,
            T("Spellsword's", 10, Weapon(AosWeaponAttribute.MageWeapon, 1, 5)),
            T("Battlemage's", 35, Weapon(AosWeaponAttribute.MageWeapon, 6, 10))
        );

        Prefix(l, AffixFamily.UseBestSkill, AffixGroup.WeaponSkill, AffixItemMask.Melee, 3,
            T("Versatile", 25, Weapon(AosWeaponAttribute.UseBestSkill, 1, 1))
        );

        Prefix(l, AffixFamily.MageArmor, AffixGroup.MageArmor, AffixItemMask.Armor, 5,
            T("Mystic", 15, Armor(AosArmorAttribute.MageArmor, 1, 1))
        );

        Prefix(l, AffixFamily.EnhancePotions, AffixGroup.EnhancePotions, AffixItemMask.Jewelry, 5,
            T("Alchemist's", 5, Attr(AosAttribute.EnhancePotions, 5, 15, 5)),
            T("Apothecary's", 30, Attr(AosAttribute.EnhancePotions, 20, 25, 5))
        );

        Prefix(l, AffixFamily.FireDamage, AffixGroup.Element, weapon, 3,
            T("Smoldering", 5, Element(AosElementAttribute.Fire, 10, 40)),
            T("Fiery", 25, Element(AosElementAttribute.Fire, 50, 70)),
            T("Blazing", 45, Element(AosElementAttribute.Fire, 80, 100))
        );

        Prefix(l, AffixFamily.ColdDamage, AffixGroup.Element, weapon, 3,
            T("Chilling", 5, Element(AosElementAttribute.Cold, 10, 40)),
            T("Frozen", 25, Element(AosElementAttribute.Cold, 50, 70)),
            T("Glacial", 45, Element(AosElementAttribute.Cold, 80, 100))
        );

        Prefix(l, AffixFamily.PoisonDamage, AffixGroup.Element, weapon, 3,
            T("Toxic", 5, Element(AosElementAttribute.Poison, 10, 40)),
            T("Venomous", 25, Element(AosElementAttribute.Poison, 50, 70)),
            T("Virulent", 45, Element(AosElementAttribute.Poison, 80, 100))
        );

        Prefix(l, AffixFamily.EnergyDamage, AffixGroup.Element, weapon, 3,
            T("Static", 5, Element(AosElementAttribute.Energy, 10, 40)),
            T("Charged", 25, Element(AosElementAttribute.Energy, 50, 70)),
            T("Electrified", 45, Element(AosElementAttribute.Energy, 80, 100))
        );
    }

    private static void AddSuffixes(List<LootAffixDef> l)
    {
        const AffixItemMask weapon = AffixItemMask.Weapon;
        const AffixItemMask worn = AffixItemMask.Worn;
        const AffixItemMask armorish = AffixItemMask.Worn | AffixItemMask.Shield;

        Suffix(l, AffixFamily.SwingSpeed, AffixGroup.SwingSpeed, weapon, 10,
            T("of Readiness", 1, Attr(AosAttribute.WeaponSpeed, 5, 10, 5)),
            T("of Alacrity", 20, Attr(AosAttribute.WeaponSpeed, 15, 20, 5)),
            T("of Swiftness", 40, Attr(AosAttribute.WeaponSpeed, 25, 30, 5))
        );

        Suffix(l, AffixFamily.AllResist, AffixGroup.AllResist, AffixItemMask.Worn | AffixItemMask.Jewelry, 4,
            T("of the Prism", 20,
                Resist(ResistanceType.Physical, 1, 3), Resist(ResistanceType.Fire, 1, 3),
                Resist(ResistanceType.Cold, 1, 3), Resist(ResistanceType.Poison, 1, 3),
                Resist(ResistanceType.Energy, 1, 3)),
            T("of the Spectrum", 45,
                Resist(ResistanceType.Physical, 4, 6), Resist(ResistanceType.Fire, 4, 6),
                Resist(ResistanceType.Cold, 4, 6), Resist(ResistanceType.Poison, 4, 6),
                Resist(ResistanceType.Energy, 4, 6)),
            T("of the Rainbow", 65,
                Resist(ResistanceType.Physical, 7, 8), Resist(ResistanceType.Fire, 7, 8),
                Resist(ResistanceType.Cold, 7, 8), Resist(ResistanceType.Poison, 7, 8),
                Resist(ResistanceType.Energy, 7, 8))
        );

        Suffix(l, AffixFamily.Luck, AffixGroup.Luck, weapon | worn | AffixItemMask.Jewelry, 8,
            T("of Chance", 1, Attr(AosAttribute.Luck, 10, 40)),
            T("of Fortune", 25, Attr(AosAttribute.Luck, 41, 70)),
            T("of Providence", 50, Attr(AosAttribute.Luck, 71, 100))
        );

        HitSuffix(l, AffixFamily.HitMagicArrow, AffixGroup.HitSpell, AosWeaponAttribute.HitMagicArrow,
            "of Darts", "of Bolts", "of the Arcane Bolt");
        HitSuffix(l, AffixFamily.HitHarm, AffixGroup.HitSpell, AosWeaponAttribute.HitHarm,
            "of Pain", "of Harm", "of Suffering");
        HitSuffix(l, AffixFamily.HitFireball, AffixGroup.HitSpell, AosWeaponAttribute.HitFireball,
            "of Embers", "of Flame", "of the Phoenix");
        HitSuffix(l, AffixFamily.HitLightning, AffixGroup.HitSpell, AosWeaponAttribute.HitLightning,
            "of Sparks", "of Lightning", "of Thunder");
        HitSuffix(l, AffixFamily.HitDispel, AffixGroup.HitDispel, AosWeaponAttribute.HitDispel,
            "of Nullification", "of Unmaking", "of Banishing");

        HitSuffix(l, AffixFamily.HitPhysicalArea, AffixGroup.HitArea, AosWeaponAttribute.HitPhysicalArea,
            "of Tremors", "of the Quake", "of the Cataclysm");
        HitSuffix(l, AffixFamily.HitFireArea, AffixGroup.HitArea, AosWeaponAttribute.HitFireArea,
            "of Cinders", "of the Blaze", "of the Inferno");
        HitSuffix(l, AffixFamily.HitColdArea, AffixGroup.HitArea, AosWeaponAttribute.HitColdArea,
            "of Frost", "of the Glacier", "of the Blizzard");
        HitSuffix(l, AffixFamily.HitPoisonArea, AffixGroup.HitArea, AosWeaponAttribute.HitPoisonArea,
            "of Blight", "of Pestilence", "of the Plague");
        HitSuffix(l, AffixFamily.HitEnergyArea, AffixGroup.HitArea, AosWeaponAttribute.HitEnergyArea,
            "of Static", "of Storms", "of the Tempest");

        HitSuffix(l, AffixFamily.LeechHits, AffixGroup.LeechHits, AosWeaponAttribute.HitLeechHits,
            "of the Leech", "of the Locust", "of the Lamprey");
        HitSuffix(l, AffixFamily.LeechMana, AffixGroup.LeechMana, AosWeaponAttribute.HitLeechMana,
            "of the Bat", "of the Wraith", "of the Vampire");
        HitSuffix(l, AffixFamily.LeechStam, AffixGroup.LeechStam, AosWeaponAttribute.HitLeechStam,
            "of the Gnat", "of the Tick", "of the Succubus");
        HitSuffix(l, AffixFamily.HitLowerAttack, AffixGroup.HitLowerAttack, AosWeaponAttribute.HitLowerAttack,
            "of Distraction", "of Confusion", "of Bewilderment");
        HitSuffix(l, AffixFamily.HitLowerDefend, AffixGroup.HitLowerDefend, AosWeaponAttribute.HitLowerDefend,
            "of Sundering", "of Rending", "of Shattering");

        Suffix(l, AffixFamily.WeaponDurability, AffixGroup.Durability, weapon, 5,
            T("of Craftsmanship", 1, Weapon(AosWeaponAttribute.DurabilityBonus, 10, 50, 10)),
            T("of Ages", 25, Weapon(AosWeaponAttribute.DurabilityBonus, 60, 100, 10))
        );

        Suffix(l, AffixFamily.ArmorDurability, AffixGroup.Durability, armorish, 5,
            T("of Craftsmanship", 1, Armor(AosArmorAttribute.DurabilityBonus, 10, 50, 10)),
            T("of Ages", 25, Armor(AosArmorAttribute.DurabilityBonus, 60, 100, 10))
        );

        Suffix(l, AffixFamily.WeaponLowerStatReq, AffixGroup.LowerStatReq, weapon, 5,
            T("of Ease", 1, Weapon(AosWeaponAttribute.LowerStatReq, 10, 50, 10)),
            T("of Effortlessness", 25, Weapon(AosWeaponAttribute.LowerStatReq, 60, 100, 10))
        );

        Suffix(l, AffixFamily.ArmorLowerStatReq, AffixGroup.LowerStatReq, armorish, 5,
            T("of Ease", 1, Armor(AosArmorAttribute.LowerStatReq, 10, 50, 10)),
            T("of Effortlessness", 25, Armor(AosArmorAttribute.LowerStatReq, 60, 100, 10))
        );

        Suffix(l, AffixFamily.SelfRepair, AffixGroup.SelfRepair, armorish, 5,
            T("of Mending", 5, Armor(AosArmorAttribute.SelfRepair, 1, 2)),
            T("of Regrowth", 30, Armor(AosArmorAttribute.SelfRepair, 3, 5))
        );

        Suffix(l, AffixFamily.BonusStr, AffixGroup.BonusStr, AffixItemMask.Jewelry, 8,
            T("of Strength", 1, Attr(AosAttribute.BonusStr, 1, 3)),
            T("of Might", 20, Attr(AosAttribute.BonusStr, 4, 6)),
            T("of the Giant", 40, Attr(AosAttribute.BonusStr, 7, 8))
        );

        Suffix(l, AffixFamily.BonusDex, AffixGroup.BonusDex, AffixItemMask.Jewelry, 8,
            T("of Dexterity", 1, Attr(AosAttribute.BonusDex, 1, 3)),
            T("of Skill", 20, Attr(AosAttribute.BonusDex, 4, 6)),
            T("of Precision", 40, Attr(AosAttribute.BonusDex, 7, 8))
        );

        Suffix(l, AffixFamily.BonusInt, AffixGroup.BonusInt, AffixItemMask.Jewelry, 8,
            T("of Energy", 1, Attr(AosAttribute.BonusInt, 1, 3)),
            T("of the Mind", 20, Attr(AosAttribute.BonusInt, 4, 6)),
            T("of Brilliance", 40, Attr(AosAttribute.BonusInt, 7, 8))
        );

        Suffix(l, AffixFamily.BonusHits, AffixGroup.BonusHits, worn, 10,
            T("of the Jackal", 1, Attr(AosAttribute.BonusHits, 1, 2)),
            T("of the Fox", 20, Attr(AosAttribute.BonusHits, 3, 4)),
            T("of the Wolf", 40, Attr(AosAttribute.BonusHits, 5, 5))
        );

        Suffix(l, AffixFamily.BonusStam, AffixGroup.BonusStam, worn, 8,
            T("of Endurance", 1, Attr(AosAttribute.BonusStam, 1, 3)),
            T("of Stamina", 15, Attr(AosAttribute.BonusStam, 4, 6)),
            T("of Vigor", 35, Attr(AosAttribute.BonusStam, 7, 8))
        );

        Suffix(l, AffixFamily.RegenHits, AffixGroup.RegenHits, worn, 6,
            T("of Regeneration", 5, Attr(AosAttribute.RegenHits, 1, 1)),
            T("of Revivification", 35, Attr(AosAttribute.RegenHits, 2, 2))
        );

        Suffix(l, AffixFamily.RegenMana, AffixGroup.RegenMana, worn, 6,
            T("of Clarity", 5, Attr(AosAttribute.RegenMana, 1, 1)),
            T("of Serenity", 35, Attr(AosAttribute.RegenMana, 2, 2))
        );

        Suffix(l, AffixFamily.RegenStam, AffixGroup.RegenStam, worn, 6,
            T("of Respite", 1, Attr(AosAttribute.RegenStam, 1, 1)),
            T("of Vitality", 25, Attr(AosAttribute.RegenStam, 2, 3))
        );

        Suffix(l, AffixFamily.ReflectPhysical, AffixGroup.ReflectPhysical, armorish, 8,
            T("of Thorns", 1, Attr(AosAttribute.ReflectPhysical, 1, 5)),
            T("of Spikes", 20, Attr(AosAttribute.ReflectPhysical, 6, 10)),
            T("of Retribution", 40, Attr(AosAttribute.ReflectPhysical, 11, 15))
        );

        Suffix(l, AffixFamily.NightSight, AffixGroup.NightSight, worn | AffixItemMask.Jewelry, 4,
            T("of Light", 1, Attr(AosAttribute.NightSight, 1, 1))
        );
    }

    private static void HitSuffix(
        List<LootAffixDef> l, AffixFamily family, AffixGroup group, AosWeaponAttribute attr,
        string low, string mid, string high
    ) => Suffix(l, family, group, AffixItemMask.Weapon, 6,
        T(low, 1, Weapon(attr, 2, 16, 2)),
        T(mid, 20, Weapon(attr, 18, 34, 2)),
        T(high, 45, Weapon(attr, 36, 50, 2))
    );

    private static void AddSkills(List<LootAffixDef> l)
    {
        Skill(l, SkillName.Swords, "of the Swordsman");
        Skill(l, SkillName.Fencing, "of the Duelist");
        Skill(l, SkillName.Macing, "of the Crusher");
        Skill(l, SkillName.Archery, "of the Archer");
        Skill(l, SkillName.Wrestling, "of the Brawler");
        Skill(l, SkillName.Parry, "of the Guardian");
        Skill(l, SkillName.Tactics, "of the Tactician");
        Skill(l, SkillName.Anatomy, "of the Anatomist");
        Skill(l, SkillName.Healing, "of the Healer");
        Skill(l, SkillName.Magery, "of the Sorcerer");
        Skill(l, SkillName.Meditation, "of the Ascetic");
        Skill(l, SkillName.EvalInt, "of the Scholar");
        Skill(l, SkillName.MagicResist, "of Warding");
        Skill(l, SkillName.AnimalTaming, "of the Tamer");
        Skill(l, SkillName.AnimalLore, "of the Naturalist");
        Skill(l, SkillName.Veterinary, "of the Veterinarian");
        Skill(l, SkillName.Musicianship, "of the Minstrel");
        Skill(l, SkillName.Provocation, "of the Instigator");
        Skill(l, SkillName.Discordance, "of Discord");
        Skill(l, SkillName.Peacemaking, "of the Peacemaker");
        Skill(l, SkillName.Chivalry, "of the Paladin");
        Skill(l, SkillName.Focus, "of Focus");
        Skill(l, SkillName.Necromancy, "of the Necromancer");
        Skill(l, SkillName.Stealing, "of the Thief");
        Skill(l, SkillName.Stealth, "of the Shadow");
        Skill(l, SkillName.SpiritSpeak, "of the Medium");
        Skill(l, SkillName.Bushido, "of the Samurai");
        Skill(l, SkillName.Ninjitsu, "of the Ninja");
    }

    // Skills are not grouped: a ring can carry several different skill bonuses, never the same one twice.
    private static void Skill(List<LootAffixDef> l, SkillName skill, string name) =>
        Family(l, (int)AffixFamily.SkillBase + (int)skill, AffixGroup.None, AffixSlot.Suffix, AffixItemMask.Jewelry, 2,
            T(name, 1, AffixMod.Skill(skill, 1, 5)),
            T(name, 20, AffixMod.Skill(skill, 6, 10)),
            T(name, 45, AffixMod.Skill(skill, 11, 15))
        );

    private static void AddSlayers(List<LootAffixDef> l)
    {
        // Super slayers
        Slayer(l, SlayerName.Repond, "of Repond", 40);
        Slayer(l, SlayerName.Silver, "of Silver", 40);
        Slayer(l, SlayerName.ReptilianDeath, "of Reptilian Death", 40);
        Slayer(l, SlayerName.ArachnidDoom, "of Arachnid Doom", 40);
        Slayer(l, SlayerName.ElementalBan, "of Elemental Ban", 40);
        Slayer(l, SlayerName.Exorcism, "of Exorcism", 40);

        Slayer(l, SlayerName.OrcSlaying, "of Orc Slaying", 5);
        Slayer(l, SlayerName.TrollSlaughter, "of Troll Slaughter", 5);
        Slayer(l, SlayerName.OgreTrashing, "of Ogre Thrashing", 5);
        Slayer(l, SlayerName.DragonSlaying, "of Dragon Slaying", 5);
        Slayer(l, SlayerName.Terathan, "of Terathan Slaying", 5);
        Slayer(l, SlayerName.SnakesBane, "of Snake's Bane", 5);
        Slayer(l, SlayerName.LizardmanSlaughter, "of Lizardman Slaughter", 5);
        Slayer(l, SlayerName.DaemonDismissal, "of Daemon Dismissal", 5);
        Slayer(l, SlayerName.GargoylesFoe, "of Gargoyle's Foe", 5);
        Slayer(l, SlayerName.BalronDamnation, "of Balron Damnation", 5);
        Slayer(l, SlayerName.Ophidian, "of Ophidian Slaying", 5);
        Slayer(l, SlayerName.SpidersDeath, "of Spider's Death", 5);
        Slayer(l, SlayerName.ScorpionsBane, "of Scorpion's Bane", 5);
        Slayer(l, SlayerName.FlameDousing, "of Flame Dousing", 5);
        Slayer(l, SlayerName.WaterDissipation, "of Water Dissipation", 5);
        Slayer(l, SlayerName.Vacuum, "of the Vacuum", 5);
        Slayer(l, SlayerName.ElementalHealth, "of Elemental Health", 5);
        Slayer(l, SlayerName.EarthShatter, "of Earth Shatter", 5);
        Slayer(l, SlayerName.BloodDrinking, "of Blood Drinking", 5);
        Slayer(l, SlayerName.SummerWind, "of Summer Wind", 5);
    }

    private static void Slayer(List<LootAffixDef> l, SlayerName slayer, string name, int minIlvl) =>
        Family(l, (int)AffixFamily.SlayerBase + (int)slayer, AffixGroup.Slayer, AffixSlot.Suffix, AffixItemMask.Weapon, 1,
            T(name, minIlvl, AffixMod.Slayer(slayer))
        );
}
