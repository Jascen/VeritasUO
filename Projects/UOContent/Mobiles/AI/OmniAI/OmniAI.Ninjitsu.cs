using System;
using Server.Spells;
using Server.Spells.Ninjitsu;

namespace Server.Mobiles;

public partial class OmniAI
{
    private static readonly TimeSpan ShurikenFlightTime = TimeSpan.FromSeconds(1);

    private long _nextShurikenThrow;
    private int _smokeBombs;
    private bool _hasSetSmokeBombs;

    public void NinjitsuPower()
    {
        if (!_hasSetSmokeBombs)
        {
            _hasSetSmokeBombs = true;
            _smokeBombs = Utility.RandomMinMax(3, 5);
        }

        if (Mobile.Hidden)
        {
            UseHiddenNinjaMove();
        }
        else if (Utility.RandomDouble() < 0.2)
        {
            DebugSay("Casting Mirror Image");
            new MirrorImage(Mobile, null).Cast();
        }
        else
        {
            UseNinjaMove();
        }

        var combatant = Mobile.Combatant;

        if (Core.TickCount - _nextShurikenThrow >= 0 && combatant != null && Mobile.InRange(combatant, 12) &&
            Mobile.InLOS(combatant))
        {
            ThrowShuriken(combatant);
        }
    }

    public void UseHiddenNinjaMove()
    {
        DebugSay("Using a hidden ninja strike");

        var ninjitsu = Mobile.Skills.Ninjitsu.Value;
        var whichOne = Utility.RandomMinMax(1, 3);

        if (whichOne == 3 && ninjitsu >= 80.0)
        {
            SpecialMove.SetCurrentMove(Mobile, new KiAttack());
        }
        else if (whichOne >= 2 && ninjitsu >= 30.0)
        {
            SpecialMove.SetCurrentMove(Mobile, new SurpriseAttack());
        }
        else if (ninjitsu >= 20.0)
        {
            SpecialMove.SetCurrentMove(Mobile, new Backstab());
        }
    }

    public void UseNinjaMove()
    {
        DebugSay("Using a ninja strike");

        var ninjitsu = Mobile.Skills.Ninjitsu.Value;
        var whichOne = Utility.RandomMinMax(1, 3);

        if (whichOne == 3 && ninjitsu >= 85.0)
        {
            SpecialMove.SetCurrentMove(Mobile, new DeathStrike());
        }
        else if (whichOne >= 2 && ninjitsu >= 60.0)
        {
            SpecialMove.SetCurrentMove(Mobile, new FocusAttack());
        }
        else
        {
            UseWeaponStrike();
        }
    }

    private void ThrowShuriken(Mobile target)
    {
        DebugSay("Throwing a shuriken");

        Mobile.Direction = Mobile.GetDirectionTo(target);

        if (Mobile.Body.IsHuman)
        {
            Mobile.Animate(Mobile.Mounted ? 26 : 9, 7, 1, true, false, 0);
        }

        Mobile.PlaySound(0x23A);
        Mobile.MovingEffect(target, 0x27AC, 1, 0, false, false);

        Timer.StartTimer(ShurikenFlightTime, () => ShurikenDamage(target));

        _nextShurikenThrow = Core.TickCount + Utility.RandomMinMax(5, 15) * 1000L;
    }

    public virtual void ShurikenDamage(Mobile target)
    {
        if (Mobile.Deleted || !Mobile.Alive || target.Deleted || !target.Alive || target.Map != Mobile.Map ||
            !Mobile.CanBeHarmful(target))
        {
            return;
        }

        Mobile.DoHarmful(target);
        AOS.Damage(target, Mobile, Utility.RandomMinMax(3, 5), 100, 0, 0, 0, 0);

        var poison = Mobile.Skills.Ninjitsu.Value switch
        {
            >= 120.0 => Poison.Lethal,
            >= 101.0 => Poison.Deadly,
            >= 100.0 => Poison.Greater,
            >= 70.0  => Poison.Regular,
            >= 50.0  => Poison.Lesser,
            _        => null
        };

        if (poison != null)
        {
            target.ApplyPoison(Mobile, poison);
        }
    }
}
