using System;
using Server.Mobiles;

namespace Server.Veritas.StartingCamp;

/// <summary>
/// Extension point for the Temptations system, which is ported separately.
/// </summary>
public static class TemptationHooks
{
    //TODO(temptations): Memento showed TemptationGump here and continued from its accept/skip callbacks. Invoke
    // onContinue to depart; never invoking it keeps the player in the camp. CampDeparture re-validates after the callback.
    public static void BeforeDeparture(PlayerMobile pm, TarotCard card, Action onContinue) => onContinue();
}
