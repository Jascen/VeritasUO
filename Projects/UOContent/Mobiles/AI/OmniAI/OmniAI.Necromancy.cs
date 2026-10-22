using System;
using Server.Items;
using Server.Spells;
using Server.Spells.Necromancy;

namespace Server.Mobiles;

public partial class OmniAI
{
    private const int ShapeshiftCooldownMs = 130000;

    private long _nextShiftTime;

    public virtual bool CanShapeShift => true;

    public Spell GetNecromancerSpell()
    {
        var necro = Mobile.Skills.Necromancy.Value;

        switch (Utility.Random(12))
        {
            case 0:
            case 1:
                {
                    // Animate Dead is left out: the AI has no safe corpse pick, and an
                    // untargeted cast only wastes mana.
                    if (Mobile.Followers is 0 or 3)
                    {
                        DebugSay("Summoning a familiar");

                        CreateNecromancerFamiliar();
                        return null;
                    }

                    goto default;
                }
            case 2:
            case 3:
                {
                    DebugSay("Cursing them");
                    return GetNecromancerCurseSpell();
                }
            case 4:
            case 5:
                {
                    if (necro <= 30.0)
                    {
                        goto default;
                    }

                    DebugSay("Casting Blood Oath");
                    return new BloodOathSpell(Mobile);
                }
            case 6:
                {
                    if (!CanShapeShift)
                    {
                        goto case 3;
                    }

                    DebugSay("Shapechanging");
                    return GetNecromancerShapeshiftSpell();
                }
            default:
                {
                    DebugSay("Random damage spell");
                    return GetNecromancerDamageSpell();
                }
        }
    }

    public Spell GetNecromancerDamageSpell()
    {
        var maxCircle = Math.Max(2, (int)((Mobile.Skills.Necromancy.Value + 20.0) / (100.0 / 7.0)));

        return Utility.Random(maxCircle + 1) switch
        {
            <= 2 => CheckForCurseWeapon(),
            <= 4 => new PoisonStrikeSpell(Mobile),
            <= 6 => new WitherSpell(Mobile),
            7    => new StrangleSpell(Mobile),
            _    => CheckForVengefulSpiritSpell()
        };
    }

    public Spell GetNecromancerCurseSpell()
    {
        var necro = Mobile.Skills.Necromancy.Value;

        return Utility.RandomMinMax(1, 4) switch
        {
            4 when necro >= 75.0  => new StrangleSpell(Mobile),
            3 when necro >= 40.0  => new MindRotSpell(Mobile),
            >= 2 when necro >= 30.0 => new EvilOmenSpell(Mobile),
            _                     => new CorpseSkinSpell(Mobile)
        };
    }

    public Spell GetNecromancerShapeshiftSpell()
    {
        if (Core.TickCount - _nextShiftTime < 0)
        {
            return GetNecromancerDamageSpell();
        }

        _nextShiftTime = Core.TickCount + ShapeshiftCooldownMs;

        var necro = Mobile.Skills.Necromancy.Value;

        return necro switch
        {
            > 110.0 => new VampiricEmbraceSpell(Mobile),
            > 80.0  => new LichFormSpell(Mobile),
            > 50.0  => new HorrificBeastSpell(Mobile),
            > 30.0  => new WraithFormSpell(Mobile),
            _       => null
        };
    }

    public Spell CheckForCurseWeapon()
    {
        if (Mobile.Skills.Necromancy.Value > 5.0 && GetEquippedWeapon() is { Cursed: false })
        {
            return new CurseWeaponSpell(Mobile);
        }

        return new PainSpikeSpell(Mobile);
    }

    public Spell CheckForVengefulSpiritSpell() =>
        Mobile.Followers >= 3 ? new PoisonStrikeSpell(Mobile) : new VengefulSpiritSpell(Mobile);

    public void CreateNecromancerFamiliar()
    {
        var necro = Mobile.Skills.Necromancy.Value;

        BaseCreature familiar = Utility.RandomMinMax(1, 5) switch
        {
            5 when necro >= 100.0   => new VampireBatFamiliar(),
            >= 4 when necro >= 80.0 => new DeathAdder(),
            >= 3 when necro >= 60.0 => new DarkWolfFamiliar(),
            >= 2 when necro >= 50.0 => new ShadowWispFamiliar(),
            _ when necro >= 30.0    => new HordeMinionFamiliar(),
            _                       => null
        };

        if (familiar == null)
        {
            return;
        }

        // Summon deletes the familiar itself when it fails.
        if (!BaseCreature.Summon(familiar, Mobile, Mobile.Location, -1, TimeSpan.FromDays(1.0)))
        {
            return;
        }

        familiar.FixedParticles(0x3728, 1, 10, 9910, EffectLayer.Head);
        familiar.PlaySound(familiar.GetIdleSound());
    }
}
