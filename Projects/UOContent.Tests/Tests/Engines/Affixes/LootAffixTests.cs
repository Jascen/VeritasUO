using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Affixes;
using Server.Items;
using Xunit;

namespace UOContent.Tests.Engines.Affixes;

[Collection("Sequential UOContent Tests")]
public class LootAffixTests : IDisposable
{
    private const int Rolls = 300;

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

    public static TheoryData<string> ItemKinds => ["melee", "ranged", "armor", "shield", "hat", "jewel"];

    private Item Create(string kind) => Track<Item>(
        kind switch
        {
            "melee"  => new Katana(),
            "ranged" => new Bow(),
            "armor"  => new PlateChest(),
            "shield" => new Buckler(),
            "hat"    => new SkullCap(),
            _        => new GoldRing()
        }
    );

    [Theory]
    [InlineData(0, 0, ItemLevel.Min)]
    [InlineData(30, 10, 40)]
    [InlineData(80, 25, ItemLevel.Max)]
    [InlineData(-5, 0, ItemLevel.Min)]
    public void ItemLevel_ClampsToRange(int monster, int dungeon, int expected)
    {
        Assert.Equal(expected, ItemLevel.Compute(monster, dungeon));
    }

    [Fact]
    public void DungeonBonus_IsZeroWithoutRegion()
    {
        Assert.Equal(0, DungeonDifficulty.GetBonus(null));
    }

    [Fact]
    public void Definitions_AreConsistent()
    {
        var groupCount = Enum.GetValues<AffixGroup>().Length;
        Assert.True(groupCount <= 64);

        foreach (var def in LootAffixDefinitions.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Name));
            Assert.True(def.MinIlvl <= def.MaxIlvl, def.Name);
            Assert.True(def.Frequency > 0, def.Name);
            Assert.NotEmpty(def.Mods);
            Assert.Same(def, LootAffixDefinitions.Get(def.Id));

            foreach (var mod in def.Mods)
            {
                Assert.True(mod.Min <= mod.Max, def.Name);
            }
        }
    }

    [Fact]
    public void RareChance_ScalesWithLuckAndIsCapped()
    {
        Assert.Equal(0.03, LootAffixGenerator.GetRareChance(0, 50, 0), 6);
        Assert.Equal(0.15, LootAffixGenerator.GetRareChance(0, 50, 1200), 6);
        Assert.Equal(0.20, LootAffixGenerator.GetRareChance(25, 100, 0), 6);
        Assert.Equal(LootAffixGenerator.MaxRareChance, LootAffixGenerator.GetRareChance(50, 100, 100_000));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(800)]
    [InlineData(1600)]
    public void LuckFromChance_InvertsLootPackLuckChance(int luck)
    {
        var chance = (int)(Math.Pow(luck, 1 / 1.8) * 100);
        Assert.InRange(LootAffixGenerator.LuckFromChance(chance), luck - 5, luck + 5);
    }

    [Theory]
    [MemberData(nameof(ItemKinds))]
    public void Generator_RespectsGroupsTiersAndMasks(string kind)
    {
        foreach (var ilvl in new[] { 1, 30, 60, 99 })
        {
            for (var i = 0; i < Rolls; i++)
            {
                var item = Create(kind);
                var mask = LootAffixGenerator.GetMask(item);

                Assert.True(LootAffixGenerator.Apply(item, ilvl, i % 2 == 0 ? 1.0 : 0.0));

                var affixes = ((ILootAffixItem)item).LootAffixes;
                Assert.NotNull(affixes);
                Assert.InRange(affixes.Prefixes.Length, 0, LootAffixGenerator.MaxAffixesPerSlot);
                Assert.InRange(affixes.Suffixes.Length, 0, LootAffixGenerator.MaxAffixesPerSlot);

                if (affixes.Rarity == ItemRarity.Magic)
                {
                    Assert.InRange(affixes.Prefixes.Length + affixes.Suffixes.Length, 1, 2);
                    Assert.True(affixes.Prefixes.Length <= 1 && affixes.Suffixes.Length <= 1);
                }

                var groups = new HashSet<AffixGroup>();
                var families = new HashSet<int>();

                CheckSlot(affixes.Prefixes, AffixSlot.Prefix);
                CheckSlot(affixes.Suffixes, AffixSlot.Suffix);

                if (item is IAosItem aos)
                {
                    foreach (var id in affixes.Prefixes)
                    {
                        CheckAttributeRange(aos, LootAffixDefinitions.Get(id));
                    }

                    foreach (var id in affixes.Suffixes)
                    {
                        CheckAttributeRange(aos, LootAffixDefinitions.Get(id));
                    }
                }

                CheckResistCap(item);
                item.Delete();

                void CheckSlot(int[] ids, AffixSlot slot)
                {
                    foreach (var id in ids)
                    {
                        var def = LootAffixDefinitions.Get(id);
                        Assert.NotNull(def);
                        Assert.Equal(slot, def.Slot);
                        Assert.InRange(ilvl, def.MinIlvl, def.MaxIlvl);
                        Assert.NotEqual(AffixItemMask.None, def.Mask & mask);
                        Assert.True(families.Add(def.Family), def.Name);

                        if (def.Group != AffixGroup.None)
                        {
                            Assert.True(groups.Add(def.Group), def.Name);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void HighLevelRares_ReachSixAffixes()
    {
        var max = 0;

        for (var i = 0; i < Rolls; i++)
        {
            var item = Create("melee");
            LootAffixGenerator.Apply(item, ItemLevel.Max, 1.0);

            var affixes = ((ILootAffixItem)item).LootAffixes;
            max = Math.Max(max, affixes.Prefixes.Length + affixes.Suffixes.Length);
            item.Delete();
        }

        Assert.Equal(6, max);
    }

    [Theory]
    [MemberData(nameof(ItemKinds))]
    public void Affixes_RoundTripThroughSerialization(string kind)
    {
        var item = Create(kind);
        Assert.True(LootAffixGenerator.Apply(item, ItemLevel.Max, 1.0));
        var original = ((ILootAffixItem)item).LootAffixes;

        var writer = new BufferWriter(true);
        item.Serialize(writer);
        var buffer = writer.Buffer.AsSpan(0, (int)writer.Position).ToArray();
        writer.Close();

        var copy = Track(CreateBlank(item));
        var reader = new BufferReader(buffer);
        copy.Deserialize(reader);
        Assert.Equal(buffer.Length, reader.Position);

        var loaded = ((ILootAffixItem)copy).LootAffixes;
        Assert.NotNull(loaded);
        Assert.Same(copy, loaded.Owner);
        Assert.Equal(original.Rarity, loaded.Rarity);
        Assert.Equal(original.Prefixes, loaded.Prefixes);
        Assert.Equal(original.Suffixes, loaded.Suffixes);
        Assert.Equal(original.RareName, loaded.RareName);
    }

    [Fact]
    public void PlainItem_SerializesWithoutAffixes()
    {
        var item = Create("melee");

        var writer = new BufferWriter(true);
        item.Serialize(writer);
        var buffer = writer.Buffer.AsSpan(0, (int)writer.Position).ToArray();
        writer.Close();

        var copy = Track(CreateBlank(item));
        copy.Deserialize(new BufferReader(buffer));

        Assert.Null(((ILootAffixItem)copy).LootAffixes);
    }

    private static Item CreateBlank(Item item) =>
        (Item)Activator.CreateInstance(item.GetType(), (Serial)World.NewItem);

    private static void CheckAttributeRange(IAosItem item, LootAffixDef def)
    {
        foreach (var mod in def.Mods)
        {
            if (mod.Kind == AffixStatKind.Attribute)
            {
                Assert.InRange(item.Attributes[(AosAttribute)mod.Stat], mod.Min, mod.Max);
            }
        }
    }

    private static void CheckResistCap(Item item)
    {
        const int cap = LootAffixGenerator.MaxResistBonus;

        switch (item)
        {
            case BaseArmor armor:
                {
                    Assert.True(armor.PhysicalBonus <= cap && armor.FireBonus <= cap && armor.ColdBonus <= cap);
                    Assert.True(armor.PoisonBonus <= cap && armor.EnergyBonus <= cap);
                    break;
                }
            case BaseJewel jewel:
                {
                    var r = jewel.Resistances;
                    Assert.True(r.Physical <= cap && r.Fire <= cap && r.Cold <= cap && r.Poison <= cap && r.Energy <= cap);
                    break;
                }
        }
    }
}
