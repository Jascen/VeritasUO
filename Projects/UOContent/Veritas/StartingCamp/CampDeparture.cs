using System;
using Server.Collections;
using Server.Engines.CharacterCreation;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Server.Veritas.StartingCamp;

public enum DepartureResult
{
    Ok,
    Dead,
    GypsyGone,
    TooFar,
    InvalidCity
}

public static class CampDeparture
{
    public const int TalkRange = 3;

    private static readonly ILogger logger = LogFactory.GetLogger(typeof(CampDeparture));

    public static CityInfo[] GetAvailableCities(Mobile m) =>
        GetAvailableCities(CharacterCreation.GetStartingCities(), m.NetState?.Flags ?? ClientFlags.None);

    internal static CityInfo[] GetAvailableCities(CityInfo[] cities, ClientFlags flags)
    {
        using var list = PooledRefList<CityInfo>.Create();

        for (var i = 0; i < cities.Length; i++)
        {
            var city = cities[i];
            var map = city.Map;

            if (map == null || map == Map.Internal)
            {
                continue;
            }

            // Ter Mur is unreachable for clients that did not install it.
            if (map == Map.TerMur && (flags & ClientFlags.TerMur) == 0)
            {
                continue;
            }

            list.Add(city);
        }

        return list.ToArray();
    }

    public static DepartureResult CanDepart(PlayerMobile pm, CampGypsy gypsy, TarotCard card)
    {
        if (!pm.Alive)
        {
            return DepartureResult.Dead;
        }

        if (gypsy?.Deleted != false)
        {
            return DepartureResult.GypsyGone;
        }

        if (pm.Map != gypsy.Map || !pm.InRange(gypsy, TalkRange))
        {
            return DepartureResult.TooFar;
        }

        return IsCardStillValid(pm, card) ? DepartureResult.Ok : DepartureResult.InvalidCity;
    }

    // Re-derive availability so a stale gump (client flags, era or Avatar status changed) cannot send anyone where
    // they could not choose now.
    private static bool IsCardStillValid(PlayerMobile pm, TarotCard card) =>
        (card.Key != AvatarHooks.CardKey || AvatarHooks.IsCardAvailable(pm)) &&
        Array.IndexOf(GetAvailableCities(pm), card.Destination) >= 0;

    public static bool TryDepart(PlayerMobile pm, CampGypsy gypsy, TarotCard card)
    {
        switch (CanDepart(pm, gypsy, card))
        {
            case DepartureResult.Ok:
                {
                    if (card.Key == AvatarHooks.CardKey && AvatarHooks.OnCardDrawn(pm, card))
                    {
                        return true;
                    }

                    TemptationHooks.BeforeDeparture(pm, card, () => Complete(pm, card));
                    return true;
                }
            case DepartureResult.TooFar:
                {
                    pm.SendLocalizedMessage(500446); // That is too far away.
                    break;
                }
            case DepartureResult.InvalidCity:
                {
                    gypsy.Say("The cards will not show me that place for you.");
                    break;
                }
        }

        return false;
    }

    // Runs after the pre-departure step, which may be a gump answered much later, so the gypsy's range is not
    // re-checked but the player and card are.
    internal static void Complete(PlayerMobile pm, TarotCard card)
    {
        if (pm.Deleted || !pm.Alive || !IsCardStillValid(pm, card))
        {
            return;
        }

        var destination = card.Destination;
        var args = new BeginJourneyEventArgs(pm, card, destination.Location, destination.Map);
        GypsyCamp.BeginJourneyEvent(args);

        if (args.Cancel || args.Map == null || args.Map == Map.Internal)
        {
            return;
        }

        Depart(pm, args.Location, args.Map);
        logger.Information("{Player} left the gypsy camp with {Card} for {Location} on {Map}", pm, card.Title, args.Location, args.Map);
    }

    internal static void Depart(PlayerMobile pm, Point3D location, Map map)
    {
        pm.CloseGump<TarotGump>();

        pm.Combatant = null;
        pm.Warmode = false;

        Effects.SendLocationParticles(
            EffectItem.Create(pm.Location, pm.Map, EffectItem.DefaultDuration), 0x376A, 9, 32, 5024
        );

        BaseCreature.TeleportPets(pm, location, map);
        pm.MoveToWorld(location, map);

        Effects.SendLocationParticles(
            EffectItem.Create(pm.Location, pm.Map, EffectItem.DefaultDuration), 0x376A, 9, 32, 5024
        );
        pm.SendSound(0x65C);
        pm.SendMessage("The card crumbles to dust in your hand as the world shifts around you.");
    }
}
