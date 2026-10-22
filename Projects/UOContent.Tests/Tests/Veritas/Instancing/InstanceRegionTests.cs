using Server;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;
using Server.Veritas.Instancing;
using Xunit;

namespace UOContent.Tests.Veritas.Instancing;

[Collection("Sequential UOContent Tests")]
public class InstanceRegionTests
{
    private static readonly Point3D Inside = new(5010, 3010, 0);

    [Fact]
    public void BothRegions_BlockEveryWayIn_AndAllowRecallingOut()
    {
        var type = new TestInstanceType();
        PlayerMobile inside = null;
        PlayerMobile outside = null;
        PlayerMobile staff = null;
        InstanceDungeonRegion dungeon = null;
        try
        {
            InstancingTestHelpers.Register(type, 1, Map.Felucca);
            var clone = Map.Maps[InstanceManager.PoolStartIndex];

            var definition = DungeonDefinition.FromRegion(CreateSourceRegion());
            dungeon = new InstanceDungeonRegion(definition, clone);
            dungeon.Register();

            inside = InstancingTestHelpers.CreatePlayer(Inside, clone);
            outside = InstancingTestHelpers.CreatePlayer(new Point3D(1500, 1600, 0), Map.Felucca);
            staff = InstancingTestHelpers.CreatePlayer(new Point3D(1500, 1600, 0), Map.Felucca);
            staff.AccessLevel = AccessLevel.GameMaster;

            Assert.Same(dungeon, Region.Find(Inside, clone));
            Assert.IsType<InstanceMapRegion>(Region.Find(new Point3D(100, 100, 0), clone));

            BaseRegion[] regions = [dungeon, (BaseRegion)clone.DefaultRegion];
            foreach (var region in regions)
            {
                var name = region.GetType().Name;

                Assert.False(region.CheckTravel(outside, Inside, TravelCheckType.RecallTo, out _), $"{name} recall in");
                Assert.False(region.CheckTravel(outside, Inside, TravelCheckType.GateTo, out _), $"{name} gate in");
                Assert.False(region.CheckTravel(inside, Inside, TravelCheckType.Mark, out _), $"{name} mark");
                Assert.False(region.CheckTravel(inside, Inside, TravelCheckType.GateFrom, out _), $"{name} gate out");

                Assert.True(region.CheckTravel(inside, Inside, TravelCheckType.RecallFrom, out _), $"{name} recall from");
                Assert.True(region.CheckTravel(inside, Inside, TravelCheckType.RecallTo, out _), $"{name} recall out");
                Assert.True(region.CheckTravel(inside, Inside, TravelCheckType.TeleportFrom, out _), $"{name} teleport");
                Assert.True(region.CheckTravel(inside, Inside, TravelCheckType.TeleportTo, out _), $"{name} teleport");

                foreach (var travel in System.Enum.GetValues<TravelCheckType>())
                {
                    Assert.True(region.CheckTravel(staff, Inside, travel, out _), $"{name} staff {travel}");
                }

                Assert.False(region.AllowHousing(inside, Inside), $"{name} housing");
            }

            // Through the spell path, which consults both the destination and the caster's region.
            Assert.False(SpellHelper.CheckTravel(outside, clone, Inside, TravelCheckType.RecallTo, out _));
            Assert.False(SpellHelper.CheckTravel(outside, clone, Inside, TravelCheckType.GateTo, out _));
            Assert.False(SpellHelper.CheckTravel(inside, TravelCheckType.Mark, out _));
            Assert.False(SpellHelper.CheckTravel(inside, TravelCheckType.GateFrom, out _));
        }
        finally
        {
            inside?.Delete();
            outside?.Delete();
            staff?.Delete();
            dungeon?.Unregister();
            InstanceManager.UnregisterPool(type);
        }
    }

    [Fact]
    public void DungeonDefinition_CopiesTheSourceRegion()
    {
        var source = CreateSourceRegion();
        var definition = DungeonDefinition.FromRegion(source);

        Assert.Equal("InstancingTestDungeon", definition.Name);
        Assert.Same(Map.Felucca, definition.Map);
        Assert.Equal(new Point3D(4990, 2990, 0), definition.Entrance);
        Assert.Equal(Inside, definition.GoLocation);
        Assert.Equal([new Rectangle2D(5000, 3000, 40, 40)], definition.Bounds);
        Assert.True(definition.Contains(Inside));
        Assert.False(definition.Contains(new Point3D(4990, 2990, 0)));

        Assert.Null(DungeonDefinition.FromRegion(new DungeonRegion("NoArea", Map.Felucca, 50)));
    }

    internal static DungeonRegion CreateSourceRegion() =>
        new("InstancingTestDungeon", Map.Felucca, 50, new Rectangle3D(5000, 3000, -128, 40, 40, 256))
        {
            Entrance = new Point3D(4990, 2990, 0),
            GoLocation = Inside
        };
}
