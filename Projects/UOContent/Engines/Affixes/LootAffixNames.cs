namespace Server.Engines.Affixes;

/// <summary>
/// Word lists for rare item names. Items save indices into these lists: only append, never
/// reorder or remove.
/// </summary>
public static class LootAffixNames
{
    private static readonly string[] _first =
    [
        "Beast", "Eagle", "Raven", "Viper", "Ghoul", "Bone", "Blood", "Bramble", "Brimstone", "Corpse",
        "Death", "Demon", "Doom", "Dragon", "Dread", "Empyrean", "Glyph", "Grim", "Hailstone", "Havoc",
        "Imp", "Loath", "Order", "Pain", "Plague", "Rune", "Shadow", "Skull", "Soul", "Spirit",
        "Storm", "Wraith", "Victory", "Wrath", "Gloom", "Ember", "Frost", "Thunder", "Venom", "Star"
    ];

    private static readonly string[] _melee =
    [
        "Bane", "Bite", "Edge", "Fang", "Gnash", "Mar", "Razor", "Scalpel", "Song", "Spike",
        "Stinger", "Thirst", "Wound", "Barb", "Spire", "Sever", "Cleaver", "Reaver", "Fury", "Kiss"
    ];

    private static readonly string[] _ranged =
    [
        "Branch", "Flight", "Horn", "Nock", "Quill", "Song", "Stinger", "Thirst", "Fletch", "Mark",
        "Strike", "Bolt", "Arc", "Wing", "Whisper"
    ];

    private static readonly string[] _armor =
    [
        "Carapace", "Cloak", "Coat", "Hide", "Jack", "Mantle", "Pelt", "Shell", "Suit", "Guard",
        "Ward", "Wrap", "Husk", "Skin", "Harness"
    ];

    private static readonly string[] _shield =
    [
        "Aegis", "Badge", "Bulwark", "Emblem", "Guard", "Rock", "Tower", "Ward", "Wing", "Bastion",
        "Wall", "Mark"
    ];

    private static readonly string[] _hat =
    [
        "Brow", "Casque", "Circlet", "Cowl", "Crest", "Visage", "Veil", "Hood", "Crown", "Mask",
        "Horn", "Dome"
    ];

    private static readonly string[] _jewelry =
    [
        "Band", "Circle", "Coil", "Eye", "Finger", "Grasp", "Hold", "Knot", "Loop", "Spiral",
        "Turn", "Whorl", "Clasp", "Gaze", "Bond", "Charm"
    ];

    public static byte RandomFirst() => (byte)Utility.Random(_first.Length);

    public static byte RandomSecond(AffixItemMask mask) => (byte)Utility.Random(GetSecondList(mask).Length);

    public static string GetRareFirst(byte index) => index < _first.Length ? _first[index] : _first[0];

    public static string GetRareSecond(Item item, byte index)
    {
        var list = GetSecondList(LootAffixGenerator.GetMask(item));
        return index < list.Length ? list[index] : list[0];
    }

    private static string[] GetSecondList(AffixItemMask mask) => mask switch
    {
        AffixItemMask.Ranged  => _ranged,
        AffixItemMask.Armor   => _armor,
        AffixItemMask.Shield  => _shield,
        AffixItemMask.Hat     => _hat,
        AffixItemMask.Jewelry => _jewelry,
        _                     => _melee
    };
}
