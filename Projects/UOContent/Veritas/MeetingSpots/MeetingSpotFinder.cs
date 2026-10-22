using System;
using System.Collections.Generic;
using Server.Items;
using Server.Logging;
using Server.Regions;

namespace Server.Veritas.MeetingSpots;

/// <summary>
/// Finds open, uncovered plazas in a facet's towns and places <see cref="MeetingSpot"/>s on them.
/// Classifying tiles reads map, region and item state, so it runs on the game loop in bounded
/// slices per tick, one town and one facet at a time.
/// </summary>
public sealed class MeetingSpotFinder
{
    public const short Blocked = short.MinValue;

    // A 5x5 patch fits a circle on foot; 7x7 fits mounted riders.
    public const int FootRadius = 2;
    public const int RoomyRadius = 3;

    public const int Stride = 2;
    public const int MinSpacing = 12;
    public const int CandidatesPerSpot = 150;
    public const int MaxStepZ = 2;

    private const int PersonHeight = 16;
    private const int PassageClearance = 3;
    private const int TilesPerTick = 1000;
    private const int MaxTownArea = 1024 * 1024;
    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(50);

    private static readonly ILogger logger = LogFactory.GetLogger(typeof(MeetingSpotFinder));

    private static readonly Queue<Map> _pending = new();
    private static MeetingSpotFinder _current;

    private readonly Map _map;
    private readonly List<TownRegion> _towns = [];
    private int _townIndex = -1;
    private TownRegion _town;
    private Rectangle2D _bounds;
    private short[] _grid;
    private int _tileCursor;
    private int _placed;
    private TimerExecutionToken _timer;

    private MeetingSpotFinder(Map map)
    {
        _map = map;

        foreach (var region in Region.Regions)
        {
            if (region is TownRegion { Parent: null } town && town.Map == map)
            {
                _towns.Add(town);
            }
        }
    }

    public readonly record struct Candidate(int X, int Y, int Z, bool Roomy);

    public static bool IsBusy(Map map) => _current?._map == map || _pending.Contains(map);

    /// <summary>Queues detection for <paramref name="map"/>. Facets are scanned one after another.</summary>
    public static bool Start(Map map)
    {
        if (map == null || map == Map.Internal || IsBusy(map))
        {
            return false;
        }

        _pending.Enqueue(map);

        if (_current == null)
        {
            StartNext();
        }

        return true;
    }

    private static void StartNext()
    {
        if (!_pending.TryDequeue(out var map))
        {
            _current = null;
            return;
        }

        var job = new MeetingSpotFinder(map);
        _current = job;
        Timer.StartTimer(TimeSpan.Zero, StepInterval, job.Step, out job._timer);
    }

    private void Step()
    {
        if (_town == null && !NextTown())
        {
            Finish();
            return;
        }

        var area = _bounds.Width * _bounds.Height;
        var end = Math.Min(area, _tileCursor + TilesPerTick);

        for (; _tileCursor < end; _tileCursor++)
        {
            var x = _bounds.X + _tileCursor % _bounds.Width;
            var y = _bounds.Y + _tileCursor / _bounds.Width;
            _grid[_tileCursor] = InArea(_town, x, y) ? ClassifyTile(_map, _town, x, y) : Blocked;
        }

        if (_tileCursor >= area)
        {
            PlaceSpots();
            _town = null;
            _grid = null;
        }
    }

    private bool NextTown()
    {
        while (++_townIndex < _towns.Count)
        {
            var town = _towns[_townIndex];

            if (!TryGetBounds(town, _map, out var bounds))
            {
                continue;
            }

            if (bounds.Width * bounds.Height > MaxTownArea)
            {
                logger.Warning("Skipping meeting spots for {Town} on {Map}: region is too large", town.Name, _map);
                continue;
            }

            _town = town;
            _bounds = bounds;
            _grid = new short[bounds.Width * bounds.Height];
            _tileCursor = 0;
            return true;
        }

        return false;
    }

    private void Finish()
    {
        _timer.Cancel();
        logger.Information("Placed {Count} meeting spots on {Map}", _placed, _map);
        StartNext();
    }

    private void PlaceSpots()
    {
        var candidates = FindCandidates(_grid, _bounds.Width, _bounds.Height, _bounds.X, _bounds.Y);

        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            if (IsNearPassageOrSpot(_map, candidates[i]))
            {
                candidates.RemoveAt(i);
            }
        }

        var total = candidates.Count;
        var chosen = SelectSpots(candidates, SpotCap(total, MeetingSpotSystem.MaxPerTown), MinSpacing);

        for (var i = 0; i < chosen.Count; i++)
        {
            var c = chosen[i];
            var spot = new MeetingSpot { Roomy = c.Roomy };
            spot.MoveToWorld(new Point3D(c.X, c.Y, c.Z), _map);
        }

        _placed += chosen.Count;

        logger.Information(
            "Meeting spots for {Town} on {Map}: placed {Count} from {Candidates} open patches",
            _town.Name,
            _map,
            chosen.Count,
            total
        );
    }

    private static bool TryGetBounds(Region town, Map map, out Rectangle2D bounds)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        foreach (var rect in town.Area)
        {
            minX = Math.Min(minX, rect.Start.X);
            minY = Math.Min(minY, rect.Start.Y);
            maxX = Math.Max(maxX, rect.End.X);
            maxY = Math.Max(maxY, rect.End.Y);
        }

        minX = Math.Max(minX, 0);
        minY = Math.Max(minY, 0);
        maxX = Math.Min(maxX, map.Width);
        maxY = Math.Min(maxY, map.Height);

        if (minX >= maxX || minY >= maxY)
        {
            bounds = default;
            return false;
        }

        bounds = new Rectangle2D(minX, minY, maxX - minX, maxY - minY);
        return true;
    }

    private static bool InArea(Region region, int x, int y)
    {
        foreach (var rect in region.Area)
        {
            if (x >= rect.Start.X && x < rect.End.X && y >= rect.Start.Y && y < rect.End.Y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The Z a person would stand at on this tile, or <see cref="Blocked"/> when the tile is not open
    /// town ground: unstandable, inside a house, outside the town, or under a roof or upper floor.
    /// </summary>
    internal static short ClassifyTile(Map map, Region town, int x, int y)
    {
        var landZ = map.GetAverageZ(x, y);

        if (!map.CanSpawnMobile(x, y, landZ - 8, landZ + 10, false, false, out var z))
        {
            return Blocked;
        }

        var region = Region.Find(new Point3D(x, y, z), map);

        if (!region.IsPartOf(town) || region.IsPartOf<HouseRegion>() || IsCovered(map, x, y, z))
        {
            return Blocked;
        }

        return (short)z;
    }

    private static bool IsCovered(Map map, int x, int y, int z)
    {
        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
        {
            var data = TileData.ItemTable[tile.ID & TileData.MaxItemValue];

            if ((data.Roof && tile.Z >= z) || (data.Surface && tile.Z >= z + PersonHeight))
            {
                return true;
            }
        }

        return false;
    }

    // Circles must not block doorways, teleporters or gates, or crowd a spot staff placed by hand.
    private static bool IsNearPassageOrSpot(Map map, Candidate c)
    {
        var p = new Point2D(c.X, c.Y);

        foreach (var _ in map.GetItemsInRange<BaseDoor>(p, PassageClearance))
        {
            return true;
        }

        foreach (var _ in map.GetItemsInRange<Teleporter>(p, PassageClearance))
        {
            return true;
        }

        foreach (var _ in map.GetItemsInRange<Moongate>(p, PassageClearance))
        {
            return true;
        }

        foreach (var _ in map.GetItemsInRange<MeetingSpot>(p, MinSpacing - 1))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Centres on a <see cref="Stride"/> grid whose 5x5 patch is all standable within
    /// <see cref="MaxStepZ"/> of the centre. <paramref name="grid"/> holds one stand Z per tile
    /// (row-major, <see cref="Blocked"/> for unusable tiles) with its first cell at the origin.
    /// </summary>
    public static List<Candidate> FindCandidates(short[] grid, int width, int height, int originX, int originY)
    {
        var candidates = new List<Candidate>();

        for (var gy = FootRadius; gy < height - FootRadius; gy += Stride)
        {
            for (var gx = FootRadius; gx < width - FootRadius; gx += Stride)
            {
                var z = grid[gy * width + gx];

                if (z == Blocked || !IsOpen(grid, width, height, gx, gy, FootRadius, z))
                {
                    continue;
                }

                var roomy = IsOpen(grid, width, height, gx, gy, RoomyRadius, z);
                candidates.Add(new Candidate(originX + gx, originY + gy, z, roomy));
            }
        }

        return candidates;
    }

    private static bool IsOpen(short[] grid, int width, int height, int cx, int cy, int radius, int z)
    {
        if (cx - radius < 0 || cy - radius < 0 || cx + radius >= width || cy + radius >= height)
        {
            return false;
        }

        for (var y = cy - radius; y <= cy + radius; y++)
        {
            for (var x = cx - radius; x <= cx + radius; x++)
            {
                var tileZ = grid[y * width + x];

                if (tileZ == Blocked || Math.Abs(tileZ - z) > MaxStepZ)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>How many spots a town gets: one per <see cref="CandidatesPerSpot"/> open patches, at least one.</summary>
    public static int SpotCap(int candidateCount, int maxPerTown) =>
        candidateCount <= 0 ? 0 : Math.Clamp(candidateCount / CandidatesPerSpot, 1, maxPerTown);

    /// <summary>
    /// Picks up to <paramref name="cap"/> candidates at random, roomy patches first, keeping every pair at
    /// least <paramref name="minSpacing"/> tiles apart.
    /// </summary>
    public static List<Candidate> SelectSpots(List<Candidate> candidates, int cap, int minSpacing)
    {
        var chosen = new List<Candidate>();

        if (cap <= 0)
        {
            return chosen;
        }

        candidates.Shuffle();
        candidates.Sort(static (a, b) => b.Roomy.CompareTo(a.Roomy));

        for (var i = 0; i < candidates.Count && chosen.Count < cap; i++)
        {
            var c = candidates[i];
            var clear = true;

            for (var j = 0; j < chosen.Count; j++)
            {
                if (Math.Max(Math.Abs(chosen[j].X - c.X), Math.Abs(chosen[j].Y - c.Y)) < minSpacing)
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
            {
                chosen.Add(c);
            }
        }

        return chosen;
    }
}
