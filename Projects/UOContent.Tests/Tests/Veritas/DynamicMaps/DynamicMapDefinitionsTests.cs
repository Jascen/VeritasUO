using Server;
using Server.Veritas.DynamicMaps;
using Xunit;

namespace UOContent.Tests.Veritas.DynamicMaps;

public class DynamicMapDefinitionsTests
{
    private static Map MakeMap(int mapID, int width, int height) =>
        new(mapID, mapID, mapID, width, height, 0, $"Test{mapID}", MapRules.FeluccaRules);

    [Fact]
    public void CreateMapDefinitionsPacket_WritesHeaderAndEntries()
    {
        var packet = DynamicMapDefinitions.CreateMapDefinitionsPacket(
            [MakeMap(0, 7168, 4096), MakeMap(0x20, 1280, 1024)]
        );

        byte[] expected =
        [
            0x3F, 0x00, 0x19,                                           // id, length 25
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,                               // unused
            0x10,                                                       // map definitions command
            0x02,                                                       // count
            0x00, 0x1C, 0x00, 0x10, 0x00,                               // map 0: 7168 x 4096
            0x20, 0x05, 0x00, 0x04, 0x00                                // map 0x20: 1280 x 1024
        ];

        Assert.Equal(expected, packet);
    }

    [Fact]
    public void GetDefinableMaps_SkipsNullInvalidIdsAndEmptyMaps()
    {
        var valid = MakeMap(1, 7168, 4096);

        var maps = DynamicMapDefinitions.GetDefinableMaps(
            [null, valid, MakeMap(0x7F, 16, 16), MakeMap(-1, 16, 16), MakeMap(2, 0, 16), MakeMap(3, 16, 0)]
        );

        Assert.Equal([valid], maps);
    }

    [Fact]
    public void CreateMapDefinitionsPacket_NoMaps_WritesEmptyList()
    {
        var packet = DynamicMapDefinitions.CreateMapDefinitionsPacket([]);

        Assert.Equal(15, packet.Length);
        Assert.Equal(0x10, packet[13]);
        Assert.Equal(0, packet[14]);
    }
}
