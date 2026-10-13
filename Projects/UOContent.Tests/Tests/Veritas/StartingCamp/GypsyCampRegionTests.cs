using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Veritas.StartingCamp;
using Xunit;

namespace UOContent.Tests.Veritas.StartingCamp;

[Collection("Sequential UOContent Tests")]
public class GypsyCampRegionTests
{
    private static Map Ruins => Map.Parse(GypsyCamp.MapName, null);

    // Same footprint as the regions.json entry; the fixture does not load regions.json.
    private static GypsyCampRegion CreateCamp() =>
        new(GypsyCamp.CampName, Ruins, 50, new Rectangle3D(96, 96, -128, 64, 64, 256));

    [Fact]
    public void PlayersCannotLeaveOrFight_StaffCan()
    {
        var region = CreateCamp();
        PlayerMobile player = null;
        PlayerMobile other = null;
        PlayerMobile staff = null;
        try
        {
            region.Register();
            player = CreatePlayer(GypsyCamp.Arrival);
            other = CreatePlayer(GypsyCamp.Arrival);
            staff = CreatePlayer(GypsyCamp.Arrival);
            staff.AccessLevel = AccessLevel.GameMaster;

            Assert.Same(region, player.Region);
            Assert.True(GypsyCamp.IsInCamp(player));

            foreach (var type in Enum.GetValues<TravelCheckType>())
            {
                Assert.False(region.CheckTravel(player, GypsyCamp.Arrival, type, out _), $"player {type}");
                Assert.True(region.CheckTravel(staff, GypsyCamp.Arrival, type, out _), $"staff {type}");
            }

            Assert.False(region.AllowHarmful(player, other));
            Assert.True(region.AllowHarmful(staff, other));

            Assert.False(region.CanUseStuckMenu(player));
            Assert.True(region.CanUseStuckMenu(staff));

            Assert.False(region.AllowHousing(player, GypsyCamp.Arrival));

            Assert.False(region.OnSkillUse(player, (int)SkillName.Stealing));
            Assert.False(region.OnSkillUse(player, (int)SkillName.Snooping));
            Assert.True(region.OnSkillUse(player, (int)SkillName.Hiding));
            Assert.True(region.OnSkillUse(staff, (int)SkillName.Stealing));

            Assert.Equal(TimeSpan.Zero, region.GetLogoutDelay(player));
        }
        finally
        {
            player?.Delete();
            other?.Delete();
            staff?.Delete();
            region.Unregister();
        }
    }

    [Fact]
    public void IsInCamp_FalseOutsideTheRegion()
    {
        var region = CreateCamp();
        PlayerMobile player = null;
        try
        {
            region.Register();
            player = CreatePlayer(new Point3D(200, 200, 0));

            Assert.False(GypsyCamp.IsInCamp(player));
        }
        finally
        {
            player?.Delete();
            region.Unregister();
        }
    }

    [Fact]
    public void TryGetStartLocation_RequiresRegionAndGypsy()
    {
        Assert.False(GypsyCamp.TryGetStartLocation(out _));

        var region = CreateCamp();
        CampGypsy gypsy = null;
        try
        {
            region.Register();
            Assert.False(GypsyCamp.TryGetStartLocation(out _));

            gypsy = new CampGypsy();
            gypsy.MoveToWorld(new Point3D(124, 125, 0), Ruins);

            Assert.True(GypsyCamp.TryGetStartLocation(out var city));
            Assert.Equal(GypsyCamp.Arrival, city.Location);
            Assert.Same(Ruins, city.Map);
        }
        finally
        {
            gypsy?.Delete();
            region.Unregister();
        }
    }

    private static PlayerMobile CreatePlayer(Point3D location)
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();
        player.Player = true;
        player.Race = Race.Human;
        player.AddItem(new Backpack());
        player.MoveToWorld(location, Ruins);
        return player;
    }
}
