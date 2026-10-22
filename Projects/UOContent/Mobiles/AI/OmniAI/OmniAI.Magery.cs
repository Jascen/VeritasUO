using System;
using Server.Spells;
using Server.Spells.Eighth;
using Server.Spells.Fifth;
using Server.Spells.First;
using Server.Spells.Fourth;
using Server.Spells.Second;
using Server.Spells.Seventh;
using Server.Spells.Sixth;
using Server.Spells.Third;

namespace Server.Mobiles;

public partial class OmniAI
{
    private const int ExplosionCooldownMs = 3000;

    // Mana cost per circle, indexed from first circle.
    private static readonly int[] _mageryMana = [4, 6, 9, 11, 14, 20, 40, 50];

    private long _nextExplosionTime;

    /// <summary>
    ///     Empowered casters are not slowed. Others wait twice the spell's cast delay and recovery
    ///     before their next Magery spell.
    /// </summary>
    public virtual bool IsEmpowered => false;

    public virtual bool CanUseMagerySummon => true;

    private const double ComboChance = 0.25;
    private const double InvisRollChance = 0.25;

    /// <summary>
    ///     The Magery school's pick: open a MageAI combo, slip into invisibility when hurt, or
    ///     fall through to the reflect- and mana-aware <see cref="GetMagerySpell"/>.
    /// </summary>
    public override Spell ChooseSpell(Mobile c)
    {
        if (Mobile.Mana > 60 && Utility.RandomDouble() < ComboChance && SetUpCombo(c) is { } opener)
        {
            DebugSay("Starting a combo");
            return opener;
        }

        if (!Mobile.Hidden && Mobile.Hits < Mobile.HitsMax / 2 && Utility.RandomDouble() < InvisRollChance &&
            ScaleBySkill(InvisChance, SkillName.Magery) > Utility.RandomDouble())
        {
            DebugSay("Attempting to invis myself");
            return new InvisibilitySpell(Mobile);
        }

        return GetMagerySpell();
    }

    // Non-Magery picks (and turns spent on a skill or special move) wait the flat school interval.
    protected override TimeSpan GetDelay(Spell spell)
    {
        if (spell is not MagerySpell)
        {
            return TimeSpan.FromMilliseconds(CastIntervalMs);
        }

        var delay = spell.GetCastDelay() + spell.GetCastRecovery();

        return IsEmpowered ? delay : delay + delay;
    }

    public Spell GetMagerySpell()
    {
        var combatant = Mobile.Combatant;

        // Open with direct damage until the combatant has taken some.
        if (combatant == null || combatant.Hits >= 0.75 * combatant.HitsMax)
        {
            return GetRandomMageryDamageSpell();
        }

        // Only sturdier creatures bless, and only once they have taken notable damage.
        var spell = Mobile.HitsMax > 300 && Mobile.Hits < 0.75 * Mobile.HitsMax ? CheckBless() : null;

        if (spell != null)
        {
            DebugSay("Blessing myself");
            return spell;
        }

        // A reflected curse or poison lands on us, so judge it against ourselves.
        var checkSelf = HasReflect(combatant);

        if (Utility.RandomDouble() > 0.75)
        {
            spell = CheckCurse(checkSelf);

            if (spell != null)
            {
                DebugSay("Cursing my opponent");
                return spell;
            }
        }

        var canPoison = checkSelf ? Mobile.Poisoned : !combatant.Poisoned;

        if (canPoison && Utility.RandomDouble() > 0.75)
        {
            DebugSay("Casting poison");
            return new PoisonSpell(Mobile);
        }

        // Chance scales with how much of a caster the opponent is.
        if (CheckManaDrain(checkSelf) > 0.75)
        {
            if (Mobile.Skills.Magery.Value > 80.0)
            {
                spell = new ManaVampireSpell(Mobile);
            }
            else if (Mobile.Skills.Magery.Value > 40.0)
            {
                spell = new ManaDrainSpell(Mobile);
            }

            if (spell != null)
            {
                DebugSay("Draining mana");
                return spell;
            }
        }

        if (CanUseMagerySummon && Utility.RandomDouble() > 0.90)
        {
            spell = CheckMagerySummon();

            if (spell != null)
            {
                DebugSay("Summoning help");
                return spell;
            }
        }

        return GetRandomMageryDamageSpell();
    }

    // Pre-AOS Magic Reflection bounces spells back at the caster; the AOS version only shifts resists.
    private static bool HasReflect(Mobile m) => !Core.AOS && m.MagicDamageAbsorb > 0;

    public Spell CheckBless()
    {
        var mod = Mobile.GetStatMod("[Magic] Str Offset");

        if (mod?.Offset > 0)
        {
            return null;
        }

        if (Mobile.Skills.Magery.Value >= 40.0)
        {
            return new BlessSpell(Mobile);
        }

        mod = Mobile.GetStatMod("[Magic] Int Offset");

        if (mod == null || mod.Offset < 0)
        {
            return new CunningSpell(Mobile);
        }

        mod = Mobile.GetStatMod("[Magic] Dex Offset");

        if (mod == null || mod.Offset < 0)
        {
            return new AgilitySpell(Mobile);
        }

        return new StrengthSpell(Mobile);
    }

    public Spell CheckCurse(bool checkSelf)
    {
        var foe = checkSelf ? Mobile : Mobile.Combatant;

        if (foe == null || CurseSpell.UnderEffect(foe))
        {
            return null;
        }

        if (Mobile.Skills.Magery.Value >= 40.0)
        {
            return new CurseSpell(Mobile);
        }

        if (foe.GetStatMod("[Magic] Int Offset")?.Offset < 0)
        {
            return null;
        }

        // Walk Feeblemind -> Weaken -> Clumsy, skipping the stats already cursed.
        var whichOne = 1;

        if (foe.GetStatMod("[Magic] Str Offset") != null)
        {
            whichOne++;
        }

        if (foe.GetStatMod("[Magic] Dex Offset") != null)
        {
            whichOne++;
        }

        return whichOne switch
        {
            1 => new FeeblemindSpell(Mobile),
            2 => new WeakenSpell(Mobile),
            _ => new ClumsySpell(Mobile)
        };
    }

    public double CheckManaDrain(bool checkSelf)
    {
        var foe = checkSelf ? Mobile : Mobile.Combatant;

        if (foe == null || foe.Mana < 10)
        {
            return 0.0;
        }

        var drain = 0.0;

        if (foe.Skills.Bushido.Value > 35.0)
        {
            drain += 0.1;
        }

        if (foe.Skills.Chivalry.Value > 35.0)
        {
            drain += 0.1;
        }

        if (foe.Skills.Magery.Value > 35.0)
        {
            drain += 0.2;
        }

        if (foe.Skills.Necromancy.Value > 35.0)
        {
            drain += 0.2;
        }

        if (foe.Skills.Ninjitsu.Value > 35.0)
        {
            drain += 0.1;
        }

        return drain == 0.0 ? 0.0 : Utility.RandomDouble() + drain;
    }

    public Spell CheckMagerySummon()
    {
        var slots = Mobile.FollowersMax - Mobile.Followers;

        if (slots < 2)
        {
            return null;
        }

        var magery = Mobile.Skills.Magery.Value;

        if (magery > 95.0 && Mobile.Mana > 50)
        {
            var whichOne = Utility.Random(10);

            if (slots > 4 && whichOne == 0)
            {
                return new SummonDaemonSpell(Mobile);
            }

            if (slots > 3 && whichOne < 2)
            {
                return new FireElementalSpell(Mobile);
            }

            if (slots > 2 && whichOne < 3)
            {
                return new WaterElementalSpell(Mobile);
            }

            if (whichOne < 4)
            {
                return new AirElementalSpell(Mobile);
            }

            if (whichOne < 5)
            {
                return new EarthElementalSpell(Mobile);
            }

            return new EnergyVortexSpell(Mobile);
        }

        if (magery > 55.0 && Mobile.Mana > 14)
        {
            // Peaceful creatures call nature friendly animals instead of blades.
            return Mobile.InitialInnocent ? new SummonCreatureSpell(Mobile) : new BladeSpiritsSpell(Mobile);
        }

        return null;
    }

    public Spell GetRandomMageryDamageSpell()
    {
        DebugSay("Random damage spell");

        var whichOne = (int)(Mobile.Skills.Magery.Value / 14.2) - 1 + Utility.RandomMinMax(-2, 0);

        if (whichOne > 6)
        {
            whichOne = 6;
        }
        else if (whichOne < 1)
        {
            whichOne = 1;
        }

        // Step down a single circle when short on mana, so mana can build up for a stronger spell.
        if (_mageryMana[whichOne] > Mobile.Mana)
        {
            whichOne--;
        }

        switch (whichOne)
        {
            case 6:
                {
                    return new FlameStrikeSpell(Mobile);
                }
            case 5:
                {
                    if (Core.TickCount - _nextExplosionTime < 0)
                    {
                        return new EnergyBoltSpell(Mobile);
                    }

                    _nextExplosionTime = Core.TickCount + ExplosionCooldownMs;
                    return new ExplosionSpell(Mobile);
                }
            case 4:
                {
                    return new MindBlastSpell(Mobile);
                }
            case 3:
                {
                    return new LightningSpell(Mobile);
                }
            case 2:
            case 1:
                {
                    if (whichOne == 1 && Mobile.Combatant?.InRange(Mobile, 2) == true)
                    {
                        return new HarmSpell(Mobile);
                    }

                    return new FireballSpell(Mobile);
                }
            default:
                {
                    return new MagicArrowSpell(Mobile);
                }
        }
    }
}
