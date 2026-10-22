using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Veritas.DynamicMaps;
using Server.Veritas.Instancing;
using Xunit;

namespace UOContent.Tests.Veritas.Instancing;

[Collection("Sequential UOContent Tests")]
public class InstancePoolTests
{
    private const int Start = InstanceManager.PoolStartIndex;

    private static Rectangle2D TestBounds => new(1000, 1000, 20, 20);

    private static Instance NewInstance(uint owner) => new((Serial)owner, null) { Bounds = [TestBounds] };

    [Fact]
    public void RegisterPool_ClonesEachBaseMap()
    {
        var type = new TestInstanceType();
        try
        {
            var next = InstancingTestHelpers.Register(type, 2, Map.Felucca, Map.Trammel);

            Assert.Equal(Start + 4, next);
            Assert.Equal(Start, type.PoolBaseIndex);
            Assert.Equal(4, type.PoolSize);

            for (var i = 0; i < 4; i++)
            {
                var clone = Map.Maps[Start + i];
                var baseMap = i < 2 ? Map.Felucca : Map.Trammel;

                Assert.NotNull(clone);
                Assert.NotSame(baseMap, clone);
                // Map.CompareTo orders by client id, so xUnit's equality would call a clone equal to its base.
                Assert.False(baseMap.Equals(clone));
                Assert.Equal(Start + i, clone.MapIndex);
                Assert.Equal(baseMap.MapID, clone.MapID);
                Assert.Equal(baseMap.Width, clone.Width);
                Assert.Equal(baseMap.Height, clone.Height);
                Assert.Equal(baseMap.Season, clone.Season);
                Assert.Equal(baseMap.Rules, clone.Rules);
                Assert.Equal($"Test{baseMap.Name}{i % 2}", clone.Name);
                Assert.Contains(Map.AllMaps, m => ReferenceEquals(m, clone));

                Assert.True(InstanceManager.IsInstanceMap(clone));
                Assert.Same(baseMap, InstanceManager.GetBaseMap(clone));
                Assert.Same(type, InstanceManager.FindType(clone));
                Assert.IsType<InstanceMapRegion>(clone.DefaultRegion);
                Assert.Same(clone.DefaultRegion, Region.Find(new Point3D(100, 100, 0), clone));
            }

            Assert.False(InstanceManager.IsInstanceMap(Map.Felucca));
            Assert.False(InstanceManager.IsInstanceMap(null));
            Assert.Same(Map.Felucca, InstanceManager.GetBaseMap(Map.Felucca));
            Assert.Null(InstanceManager.FindType(Map.Felucca));
        }
        finally
        {
            InstanceManager.UnregisterPool(type);
        }

        Assert.Null(Map.Maps[Start]);
        Assert.Equal(0, type.PoolSize);
    }

    [Fact]
    public void RegisterPool_StopsBelowTheInternalMap()
    {
        var type = new TestInstanceType();
        try
        {
            // (0x7F - 0x7A) / 2 = 2 clones per map fit.
            var next = InstanceManager.RegisterPool(type, [Map.Felucca, Map.Trammel], 10, 0x7A, m => m.MapID);

            Assert.Equal(0x7E, next);
            Assert.Equal(4, type.PoolSize);
            Assert.Equal("Internal", Map.Maps[0x7F].Name);
        }
        finally
        {
            InstanceManager.UnregisterPool(type);
        }
    }

    [Fact]
    public void RegisterPool_RefusesOccupiedIndices()
    {
        var type = new TestInstanceType();
        try
        {
            Assert.Equal(0, InstanceManager.RegisterPool(type, [Map.Felucca], 2, 0, m => m.MapID));
            Assert.Equal(0, type.PoolSize);
            Assert.Same(Map.Felucca, Map.Maps[0]);
        }
        finally
        {
            InstanceManager.UnregisterPool(type);
        }
    }

    [Fact]
    public void DynamicMapDefinitions_SkipsPoolMaps()
    {
        var type = new TestInstanceType();
        try
        {
            InstancingTestHelpers.Register(type, 1, Map.Felucca);
            var clone = Map.Maps[Start];

            var maps = DynamicMapDefinitions.GetDefinableMaps(Map.AllMaps);

            Assert.DoesNotContain(maps, m => ReferenceEquals(m, clone));
            Assert.Contains(maps, m => ReferenceEquals(m, Map.Felucca));
        }
        finally
        {
            InstanceManager.UnregisterPool(type);
        }
    }

    [Fact]
    public void AcquireMapIndex_UsesFreeSlotThenEvictsOldestEmptyInstance()
    {
        var type = new TestInstanceType();
        try
        {
            InstancingTestHelpers.Register(type, 2, Map.Felucca, Map.Trammel);

            var a = NewInstance(1);
            var b = NewInstance(2);
            Assert.True(type.EnsureLive(a, Map.Felucca));
            Assert.True(type.EnsureLive(b, Map.Felucca));
            Assert.Equal(Start, a.LiveMapIndex);
            Assert.Equal(Start + 1, b.LiveMapIndex);
            Assert.Equal([a, b], type.Built);

            a.Touch(Core.Now - TimeSpan.FromMinutes(10));
            b.Touch(Core.Now - TimeSpan.FromMinutes(5));

            var c = NewInstance(3);
            Assert.True(type.EnsureLive(c, Map.Felucca));

            Assert.Equal(Start, c.LiveMapIndex);
            Assert.False(a.IsLive);
            Assert.Equal([a], type.Freed);
            Assert.Same(c, type.GetLiveInstance(Map.Maps[Start]));

            // Trammel's clones are untouched by Felucca's demand.
            Assert.Equal(Start + 2, type.AcquireMapIndex(Map.Trammel));
            Assert.Equal(-1, type.AcquireMapIndex(Map.Malas));
        }
        finally
        {
            InstanceManager.UnregisterPool(type);
        }
    }

    [Fact]
    public void AcquireMapIndex_FailsWhenEveryInstanceHasPlayers_AndFreeingSendsThemBack()
    {
        var type = new TestInstanceType();
        PlayerMobile first = null;
        PlayerMobile second = null;
        try
        {
            InstancingTestHelpers.Register(type, 2, Map.Felucca);

            var a = NewInstance(1);
            var b = NewInstance(2);
            type.EnsureLive(a, Map.Felucca);
            type.EnsureLive(b, Map.Felucca);

            var home = new Point3D(1500, 1600, 0);
            first = InstancingTestHelpers.CreatePlayer(home, Map.Felucca);
            second = InstancingTestHelpers.CreatePlayer(home, Map.Felucca);

            var inside = new Point3D(1005, 1005, 0);
            Assert.True(type.SendToInstance(first, a, inside));
            Assert.True(type.SendToInstance(second, b, inside));
            Assert.Same(a.LiveMap, first.Map);
            Assert.True(InstanceReturnPoints.TryGet(first.Serial, out var recorded));
            Assert.Equal(home, recorded.Location);
            Assert.Same(Map.Felucca, recorded.Map);

            Assert.True(type.HasPlayersInside(a));
            Assert.Equal(-1, type.AcquireMapIndex(Map.Felucca));
            Assert.False(type.EnsureLive(NewInstance(3), Map.Felucca));

            // Sweeping keeps occupied instances alive no matter how long ago they were touched.
            a.Touch(Core.Now - TimeSpan.FromHours(1));
            type.SweepUnload();
            Assert.True(a.IsLive);

            var clone = a.LiveMap;
            var dropped = new Item(0x1F14);
            dropped.MoveToWorld(new Point3D(1010, 1010, 0), clone);
            var tracked = new Item(0x1F14);
            tracked.MoveToWorld(new Point3D(3000, 3000, 0), clone);
            a.Items.Add(tracked);

            type.FreeInstance(a);

            Assert.False(a.IsLive);
            Assert.Same(Map.Felucca, first.Map);
            Assert.Equal(home, first.Location);
            Assert.False(InstanceReturnPoints.TryGet(first.Serial, out _));
            Assert.True(dropped.Deleted);
            Assert.True(tracked.Deleted);
            Assert.Empty(a.Items);
            Assert.Same(b.LiveMap, second.Map);
        }
        finally
        {
            first?.Delete();
            second?.Delete();
            InstanceManager.UnregisterPool(type);
        }
    }

    [Fact]
    public void SweepUnload_FreesIdleEmptyInstances()
    {
        var type = new TestInstanceType();
        try
        {
            InstancingTestHelpers.Register(type, 2, Map.Felucca);

            var idle = NewInstance(1);
            var recent = NewInstance(2);
            type.EnsureLive(idle, Map.Felucca);
            type.EnsureLive(recent, Map.Felucca);
            idle.Touch(Core.Now - type.UnloadAfter - TimeSpan.FromSeconds(1));

            type.SweepUnload();

            Assert.False(idle.IsLive);
            Assert.True(recent.IsLive);
        }
        finally
        {
            InstanceManager.UnregisterPool(type);
        }
    }
}
