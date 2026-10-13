using System;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Engines.FoodBuffs;

namespace Server.Items;

/// <summary>
/// Single-serving drinks brewed with Cooking. Unlike <see cref="BaseBeverage"/> they cannot be
/// filled or poured, which keeps quality and buffs tied to the cook who made them.
/// </summary>
[SerializationGenerator(0)]
public abstract partial class CraftedDrink : Item, ICraftable
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private FoodQuality _quality;

    public CraftedDrink(int itemID, int amount = 1) : base(itemID)
    {
        Stackable = true;
        Amount = amount;
    }

    public override double DefaultWeight => 1.0;

    public abstract FoodBuffProfile Profile { get; }

    public virtual int AlcoholContent => 0;

    public virtual int ThirstFill => 10;

    public override bool CanStackWith(Item dropped) =>
        (dropped is not CraftedDrink drink || Quality == drink.Quality && PlayerConstructed == drink.PlayerConstructed) &&
        base.CanStackWith(dropped);

    public override void OnDoubleClick(Mobile from)
    {
        if (!Movable)
        {
            return;
        }

        if (!from.InRange(GetWorldLocation(), 1))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        Drink(from);
    }

    public virtual bool Drink(Mobile from)
    {
        if (from.Thirst >= 20 && AlcoholContent == 0)
        {
            from.SendMessage("You are too full to drink any more.");
            return false;
        }

        from.Thirst = Math.Min(from.Thirst + ThirstFill, 20);
        from.PlaySound(Utility.RandomList(0x30, 0x2D6));

        if (from.Body.IsHuman && !from.Mounted)
        {
            from.Animate(34, 5, 1, true, false, 0);
        }

        if (AlcoholContent > 0)
        {
            from.BAC = Math.Min(from.BAC + AlcoholContent, 60);
            FoodBuffSystem.OnAlcoholConsumed(from);
            BaseBeverage.CheckHeaveTimer(from);
        }

        if (PlayerConstructed)
        {
            FoodBuffSystem.Apply(from, Profile, Quality);
        }

        Consume();
        return true;
    }

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        Quality = quality switch
        {
            0 => FoodQuality.Low,
            2 => FoodQuality.Exceptional,
            _ => FoodQuality.Regular
        };

        return quality;
    }

    public override void AddNameProperty(IPropertyList list)
    {
        base.AddNameProperty(list);

        if (PlayerConstructed && Quality == FoodQuality.Exceptional)
        {
            list.Add(1060636); // exceptional
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (PlayerConstructed)
        {
            list.Add(1060658, $"{"Refreshed"}\t{Profile.GetDescription(Quality)}"); // ~1_val~: ~2_val~
        }
    }
}

[SerializationGenerator(0)]
public partial class SpringTonic : CraftedDrink
{
    [Constructible]
    public SpringTonic(int amount = 1) : base(0x1F91, amount) => Hue = 0x48D;

    public override string DefaultName => "spring tonic";

    public override FoodBuffProfile Profile => FoodBuffTable.SpringTonic;
}

[SerializationGenerator(0)]
public partial class FruitJuice : CraftedDrink
{
    [Constructible]
    public FruitJuice(int amount = 1) : base(0x1F7D, amount)
    {
    }

    public override string DefaultName => "fruit juice";

    public override FoodBuffProfile Profile => FoodBuffTable.FruitJuice;
}

[SerializationGenerator(0)]
public partial class HerbalTea : CraftedDrink
{
    [Constructible]
    public HerbalTea(int amount = 1) : base(0x1F89, amount) => Hue = 0x1C7;

    public override string DefaultName => "herbal tea";

    public override FoodBuffProfile Profile => FoodBuffTable.HerbalTea;
}

[SerializationGenerator(0)]
public partial class HeartyAle : CraftedDrink
{
    [Constructible]
    public HeartyAle(int amount = 1) : base(0x9EE, amount)
    {
    }

    public override string DefaultName => "hearty ale";

    public override FoodBuffProfile Profile => FoodBuffTable.HeartyAle;

    public override int AlcoholContent => 2;
}

[SerializationGenerator(0)]
public partial class SpicedWine : CraftedDrink
{
    [Constructible]
    public SpicedWine(int amount = 1) : base(0x1F8D, amount)
    {
    }

    public override string DefaultName => "spiced wine";

    public override FoodBuffProfile Profile => FoodBuffTable.SpicedWine;

    public override int AlcoholContent => 3;
}

[SerializationGenerator(0)]
public partial class DwarvenLiquor : CraftedDrink
{
    [Constructible]
    public DwarvenLiquor(int amount = 1) : base(0x1F85, amount)
    {
    }

    public override string DefaultName => "dwarven liquor";

    public override FoodBuffProfile Profile => FoodBuffTable.DwarvenLiquor;

    public override int AlcoholContent => 5;
}
