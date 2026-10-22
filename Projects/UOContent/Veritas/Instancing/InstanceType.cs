using System;
using System.Collections.Generic;
using Server.Collections;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Server.Veritas.Instancing;

/// <summary>
/// One instanced system and its pool of cloned maps. Instances are transient: they take a pool map while live and
/// everything built for them is deleted when they are freed.
/// </summary>
public abstract class InstanceType
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(InstanceType));

    private Map[] _maps = [];
    private Instance[] _live = [];

    public abstract string Key { get; }

    public virtual TimeSpan UnloadAfter => TimeSpan.FromMinutes(5);

    public int PoolBaseIndex { get; private set; } = -1;

    public int PoolSize => _maps.Length;

    public ReadOnlySpan<Map> PoolMaps => _maps;

    internal void AssignPool(Map[] maps)
    {
        _maps = maps;
        _live = new Instance[maps.Length];
        PoolBaseIndex = maps.Length > 0 ? maps[0].MapIndex : -1;
    }

    public bool IsPoolMap(Map map) => SlotOf(map) >= 0;

    public Instance GetLiveInstance(Map map)
    {
        var slot = SlotOf(map);
        return slot >= 0 ? _live[slot] : null;
    }

    public IEnumerable<Instance> GetLiveInstances()
    {
        for (var i = 0; i < _live.Length; i++)
        {
            if (_live[i] != null)
            {
                yield return _live[i];
            }
        }
    }

    // Pool maps are registered contiguously, so the slot is an offset from the first index.
    private int SlotOf(Map map)
    {
        if (map == null || PoolBaseIndex < 0)
        {
            return -1;
        }

        var slot = map.MapIndex - PoolBaseIndex;
        return slot >= 0 && slot < _maps.Length && _maps[slot] == map ? slot : -1;
    }

    /// <summary>
    /// Returns the index of a pool map cloned from <paramref name="baseMap"/>: a free one if any, otherwise the map of
    /// the least recently used instance with nobody inside, which is freed. Returns -1 when every map is occupied.
    /// </summary>
    public int AcquireMapIndex(Map baseMap)
    {
        var victim = -1;
        for (var i = 0; i < _maps.Length; i++)
        {
            if (InstanceManager.GetBaseMap(_maps[i]) != baseMap)
            {
                continue;
            }

            var inst = _live[i];
            if (inst == null)
            {
                return _maps[i].MapIndex;
            }

            if (!HasPlayersInside(inst) && (victim < 0 || inst.LastTouched < _live[victim].LastTouched))
            {
                victim = i;
            }
        }

        if (victim < 0)
        {
            return -1;
        }

        FreeInstance(_live[victim]);
        return _maps[victim].MapIndex;
    }

    public bool EnsureLive(Instance inst, Map baseMap)
    {
        if (inst.IsLive)
        {
            return true;
        }

        var index = AcquireMapIndex(baseMap);
        if (index < 0)
        {
            return false;
        }

        inst.LiveMapIndex = index;
        _live[index - PoolBaseIndex] = inst;
        inst.Touch();

        try
        {
            BuildContents(inst);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to build {Type} instance on {Map}", Key, inst.LiveMap);
            FreeInstance(inst);
            return false;
        }

        return true;
    }

    protected abstract void BuildContents(Instance inst);

    protected virtual void OnFreed(Instance inst)
    {
    }

    /// <summary>Where a player goes when no return point was recorded. <paramref name="inst"/> may be null.</summary>
    protected virtual WorldLocation GetFallbackReturn(Instance inst, Map poolMap, Point3D location) =>
        new(location, InstanceManager.GetBaseMap(poolMap));

    /// <summary>The areas on a pool map that may hold leftovers from before a restart.</summary>
    protected virtual Rectangle2D[] GetCleanupBounds(Map baseMap) => [];

    public void FreeInstance(Instance inst)
    {
        if (inst?.IsLive != true)
        {
            return;
        }

        var map = inst.LiveMap;

        // Ejecting removes visitors, so walk a copy.
        using var visitors = PooledRefList<Serial>.Create(inst.Visitors.Count);
        foreach (var serial in inst.Visitors)
        {
            visitors.Add(serial);
        }

        for (var i = 0; i < visitors.Count; i++)
        {
            var serial = visitors[i];
            var m = World.FindMobile(serial);
            if (m == null)
            {
                continue;
            }

            if (m.Map == map)
            {
                Eject(m, inst);
            }
            else if (m.Map == Map.Internal && m.LogoutMap == map)
            {
                // Logged out inside: they return to the map on login, so move where that happens.
                var dest = ResolveReturn(m, inst, map, m.LogoutLocation);
                m.LogoutMap = dest.Map;
                m.LogoutLocation = dest.Location;
                InstanceReturnPoints.Remove(serial);
            }
            else if (!InstanceManager.IsInstanceMap(m.Map) && !InstanceManager.IsInstanceMap(m.LogoutMap))
            {
                InstanceReturnPoints.Remove(serial);
            }
        }

        ClearArea(map, inst.Bounds, inst);

        for (var i = 0; i < inst.Items.Count; i++)
        {
            inst.Items[i].Delete();
        }

        inst.Items.Clear();
        inst.Region?.Unregister();
        inst.Region = null;

        _live[inst.LiveMapIndex - PoolBaseIndex] = null;
        inst.LiveMapIndex = -1;
        inst.Visitors.Clear();

        OnFreed(inst);
    }

    /// <summary>
    /// Removes every non-player thing in <paramref name="bounds"/>. Players, their corpses and their pets are sent out
    /// instead, since they belong to someone.
    /// </summary>
    private void ClearArea(Map map, ReadOnlySpan<Rectangle2D> bounds, Instance inst)
    {
        using var items = PooledRefList<Item>.Create();
        using var mobiles = PooledRefList<Mobile>.Create();

        for (var i = 0; i < bounds.Length; i++)
        {
            foreach (var item in map.GetItemsInBounds(bounds[i]))
            {
                items.Add(item);
            }

            foreach (var m in map.GetMobilesInBounds(bounds[i]))
            {
                mobiles.Add(m);
            }
        }

        // Corpses go first: their destination is the owner's return point, which ejecting the owner clears.
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is Corpse { Deleted: false, Owner: PlayerMobile owner } corpse && corpse.Map == map)
            {
                var dest = ResolveReturn(owner, inst, map, corpse.Location);
                corpse.MoveToWorld(dest.Location, dest.Map);
            }
        }

        for (var i = 0; i < mobiles.Count; i++)
        {
            if (mobiles[i] is PlayerMobile { Deleted: false } pm && pm.Map == map)
            {
                Eject(pm, inst);
            }
        }

        // Spawners go before mobiles so they take their own spawn with them.
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!item.Deleted && item.Map == map)
            {
                item.Delete();
            }
        }

        for (var i = 0; i < mobiles.Count; i++)
        {
            var m = mobiles[i];
            if (m.Deleted || m.Map != map)
            {
                continue;
            }

            if (m is BaseCreature { Controlled: true, Summoned: false, ControlMaster: PlayerMobile master })
            {
                var dest = master.Map != null && master.Map != Map.Internal && master.Map != map
                    ? new WorldLocation(master.Location, master.Map)
                    : ResolveReturn(master, inst, map, m.Location);
                m.MoveToWorld(dest.Location, dest.Map);
            }
            else
            {
                m.Delete();
            }
        }
    }

    public void SweepUnload()
    {
        var now = Core.Now;
        for (var i = 0; i < _live.Length; i++)
        {
            var inst = _live[i];
            if (inst == null)
            {
                continue;
            }

            if (HasPlayersInside(inst))
            {
                inst.Touch();
            }
            else if (now - inst.LastTouched >= UnloadAfter)
            {
                FreeInstance(inst);
            }
        }
    }

    public bool HasPlayersInside(Instance inst)
    {
        var map = inst.LiveMap;
        if (map == null)
        {
            return false;
        }

        foreach (var serial in inst.Visitors)
        {
            if (World.FindMobile(serial)?.Map == map)
            {
                return true;
            }
        }

        // Staff and anyone who arrived without the gate are not visitors.
        var bounds = inst.Bounds;
        for (var i = 0; i < bounds.Length; i++)
        {
            foreach (var _ in map.GetMobilesInBounds<PlayerMobile>(bounds[i]))
            {
                return true;
            }
        }

        return false;
    }

    public bool SendToInstance(Mobile m, Instance inst, Point3D location)
    {
        var map = inst.LiveMap;
        if (map == null || m.Deleted)
        {
            return false;
        }

        // Moving between instances keeps the original way home.
        if (!InstanceManager.IsInstanceMap(m.Map))
        {
            InstanceReturnPoints.Set(m.Serial, new WorldLocation(m.Location, m.Map));
        }

        inst.Visitors.Add(m.Serial);
        inst.Touch();

        BaseCreature.TeleportPets(m, location, map);
        m.MoveToWorld(location, map);
        return true;
    }

    /// <summary>Sends a mobile on a pool map back to where it entered from. <paramref name="inst"/> may be null.</summary>
    public void Eject(Mobile m, Instance inst)
    {
        var dest = ResolveReturn(m, inst, m.Map, m.Location);
        InstanceReturnPoints.Remove(m.Serial);
        inst?.Visitors.Remove(m.Serial);

        BaseCreature.TeleportPets(m, dest.Location, dest.Map);
        m.MoveToWorld(dest.Location, dest.Map);
    }

    private WorldLocation ResolveReturn(Mobile m, Instance inst, Map poolMap, Point3D location)
    {
        if (InstanceReturnPoints.TryGet(m.Serial, out var point) && point.Map != null && point.Map != Map.Internal &&
            !InstanceManager.IsInstanceMap(point.Map))
        {
            return point;
        }

        return GetFallbackReturn(inst, poolMap, location);
    }

    public virtual void OnLogin(PlayerMobile pm)
    {
        var inst = GetLiveInstance(pm.Map);
        if (inst?.Visitors.Contains(pm.Serial) == true)
        {
            inst.Touch();
            return;
        }

        Eject(pm, null);
        pm.SendMessage("The place you were in is gone. You have been returned to where you entered it.");
    }

    /// <summary>
    /// Instance contents are saved with the world but no instance survives a restart, so anything that loaded onto a
    /// pool map is a leftover.
    /// </summary>
    public void CleanupAfterLoad()
    {
        for (var i = 0; i < _maps.Length; i++)
        {
            if (_live[i] == null)
            {
                ClearArea(_maps[i], GetCleanupBounds(InstanceManager.GetBaseMap(_maps[i])), null);
            }
        }
    }
}
