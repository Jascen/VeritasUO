using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Affixes;
using Server.Items;
using Xunit;

namespace UOContent.Tests.Engines.Affixes;

[Collection("Sequential UOContent Tests")]
public class LowQualityLootTests : IDisposable
{
    private readonly List<Item> _created = [];

    public void Dispose()
    {
        foreach (var item in _created)
        {
            item.Delete();
        }
    }

    private T Track<T>(T item) where T : Item
    {
        _created.Add(item);
        return item;
    }

    [Fact]
    public void LowQualityArmor_HalvesStrRequirement()
    {
        var regular = Track(new PlateChest());
        var low = Track(new PlateChest { Quality = ArmorQuality.Low });

        Assert.Equal(LowQualityLoot.ReduceStrRequirement(regular.StrRequirement), low.StrRequirement);
        Assert.True(low.StrRequirement < regular.StrRequirement);
    }

    [Fact]
    public void LowQualityWeapon_HalvesStrRequirement()
    {
        var regular = Track(new Halberd());
        var low = Track(new Halberd { Quality = WeaponQuality.Low });

        Assert.Equal(LowQualityLoot.ReduceStrRequirement(regular.StrRequirement), low.StrRequirement);
        Assert.True(low.StrRequirement < regular.StrRequirement);
    }

    [Fact]
    public void StaffOverride_IsNotHalved()
    {
        var weapon = Track(new Halberd { StrRequirement = 80, Quality = WeaponQuality.Low });
        var armor = Track(new PlateChest { StrRequirement = 80, Quality = ArmorQuality.Low });

        Assert.Equal(80, weapon.StrRequirement);
        Assert.Equal(80, armor.StrRequirement);
    }

    [Theory]
    [InlineData(95, 48)]
    [InlineData(40, 20)]
    [InlineData(0, 0)]
    public void ReduceStrRequirement_RoundsUp(int req, int expected)
    {
        Assert.Equal(expected, LowQualityLoot.ReduceStrRequirement(req));
    }

    [Fact]
    public void Chance_FallsToZeroByFalloffLevel()
    {
        for (var level = ItemLevel.Min; level < ItemLevel.Max; level++)
        {
            Assert.True(LowQualityLoot.GetChance(level + 1) <= LowQualityLoot.GetChance(level));
        }

        Assert.True(LowQualityLoot.GetChance(ItemLevel.Min) > 0);
        Assert.Equal(0, LowQualityLoot.GetChance(LowQualityLoot.FalloffItemLevel));
        Assert.Equal(0, LowQualityLoot.GetChance(ItemLevel.Max));
    }

    [Fact]
    public void LowQualityArmor_LosesResistsAndDurability()
    {
        var regular = Track(new PlateChest());
        var low = Track(new PlateChest { Quality = ArmorQuality.Low });

        Assert.Equal(regular.PhysicalResistance - LowQualityLoot.ResistPenalty, low.PhysicalResistance);
        Assert.Equal(regular.FireResistance - LowQualityLoot.ResistPenalty, low.FireResistance);
        Assert.Equal(regular.EnergyResistance - LowQualityLoot.ResistPenalty, low.EnergyResistance);
        Assert.Equal(regular.GetDurabilityBonus() - LowQualityLoot.DurabilityPenalty, low.GetDurabilityBonus());
    }

    [Fact]
    public void LowQualityArmor_ResistPenaltyFloorsAtZero()
    {
        var regular = Track(new PlateChest());
        var low = Track(new PlateChest { Quality = ArmorQuality.Low });

        // Push fire to exactly the penalty, and cold to zero, via the per-item bonus.
        regular.FireBonus = low.FireBonus = LowQualityLoot.ResistPenalty - regular.BaseFireResistance;
        regular.ColdBonus = low.ColdBonus = -regular.BaseColdResistance;

        Assert.Equal(LowQualityLoot.ResistPenalty, regular.FireResistance);
        Assert.Equal(0, low.FireResistance);
        Assert.Equal(0, low.ColdResistance);
    }

        [Fact]
    public void LowQualityWeapon_LosesDurability()
    {
        var regular = Track(new Halberd());
        var low = Track(new Halberd { Quality = WeaponQuality.Low });

        Assert.Equal(regular.GetDurabilityBonus() - LowQualityLoot.DurabilityPenalty, low.GetDurabilityBonus());
    }

    [Fact]
    public void TryApply_IgnoresClothingAndNullKiller()
    {
        var robe = Track(new Robe());
        LowQualityLoot.TryApply(robe, null);
        Assert.Equal(ClothingQuality.Regular, robe.Quality);

        var chest = Track(new PlateChest());
        LowQualityLoot.TryApply(chest, null);
        Assert.Equal(ArmorQuality.Regular, chest.Quality);
    }
}
