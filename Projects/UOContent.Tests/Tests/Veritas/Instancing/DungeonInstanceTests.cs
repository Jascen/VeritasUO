using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Veritas.Instancing;
using Xunit;

namespace UOContent.Tests.Veritas.Instancing;

[Collection("Sequential UOContent Tests")]
public class DungeonInstanceTests
{
    private static readonly Rectangle3D[] Area = [new(5000, 3000, -128, 40, 40, 256)];

    [Fact]
    public void RetargetMapDest_KeepsInternalTeleportersInTheInstance()
    {
        var clone = new Map(0, 0x70, 0, 7168, 4096, 0, "RetargetTest", MapRules.FeluccaRules);
        var inside = new Point3D(5010, 3010, 0);
        var outside = new Point3D(1000, 1000, 0);

        Assert.Same(clone, DungeonInstanceType.RetargetMapDest(Map.Felucca, inside, Map.Felucca, clone, Area));
        Assert.Same(clone, DungeonInstanceType.RetargetMapDest(Map.Felucca, Point3D.Zero, Map.Felucca, clone, Area));
        Assert.Same(Map.Felucca, DungeonInstanceType.RetargetMapDest(Map.Felucca, outside, Map.Felucca, clone, Area));
        Assert.Same(Map.Trammel, DungeonInstanceType.RetargetMapDest(Map.Trammel, inside, Map.Felucca, clone, Area));
        Assert.Null(DungeonInstanceType.RetargetMapDest(null, inside, Map.Felucca, clone, Area));
    }

    [Fact]
    public void RemapLink_FollowsOnlyClonedItems()
    {
        var linked = new Item(0x1F14);
        var linkedClone = new Item(0x1F14);
        var uncloned = new Item(0x1F14);
        try
        {
            var clones = new Dictionary<Item, Item> { [linked] = linkedClone };

            Assert.Same(linkedClone, DungeonInstanceType.RemapLink(linked, clones));
            Assert.Null(DungeonInstanceType.RemapLink(uncloned, clones));
            Assert.Null(DungeonInstanceType.RemapLink(null, clones));
        }
        finally
        {
            linked.Delete();
            linkedClone.Delete();
            uncloned.Delete();
        }
    }

    [Fact]
    public void CloneItem_CopiesDoorsWhoseConstructorNeedsAFacing()
    {
        var door = new MetalDoor(DoorFacing.EastCCW) { Locked = true, KeyValue = 1234 };
        Item clone = null;
        try
        {
            clone = DungeonInstanceType.CloneItem(door);

            var copy = Assert.IsType<MetalDoor>(clone);
            Assert.NotSame(door, copy);
            Assert.Equal(door.ClosedId, copy.ClosedId);
            Assert.Equal(door.OpenedId, copy.OpenedId);
            Assert.Equal(door.Offset, copy.Offset);
            Assert.Equal(door.ItemID, copy.ItemID);
            Assert.True(copy.Locked);
            Assert.Equal(1234u, copy.KeyValue);
        }
        finally
        {
            door.Delete();
            clone?.Delete();
        }
    }

    [Fact]
    public void Enter_BuildsTheDungeonOnAPoolMap_AndFreeLeavesNothingBehind()
    {
        var type = new DungeonInstanceType();
        var sources = new List<Item>();
        PlayerMobile player = null;
        try
        {
            InstancingTestHelpers.Register(type, 1, Map.Felucca);
            var definition = DungeonDefinition.FromRegion(InstanceRegionTests.CreateSourceRegion());

            var doorA = Place(sources, new MetalDoor(DoorFacing.WestCW), new Point3D(5005, 3005, 0));
            var doorB = Place(sources, new MetalDoor(DoorFacing.EastCCW), new Point3D(5006, 3005, 0));
            doorA.Link = doorB;
            doorB.Link = doorA;
            var stairs = Place(sources, new Teleporter(new Point3D(5020, 3020, 0), Map.Felucca), new Point3D(5002, 3002, 0));
            var exit = Place(sources, new Teleporter(new Point3D(1000, 1000, 0), Map.Felucca), new Point3D(5003, 3002, 0));
            var spawner = Place(sources, new Spawner("Mongbat"), new Point3D(5010, 3010, 0));
            Place(sources, new Teleporter(new Point3D(5010, 3010, 0), Map.Felucca), new Point3D(4900, 2900, 0));

            var home = new Point3D(4990, 2990, 0);
            player = InstancingTestHelpers.CreatePlayer(home, Map.Felucca);

            Assert.True(type.Enter(player, definition, player.Serial));

            var inst = type.Find(player.Serial, definition);
            Assert.NotNull(inst);
            var clone = inst.LiveMap;
            Assert.True(InstanceManager.IsInstanceMap(clone));
            Assert.Same(clone, player.Map);
            Assert.Equal(definition.GoLocation, player.Location);
            Assert.IsType<InstanceDungeonRegion>(Region.Find(definition.GoLocation, clone));

            // Only the five sources inside the dungeon area are copied.
            Assert.Equal(5, inst.Items.Count);
            Assert.All(inst.Items, item => Assert.Same(clone, item.Map));

            var doors = inst.Items.OfType<MetalDoor>().ToDictionary(d => d.Location);
            var cloneA = doors[doorA.Location];
            var cloneB = doors[doorB.Location];
            Assert.Same(cloneB, cloneA.Link);
            Assert.Same(cloneA, cloneB.Link);
            Assert.Same(doorB, doorA.Link);

            var teleporters = inst.Items.OfType<Teleporter>().ToDictionary(t => t.Location);
            Assert.Same(clone, teleporters[stairs.Location].MapDest);
            Assert.Same(Map.Felucca, teleporters[exit.Location].MapDest);
            Assert.Same(Map.Felucca, stairs.MapDest);

            var spawnerClone = Assert.Single(inst.Items.OfType<Spawner>());
            Assert.Equal(spawner.SpawnBounds, spawnerClone.SpawnBounds);
            var spawned = spawnerClone.Spawned.Keys.OfType<Mobile>().ToList();
            Assert.All(spawned, m => Assert.Same(clone, m.Map));

            // Re-entering keeps the instance and the original way home.
            Assert.True(type.Enter(player, definition, player.Serial));
            Assert.Same(inst, type.Find(player.Serial, definition));
            Assert.True(InstanceReturnPoints.TryGet(player.Serial, out var recorded));
            Assert.Equal(home, recorded.Location);

            var litter = new Item(0x1F14);
            litter.MoveToWorld(new Point3D(5015, 3015, 0), clone);
            var region = inst.Region;
            var copies = inst.Items.ToList();

            type.FreeInstance(inst);

            Assert.Null(type.Find(player.Serial, definition));
            Assert.Same(Map.Felucca, player.Map);
            Assert.Equal(home, player.Location);
            Assert.False(region.Registered);
            Assert.All(copies, item => Assert.True(item.Deleted));
            Assert.All(spawned, m => Assert.True(m.Deleted));
            Assert.True(litter.Deleted);
            Assert.Equal(0, CountEntities(clone, definition.Bounds[0]));
            Assert.All(sources, item => Assert.False(item.Deleted));
        }
        finally
        {
            player?.Delete();
            foreach (var item in sources)
            {
                item.Delete();
            }

            InstanceManager.UnregisterPool(type);
        }
    }

    private static int CountEntities(Map map, Rectangle2D bounds)
    {
        var count = 0;
        foreach (var _ in map.GetItemsInBounds(bounds))
        {
            count++;
        }

        foreach (var _ in map.GetMobilesInBounds(bounds))
        {
            count++;
        }

        return count;
    }

    private static T Place<T>(List<Item> sources, T item, Point3D location) where T : Item
    {
        item.MoveToWorld(location, Map.Felucca);
        sources.Add(item);
        return item;
    }
}
