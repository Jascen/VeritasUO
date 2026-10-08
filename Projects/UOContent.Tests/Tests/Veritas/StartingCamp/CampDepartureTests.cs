using System;
using Server;
using Server.Engines.CharacterCreation;
using Server.Items;
using Server.Mobiles;
using Server.Veritas.StartingCamp;
using Xunit;

namespace UOContent.Tests.Veritas.StartingCamp;

[Collection("Sequential UOContent Tests")]
public class CampDepartureTests
{
    private static readonly Point3D CampSpot = new(1000, 1000, 0);

    [Fact]
    public void GetAvailableCities_HidesTerMurWithoutClientFlag()
    {
        CityInfo[] cities =
        [
            ..CharacterCreation.NewHavenStartingCities,
            ..CharacterCreation.TrammelStartingCities,
            ..CharacterCreation.StartingCitiesSA
        ];

        var without = CampDeparture.GetAvailableCities(cities, ClientFlags.None);
        var with = CampDeparture.GetAvailableCities(cities, ClientFlags.TerMur);

        Assert.Equal(cities.Length - CharacterCreation.StartingCitiesSA.Length, without.Length);
        Assert.DoesNotContain(without, c => c.Map == Map.TerMur);
        Assert.Equal(cities.Length, with.Length);
    }

    [Fact]
    public void GetAvailableCities_SkipsInternalAndNullMaps()
    {
        CityInfo[] cities =
        [
            new("Nowhere", "Void", 0, 0, 0, Map.Internal),
            new("Null", "Void", 0, 0, 0, null),
            CharacterCreation.TrammelStartingCities[0]
        ];

        var result = CampDeparture.GetAvailableCities(cities, ClientFlags.None);

        Assert.Single(result);
        Assert.Same(CharacterCreation.TrammelStartingCities[0], result[0]);
    }

    [Fact]
    public void CanDepart_ValidatesPlayerGypsyAndCity()
    {
        var city = Array.Find(CharacterCreation.GetStartingCities(), c => c.Map != Map.TerMur);
        Assert.NotNull(city);

        PlayerMobile player = null;
        CampGypsy gypsy = null;
        try
        {
            player = CreatePlayer();
            gypsy = new CampGypsy();
            gypsy.MoveToWorld(new Point3D(CampSpot.X + 1, CampSpot.Y, 0), Map.Felucca);

            Assert.Equal(DepartureResult.Ok, CampDeparture.CanDepart(player, gypsy, city));

            var forged = new CityInfo("Forged", "Nowhere", 100, 100, 0, Map.Felucca);
            Assert.Equal(DepartureResult.InvalidCity, CampDeparture.CanDepart(player, gypsy, forged));

            gypsy.MoveToWorld(new Point3D(CampSpot.X + CampDeparture.TalkRange + 1, CampSpot.Y, 0), Map.Felucca);
            Assert.Equal(DepartureResult.TooFar, CampDeparture.CanDepart(player, gypsy, city));

            gypsy.Delete();
            Assert.Equal(DepartureResult.GypsyGone, CampDeparture.CanDepart(player, gypsy, city));
            Assert.Equal(DepartureResult.GypsyGone, CampDeparture.CanDepart(player, null, city));

            // Ghost body rather than Kill(): the full death path needs systems the fixture does not configure.
            player.Body = 0x192;
            Assert.False(player.Alive);
            Assert.Equal(DepartureResult.Dead, CampDeparture.CanDepart(player, gypsy, city));
        }
        finally
        {
            gypsy?.Delete();
            player?.Delete();
        }
    }

    [Fact]
    public void Depart_MovesPlayerToCity()
    {
        var city = Array.Find(CharacterCreation.GetStartingCities(), c => c.Map != Map.TerMur);
        Assert.NotNull(city);

        PlayerMobile player = null;
        try
        {
            player = CreatePlayer();

            CampDeparture.Depart(player, city);

            Assert.Equal(city.Location, player.Location);
            Assert.Same(city.Map, player.Map);
        }
        finally
        {
            player?.Delete();
        }
    }

    private static PlayerMobile CreatePlayer()
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();
        player.Player = true;
        player.Race = Race.Human;
        player.AddItem(new Backpack());
        player.MoveToWorld(CampSpot, Map.Felucca);
        return player;
    }
}
