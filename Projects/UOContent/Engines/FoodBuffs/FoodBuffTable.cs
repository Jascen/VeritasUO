using System;
using System.Collections.Generic;
using Server.Items;

namespace Server.Engines.FoodBuffs;

/// <summary>
/// Which crafted foods and drinks grant which bonuses. Values are Regular quality and deliberately
/// small (a fraction of one good jewel) so food complements gear rather than replacing it.
/// Food types missing from this table grant no buff.
/// </summary>
public static class FoodBuffTable
{
    private static readonly TimeSpan MealDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DrinkDuration = TimeSpan.FromMinutes(20);

    public static readonly FoodBuffProfile Warrior = new()
    {
        Slot = FoodBuffSlot.Meal,
        Duration = MealDuration,
        Str = 3,
        Attributes = [(AosAttribute.WeaponDamage, 5), (AosAttribute.RegenHits, 1)]
    };

    public static readonly FoodBuffProfile Archer = new()
    {
        Slot = FoodBuffSlot.Meal,
        Duration = MealDuration,
        Dex = 3,
        Attributes = [(AosAttribute.AttackChance, 3), (AosAttribute.WeaponSpeed, 3)]
    };

    public static readonly FoodBuffProfile Mage = new()
    {
        Slot = FoodBuffSlot.Meal,
        Duration = MealDuration,
        Int = 3,
        Attributes = [(AosAttribute.LowerRegCost, 5), (AosAttribute.LowerManaCost, 2)]
    };

    public static readonly FoodBuffProfile Defender = new()
    {
        Slot = FoodBuffSlot.Meal,
        Duration = MealDuration,
        Attributes = [(AosAttribute.DefendChance, 3), (AosAttribute.RegenStam, 1)]
    };

    public static readonly FoodBuffProfile Wasabi = new()
    {
        Slot = FoodBuffSlot.Meal,
        Duration = MealDuration,
        Attributes = [(AosAttribute.WeaponDamage, 3)],
        WeaponAttributes = [(AosWeaponAttribute.HitFireArea, 5)]
    };

    public static readonly FoodBuffProfile Sushi = new()
    {
        Slot = FoodBuffSlot.Meal,
        Duration = MealDuration,
        Attributes = [(AosAttribute.AttackChance, 2)],
        WeaponAttributes = [(AosWeaponAttribute.HitColdArea, 5)]
    };

    public static readonly FoodBuffProfile SushiPlatterMeal = new()
    {
        Slot = FoodBuffSlot.Meal,
        Duration = MealDuration,
        Attributes = [(AosAttribute.AttackChance, 3)],
        WeaponAttributes = [(AosWeaponAttribute.HitColdArea, 8)]
    };

    public static readonly FoodBuffProfile Miso = new()
    {
        Slot = FoodBuffSlot.Meal,
        Duration = MealDuration,
        Attributes = [(AosAttribute.RegenMana, 1)],
        WeaponAttributes = [(AosWeaponAttribute.HitLeechMana, 5)]
    };

    public static readonly FoodBuffProfile GreenTeaDrink = new()
    {
        Slot = FoodBuffSlot.Drink,
        Duration = DrinkDuration,
        Attributes = [(AosAttribute.RegenMana, 2)]
    };

    public static readonly FoodBuffProfile SpringTonic = new()
    {
        Slot = FoodBuffSlot.Drink,
        Duration = DrinkDuration,
        Attributes = [(AosAttribute.RegenStam, 2)]
    };

    public static readonly FoodBuffProfile FruitJuice = new()
    {
        Slot = FoodBuffSlot.Drink,
        Duration = DrinkDuration,
        Attributes = [(AosAttribute.RegenHits, 2)]
    };

    public static readonly FoodBuffProfile HerbalTea = new()
    {
        Slot = FoodBuffSlot.Drink,
        Duration = DrinkDuration,
        Attributes = [(AosAttribute.RegenMana, 2)]
    };

    public static readonly FoodBuffProfile HeartyAle = new()
    {
        Slot = FoodBuffSlot.Drink,
        Duration = DrinkDuration,
        Str = 4,
        Dex = -3,
        Attributes = [(AosAttribute.WeaponDamage, 5)]
    };

    public static readonly FoodBuffProfile SpicedWine = new()
    {
        Slot = FoodBuffSlot.Drink,
        Duration = DrinkDuration,
        Int = 3,
        Attributes = [(AosAttribute.SpellDamage, 4), (AosAttribute.AttackChance, -2)]
    };

    public static readonly FoodBuffProfile DwarvenLiquor = new()
    {
        Slot = FoodBuffSlot.Drink,
        Duration = DrinkDuration,
        Int = -3,
        Attributes = [(AosAttribute.RegenHits, 2), (AosAttribute.DefendChance, 4), (AosAttribute.RegenMana, -1)]
    };

    private static readonly Dictionary<Type, FoodBuffProfile> _profiles = new()
    {
        [typeof(Ribs)] = Warrior,
        [typeof(LambLeg)] = Warrior,
        [typeof(MeatPie)] = Warrior,
        [typeof(SausagePizza)] = Warrior,

        [typeof(CookedBird)] = Archer,
        [typeof(ChickenLeg)] = Archer,
        [typeof(FishSteak)] = Archer,

        [typeof(FruitPie)] = Mage,
        [typeof(ApplePie)] = Mage,
        [typeof(PeachCobbler)] = Mage,
        [typeof(Cake)] = Mage,
        [typeof(Cookies)] = Mage,
        [typeof(Muffins)] = Mage,

        [typeof(BreadLoaf)] = Defender,
        [typeof(CheesePizza)] = Defender,
        [typeof(PumpkinPie)] = Defender,
        [typeof(Quiche)] = Defender,
        [typeof(FriedEggs)] = Defender,

        [typeof(WasabiClumps)] = Wasabi,
        [typeof(SushiRolls)] = Sushi,
        [typeof(SushiPlatter)] = SushiPlatterMeal,
        [typeof(MisoSoup)] = Miso,
        [typeof(WhiteMisoSoup)] = Miso,
        [typeof(RedMisoSoup)] = Miso,
        [typeof(AwaseMisoSoup)] = Miso,
        [typeof(GreenTea)] = GreenTeaDrink
    };

    public static FoodBuffProfile Get(Type type) => _profiles.GetValueOrDefault(type);
}
