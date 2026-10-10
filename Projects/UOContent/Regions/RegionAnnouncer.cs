namespace Server.Regions;

public static class RegionAnnouncer
{
    public static void Announce(Mobile m, Region oldRegion, Region newRegion)
    {
        var map = m.Map;

        if (m.NetState == null || map == null || map == Map.Internal)
        {
            return;
        }

        var newName = GetNamedRegion(newRegion)?.Name;

        // Unnamed sub-regions (houses, spawn zones) inherit their parent's name, so moving into one stays silent
        if (oldRegion?.Map == newRegion?.Map && newName == GetNamedRegion(oldRegion)?.Name)
        {
            return;
        }

        if (newName != null)
        {
            m.SendMessage($"Now entering: {newName}");
        }
        else
        {
            m.SendMessage($"Now entering: The land of {map.Name}");
        }
    }

    private static Region GetNamedRegion(Region region)
    {
        while (region != null && !region.IsDefault)
        {
            if (!string.IsNullOrWhiteSpace(region.Name))
            {
                return region;
            }

            region = region.Parent;
        }

        return null;
    }
}
