using System;
using System.Collections.Generic;
using Server.Collections;
using Server.Mobiles;

namespace Server.Veritas.StartingCamp;

/// <summary>
/// Builds the gypsy's deck from the shard's starting cities, so the cards follow whatever era is configured.
/// </summary>
public static class TarotDeck
{
    public const string FallbackTitle = "The World";

    private const string RoyalCity = "Royal City";

    private static readonly Dictionary<string, (string Title, string Lore)> _cardsByCity =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["New Haven"] = (
                "The Fool",
                "The Fool steps off the cliff with a smile, trusting the road to rise and meet him. New Haven was rebuilt for " +
                "those taking their first steps: instructors in every trade, gentle roads, and neighbors who remember being new " +
                "themselves. If you would rather learn before you leap, begin here."
            ),
            ["Britain"] = (
                "The Emperor",
                "The Emperor sits upon a throne of order and law. Britain is the seat of Lord British and the heart of the realm, " +
                "crowded with guildhalls, markets, and the Royal Guard. Bards sing of Compassion in its taverns, and every road in " +
                "Britannia seems to lead back to its gates."
            ),
            ["Yew"] = (
                "Justice",
                "Justice holds her scales level and her sword unsheathed. Yew lies deep in the great forest, home of the Empath " +
                "Abbey and the Court of Truth. Woodcutters, druids, and judges share its quiet groves, and every deed done there " +
                "is weighed in time."
            ),
            ["Minoc"] = (
                "The Hanged Man",
                "The Hanged Man gives of himself so that something greater may be gained. Minoc is a town of miners and tinkers " +
                "who pour their labor into the mountain, and of Sacrifice, which they know better than most. Ore, gears, and hard " +
                "honest work await."
            ),
            ["Moonglow"] = (
                "The Magician",
                "The Magician turns knowledge into power. Moonglow is an island of scholars, home to the Lycaeum and to Honesty. " +
                "Mages read the stars from its towers, reagents grow in its tended gardens, and no question goes unasked for long."
            ),
            ["Trinsic"] = (
                "Strength",
                "Strength is not the clenched fist but the steady hand that holds it back. Trinsic is a walled city of paladins " +
                "at the desert's edge, sworn to Honor. Its gates are well guarded and its training yards are never empty."
            ),
            ["Jhelom"] = (
                "The Chariot",
                "The Chariot charges ahead and does not look back. Jhelom's isles belong to duelists and mercenaries who live by " +
                "Valor and settle their quarrels in the pits. If you would rather earn your name with a blade, sail for Jhelom."
            ),
            ["Skara Brae"] = (
                "The Star",
                "The Star shines after the storm and promises that hope returns. Skara Brae sits on a lake island, a town of " +
                "rangers and healers devoted to Spirituality. It has weathered old plagues and older sorrows, and it is gentler " +
                "for them."
            ),
            ["Vesper"] = (
                "Wheel of Fortune",
                "The Wheel turns, and fortunes rise and fall with it. Vesper is a city of canals and merchants, where trade barges " +
                "come and go from the Ironwood Inn. A clever hand can make a fortune here, and a careless one can lose it."
            ),
            [RoyalCity] = (
                "The Empress",
                "The Empress tends her people as a gardener tends a field. Royal City is Queen Zhah's capital in Ter Mur, where " +
                "the gargoyle people rebuild what was lost. Its markets are strange to human eyes, and its people are patient " +
                "with those who come to learn."
            ),
            ["Magincia"] = (
                "The Tower",
                "The Tower falls so that something humbler can be built in its place. Magincia was once the proudest city in the " +
                "land and paid dearly for it. Its people rebuild now, and they remember what pride cost them."
            ),
            ["Haven"] = (
                "Temperance",
                "Temperance pours patiently from one cup to another, losing nothing. Haven is a peaceful harbor of healers and " +
                "new beginnings, a place to find your balance before the wider world asks anything of you."
            ),
        };

    public static TarotCard[] BuildFor(PlayerMobile pm) => Build(CampDeparture.GetAvailableCities(pm), pm.Race);

    internal static TarotCard[] Build(CityInfo[] cities, Race race)
    {
        using var cards = PooledRefList<TarotCard>.Create();

        for (var i = 0; i < cities.Length; i++)
        {
            var card = CreateCard(cities[i], race);

            // Gargoyles are returning to their homeland, so their reading opens with it.
            if (race == Race.Gargoyle && card.Key.InsensitiveEquals(RoyalCity))
            {
                cards.Insert(0, card);
            }
            else
            {
                cards.Add(card);
            }
        }

        return cards.ToArray();
    }

    private static TarotCard CreateCard(CityInfo city, Race race)
    {
        if (!_cardsByCity.TryGetValue(city.City, out var definition))
        {
            return new TarotCard(
                city.City,
                FallbackTitle,
                $"The World is wide, and this card points to only one corner of it. Your road leads to {city.City}, where " +
                $"{city.Building} keeps a bed and a fire for travelers. What you make of it from there is your own affair.",
                city
            );
        }

        var lore = race == Race.Gargoyle && city.City.InsensitiveEquals(RoyalCity)
            ? $"{definition.Lore} For you, it is not a strange land at all. It is home."
            : definition.Lore;

        return new TarotCard(city.City, definition.Title, lore, city);
    }
}
