using Server.Mobiles;
using Server.Veritas.StartingCamp;

namespace Server.Engines.Avatar;

/// <summary>
/// The safe zone where Avatars are reborn and the only place shop purchases are allowed: the gypsy starter camp.
/// </summary>
public static class AvatarSanctuary
{
    public static bool IsIn(PlayerMobile from) => GypsyCamp.IsInCamp(from);

    public static void MoveTo(PlayerMobile from) =>
        from.MoveToWorld(GypsyCamp.Arrival, Map.TryParse(GypsyCamp.MapName, null, out var map) ? map : Map.Felucca);
}
