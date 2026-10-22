using Server.Mobiles;
using Server.Regions;
using Server.Spells;

namespace Server.Veritas.Instancing;

/// <summary>
/// The copy of a dungeon's region on a pool map. Registered while the instance is live so spawners, music, lighting
/// and travel rules behave as in the real dungeon, minus every way to travel in.
/// </summary>
public class InstanceDungeonRegion : DungeonRegion
{
    public InstanceDungeonRegion(DungeonDefinition definition, Map map)
        : base(definition.Name, map, definition.Region.Priority, definition.Area)
    {
        Entrance = definition.Entrance;
        GoLocation = definition.GoLocation;
        Music = definition.Region.Music;
    }

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;
        return InstanceMapRegion.AllowsTravel(Map, m, travelType);
    }

    public override void OnEnter(Mobile m)
    {
        base.OnEnter(m);

        if (m is PlayerMobile)
        {
            InstanceManager.FindLiveInstance(Map)?.Touch();
        }
    }
}
