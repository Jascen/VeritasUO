using System;
using Server.Engines.PartySystem;
using Server.Guilds;
using Server.Items;
using Server.Multis;
using Server.SkillHandlers;
using Server.Spells;
using Server.Spells.Eighth;
using Server.Spells.Fifth;
using Server.Spells.Sixth;
using Server.Spells.Third;
using Server.Targeting;

namespace Server.Mobiles;

/// <summary>
///     Multi-school creature AI, ported from Peoharen's OmniAI as shipped in Ultima Memento.
///     Every cast tick it rolls among the schools the creature is trained in (Bard, Bushido,
///     Chivalry, Magery, Necromancy, Ninjitsu), and layers kiting, field dodging, teleport
///     escapes and self-healing over the stock movement.
/// </summary>
public partial class OmniAI : BaseAI
{
    private const double SchoolSkillThreshold = 10.0;
    private const int CastIntervalMs = 4000;
    private const int FieldCheckIntervalMs = 3000;
    private const int FieldRetryIntervalMs = 500;
    private const int ArmedCheckIntervalMs = 12000;
    private const int TeleportCooldownMs = 4000;
    private const int MaxTeleportAttempts = 3;

    private static readonly int[] TeleportOffsets =
    [
        -1, -1, -1, 0, -1, 1,
        0, -1, 0, 1, 1, -1,
        1, 0, 1, 1, -2, -2,
        -2, -1, -2, 0, -2, 1,
        -2, 2, -1, -2, -1, 2,
        0, -2, 0, 2, 1, -2,
        1, 2, 2, -2, 2, -1,
        2, 0, 2, 1, 2, 2
    ];

    private int _teleportAttempts;
    private long _nextTeleportTime;
    private long _nextCastTime;
    private long _nextHealTime;
    private long _nextFieldCheck;
    private long _nextArmedCheck;

    public OmniAI(BaseCreature m) : base(m)
    {
        var now = Core.TickCount;
        _nextTeleportTime = now;
        _nextCastTime = now;
        _nextHealTime = now;
        _nextFieldCheck = now;
        _nextArmedCheck = now;
        _nextWeaponSwap = now;
        _nextShiftTime = now;
        _nextShurikenThrow = now;
        _nextExplosionTime = now;
    }

    public virtual bool CanUseBard => Mobile.Skills.Musicianship.Base > SchoolSkillThreshold;

    public virtual bool CanUseBushido => Mobile.Skills.Bushido.Base > SchoolSkillThreshold;

    public virtual bool CanUseChivalry => Mobile.Skills.Chivalry.Base > SchoolSkillThreshold;

    public virtual bool CanUseMagery => Mobile.Skills.Magery.Base > SchoolSkillThreshold;

    public virtual bool CanUseNecromancy => Mobile.Skills.Necromancy.Base > SchoolSkillThreshold;

    public virtual bool CanUseNinjitsu => Mobile.Skills.Ninjitsu.Base > SchoolSkillThreshold;

    public virtual bool SwapWeapons => CanUseBushido || CanUseNinjitsu;

    public virtual bool MoveErratically => false;

    public virtual bool RequiresRanged => Mobile.Weapon is BaseRanged;

    public virtual bool PrefersRanged
    {
        get
        {
            if (!CanUseMagery && !CanUseNecromancy)
            {
                return false;
            }

            var delta = Mobile.RawInt - Mobile.RawStr;

            return delta >= 100 || delta > 0 && Mobile.Weapon is null or Fists;
        }
    }

    public override double FleeHealthThreshold => 0.2;

    // 10% base, plus one point per hit point the combatant has over us.
    public override double FleeChance =>
        Mobile.Combatant is { } c && Mobile.Hits < c.Hits
            ? Math.Min(1.0, (10 + c.Hits - Mobile.Hits) / 100.0)
            : 0.1;

    protected override bool StartFlee() =>
        !Mobile.Controlled && !Mobile.Summoned && !Mobile.CanSwim && base.StartFlee();

    public override bool Think()
    {
        if (Mobile.Deleted)
        {
            return false;
        }

        var now = Core.TickCount;

        if (now - _nextFieldCheck >= 0)
        {
            // A step can be refused by the move clock, so retry quickly while still standing in a field.
            _nextFieldCheck = now + (CheckForFieldSpells() ? FieldRetryIntervalMs : FieldCheckIntervalMs);
        }

        if (now - _nextArmedCheck >= 0)
        {
            CheckArmed(SwapWeapons);
            _nextArmedCheck = now + ArmedCheckIntervalMs;
        }

        RefreshStealth();

        return ProcessTarget() || base.Think();
    }

    public override bool DoActionWander()
    {
        TryHide();

        if (AcquireFocusMob(Mobile.RangePerception, Mobile.FightMode, false, false, true))
        {
            this.DebugSayFormatted($"I have detected {Mobile.FocusMob.Name}, attacking");

            Mobile.Combatant = Mobile.FocusMob;
            Action = ActionType.Combat;
        }
        else if (Mobile.Mana < Mobile.ManaMax && Mobile.Skills.Meditation.Value > 60.0 && !Mobile.Meditating)
        {
            DebugSay("I am going to meditate");

            Mobile.UseSkill(SkillName.Meditation);
        }
        else
        {
            Mobile.Warmode = false;
            base.DoActionWander();
            TryToHeal();
        }

        return true;
    }

    public override bool DoActionCombat()
    {
        var c = Mobile.Combatant;
        Mobile.Warmode = true;

        if (c?.Deleted != false || c.Map != Mobile.Map || !c.Alive || c.IsDeadBondedPet)
        {
            DebugSay("My combatant is gone, so my guard is up");

            Action = ActionType.Guard;
            return true;
        }

        if (!Mobile.InRange(c, Mobile.RangePerception))
        {
            if (AcquireFocusMob(Mobile.RangePerception, Mobile.FightMode, false, false, true))
            {
                Mobile.Combatant = Mobile.FocusMob;
                Mobile.FocusMob = null;
            }
            else if (!Mobile.InRange(c, Mobile.ChaseLeashRange))
            {
                Mobile.Combatant = null;
            }

            c = Mobile.Combatant;

            if (c == null)
            {
                DebugSay("My combatant has fled, so I am on guard");

                Action = ActionType.Guard;
                return true;
            }
        }

        if (MoveTo(c, Mobile.RangeFight))
        {
            if (Core.TickCount - Mobile.LastMoveTime > 400)
            {
                Mobile.Direction = Mobile.GetDirectionTo(c);
            }
        }
        else if (OnFailedMove())
        {
            return true;
        }
        else if (Mobile.GetDistanceToSqrt(c) > Mobile.RangePerception + 1)
        {
            this.DebugSayFormatted($"I cannot find {c.Name}, so my guard is up");

            Action = ActionType.Guard;
            return true;
        }

        if (Mobile.TriggerAbility(MonsterAbilityTrigger.CombatAction, c))
        {
            DebugSay("I used my abilities!");
            return true;
        }

        if (TryToHeal())
        {
            return true;
        }

        if (Mobile.Spell == null && Core.TickCount - _nextCastTime >= 0 && Mobile.InRange(c, 12))
        {
            _nextCastTime = Core.TickCount + CastIntervalMs;
            UseRandomSchool();
        }

        return true;
    }

    private void UseRandomSchool()
    {
        Span<int> schools = stackalloc int[6];
        var count = 0;

        if (CanUseBard)
        {
            schools[count++] = 0;
        }

        if (CanUseBushido)
        {
            schools[count++] = 1;
        }

        if (CanUseChivalry)
        {
            schools[count++] = 2;
        }

        if (CanUseMagery)
        {
            schools[count++] = 3;
        }

        if (CanUseNecromancy)
        {
            schools[count++] = 4;
        }

        if (CanUseNinjitsu)
        {
            schools[count++] = 5;
        }

        if (count == 0)
        {
            return;
        }

        switch (schools[Utility.Random(count)])
        {
            case 0:
                {
                    BardPower();
                    break;
                }
            case 1:
                {
                    BushidoPower();
                    break;
                }
            case 2:
                {
                    ChivalryPower();
                    break;
                }
            case 3:
                {
                    MageryPower();
                    break;
                }
            case 4:
                {
                    NecromancerPower();
                    break;
                }
            case 5:
                {
                    NinjitsuPower();
                    break;
                }
        }
    }

    public override bool DoActionGuard()
    {
        TryHide();

        if (AcquireFocusMob(Mobile.RangePerception, Mobile.FightMode, false, false, true))
        {
            this.DebugSayFormatted($"I have detected {Mobile.FocusMob.Name}, attacking");

            Mobile.Combatant = Mobile.FocusMob;
            Action = ActionType.Combat;
        }
        else
        {
            base.DoActionGuard();
        }

        return true;
    }

    public override bool DoActionFlee()
    {
        if (Mobile.Hits > Mobile.HitsMax / 2)
        {
            DebugSay("I am stronger now, so I will continue fighting");

            Mobile.StopFlee();
            Action = ActionType.Combat;
            return true;
        }

        if (CanUseNinjitsu && Mobile.Combatant != null && _smokeBombs > 0 && Mobile.Mana >= 10 && !Mobile.Hidden)
        {
            --_smokeBombs;
            this.DebugSayFormatted($"I am using a smoke bomb, {_smokeBombs} left");

            Mobile.Mana -= 10;
            Mobile.Hidden = true;
            Stealth.OnUse(Mobile);
            Mobile.FixedParticles(0x3709, 1, 30, 9904, 1108, 6, EffectLayer.RightFoot);
            Mobile.PlaySound(0x22F);
        }

        Mobile.FocusMob = Mobile.Combatant;
        return base.DoActionFlee();
    }

    private bool OnFailedMove()
    {
        var canOnlySwim = Mobile.CanSwim && Mobile.CantWalk;

        if (CanUseMagery && !canOnlySwim && !Mobile.DisallowAllMoves && Mobile.Combatant != null &&
            Mobile.InLOS(Mobile.Combatant) && Core.TickCount - _nextTeleportTime >= 0 && Utility.RandomDouble() < 0.33)
        {
            DebugSay("I am stuck, I'm going to try teleporting away");

            Mobile.Target?.Cancel(Mobile, TargetCancelType.Canceled);
            new TeleportSpell(Mobile).Cast();

            return true;
        }

        if (AcquireFocusMob(Mobile.RangePerception, Mobile.FightMode, false, false, true))
        {
            this.DebugSayFormatted($"My move is blocked, so I am going to attack {Mobile.FocusMob.Name}");

            Mobile.Combatant = Mobile.FocusMob;
            Action = ActionType.Combat;

            return true;
        }

        DebugSay("I am stuck");
        return false;
    }

    public override bool MoveTo(Mobile m, int range)
    {
        if (m == null || Mobile.Hidden && CanUseNinjitsu || !(MoveErratically || RequiresRanged || PrefersRanged) ||
            !Mobile.InLOS(m))
        {
            return base.MoveTo(m, range);
        }

        var forceMove = MoveErratically;

        if ((forceMove || Mobile.InRange(m, 2)) && CheckMove())
        {
            // Only retreat a fraction of the time.
            if (Utility.RandomDouble() < 0.25)
            {
                // Run from the master so a player can corner this creature with their pet.
                var focus = (m as BaseCreature)?.GetMaster() ?? m;
                return DoMove((Mobile.GetDirectionTo(focus) - 4) & Direction.Mask);
            }

            if (!forceMove)
            {
                return true;
            }
        }

        return !forceMove && Mobile.InRange(m, 4) || base.MoveTo(m, range);
    }

    private void TryHide()
    {
        if (CanUseNinjitsu && !Mobile.Hidden && !Mobile.Poisoned)
        {
            Mobile.UseSkill(SkillName.Hiding);
        }
    }

    // Hidden creatures reveal when their stealth steps run out, so a hidden ninja re-stealths.
    private void RefreshStealth()
    {
        if (CanUseNinjitsu && Mobile.Hidden && !Mobile.Poisoned && Mobile.AllowedStealthSteps <= 0 &&
            Mobile.Skills.Stealth.Value > 0)
        {
            Stealth.OnUse(Mobile);
        }
    }

    private void MarkTeleport()
    {
        _teleportAttempts = 0;
        _nextTeleportTime = Core.TickCount + TeleportCooldownMs;
    }

    private bool ProcessTarget()
    {
        var targ = Mobile.Target;

        if (targ == null)
        {
            return false;
        }

        var spell = (targ as ISpellTarget<Mobile>)?.Spell;
        var toTarget = Mobile.Combatant;

        // Location-targeted summons land next to the combatant.
        if (spell is EnergyVortexSpell or BladeSpiritsSpell)
        {
            if (toTarget != null && Mobile.Map is { } summonMap && Mobile.InLOS(toTarget))
            {
                targ.Invoke(Mobile, new LandTarget(toTarget.Location, summonMap));
            }
            else
            {
                targ.Cancel(Mobile, TargetCancelType.Canceled);
            }

            return true;
        }

        if (spell is DispelSpell)
        {
            if (!Mobile.AutoDispel && FindDispelTarget() is { } dispelTarget)
            {
                targ.Invoke(Mobile, dispelTarget);
            }
            else
            {
                targ.Cancel(Mobile, TargetCancelType.Canceled);
            }

            return true;
        }

        if (spell is TeleportSpell)
        {
            ProcessTeleportTarget(targ, toTarget);
            return true;
        }

        if ((targ.Flags & TargetFlags.Harmful) != 0 && toTarget != null)
        {
            if ((targ.Range == -1 || Mobile.InRange(toTarget, targ.Range)) && Mobile.CanSee(toTarget) &&
                Mobile.InLOS(toTarget))
            {
                targ.Invoke(Mobile, toTarget);
                return true;
            }
        }
        else if ((targ.Flags & TargetFlags.Beneficial) != 0)
        {
            targ.Invoke(Mobile, Mobile);
            return true;
        }
        else
        {
            targ.Cancel(Mobile, TargetCancelType.Canceled);
        }

        return false;
    }

    private Mobile FindDispelTarget()
    {
        Mobile found = null;
        var seen = 0;

        // Reservoir pick: uniform over every dispellable enemy summon without building a list.
        foreach (var m in Mobile.GetMobilesInRange(12))
        {
            if (m is BaseCreature { IsDispellable: true } bc && bc.SummonMaster != Mobile &&
                CanTarget(Mobile, bc) && Utility.Random(++seen) == 0)
            {
                found = bc;
            }
        }

        return found;
    }

    private void ProcessTeleportTarget(Target targ, Mobile toTarget)
    {
        var map = Mobile.Map;

        if (++_teleportAttempts > MaxTeleportAttempts || map == null || toTarget?.Deleted != false)
        {
            targ.Cancel(Mobile, TargetCancelType.Canceled);
            MarkTeleport();
            return;
        }

        int px, py;

        if (Mobile.Hits < Mobile.HitsMax / 10)
        {
            // Nearly dead: land ~10 tiles from the combatant, on the far side from it.
            var rx = Mobile.X - toTarget.X;
            var ry = Mobile.Y - toTarget.Y;
            var d = Math.Max(1.0, Mobile.GetDistanceToSqrt(toTarget));

            px = toTarget.X + (int)(rx * (10 / d));
            py = toTarget.Y + (int)(ry * (10 / d));
        }
        else
        {
            px = toTarget.X;
            py = toTarget.Y;
        }

        var teleRange = targ.Range >= 0 ? targ.Range : 12;

        for (var i = 0; i < TeleportOffsets.Length; i += 2)
        {
            if (TryTeleport(new Point3D(px + TeleportOffsets[i], py + TeleportOffsets[i + 1], 0), map, targ, teleRange))
            {
                return;
            }
        }

        // The goal may be walled off: teleport to the farthest reachable step along a walking path.
        var path = new MovementPath(Mobile, new Point3D(px, py, map.GetAverageZ(px, py)));

        if (path.Success)
        {
            var x = Mobile.X;
            var y = Mobile.Y;
            var directions = path.Directions;
            var best = -1;
            int bestX = 0, bestY = 0;

            for (var i = 0; i < directions.Length; i++)
            {
                Movement.Movement.Offset(directions[i], ref x, ref y);

                if (Mobile.InRange(new Point2D(x, y), teleRange))
                {
                    best = i;
                    bestX = x;
                    bestY = y;
                }
            }

            if (best >= 0 && TryTeleport(new Point3D(bestX, bestY, 0), map, targ, teleRange))
            {
                return;
            }
        }

        for (var i = 0; i < 10; ++i)
        {
            var p = new Point3D(
                Mobile.X - teleRange + Utility.Random(teleRange * 2 + 1),
                Mobile.Y - teleRange + Utility.Random(teleRange * 2 + 1),
                0
            );

            if (TryTeleport(p, map, targ, teleRange))
            {
                return;
            }
        }
    }

    private bool TryTeleport(Point3D p, Map map, Target targ, int teleRange)
    {
        if (targ.Range != -1 && !Mobile.InRange(p, teleRange))
        {
            return false;
        }

        var lt = new LandTarget(p, map);
        var landing = new Point3D(lt.X, lt.Y, lt.Z);

        if (!Mobile.InLOS(lt))
        {
            return false;
        }

        if (Mobile.CanSwim && BaseBoat.FindBoatAt(landing, map) != null ||
            map.CanSpawnMobile(lt.X, lt.Y, lt.Z) && !SpellHelper.CheckMulti(landing, map))
        {
            targ.Invoke(Mobile, lt);
            MarkTeleport();
            return true;
        }

        return false;
    }

    public static bool CanTarget(Mobile from, Mobile to) => CanTarget(from, to, true, false, false);

    public static bool CanTarget(Mobile from, Mobile to, bool harm) => CanTarget(from, to, harm, false, false);

    public static bool CanTarget(Mobile from, Mobile to, bool harm, bool checkGuildParty, bool allowNull)
    {
        if (to == null)
        {
            return false;
        }

        if (from == null)
        {
            return allowNull;
        }

        if (from == to && !harm)
        {
            return true;
        }

        if (harm && to.Blessed || to.AccessLevel != AccessLevel.Player && to.Hidden)
        {
            return false;
        }

        if (checkGuildParty)
        {
            var fromGuild = GetGuild(from);
            var toGuild = GetGuild(to);

            if (fromGuild != null && toGuild != null && (fromGuild == toGuild || fromGuild.IsAlly(toGuild)))
            {
                return !harm;
            }

            if (GetParty(from)?.Contains(to) == true)
            {
                return !harm;
            }
        }

        return harm ? IsGoodGuy(from) != IsGoodGuy(to) : IsGoodGuy(from) == IsGoodGuy(to);
    }

    public static bool IsGoodGuy(Mobile m)
    {
        if (m.Criminal)
        {
            return false;
        }

        if (m.Player && m.Kills < 5)
        {
            return true;
        }

        if (m is BaseCreature { Controlled: true } or BaseCreature { Summoned: true })
        {
            var bc = (BaseCreature)m;
            var master = bc.ControlMaster ?? bc.SummonMaster;

            return master != null && IsGoodGuy(master);
        }

        return false;
    }

    public static Guild GetGuild(Mobile m)
    {
        if (m.Guild is Guild guild)
        {
            return guild;
        }

        if (m is BaseCreature bc)
        {
            return bc.ControlMaster?.Guild as Guild ?? bc.SummonMaster?.Guild as Guild;
        }

        return null;
    }

    public static Party GetParty(Mobile m)
    {
        var party = Party.Get(m);

        if (party == null && m is BaseCreature bc)
        {
            party = Party.Get(bc.ControlMaster) ?? Party.Get(bc.SummonMaster);
        }

        return party;
    }
}
