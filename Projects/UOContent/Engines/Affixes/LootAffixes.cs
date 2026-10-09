using System;
using ModernUO.Serialization;

namespace Server.Engines.Affixes;

public enum ItemRarity : byte
{
    Normal,
    Magic,
    Rare
}

/// <summary>
/// Affixes rolled onto a piece of creature loot. The item's real properties live in its
/// AOS attribute objects; this only remembers which affixes produced them, for naming.
/// </summary>
[PropertyObject]
[SerializationGenerator(0)]
public partial class LootAffixes
{
    private const string MagicColor = "#4169E1";
    private const string RareColor = "#FFD700";

    [DirtyTrackingEntity]
    [CanBeNull]
    private Item _owner;

    [SerializableField(0, setter: "private")]
    private ItemRarity _rarity;

    [EncodedInt]
    [SerializableField(1, setter: "private")]
    private int[] _prefixes;

    [EncodedInt]
    [SerializableField(2, setter: "private")]
    private int[] _suffixes;

    [SerializableField(3, setter: "private")]
    private byte _rareName1;

    [SerializableField(4, setter: "private")]
    private byte _rareName2;

    public LootAffixes(Item owner)
    {
        _owner = owner;
        _prefixes = [];
        _suffixes = [];
    }

    public LootAffixes(
        Item owner, ItemRarity rarity, int[] prefixes, int[] suffixes, byte rareName1, byte rareName2
    )
    {
        _owner = owner;
        _rarity = rarity;
        _prefixes = prefixes;
        _suffixes = suffixes;
        _rareName1 = rareName1;
        _rareName2 = rareName2;
    }

    public Item Owner => _owner;

    public bool IsEmpty => _rarity == ItemRarity.Normal;

    [CommandProperty(AccessLevel.GameMaster)]
    public string PrefixNames => JoinNames(_prefixes);

    [CommandProperty(AccessLevel.GameMaster)]
    public string SuffixNames => JoinNames(_suffixes);

    [CommandProperty(AccessLevel.GameMaster)]
    public string RareName => _rarity == ItemRarity.Rare ? $"{RareFirstName} {RareSecondName}" : null;

    private string RareFirstName => LootAffixNames.GetRareFirst(_rareName1);

    private string RareSecondName => LootAffixNames.GetRareSecond(_owner, _rareName2);

    private static string JoinNames(int[] ids)
    {
        if (ids == null || ids.Length == 0)
        {
            return null;
        }

        var names = new string[ids.Length];

        for (var i = 0; i < ids.Length; i++)
        {
            names[i] = LootAffixDefinitions.Get(ids[i])?.Name ?? $"#{ids[i]}";
        }

        return string.Join(", ", names);
    }

    /// <summary>
    /// Adds the Diablo-style title line(s). Returns false when there is nothing to show so the
    /// item falls back to its normal name.
    /// </summary>
    public bool AddNameProperty(IPropertyList list, Item item, string name)
    {
        if (_rarity == ItemRarity.Magic)
        {
            var prefix = _prefixes.Length > 0 ? LootAffixDefinitions.Get(_prefixes[0])?.Name : null;
            var suffix = _suffixes.Length > 0 ? LootAffixDefinitions.Get(_suffixes[0])?.Name : null;

            // ~1_PREFIX~~2_NAME~~3_SUFFIX~
            if (name != null)
            {
                list.Add(
                    1050045,
                    $"{"<BASEFONT COLOR="}{MagicColor}{">"}{prefix}{(prefix != null ? " " : "")}\t{name}\t{(suffix != null ? " " : "")}{suffix}{"</BASEFONT>"}"
                );
            }
            else
            {
                list.Add(
                    1050045,
                    $"{"<BASEFONT COLOR="}{MagicColor}{">"}{prefix}{(prefix != null ? " " : "")}\t{item.LabelNumber:#}\t{(suffix != null ? " " : "")}{suffix}{"</BASEFONT>"}"
                );
            }

            return true;
        }

        if (_rarity == ItemRarity.Rare)
        {
            list.Add(
                1050045,
                $"{"<BASEFONT COLOR="}{RareColor}{">"}{RareFirstName}{" "}\t{RareSecondName}\t{"</BASEFONT>"}"
            );

            if (name != null)
            {
                list.Add(name);
            }
            else
            {
                list.Add(item.LabelNumber);
            }

            return true;
        }

        return false;
    }

    public override string ToString() => "...";
}
