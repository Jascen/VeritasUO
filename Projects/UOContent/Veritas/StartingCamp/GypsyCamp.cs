using ModernUO.CodeGeneratedEvents;
using Server.Logging;
using Server.Mobiles;

namespace Server.Veritas.StartingCamp;

public static class GypsyCamp
{
    public const string MapName = "Ruins";
    public const string CampName = "the Gypsy Camp";

    // Open grass south of the gypsy's tent, so new players walk up to the camp.
    public static readonly Point3D Arrival = new(122, 140, 0);

    private static readonly ILogger logger = LogFactory.GetLogger(typeof(GypsyCamp));

    public static bool Enabled { get; private set; } = true;

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("veritas.gypsyCamp.enabled", true);
    }

    public static bool IsInCamp(Mobile m) => m?.Region?.IsPartOf<GypsyCampRegion>() == true;

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (IsInCamp(pm))
        {
            CampWelcomeGump.DisplayTo(pm);
        }
    }

    /// <summary>
    /// The camp's arrival point, or false when new characters must use the stock start locations instead.
    /// Requires a gypsy on the island so nobody is stranded on a camp whose spawner has not been generated.
    /// </summary>
    public static bool TryGetStartLocation(out CityInfo city)
    {
        city = null;

        if (!Enabled || !Map.TryParse(MapName, null, out var map))
        {
            return false;
        }

        if (Region.Find(Arrival, map) is not { } region || !region.IsPartOf<GypsyCampRegion>())
        {
            logger.Warning("Gypsy camp region is missing at {Location} on {Map}", Arrival, map);
            return false;
        }

        if (!HasGypsy(region.GetRegion<GypsyCampRegion>()))
        {
            logger.Warning("Gypsy camp has no gypsy; generate the Ruins spawners");
            return false;
        }

        city = new CityInfo(CampName, CampName, Arrival.X, Arrival.Y, Arrival.Z, map);
        return true;
    }

    private static bool HasGypsy(GypsyCampRegion region)
    {
        var area = region.Area;

        for (var i = 0; i < area.Length; i++)
        {
            var rect = area[i];
            var bounds = new Rectangle2D(rect.Start.X, rect.Start.Y, rect.Width, rect.Height);

            foreach (var gypsy in region.Map.GetMobilesInBounds<CampGypsy>(bounds))
            {
                if (!gypsy.Deleted)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
