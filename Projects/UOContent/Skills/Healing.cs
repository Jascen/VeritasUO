using System;
using Server.Items;

namespace Server.SkillHandlers;

/// <summary>
/// Using Healing directly tends to the user's own wounds without a bandage: it cures poison and bleeding,
/// or failing that restores a small amount of health.
/// </summary>
public static class Healing
{
    public static void Initialize()
    {
        SkillInfo.Table[(int)SkillName.Healing].Callback = OnUse;
    }

    public static TimeSpan OnUse(Mobile m)
    {
        if (m.Hunger < 6)
        {
            m.SendMessage("You are starving to death and cannot do that!");
            return TimeSpan.FromSeconds(1.0);
        }

        if (MortalStrike.IsWounded(m))
        {
            m.SendLocalizedMessage(1005000); // You cannot heal yourself in your current state.
            return TimeSpan.FromSeconds(1.0);
        }

        var isBleeding = BleedAttack.IsBleeding(m);
        var isPoisoned = m.Poisoned;

        if (isBleeding || isPoisoned)
        {
            if (isBleeding && isPoisoned)
            {
                m.SendMessage("You feel a little healthier.");
            }
            else if (isPoisoned)
            {
                m.SendMessage("The infection begins to clear.");
            }
            else
            {
                m.SendMessage("You work quickly to stem the bleeding!");
            }

            if (isPoisoned)
            {
                m.CurePoison(m);
            }

            if (isBleeding)
            {
                BleedAttack.EndBleed(m, false);
            }

            return TimeSpan.FromSeconds(15.0);
        }

        if (m.Hits >= m.HitsMax)
        {
            m.SendMessage("You already feel healthy.");
            return TimeSpan.FromSeconds(1.0);
        }

        var skill = m.Skills.Healing.Value;
        var amount = 2 + (int)skill / 5;

        if (skill > 100)
        {
            amount += (int)(3 * (skill - 100) / 5);
        }

        if (m.CheckSkill(SkillName.Healing, -50, 99.9))
        {
            m.SendMessage("You focus intently and heal your wounds.");
        }
        else
        {
            m.SendMessage("You are distracted, but heal some wounds.");
            amount = (int)(amount * 0.75);
        }

        m.Heal(amount, m, false);

        return TimeSpan.FromSeconds(15.0);
    }
}
