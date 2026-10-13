using System;
using System.Text.Json.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;

namespace Server.Veritas.StartingCamp;

/// <summary>
/// The starter island. Players can only leave through the gypsy, so every other way off the island is closed to them.
/// Staff are exempt so they can move freely.
/// </summary>
public class GypsyCampRegion : BaseRegion
{
    [JsonConstructor]
    public GypsyCampRegion(string name, Map map, int priority, params Rectangle3D[] area) : base(name, map, priority, area)
    {
    }

    public GypsyCampRegion(string name, Map map, Region parent, params Rectangle3D[] area) : base(name, map, parent, area)
    {
    }

    public GypsyCampRegion(string name, Map map, Region parent, int priority, params Rectangle3D[] area)
        : base(name, map, parent, priority, area)
    {
    }

    public override bool AllowHousing(Mobile from, Point3D p) => false;

    public override bool AllowHarmful(Mobile from, Mobile target) => from.AccessLevel > AccessLevel.Player;

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null; // Use default message
        return m.AccessLevel > AccessLevel.Player;
    }

    // Leaving via the stuck menu would skip the gypsy's departure, which later systems hook into.
    public override bool CanUseStuckMenu(Mobile m) => m.AccessLevel > AccessLevel.Player;

    public override bool OnBeginSpellCast(Mobile m, ISpell s)
    {
        if (m.AccessLevel == AccessLevel.Player)
        {
            m.SendLocalizedMessage(501802); // Thy spell doth not appear to work...
            return false;
        }

        return base.OnBeginSpellCast(m, s);
    }

    // Stealing and snooping are not harmful actions, so AllowHarmful does not cover them.
    public override bool OnSkillUse(Mobile m, int skill)
    {
        if (m.AccessLevel == AccessLevel.Player && (SkillName)skill is SkillName.Stealing or SkillName.Snooping)
        {
            m.SendLocalizedMessage(501802); // Thy spell doth not appear to work...
            return false;
        }

        return base.OnSkillUse(m, skill);
    }

    public override TimeSpan GetLogoutDelay(Mobile m) => TimeSpan.Zero;

    // New characters are placed here before they have a NetState, so DisplayTo no-ops for them and the login hook shows
    // the story instead. This covers anyone moved onto the island afterwards.
    public override void OnEnter(Mobile m)
    {
        base.OnEnter(m);

        if (m is PlayerMobile)
        {
            CampWelcomeGump.DisplayTo(m);
        }
    }

    public override void OnExit(Mobile m)
    {
        base.OnExit(m);

        m.CloseGump<CampWelcomeGump>();
        m.CloseGump<TarotGump>();
    }
}
