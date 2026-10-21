using Server.Spells;
using Server.Spells.Chivalry;

namespace Server.Mobiles;

public partial class OmniAI
{
    public void ChivalryPower()
    {
        if (Utility.Random(100) > 30)
        {
            GetPaladinSpell()?.Cast();
        }
        else
        {
            UseWeaponStrike();
        }
    }

    public Spell GetPaladinSpell()
    {
        if (CheckForRemoveCurse() && Utility.RandomDouble() > 0.25)
        {
            DebugSay("Casting Remove Curse");
            return new RemoveCurseSpell(Mobile);
        }

        var chivalry = Mobile.Skills.Chivalry.Value;
        var whichOne = Utility.RandomMinMax(1, 4);

        if (whichOne == 4 && chivalry >= 55.0 && Mobile.Mana >= 10)
        {
            DebugSay("Casting Holy Light");
            return new HolyLightSpell(Mobile);
        }

        if (whichOne >= 3 && CheckForDispelEvil())
        {
            DebugSay("Casting Dispel Evil");
            return new DispelEvilSpell(Mobile);
        }

        if (whichOne >= 2 && !DivineFurySpell.UnderEffect(Mobile) && chivalry >= 35.0)
        {
            DebugSay("Casting Divine Fury");
            return new DivineFurySpell(Mobile);
        }

        if (CheckForConsecrateWeapon())
        {
            DebugSay("Casting Consecrate Weapon");
            return new ConsecrateWeaponSpell(Mobile);
        }

        return null;
    }

    public bool CheckForConsecrateWeapon() =>
        Mobile.Skills.Chivalry.Value >= 15.0 && Mobile.Mana > 9 && GetEquippedWeapon() is { Consecrated: false };

    public bool CheckForDispelEvil()
    {
        if (Mobile.Skills.Chivalry.Value < 35.0 || Mobile.Mana <= 9)
        {
            return false;
        }

        foreach (var m in Mobile.GetMobilesInRange(4))
        {
            if (m == Mobile)
            {
                continue;
            }

            if (m is BaseCreature bc && (bc.Summoned && !bc.IsAnimatedDead || !bc.Controlled && bc.Karma < 0) ||
                TransformationSpellHelper.UnderTransformation(m))
            {
                return true;
            }
        }

        return false;
    }

    public bool CheckForRemoveCurse()
    {
        if (Mobile.Skills.Chivalry.Value < 5.0 || Mobile.Mana <= 19)
        {
            return false;
        }

        if (Mobile.GetStatMod("[Magic] Str Offset")?.Offset < 0 ||
            Mobile.GetStatMod("[Magic] Dex Offset")?.Offset < 0 ||
            Mobile.GetStatMod("[Magic] Int Offset")?.Offset < 0)
        {
            return true;
        }

        // Blood Oath and Strangle leave no queryable mark, so a necromancer foe is treated as a likely curser.
        return Mobile.Combatant?.Skills.Necromancy.Value > 20.0 && Utility.RandomDouble() > 0.6;
    }
}
