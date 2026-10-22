using System;
using System.Collections.Generic;
using System.Reflection;
using Server.Collections;
using Server.Commands;
using Server.Engines.PartySystem;
using Server.Engines.Spawners;
using Server.Items;
using Server.Regions;

namespace Server.Veritas.Instancing;

/// <summary>
/// Party-shared copies of dungeons. An instance copies the dungeon's region plus its spawners, doors and teleporters
/// from the base map; terrain and statics come from the shared map files.
/// </summary>
public sealed class DungeonInstanceType : InstanceType
{
    private readonly Dictionary<(Serial, DungeonDefinition), Instance> _byOwner = [];

    public static DungeonInstanceType Instance { get; } = new();

    public override string Key => "Dungeon";

    /// <summary>A party shares its leader's instances; anyone else gets their own.</summary>
    public static Serial GetOwnerKey(Mobile m) => Party.Get(m)?.Leader?.Serial ?? m.Serial;

    public Instance Find(Serial ownerKey, DungeonDefinition definition) =>
        _byOwner.TryGetValue((ownerKey, definition), out var inst) ? inst : null;

    public bool Enter(Mobile m, DungeonDefinition definition) => Enter(m, definition, GetOwnerKey(m));

    public bool Enter(Mobile m, DungeonDefinition definition, Serial ownerKey)
    {
        var inst = Find(ownerKey, definition) ?? new Instance(ownerKey, definition);

        if (!EnsureLive(inst, definition.Map))
        {
            m.SendMessage("Every copy of that dungeon is occupied. Try again later.");
            return false;
        }

        _byOwner[(ownerKey, definition)] = inst;
        return SendToInstance(m, inst, definition.GoLocation);
    }

    protected override void OnFreed(Instance inst) => _byOwner.Remove((inst.OwnerKey, inst.Definition));

    protected override WorldLocation GetFallbackReturn(Instance inst, Map poolMap, Point3D location)
    {
        var baseMap = InstanceManager.GetBaseMap(poolMap);
        var definition = inst?.Definition ?? DungeonDefinition.FindAt(baseMap, location);

        return definition != null
            ? new WorldLocation(definition.Entrance, definition.Map)
            : new WorldLocation(location, baseMap);
    }

    protected override Rectangle2D[] GetCleanupBounds(Map baseMap)
    {
        var list = new List<Rectangle2D>();
        var all = DungeonDefinition.All;
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Map == baseMap)
            {
                list.AddRange(all[i].Bounds);
            }
        }

        return list.ToArray();
    }

    protected override void BuildContents(Instance inst)
    {
        var definition = inst.Definition;
        var baseMap = definition.Map;
        var map = inst.LiveMap;

        // Registered first so region spawners can find it.
        var region = new InstanceDungeonRegion(definition, map);
        region.Register();
        inst.Region = region;

        // Collect before cloning: a clone briefly lands on the base map, which would break the enumeration.
        using var sources = PooledRefList<Item>.Create();
        var bounds = definition.Bounds;
        for (var i = 0; i < bounds.Length; i++)
        {
            foreach (var item in baseMap.GetItemsInBounds(bounds[i]))
            {
                if (IsCloneable(item) && !sources.Contains(item))
                {
                    sources.Add(item);
                }
            }
        }

        var clones = new Dictionary<Item, Item>(sources.Count);
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            var clone = CloneItem(source);
            if (clone == null)
            {
                continue;
            }

            clones[source] = clone;
            inst.Items.Add(clone);
            clone.MoveToWorld(source.Location, map);
        }

        foreach (var (source, clone) in clones)
        {
            switch (clone)
            {
                case BaseDoor door:
                    {
                        door.Link = RemapLink(((BaseDoor)source).Link, clones) as BaseDoor;

                        // Instances start with every door closed; closing applies the door's offset from its open spot.
                        if (door.Open)
                        {
                            door.Open = false;
                        }

                        break;
                    }
                case Teleporter teleporter:
                    {
                        var src = (Teleporter)source;
                        teleporter.MapDest = RetargetMapDest(src.MapDest, src.PointDest, baseMap, map, definition.Area);
                        break;
                    }
                case BaseSpawner spawner:
                    {
                        // Moving re-centres home-range bounds, so restore the source's exactly.
                        spawner.SpawnBounds = ((BaseSpawner)source).SpawnBounds;

                        if (spawner is RegionSpawner { SpawnRegion: { } spawnRegion } regionSpawner)
                        {
                            regionSpawner.SpawnRegion = Server.Region.Find(spawnRegion.Name, map) as BaseRegion;
                        }

                        break;
                    }
            }
        }

        foreach (var clone in clones.Values)
        {
            if (clone is BaseSpawner { Running: true } spawner)
            {
                spawner.Respawn();
            }
        }
    }

    public static bool IsCloneable(Item item) =>
        item.Parent == null && item is BaseSpawner or BaseDoor or Teleporter;

    /// <summary>The clone of a door's link, or null when the link was not cloned with it.</summary>
    public static Item RemapLink(Item link, IReadOnlyDictionary<Item, Item> clones) =>
        link != null && clones.TryGetValue(link, out var clone) ? clone : null;

    /// <summary>
    /// Teleporters that lead elsewhere inside the dungeon must stay in the instance; ones that lead out keep their real
    /// destination. A null map already means "the teleporter's own map".
    /// </summary>
    public static Map RetargetMapDest(Map mapDest, Point3D pointDest, Map baseMap, Map instanceMap, Rectangle3D[] area)
    {
        if (mapDest != baseMap)
        {
            return mapDest;
        }

        return pointDest == Point3D.Zero || DungeonDefinition.Contains(area, pointDest) ? instanceMap : mapDest;
    }

    /// <summary>Builds a copy the same way [Dupe does, also accepting constructors that take value types (doors).</summary>
    public static Item CloneItem(Item source)
    {
        var ctor = source.GetType().GetConstructor(out var paramCount);
        object[] args = null;

        if (ctor != null)
        {
            if (paramCount > 0)
            {
                args = new object[paramCount];
                Array.Fill(args, Type.Missing);
            }
        }
        else
        {
            ctor = FindValueTypeConstructor(source.GetType(), out args);
        }

        return ctor == null ? null : Dupe.DoDupe(source, ctor, args);
    }

    // Duping copies every property afterwards, so placeholder arguments only have to construct the object.
    private static ConstructorInfo FindValueTypeConstructor(Type type, out object[] args)
    {
        var ctors = type.GetConstructors();
        for (var i = 0; i < ctors.Length; i++)
        {
            var parameters = ctors[i].GetParameters();
            var valid = true;
            for (var j = 0; j < parameters.Length; j++)
            {
                if (!parameters[j].IsOptional && !parameters[j].ParameterType.IsValueType)
                {
                    valid = false;
                    break;
                }
            }

            if (!valid)
            {
                continue;
            }

            args = new object[parameters.Length];
            for (var j = 0; j < parameters.Length; j++)
            {
                var p = parameters[j];
                args[j] = p.IsOptional ? Type.Missing : Activator.CreateInstance(p.ParameterType);
            }

            return ctors[i];
        }

        args = null;
        return null;
    }
}
