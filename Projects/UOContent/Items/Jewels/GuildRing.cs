using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Token of NPC guild membership. Grants the guild's skill bonuses and can only be worn by
/// its owner while they remain in that guild.
/// </summary>
[SerializationGenerator(0)]
public partial class GuildRing : BaseRing
{
    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _owner;

    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private NpcGuild _guild;

    [Constructible]
    public GuildRing(NpcGuild guild) : this(guild, null)
    {
    }

    public GuildRing(NpcGuild guild, Mobile owner) : base(0x108a)
    {
        _guild = guild;
        _owner = owner;
        LootType = LootType.Blessed;

        var bonuses = NpcGuildBenefits.GetSkillBonuses(guild);
        for (var i = 0; i < bonuses.Length; i++)
        {
            SkillBonuses.SetValues(i, bonuses[i].Skill, bonuses[i].Bonus);
        }
    }

    public override string DefaultName => "guild ring";

    public override double DefaultWeight => 0.1;

    public override void AddNameProperties(IPropertyList list)
    {
        base.AddNameProperties(list);

        list.Add(1070722, $"{"Member of the"} {NpcGuildBenefits.GetName(_guild)}"); // ~1_NOTHING~

        if (_owner != null)
        {
            list.Add(1049644, _owner.Name); // [~1_stuff~]
        }
    }

    public override bool CanEquip(Mobile from)
    {
        if (from.AccessLevel < AccessLevel.GameMaster)
        {
            if (from != _owner)
            {
                from.SendMessage("This is not your guild ring!");
                return false;
            }

            if (from is not PlayerMobile pm || pm.NpcGuild != _guild)
            {
                from.SendMessage("You are no longer a member of that guild.");
                return false;
            }
        }

        return base.CanEquip(from);
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        _owner = null;
    }
}
