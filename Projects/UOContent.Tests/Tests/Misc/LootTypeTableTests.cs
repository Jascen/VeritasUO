using System;
using Server;
using Server.Items;
using Xunit;

namespace UOContent.Tests;

public class LootTypeTableTests
{
    public static TheoryData<string> ClothingTables => ["ML", "SE", "AOS", "Base", "SEHat", "AOSHat", "Hat"];

    private static Type[] GetTable(string name) => name switch
    {
        "ML"     => Loot.MLClothingTypes,
        "SE"     => Loot.SEClothingTypes,
        "AOS"    => Loot.AosClothingTypes,
        "Base"   => Loot.ClothingTypes,
        "SEHat"  => Loot.SEHatTypes,
        "AOSHat" => Loot.AosHatTypes,
        _        => Loot.HatTypes
    };

    // Loot.Construct<T> discards an instance that is not a T, leaving it orphaned in the world.
    [Theory]
    [MemberData(nameof(ClothingTables))]
    public void ClothingTables_OnlyContainClothing(string name)
    {
        var expected = name.EndsWith("Hat", StringComparison.Ordinal) ? typeof(BaseHat) : typeof(BaseClothing);

        foreach (var type in GetTable(name))
        {
            Assert.True(type.IsSubclassOf(expected), $"{type.Name} in {name} is not a {expected.Name}");
        }
    }
}
