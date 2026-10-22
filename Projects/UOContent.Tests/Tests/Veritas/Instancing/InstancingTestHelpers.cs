using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Veritas.Instancing;

namespace UOContent.Tests.Veritas.Instancing;

internal sealed class TestInstanceType : InstanceType
{
    public List<Instance> Built { get; } = [];
    public List<Instance> Freed { get; } = [];

    public override string Key => "Test";

    protected override void BuildContents(Instance inst) => Built.Add(inst);

    protected override void OnFreed(Instance inst) => Freed.Add(inst);
}

internal static class InstancingTestHelpers
{
    // The fixture does not run Configure, so no production pool exists and tests register their own.
    public static int Register(InstanceType type, int clonesPerMap, params Map[] baseMaps) =>
        InstanceManager.RegisterPool(type, baseMaps, clonesPerMap, InstanceManager.PoolStartIndex, m => m.MapID);

    public static PlayerMobile CreatePlayer(Point3D location, Map map)
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();
        player.Player = true;
        player.Race = Race.Human;
        player.AddItem(new Backpack());
        player.MoveToWorld(location, map);
        return player;
    }
}
