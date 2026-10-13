using System.Collections.Generic;
using Server;
using Server.Engines.CharacterCreation;
using Server.Veritas.StartingCamp;
using Xunit;

namespace UOContent.Tests.Veritas.StartingCamp;

// Needs the fixture: CharacterCreation's city arrays capture Map instances, which are null before the maps register.
[Collection("Sequential UOContent Tests")]
public class TarotDeckTests
{
    private static CityInfo[] HighSeasCities =>
    [
        ..CharacterCreation.NewHavenStartingCities,
        ..CharacterCreation.TrammelStartingCities,
        ..CharacterCreation.StartingCitiesSA
    ];

    [Fact]
    public void Build_OneNamedCardPerCity()
    {
        var cities = HighSeasCities;
        var cards = TarotDeck.Build(cities, Race.Human);

        Assert.Equal(cities.Length, cards.Length);

        var keys = new HashSet<string>();
        for (var i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            Assert.Same(cities[i], card.Destination);
            Assert.NotEqual(TarotDeck.FallbackTitle, card.Title);
            Assert.False(string.IsNullOrWhiteSpace(card.Lore));
            Assert.Equal(GypsyCampArt.CardFace, card.GumpArtId);
            Assert.True(keys.Add(card.Key), $"Duplicate key {card.Key}");
        }
    }

    [Fact]
    public void Build_OtherEraCitiesHaveCards()
    {
        CityInfo[] cities = [..CharacterCreation.FeluccaStartingCities, ..CharacterCreation.OldHavenStartingCities];

        var cards = TarotDeck.Build(cities, Race.Human);

        Assert.DoesNotContain(cards, c => c.Title == TarotDeck.FallbackTitle);
        Assert.Contains(cards, c => c.Key == "Magincia" && c.Title == "The Tower");
    }

    [Fact]
    public void Build_UnknownCityGetsTheWorld()
    {
        var city = new CityInfo("Buccaneer's Den", "The Pirate's Rest", 2700, 2100, 0, Map.Felucca);

        var card = Assert.Single(TarotDeck.Build([city], Race.Human));

        Assert.Equal(TarotDeck.FallbackTitle, card.Title);
        Assert.Contains("Buccaneer's Den", card.Lore);
        Assert.Contains("The Pirate's Rest", card.Lore);
    }

    [Fact]
    public void Build_GargoylesSeeRoyalCityFirst()
    {
        var human = TarotDeck.Build(HighSeasCities, Race.Human);
        var gargoyle = TarotDeck.Build(HighSeasCities, Race.Gargoyle);

        Assert.NotEqual("Royal City", human[0].Key);
        Assert.Equal("Royal City", gargoyle[0].Key);
        Assert.Equal(human.Length, gargoyle.Length);
        Assert.Contains("home", gargoyle[0].Lore);
    }
}
