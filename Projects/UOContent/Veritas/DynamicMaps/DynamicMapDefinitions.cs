using System;
using System.Buffers;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;
using Server.Network;
using Server.Veritas.Instancing;

namespace Server.Veritas.DynamicMaps;

/// <summary>
/// Ported from Ultima Memento. On login, sends the client the list of server maps (id, width, height)
/// via the UltimaLive-style 0x3F map definitions packet so a supporting client can load custom maps.
/// </summary>
public static class DynamicMapDefinitions
{
    private const byte MapDefinitionsCommand = 0x10;
    private const int HeaderLength = 3 + 10 + 1 + 1;
    private const int EntryLength = 5;

    public static bool Enabled { get; private set; }

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("veritas.dynamicMapDefinitions.enabled", false);
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (!Enabled || pm?.NetState is not { } ns)
        {
            return;
        }

        ns.SendMapDefinitions();
    }

    public static void SendMapDefinitions(this NetState ns)
    {
        if (ns.CannotSendPackets())
        {
            return;
        }

        ns.Send(CreateMapDefinitionsPacket(Map.AllMaps));
    }

    public static List<Map> GetDefinableMaps(IEnumerable<Map> maps)
    {
        var list = new List<Map>();

        foreach (var m in maps)
        {
            if (m == null || m == Map.Internal || m.MapID is < 0 or > 0x7E || m.Width <= 0 || m.Height <= 0)
            {
                continue;
            }

            // Instance pool maps reuse their base map's client id, so the client already has them.
            if (InstanceManager.IsInstanceMap(m))
            {
                continue;
            }

            list.Add(m);
        }

        return list;
    }

    public static byte[] CreateMapDefinitionsPacket(IEnumerable<Map> allMaps)
    {
        var maps = GetDefinableMaps(allMaps);
        var count = Math.Min(maps.Count, byte.MaxValue);
        var length = HeaderLength + count * EntryLength;

        var packet = new byte[length];
        var writer = new SpanWriter(packet);
        writer.Write((byte)0x3F);     // Packet ID
        writer.Write((ushort)length); // Length
        writer.Clear(10);             // Unused block header
        writer.Write(MapDefinitionsCommand);
        writer.Write((byte)count);

        for (var i = 0; i < count; i++)
        {
            var m = maps[i];
            writer.Write((byte)m.MapID);
            writer.Write((ushort)m.Width);
            writer.Write((ushort)m.Height);
        }

        return packet;
    }
}
