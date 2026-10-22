using ModernUO.Serialization;
using Server.Engines.PartySystem;

namespace Server.Veritas.Instancing;

/// <summary>
/// Sends whoever walks through or uses it into their party's instance of <see cref="Dungeon"/>. Party members standing
/// at the gate come along.
/// </summary>
[SerializationGenerator(0)]
public partial class DungeonInstanceGate : Item
{
    public const int UseRange = 2;

    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _dungeon;

    [Constructible]
    public DungeonInstanceGate(string dungeon = null) : base(0xF6C)
    {
        _dungeon = dungeon;
        Movable = false;
        Hue = 0x48D;
        Light = LightType.Circle300;
    }

    public override string DefaultName => "a dungeon gate";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (!string.IsNullOrEmpty(_dungeon))
        {
            list.Add(1060658, $"{"Dungeon"}\t{_dungeon}"); // ~1_val~: ~2_val~
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), UseRange))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        Use(from);
    }

    public override bool OnMoveOver(Mobile m)
    {
        if (m.Player)
        {
            Use(m);
        }

        return true;
    }

    public void Use(Mobile from)
    {
        var definition = InstanceManager.Enabled
            ? DungeonDefinition.Find(_dungeon, InstanceManager.GetBaseMap(Map))
            : null;

        if (definition == null)
        {
            from.SendMessage("The gate shimmers, but leads nowhere.");
            return;
        }

        var map = Map;
        var location = GetWorldLocation();

        if (!DungeonInstanceType.Instance.Enter(from, definition))
        {
            return;
        }

        var party = Party.Get(from);
        if (party == null)
        {
            return;
        }

        for (var i = 0; i < party.Members.Count; i++)
        {
            var member = party.Members[i].Mobile;
            if (member != from && member.Map == map && member.InRange(location, UseRange))
            {
                DungeonInstanceType.Instance.Enter(member, definition);
            }
        }
    }
}
