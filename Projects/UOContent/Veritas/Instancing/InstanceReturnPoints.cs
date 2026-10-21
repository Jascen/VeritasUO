using System.Collections.Generic;
using Server.Logging;

namespace Server.Veritas.Instancing;

/// <summary>
/// Where each player stood before entering an instance. Saved so a player who logs out inside an instance can still be
/// sent home after a restart, when the instance itself no longer exists.
/// </summary>
public class InstanceReturnPoints : GenericPersistence
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(InstanceReturnPoints));

    private static readonly Dictionary<Serial, WorldLocation> _points = [];

    private static InstanceReturnPoints _instance;

    public InstanceReturnPoints() : base("InstanceReturnPoints", 10)
    {
    }

    public static int Count => _points.Count;

    public static void Configure()
    {
        _instance = new InstanceReturnPoints();
    }

    public static void Set(Serial serial, WorldLocation location) => _points[serial] = location;

    public static bool TryGet(Serial serial, out WorldLocation location) => _points.TryGetValue(serial, out location);

    public static void Remove(Serial serial) => _points.Remove(serial);

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        writer.WriteEncodedInt(_points.Count);
        foreach (var (serial, location) in _points)
        {
            writer.Write(serial);
            writer.Write(location.Location);
            writer.Write(location.Map);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version

        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            var location = reader.ReadPoint3D();
            var map = reader.ReadMap();

            // A return point on a pool map would send the player into whatever instance reuses that map.
            if (map != null && map != Map.Internal && !InstanceManager.IsInstanceMap(map) && World.FindMobile(serial) != null)
            {
                _points[serial] = new WorldLocation(location, map);
            }
        }

        logger.Information("Loaded instance return points for {Count} characters", _points.Count);
    }
}
