using System;
using System.Collections.Generic;
using Server.Regions;

namespace Server.Veritas.Instancing;

/// <summary>
/// One live copy of an instanced area. It occupies a single pool map until it is freed; nothing about it is saved.
/// </summary>
public class Instance
{
    public Instance(Serial ownerKey, DungeonDefinition definition)
    {
        OwnerKey = ownerKey;
        Definition = definition;
        Bounds = definition?.Bounds ?? [];
        LastTouched = Core.Now;
    }

    public Serial OwnerKey { get; }

    public DungeonDefinition Definition { get; }

    /// <summary>The area on the pool map that holds this instance's contents; freeing clears everything inside it.</summary>
    public Rectangle2D[] Bounds { get; init; }

    public int LiveMapIndex { get; internal set; } = -1;

    public Map LiveMap => LiveMapIndex >= 0 ? Map.Maps[LiveMapIndex] : null;

    public bool IsLive => LiveMapIndex >= 0;

    public DateTime LastTouched { get; private set; }

    /// <summary>Everyone sent in, so freeing can find them even after they log out and leave the map.</summary>
    public HashSet<Serial> Visitors { get; } = [];

    /// <summary>Items built for this instance; deleted when it is freed.</summary>
    public List<Item> Items { get; } = [];

    public BaseRegion Region { get; internal set; }

    public void Touch() => LastTouched = Core.Now;

    internal void Touch(DateTime when) => LastTouched = when;
}
