using System;
using Server.Regions;
using Server.Spells;

namespace Server.Veritas.Instancing;

/// <summary>
/// The default region of a pool map. It has no area and is never registered: <see cref="Region.Find"/> falls back to
/// <see cref="Map.DefaultRegion"/> for any tile no registered region covers, so this governs the whole clone map.
/// </summary>
public class InstanceMapRegion : BaseRegion
{
    public InstanceMapRegion(Map map) : base(map.Name, map, 0, Array.Empty<Rectangle3D>())
    {
    }

    public override bool AllowHousing(Mobile from, Point3D p) => false;

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;
        return AllowsTravel(Map, m, travelType);
    }

    /// <summary>
    /// SpellHelper asks both the destination and the caster's current region with the same travel type, and the
    /// region only gets a point, not a map. A caster already on the pool map is therefore leaving (recalling or gating
    /// out); anyone else naming this map as a destination is trying to get in.
    /// </summary>
    internal static bool AllowsTravel(Map instanceMap, Mobile m, TravelCheckType travelType)
    {
        if (m?.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        return travelType switch
        {
            // A rune would lead back into a map that will be reused by someone else.
            TravelCheckType.Mark => false,
            // The gate spell opens a return gate at the caster's location, which would let anyone walk in.
            TravelCheckType.GateFrom => false,
            TravelCheckType.RecallTo or TravelCheckType.GateTo => m != null && m.Map == instanceMap,
            _ => true
        };
    }
}
