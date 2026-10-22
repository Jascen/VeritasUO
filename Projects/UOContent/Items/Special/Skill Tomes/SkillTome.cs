using System.Numerics;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

/// <summary>
/// Holds up to 20 virtual <see cref="TomePage"/>s for one skill. Blank until the first page binds it to that page's skill.
/// Each bound page grants +1 to the skill while the tome sits directly in the owner's backpack.
/// </summary>
[SerializationGenerator(0)]
public partial class SkillTome : Item
{
    [InvalidateProperties]
    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private SkillName _skill;

    // Bit (n - 1) set means page n is bound.
    [InvalidateProperties]
    [SerializableField(1, setter: "private")]
    private int _pages;

    [Constructible]
    public SkillTome() : base(0x0FEF) => Hue = 0x48D;

    public override double DefaultWeight => 3.0;

    public bool IsBound => _pages != 0;

    [CommandProperty(AccessLevel.GameMaster)]
    public int PageTotal => BitOperations.PopCount((uint)_pages);

    public bool HasSection(int section) => (_pages & SkillTomeInfo.Sections[section].Mask) != 0;

    public override void AddNameProperty(IPropertyList list)
    {
        if (IsBound)
        {
            var (_, title, _) = SkillTomeInfo.GetFlavor(_skill);
            list.Add(title);
        }
        else
        {
            list.Add("a blank tome");
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (!IsBound)
        {
            list.Add("Bind a page into this tome to begin");
            return;
        }

        var total = PageTotal;
        list.Add($"{SkillTomeInfo.GetSkillName(_skill)}{" +"}{total}");
        list.Add($"{"Pages: "}{total}{" / "}{SkillTomeInfo.PageCount}");

        var (_, _, lore) = SkillTomeInfo.GetFlavor(_skill);

        if (lore != null)
        {
            list.Add(lore);
        }

        list.Add("Grants its bonus only while loose in your backpack");
    }

    public override void OnSingleClick(Mobile from)
    {
        if (IsBound)
        {
            var (_, title, _) = SkillTomeInfo.GetFlavor(_skill);
            LabelTo(from, $"{title} ({PageTotal}/{SkillTomeInfo.PageCount})");
        }
        else
        {
            LabelTo(from, "a blank tome");
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        if (IsBound)
        {
            SkillTomeGump.DisplayTo(from, this);
        }
        else
        {
            BeginBind(from);
        }
    }

    public void BeginBind(Mobile from)
    {
        from.SendMessage("Select a page to bind into this tome.");
        from.Target = new BindTarget(this);
    }

    public override bool OnDragDrop(Mobile from, Item dropped) =>
        dropped is TomePage page ? TryAddPage(from, page) : base.OnDragDrop(from, dropped);

    public bool TryAddPage(Mobile from, TomePage page)
    {
        if (Deleted || page?.Deleted != false)
        {
            return false;
        }

        if (!IsChildOf(from.Backpack) || !page.IsChildOf(from.Backpack))
        {
            from.SendMessage("Both the tome and the page must be in your backpack.");
            return false;
        }

        if (IsBound && page.Skill != _skill)
        {
            from.SendMessage("That page does not belong in this tome.");
            return false;
        }

        var info = page.Info;

        if (HasSection(page.Section))
        {
            from.SendMessage("This tome already holds that page.");
            return false;
        }

        if (!IsBound)
        {
            Skill = page.Skill;
        }

        Pages = _pages | info.Mask;
        page.Delete();

        if (info.PageCount == 1)
        {
            from.SendMessage($"You bind page {info.FirstPage} into the tome.");
        }
        else
        {
            from.SendMessage($"You bind pages {info.FirstPage}-{info.LastPage} into the tome.");
        }

        from.PlaySound(0x249);

        SkillTomeBonus.Refresh(SkillTomeBonus.GetBackpackOwner(Parent));
        return true;
    }

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);
        SkillTomeBonus.Refresh(SkillTomeBonus.GetBackpackOwner(parent));
    }

    public override void OnRemoved(IEntity parent)
    {
        base.OnRemoved(parent);
        SkillTomeBonus.Refresh(SkillTomeBonus.GetBackpackOwner(parent));
    }

    // Skill mods are not saved, so rebuild the owner's bonus once the world has loaded.
    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Timer.DelayCall(() => SkillTomeBonus.Refresh(SkillTomeBonus.GetBackpackOwner(Parent)));
    }

    private class BindTarget : Target
    {
        private readonly SkillTome _tome;

        public BindTarget(SkillTome tome) : base(2, false, TargetFlags.None) => _tome = tome;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is TomePage page)
            {
                _tome.TryAddPage(from, page);
            }
            else
            {
                from.SendMessage("That is not a tome page.");
            }
        }
    }

    private class SkillTomeGump : DynamicGump
    {
        private const int Width = 560;
        private const int RowHeight = 20;
        private const int ListTop = 85;
        private static readonly int Rows = (SkillTomeInfo.Sections.Length + 1) / 2;

        private readonly SkillTome _tome;

        public override bool Singleton => true;

        private SkillTomeGump(SkillTome tome) : base(50, 50) => _tome = tome;

        public static void DisplayTo(Mobile from, SkillTome tome)
        {
            if (from?.NetState == null || tome?.Deleted != false || !tome.IsBound)
            {
                return;
            }

            from.SendGump(new SkillTomeGump(tome));
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            var height = ListTop + Rows * RowHeight + 50;
            var (_, title, lore) = SkillTomeInfo.GetFlavor(_tome.Skill);

            builder.AddPage();

            builder.AddBackground(0, 0, Width, height, 5054);
            builder.AddImageTiled(8, 8, Width - 16, height - 16, 2624);
            builder.AddAlphaRegion(8, 8, Width - 16, height - 16);

            builder.AddHtml(15, 15, Width - 30, 20, title, "#FFFFFF", align: TextAlignment.Center);
            builder.AddHtml(
                15,
                37,
                Width - 30,
                20,
                $"{SkillTomeInfo.GetSkillName(_tome.Skill)} +{_tome.PageTotal} ({_tome.PageTotal}/{SkillTomeInfo.PageCount} pages)",
                "#C0C0C0",
                align: TextAlignment.Center
            );

            if (lore != null)
            {
                builder.AddHtml(15, 57, Width - 30, 20, lore, "#A0A0A0", align: TextAlignment.Center);
            }

            var sections = SkillTomeInfo.Sections;

            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                var x = i < Rows ? 25 : Width / 2 + 5;
                var y = ListTop + i % Rows * RowHeight;
                var material = SkillTomeInfo.GetMaterialName(section.Rarity);
                var color = _tome.HasSection(i) ? SkillTomeInfo.GetRarityColor(section.Rarity) : "#606060";

                if (section.PageCount == 1)
                {
                    builder.AddHtml(x, y, Width / 2 - 30, 20, $"{material} - page {section.FirstPage} (+1)", color);
                }
                else
                {
                    builder.AddHtml(
                        x,
                        y,
                        Width / 2 - 30,
                        20,
                        $"{material} - pages {section.FirstPage}-{section.LastPage} (+{section.PageCount})",
                        color
                    );
                }
            }

            builder.AddButton(30, height - 35, 4005, 4007, 1);
            builder.AddHtml(65, height - 33, 200, 20, "Bind a page", "#FFFFFF");
        }

        public override void OnResponse(NetState state, in RelayInfo info)
        {
            var from = state.Mobile;

            if (info.ButtonID != 1 || _tome.Deleted || !_tome.IsChildOf(from.Backpack))
            {
                return;
            }

            _tome.BeginBind(from);
        }
    }
}
