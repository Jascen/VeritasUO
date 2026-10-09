using Server;
using Server.Engines.FoodBuffs;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class FoodBuffTests
{
    private static PlayerMobile CreatePlayer(int x)
    {
        var m = new PlayerMobile(World.NewMobile);
        m.DefaultMobileInit();
        m.MoveToWorld(new Point3D(x, 600, 0), Map.Felucca);
        m.AddItem(new Backpack());
        m.Hunger = 10;
        m.Thirst = 0;
        m.RawStr = 50;
        m.RawDex = 50;
        m.RawInt = 50;
        return m;
    }

    private static T Crafted<T>(T item, int quality = 1) where T : Item, Server.Engines.Craft.ICraftable
    {
        item.PlayerConstructed = true;
        item.OnCraft(quality, false, null, null, null, null, null, 0);
        return item;
    }

    private static void Cleanup(PlayerMobile m)
    {
        FoodBuffSystem.Clear(m);
        m.BAC = 0;
        BaseBeverage.CheckHeaveTimer(m);
        m.Delete();
    }

    [Fact]
    public void VendorFood_GrantsNoBuff()
    {
        var m = CreatePlayer(4300);

        try
        {
            Assert.True(new Ribs().Eat(m));

            Assert.False(FoodBuffSystem.HasBuff(m, FoodBuffSlot.Meal));
            Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.WeaponDamage));
        }
        finally
        {
            Cleanup(m);
        }
    }

    [Fact]
    public void ExceptionalCraftedMeal_ScalesBonuses()
    {
        var m = CreatePlayer(4310);

        try
        {
            var ribs = Crafted(new Ribs(), 2);
            Assert.Equal(FoodQuality.Exceptional, ribs.Quality);

            var str = m.Str;
            Assert.True(ribs.Eat(m));

            Assert.Equal(8, AosAttributes.GetValue(m, AosAttribute.WeaponDamage)); // 5 * 1.5, rounded
            Assert.Equal(2, AosAttributes.GetValue(m, AosAttribute.RegenHits));
            Assert.Equal(str + 5, m.Str);
        }
        finally
        {
            Cleanup(m);
        }
    }

    [Fact]
    public void MealBuff_StopsWhileHungry()
    {
        var m = CreatePlayer(4320);

        try
        {
            Assert.True(Crafted(new Ribs()).Eat(m));
            Assert.Equal(5, AosAttributes.GetValue(m, AosAttribute.WeaponDamage));

            m.Hunger = FoodBuffSystem.MinFullness - 1;
            Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.WeaponDamage));

            m.Hunger = FoodBuffSystem.MinFullness;
            Assert.Equal(5, AosAttributes.GetValue(m, AosAttribute.WeaponDamage));
        }
        finally
        {
            Cleanup(m);
        }
    }

    [Fact]
    public void NewMeal_ReplacesPreviousMeal()
    {
        var m = CreatePlayer(4330);

        try
        {
            var str = m.Str;
            Assert.True(Crafted(new Ribs()).Eat(m));
            Assert.True(Crafted(new FruitPie()).Eat(m));

            Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.WeaponDamage));
            Assert.Equal(5, AosAttributes.GetValue(m, AosAttribute.LowerRegCost));
            Assert.Equal(str, m.Str);
        }
        finally
        {
            Cleanup(m);
        }
    }

    [Fact]
    public void MealAndDrink_Stack()
    {
        var m = CreatePlayer(4340);

        try
        {
            Assert.True(Crafted(new Ribs()).Eat(m));
            Assert.True(Crafted(new FruitJuice()).Drink(m));

            Assert.Equal(3, AosAttributes.GetValue(m, AosAttribute.RegenHits));
        }
        finally
        {
            Cleanup(m);
        }
    }

    [Fact]
    public void Alcohol_AppliesDrawbacksAndBac()
    {
        var m = CreatePlayer(4350);

        try
        {
            var str = m.Str;
            var dex = m.Dex;

            Assert.True(Crafted(new HeartyAle()).Drink(m));

            Assert.Equal(str + 4, m.Str);
            Assert.Equal(dex - 3, m.Dex);
            Assert.Equal(5, AosAttributes.GetValue(m, AosAttribute.WeaponDamage));
            Assert.Equal(2, m.BAC);
            Assert.Equal(10, m.Thirst);
        }
        finally
        {
            Cleanup(m);
        }
    }

    [Fact]
    public void SpicyFood_GrantsHitProc()
    {
        var m = CreatePlayer(4360);

        try
        {
            Assert.True(Crafted(new WasabiClumps()).Eat(m));

            Assert.Equal(5, AosWeaponAttributes.GetValue(m, AosWeaponAttribute.HitFireArea));
        }
        finally
        {
            Cleanup(m);
        }
    }

    [Fact]
    public void HeavyDrinking_LeavesHangover()
    {
        var m = CreatePlayer(4370);

        try
        {
            m.BAC = FoodBuffSystem.HangoverPeakBac;
            FoodBuffSystem.OnAlcoholConsumed(m);
            m.BAC = 0;
            FoodBuffSystem.OnSober(m);

            Assert.True(FoodBuffSystem.HasHangover(m));
            Assert.Equal(FoodBuffSystem.HangoverStamRegen, AosAttributes.GetValue(m, AosAttribute.RegenStam));
        }
        finally
        {
            Cleanup(m);
        }
    }

    [Fact]
    public void LightDrinking_NoHangover()
    {
        var m = CreatePlayer(4380);

        try
        {
            m.BAC = FoodBuffSystem.HangoverPeakBac - 1;
            FoodBuffSystem.OnAlcoholConsumed(m);
            m.BAC = 0;
            FoodBuffSystem.OnSober(m);

            Assert.False(FoodBuffSystem.HasHangover(m));
        }
        finally
        {
            Cleanup(m);
        }
    }

    [Fact]
    public void CraftedAndVendorFood_DoNotStack()
    {
        var crafted = Crafted(new Ribs());
        var vendor = new Ribs();
        var exceptional = Crafted(new Ribs(), 2);
        var matching = Crafted(new Ribs());

        try
        {
            Assert.False(crafted.CanStackWith(vendor));
            Assert.False(crafted.CanStackWith(exceptional));
            Assert.True(crafted.CanStackWith(matching));
        }
        finally
        {
            crafted.Delete();
            vendor.Delete();
            exceptional.Delete();
            matching.Delete();
        }
    }
}
