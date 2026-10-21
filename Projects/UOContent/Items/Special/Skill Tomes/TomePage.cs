using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items;

/// <summary>
/// One section of a skill's tome: a single page, or a multi-page folio for rare and ultra rare drops.
/// Bound into a <see cref="SkillTome"/>, each of its pages grants +1 to that skill.
/// </summary>
[SerializationGenerator(0)]
public partial class TomePage : Item
{
    // Rarity weights (common, uncommon, rare, ultra rare) for ordinary drops and for champion-tier drops.
    private static readonly int[] _standardWeights = [75, 18, 6, 1];
    private static readonly int[] _championWeights = [45, 30, 18, 7];

    private static SkillName[] _availableSkills;

    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private SkillName _skill;

    [InvalidateProperties]
    [SerializableField(1, allowFieldChange: nameof(AllowSectionChange), fieldChanged: nameof(OnSectionChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _section;

    /// <param name="section">Index into <see cref="SkillTomeInfo.Sections"/>.</param>
    [Constructible]
    public TomePage(SkillName skill = SkillName.Alchemy, int section = 0) : base(0x14F0)
    {
        _skill = skill;
        _section = SkillTomeInfo.IsValidSection(section) ? section : 0;
        Hue = SkillTomeInfo.GetRarityHue(Rarity);
        LootType = LootType.Cursed;
    }

    public override double DefaultWeight => 0.1;

    public TomeSection Info => SkillTomeInfo.Sections[_section];

    public PageRarity Rarity => Info.Rarity;

    private bool AllowSectionChange(ref int value) => SkillTomeInfo.IsValidSection(value);

    private void OnSectionChanged(int oldValue, int newValue) => Hue = SkillTomeInfo.GetRarityHue(Rarity);

    private static SkillName[] AvailableSkills
    {
        get
        {
            if (_availableSkills == null)
            {
                var count = 0;
                var table = SkillInfo.Table;
                var skills = new SkillName[table.Length];

                for (var i = 0; i < table.Length; i++)
                {
                    var skill = (SkillName)table[i].SkillID;

                    if (SkillTomeInfo.IsAvailable(skill))
                    {
                        skills[count++] = skill;
                    }
                }

                _availableSkills = skills[..count];
            }

            return _availableSkills;
        }
    }

    public static TomePage CreateRandom(bool championTier = false) =>
        Create(AvailableSkills.RandomElement(), RollRarity(championTier ? _championWeights : _standardWeights));

    public static TomePage Create(SkillName skill, PageRarity rarity)
    {
        var (first, last) = SkillTomeInfo.GetSectionRange(rarity);
        return new TomePage(skill, Utility.RandomMinMax(first, last));
    }

    private static PageRarity RollRarity(int[] weights)
    {
        var total = 0;

        for (var i = 0; i < weights.Length; i++)
        {
            total += weights[i];
        }

        var roll = Utility.Random(total);

        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];

            if (roll < 0)
            {
                return (PageRarity)i;
            }
        }

        return PageRarity.Common;
    }

    public override void AddNameProperty(IPropertyList list)
    {
        var (author, _, _) = SkillTomeInfo.GetFlavor(_skill);
        list.Add($"{SkillTomeInfo.GetMaterialName(Rarity)}{" of "}{author}");
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        var info = Info;
        var (_, title, _) = SkillTomeInfo.GetFlavor(_skill);

        if (info.PageCount == 1)
        {
            list.Add($"{SkillTomeInfo.GetRarityName(info.Rarity)}{" - page "}{info.FirstPage}{" of "}{title}");
        }
        else
        {
            list.Add(
                $"{SkillTomeInfo.GetRarityName(info.Rarity)}{" - pages "}{info.FirstPage}{"-"}{info.LastPage}{" of "}{title}"
            );
        }

        list.Add($"{"+"}{info.PageCount}{" "}{SkillTomeInfo.GetSkillName(_skill)}{" when bound in a tome"}");
    }

    public override void OnSingleClick(Mobile from)
    {
        var (author, _, _) = SkillTomeInfo.GetFlavor(_skill);
        var info = Info;

        if (info.PageCount == 1)
        {
            LabelTo(from, $"{SkillTomeInfo.GetMaterialName(info.Rarity)} of {author} (page {info.FirstPage})");
        }
        else
        {
            LabelTo(
                from,
                $"{SkillTomeInfo.GetMaterialName(info.Rarity)} of {author} (pages {info.FirstPage}-{info.LastPage})"
            );
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        from.SendMessage("Select a tome to bind this page into.");
        from.Target = new BindTarget(this);
    }

    private class BindTarget : Target
    {
        private readonly TomePage _page;

        public BindTarget(TomePage page) : base(2, false, TargetFlags.None) => _page = page;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is SkillTome tome)
            {
                tome.TryAddPage(from, _page);
            }
            else
            {
                from.SendMessage("That is not a tome.");
            }
        }
    }
}
