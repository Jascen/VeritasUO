using Server.Mobiles;

namespace Server.Veritas.StartingCamp;

/// <summary>
/// Raised just before a player leaves the gypsy camp. Subscribers may change the destination or cancel the departure.
/// </summary>
public class BeginJourneyEventArgs(PlayerMobile mobile, TarotCard card, Point3D location, Map map)
{
    public PlayerMobile Mobile { get; } = mobile;

    public TarotCard Card { get; } = card;

    public Point3D Location { get; set; } = location;

    public Map Map { get; set; } = map;

    public bool Cancel { get; set; }
}
