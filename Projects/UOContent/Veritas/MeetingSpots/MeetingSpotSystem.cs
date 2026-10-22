using System;
using System.Collections.Generic;
using Server.Logging;

namespace Server.Veritas.MeetingSpots;

/// <summary>
/// Small groups of idle townsfolk gathered in conversation circles at <see cref="MeetingSpot"/>s.
/// Each spot rerolls its group on its own schedule so towns change gradually rather than all at once.
/// </summary>
public static class MeetingSpotSystem
{
    private const int SpotsPerTick = 25;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

    // Newly found spots fill in quickly; spots loaded from a save keep their group for a while.
    private static readonly TimeSpan FreshSpotDelay = TimeSpan.FromMinutes(5);

    private static readonly ILogger logger = LogFactory.GetLogger(typeof(MeetingSpotSystem));

    private static readonly List<MeetingSpot> _spots = [];
    private static int _cursor;

    public static bool Enabled { get; private set; } = true;

    /// <summary>Chance that a spot hosts a group after a reroll.</summary>
    public static double Occupancy { get; private set; } = 0.5;

    public static TimeSpan RerollMin { get; private set; } = TimeSpan.FromHours(2);

    public static TimeSpan RerollMax { get; private set; } = TimeSpan.FromHours(5);

    public static int MaxPerTown { get; private set; } = 8;

    public static IReadOnlyList<MeetingSpot> Spots => _spots;

    /// <summary>Facets that get spots detected automatically.</summary>
    public static Map[] Facets => [Map.Felucca, Map.Trammel];

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("veritas.meetingSpots.enabled", true);
        Occupancy = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("veritas.meetingSpots.occupancy", 0.5), 0.0, 1.0);
        RerollMin = ServerConfiguration.GetOrUpdateSetting("veritas.meetingSpots.rerollMin", TimeSpan.FromHours(2));
        RerollMax = ServerConfiguration.GetOrUpdateSetting("veritas.meetingSpots.rerollMax", TimeSpan.FromHours(5));
        MaxPerTown = Math.Max(1, ServerConfiguration.GetOrUpdateSetting("veritas.meetingSpots.maxPerTown", 8));

        if (RerollMin < TimeSpan.FromMinutes(1))
        {
            RerollMin = TimeSpan.FromMinutes(1);
        }

        if (RerollMax < RerollMin)
        {
            RerollMax = RerollMin;
        }
    }

    public static void Initialize()
    {
        if (Enabled)
        {
            EventSink.ServerStarted += OnServerStarted;
        }
    }

    private static void OnServerStarted()
    {
        Timer.DelayCall(TickInterval, TickInterval, Tick);

        foreach (var map in Facets)
        {
            if (map != null && CountSpots(map) == 0)
            {
                logger.Information("No meeting spots on {Map}; detecting town plazas", map);
                MeetingSpotFinder.Start(map);
            }
        }
    }

    internal static void Register(MeetingSpot spot, bool fresh)
    {
        _spots.Add(spot);

        var window = fresh ? FreshSpotDelay : RerollMax;
        spot.NextReroll = Core.Now + TimeSpan.FromSeconds(Utility.RandomDouble() * window.TotalSeconds);
    }

    internal static void Unregister(MeetingSpot spot)
    {
        var index = _spots.IndexOf(spot);

        if (index < 0)
        {
            return;
        }

        _spots.RemoveAt(index);

        if (index < _cursor)
        {
            _cursor--;
        }
    }

    public static TimeSpan RandomRerollDelay() =>
        RerollMin + TimeSpan.FromSeconds(Utility.RandomDouble() * (RerollMax - RerollMin).TotalSeconds);

    public static int CountSpots(Map map)
    {
        var count = 0;

        for (var i = 0; i < _spots.Count; i++)
        {
            if (_spots[i].Map == map)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// True when a player could see the spot, so a reroll would make people visibly pop in or out.
    /// Staff do not count, so they can watch rerolls happen.
    /// </summary>
    public static bool IsWatched(MeetingSpot spot)
    {
        foreach (var ns in spot.Map.GetClientsInRange(spot.Location))
        {
            if (ns.Mobile?.AccessLevel == AccessLevel.Player)
            {
                return true;
            }
        }

        return false;
    }

    private static void Tick()
    {
        if (!Enabled)
        {
            return;
        }

        var count = Math.Min(SpotsPerTick, _spots.Count);

        for (var i = 0; i < count; i++)
        {
            if (_cursor >= _spots.Count)
            {
                _cursor = 0;
            }

            _spots[_cursor++].CheckReroll();
        }
    }
}
