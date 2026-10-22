using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using ModernUO.CodeGeneratedEvents;
using Server.Json;
using Server.Logging;
using Server.Mobiles;

namespace Server.Veritas.Instancing;

/// <summary>
/// Registers the pool maps instances run on and drives every <see cref="InstanceType"/>. A pool map is a separate
/// <see cref="Map"/> that reuses a real map's client id and files, so players see the same terrain while everything
/// on it is isolated. <see cref="Map"/> compares by reference, so a clone is never equal to its base map; use
/// <see cref="GetBaseMap"/> where content needs the real map.
/// </summary>
public static class InstanceManager
{
    public const int PoolStartIndex = 0x40;

    // Exclusive: 0x7F is Map.Internal.
    private const int PoolEndIndex = 0x7F;

    private static readonly ILogger logger = LogFactory.GetLogger(typeof(InstanceManager));

    private static readonly Dictionary<Map, Map> _baseMaps = new(ReferenceEqualityComparer.Instance);
    private static readonly List<InstanceType> _types = [];
    private static Timer _sweepTimer;

    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    public static bool Enabled { get; private set; }

    public static IReadOnlyList<InstanceType> Types => _types;

    // After MapLoader (priority 2) so the base maps exist, and before regions and the world load so anything saved on
    // a pool map has a map to load onto.
    [CallPriority(3)]
    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("veritas.instancing.enabled", true);
        if (!Enabled)
        {
            return;
        }

        var clonesPerMap = ServerConfiguration.GetOrUpdateSetting("veritas.instancing.dungeon.clonesPerMap", 4);
        var fileIndexes = LoadFileIndexes();

        RegisterPool(
            DungeonInstanceType.Instance,
            GetDefaultBaseMaps(),
            clonesPerMap,
            PoolStartIndex,
            m => fileIndexes.TryGetValue(m.MapIndex, out var fileIndex) ? fileIndex : m.MapID
        );
    }

    public static void Initialize()
    {
        if (_types.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _types.Count; i++)
        {
            _types[i].CleanupAfterLoad();
        }

        _sweepTimer?.Stop();
        _sweepTimer = Timer.DelayCall(SweepInterval, SweepInterval, Sweep);
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (pm != null)
        {
            FindType(pm.Map)?.OnLogin(pm);
        }
    }

    public static bool IsInstanceMap(Map map) => map != null && _baseMaps.ContainsKey(map);

    /// <summary>The real map a pool map was cloned from, or <paramref name="map"/> itself when it is not a clone.</summary>
    public static Map GetBaseMap(Map map) => map != null && _baseMaps.TryGetValue(map, out var baseMap) ? baseMap : map;

    public static InstanceType FindType(Map map)
    {
        if (!IsInstanceMap(map))
        {
            return null;
        }

        for (var i = 0; i < _types.Count; i++)
        {
            if (_types[i].IsPoolMap(map))
            {
                return _types[i];
            }
        }

        return null;
    }

    public static Instance FindLiveInstance(Map map) => FindType(map)?.GetLiveInstance(map);

    public static List<Map> GetDefaultBaseMaps()
    {
        var list = new List<Map>();
        for (var i = 0; i <= 5; i++)
        {
            if (Map.Maps[i] != null)
            {
                list.Add(Map.Maps[i]);
            }
        }

        return list;
    }

    /// <summary>
    /// Registers <paramref name="clonesPerMap"/> clones of each base map at consecutive indices from
    /// <paramref name="startIndex"/>. Returns the next unused index.
    /// </summary>
    public static int RegisterPool(
        InstanceType type, IReadOnlyList<Map> baseMaps, int clonesPerMap, int startIndex, Func<Map, int> fileIndexOf
    )
    {
        if (baseMaps.Count == 0 || clonesPerMap <= 0)
        {
            return startIndex;
        }

        var maxPerMap = Math.Max(0, (PoolEndIndex - startIndex) / baseMaps.Count);
        if (clonesPerMap > maxPerMap)
        {
            logger.Warning(
                "{Type} instancing wants {Requested} clones per map but only {Max} fit below the internal map",
                type.Key,
                clonesPerMap,
                maxPerMap
            );
            clonesPerMap = maxPerMap;
        }

        var count = baseMaps.Count * clonesPerMap;
        for (var i = startIndex; i < startIndex + count; i++)
        {
            if (Map.Maps[i] != null)
            {
                logger.Error(
                    "{Type} instancing disabled: map index {Index} is already used by {Map}",
                    type.Key,
                    i,
                    Map.Maps[i]
                );
                return startIndex;
            }
        }

        var maps = new Map[count];
        var index = startIndex;
        for (var b = 0; b < baseMaps.Count; b++)
        {
            var baseMap = baseMaps[b];
            for (var i = 0; i < clonesPerMap; i++)
            {
                var map = new Map(
                    baseMap.MapID,
                    index,
                    fileIndexOf(baseMap),
                    baseMap.Width,
                    baseMap.Height,
                    baseMap.Season,
                    $"{type.Key}{baseMap.Name}{i}",
                    baseMap.Rules
                );
                map.DefaultRegion = new InstanceMapRegion(map);

                Map.Maps[index] = map;
                Map.AllMaps.Add(map);
                _baseMaps[map] = baseMap;
                maps[index - startIndex] = map;
                index++;
            }
        }

        type.AssignPool(maps);
        _types.Add(type);

        logger.Information(
            "{Type} instancing registered {Count} pool maps (0x{First:X2}-0x{Last:X2})",
            type.Key,
            count,
            startIndex,
            index - 1
        );

        return index;
    }

    /// <summary>Frees every instance and removes the type's pool maps. For tests; the server never unregisters.</summary>
    internal static void UnregisterPool(InstanceType type)
    {
        foreach (var inst in new List<Instance>(type.GetLiveInstances()))
        {
            type.FreeInstance(inst);
        }

        foreach (var map in type.PoolMaps)
        {
            Map.Maps[map.MapIndex] = null;
            Map.AllMaps.Remove(map);
            _baseMaps.Remove(map);
        }

        type.AssignPool([]);
        _types.Remove(type);
    }

    private static void Sweep()
    {
        for (var i = 0; i < _types.Count; i++)
        {
            _types[i].SweepUnload();
        }
    }

    // Map keeps its file index private, so read it from the same definitions MapLoader registered the maps from.
    private static Dictionary<int, int> LoadFileIndexes()
    {
        var result = new Dictionary<int, int>();
        var path = Path.Combine(Core.BaseDirectory, "Data/map-definitions.json");

        try
        {
            var entries = JsonConfig.Deserialize<List<MapFileEntry>>(path);
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    result[entry.Index] = entry.FileIndex;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Could not read map file indexes from {Path}; pool maps will use each map's id", path);
        }

        return result;
    }

    private sealed class MapFileEntry
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("fileIndex")]
        public int FileIndex { get; set; }
    }
}
