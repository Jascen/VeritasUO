namespace Server.Veritas.StartingCamp;

/// <summary>A card in the gypsy's deck. Drawing it sends the player to <see cref="Destination"/>.</summary>
public sealed record TarotCard(string Key, string Title, string Lore, CityInfo Destination, int GumpArtId = GypsyCampArt.CardFace);
