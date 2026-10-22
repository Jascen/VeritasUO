using Server.Items;
using Server.Spells;
using Server.Spells.Bushido;

namespace Server.Mobiles;

public partial class OmniAI
{
    public void BushidoPower()
    {
        if (Utility.RandomBool() &&
            !(Confidence.IsConfident(Mobile) || CounterAttack.IsCountering(Mobile) || Evasion.IsEvading(Mobile)))
        {
            UseBushidoStance();
        }
        else
        {
            UseBushidoMove();
        }
    }

    public void UseBushidoStance()
    {
        if (GetEquippedWeapon() == null)
        {
            return;
        }

        DebugSay("Using a samurai stance");

        var bushido = Mobile.Skills.Bushido.Value;

        Spell spell = Utility.RandomMinMax(1, 3) switch
        {
            3 when bushido >= 60.0    => new Evasion(Mobile, null),
            >= 2 when bushido >= 40.0 => new CounterAttack(Mobile, null),
            _ when bushido >= 25.0    => new Confidence(Mobile, null),
            _                         => null
        };

        spell?.Cast();
    }

    public void UseBushidoMove()
    {
        var combatant = Mobile.Combatant;

        if (combatant == null || GetEquippedWeapon() is not { } weapon)
        {
            return;
        }

        DebugSay("Using a samurai or special move strike");

        var bushido = Mobile.Skills.Bushido.Value;
        var tactics = Mobile.Skills.Tactics.Value;
        var whichOne = Utility.RandomMinMax(1, 4);

        if (whichOne == 4 && bushido >= 70.0)
        {
            SpecialMove.SetCurrentMove(Mobile, new MomentumStrike());
        }
        else if (whichOne >= 3 && bushido >= 50.0)
        {
            SpecialMove.SetCurrentMove(Mobile, new LightningStrike());
        }
        else if (whichOne >= 2 && bushido >= 25.0 && combatant.Hits <= Mobile.DamageMin)
        {
            SpecialMove.SetCurrentMove(Mobile, new HonorableExecution());
        }
        else if (whichOne >= 2 && tactics >= 90.0)
        {
            WeaponAbility.SetCurrentAbility(Mobile, weapon.PrimaryAbility);
        }
        else if (tactics >= 60.0)
        {
            WeaponAbility.SetCurrentAbility(Mobile, weapon.SecondaryAbility);
        }
    }
}
