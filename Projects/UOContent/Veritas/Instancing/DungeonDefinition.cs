using System;
using System.Collections.Generic;
using Server.Regions;

namespace Server.Veritas.Instancing;

/// <summary>
/// A dungeon that can be instanced: a top-level <see cref="DungeonRegion"/> loaded from regions.json on a base map.
/// </summary>
public sealed class DungeonDefinition
{
    private static List<DungeonDefinition> _all;

    private DungeonDefinition(DungeonRegion region)
    {
        Region = region;
        Area = region.Area;
        Bounds = new Rectangle2D[Area.Length];
        for (var i = 0; i < Area.Length; i++)
        {
            var rect = Area[i];
            Bounds[i] = new Rectangle2D(rect.Start.X, rect.Start.Y, rect.Width, rect.Height);
        }

        Entrance = region.Entrance != Point3D.Zero ? region.Entrance : FindInsideSpot(region.Map, Area[0]);
        GoLocation = region.GoLocation != Point3D.Zero ? region.GoLocation : FindInsideSpot(region.Map, Area[0]);
    }

    public DungeonRegion Region { get; }

    public string Name => Region.Name;

    public Map Map => Region.Map;

    public Rectangle3D[] Area { get; }

    public Rectangle2D[] Bounds { get; }

    /// <summary>Outside the dungeon on the base map; where players go when they have no recorded return point.</summary>
    public Point3D Entrance { get; }

    /// <summary>Inside the dungeon; where players arrive in an instance.</summary>
    public Point3D GoLocation { get; }

    public static IReadOnlyList<DungeonDefinition> All => _all ??= Build();

    public static DungeonDefinition FromRegion(DungeonRegion region) =>
        IsInstanceable(region) ? new DungeonDefinition(region) : null;

    /// <summary>Finds a dungeon by name, preferring the given base map since Felucca and Trammel share dungeon names.</summary>
    public static DungeonDefinition Find(string name, Map preferredMap = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        DungeonDefinition fallback = null;
        var all = All;
        for (var i = 0; i < all.Count; i++)
        {
            var def = all[i];
            if (!def.Name.InsensitiveEquals(name))
            {
                continue;
            }

            if (preferredMap == null || def.Map == preferredMap)
            {
                return def;
            }

            fallback ??= def;
        }

        return fallback;
    }

    public static DungeonDefinition FindAt(Map baseMap, Point3D p)
    {
        var all = All;
        for (var i = 0; i < all.Count; i++)
        {
            var def = all[i];
            if (def.Map == baseMap && def.Contains(p))
            {
                return def;
            }
        }

        return null;
    }

    public bool Contains(Point3D p) => Contains(Area, p);

    public static bool Contains(Rectangle3D[] area, Point3D p)
    {
        for (var i = 0; i < area.Length; i++)
        {
            if (area[i].Contains(p))
            {
                return true;
            }
        }

        return false;
    }

    internal static void Reset() => _all = null;

    private static bool IsInstanceable(DungeonRegion region) =>
        region is not InstanceDungeonRegion && region.Area.Length > 0 && !string.IsNullOrEmpty(region.Name) &&
        region.Map != null && region.Map != Map.Internal && !InstanceManager.IsInstanceMap(region.Map) &&
        region.Parent is not DungeonRegion;

    private static List<DungeonDefinition> Build()
    {
        var list = new List<DungeonDefinition>();
        var regions = Server.Region.Regions;
        for (var i = 0; i < regions.Count; i++)
        {
            if (regions[i] is DungeonRegion dr && IsInstanceable(dr))
            {
                list.Add(new DungeonDefinition(dr));
            }
        }

        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static Point3D FindInsideSpot(Map map, Rectangle3D rect)
    {
        var x = rect.Start.X + rect.Width / 2;
        var y = rect.Start.Y + rect.Height / 2;
        return new Point3D(x, y, map.GetAverageZ(x, y));
    }
}
