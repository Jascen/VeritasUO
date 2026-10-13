namespace Server.Veritas.StartingCamp;

/// <summary>
/// Gump art used by the gypsy camp. Memento's custom art is not in the stock client,
/// so every slot points at a placeholder until real art ships.
/// Positions in <see cref="TarotGump"/> mirror Memento's layout so its art drops straight in.
/// </summary>
public static class GypsyCampArt
{
    public const int Placeholder = 0x0000;

    //TODO(art): Memento used 2611 (reading table background).
    public const int Background = Placeholder;

    //TODO(art): Memento used 11473 (title banner on the intro page).
    public const int Header = Placeholder;

    //TODO(art): Memento used 1102 (gypsy portrait).
    public const int Portrait = Placeholder;

    //TODO(art): Memento used 2610 (welcome scroll background).
    public const int WelcomeBackground = Placeholder;

    //TODO(art): Memento used 11474 (welcome title banner).
    public const int WelcomeHeader = Placeholder;

    //TODO(art): per-card faces default to this until each TarotCard gets its own art.
    public const int CardFace = Placeholder;
}
