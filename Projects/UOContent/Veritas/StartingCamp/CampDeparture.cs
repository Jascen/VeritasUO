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

    public static DepartureResult CanDepart(PlayerMobile pm, CampGypsy gypsy, CityInfo city)
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

        // Re-derive the list so a stale gump (client flags changed, era changed) cannot send anyone to a city they could not pick now.
        return Array.IndexOf(GetAvailableCities(pm), city) >= 0 ? DepartureResult.Ok : DepartureResult.InvalidCity;
    }

    public static bool TryDepart(PlayerMobile pm, CampGypsy gypsy, CityInfo city)
    {
        var result = CanDepart(pm, gypsy, city);

        switch (result)
        {
            case DepartureResult.Ok:
                {
                    Depart(pm, city);
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

    internal static void Depart(PlayerMobile pm, CityInfo city)
    {
        pm.CloseGump<TownPickerGump>();

        pm.Combatant = null;
        pm.Warmode = false;

        Effects.SendLocationParticles(
            EffectItem.Create(pm.Location, pm.Map, EffectItem.DefaultDuration), 0x376A, 9, 32, 5024
        );

        BaseCreature.TeleportPets(pm, city.Location, city.Map);
        pm.MoveToWorld(city.Location, city.Map);

        Effects.SendLocationParticles(
            EffectItem.Create(pm.Location, pm.Map, EffectItem.DefaultDuration), 0x376A, 9, 32, 5024
        );
        pm.SendSound(0x65C);
        pm.SendMessage("The card crumbles to dust in your hand as the world shifts around you.");

        logger.Information("{Player} left the gypsy for {City}", pm, city.City);
    }
}
