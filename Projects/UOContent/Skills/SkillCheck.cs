using System;
using System.Runtime.CompilerServices;
using Server.Engines.Avatar;
using Server.Factions;
using Server.Mobiles;

namespace Server.Misc;

public static class SkillCheck
{
    private static int _statMax;
    private static double _statGainChanceMultiplier;
    private static bool _usePub45StatGain;
    private static double _primaryStatGainChance;
    private static TimeSpan _statGainDelay;
    private static TimeSpan _petStatGainDelay;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool RollStatIncreaseChance(double statGain) =>
        statGain / 33.3 * _statGainChanceMultiplier > Utility.RandomDouble();

    public static void Configure()
    {
        _statMax = ServerConfiguration.GetOrUpdateSetting("stats.statMax", Core.LBR ? 125 : 100);
        _statGainChanceMultiplier = ServerConfiguration.GetOrUpdateSetting("stats.gainChanceMultiplier", 1.0);
        _primaryStatGainChance = ServerConfiguration.GetSetting("stats.primaryStatGainChance", 0.75);
        _petStatGainDelay = ServerConfiguration.GetSetting("stats.petGainDelay", TimeSpan.FromMinutes(5.0));

        // Publish 45 - Preparation for UOKR
        _usePub45StatGain = ServerConfiguration.GetSetting("stats.usePub45StatGain", false);

        // Pub45 rolls are gated behind skill gains and need a short delay; the per-use legacy roll is
        // throttled by the delay instead.
        _statGainDelay = ServerConfiguration.GetSetting(
            "stats.gainDelay",
            TimeSpan.FromMinutes(_usePub45StatGain ? 0.05 : 7.5)
        );
    }

    public static void Initialize()
    {
        Mobile.SkillCheckLocationHandler = Mobile_SkillCheckLocation;
        Mobile.SkillCheckDirectLocationHandler = Mobile_SkillCheckDirectLocation;

        Mobile.SkillCheckTargetHandler = Mobile_SkillCheckTarget;
        Mobile.SkillCheckDirectTargetHandler = Mobile_SkillCheckDirectTarget;
    }

    public static bool Mobile_SkillCheckLocation(Mobile from, SkillName skillName, double minSkill, double maxSkill)
    {
        var skill = from.Skills[skillName];

        if (skill == null)
        {
            return false;
        }

        TryGainStat(from, skill);

        var success = CheckLocation(from, skill, minSkill, maxSkill);
        SkillEvents.InvokeSkillUsed(from, skill, success);
        return success;
    }

    private static bool CheckLocation(Mobile from, Skill skill, double minSkill, double maxSkill)
    {
        var value = skill.Value;

        if (value < minSkill)
        {
            return false; // Too difficult
        }

        if (value >= maxSkill || minSkill >= maxSkill)
        {
            return true; // No challenge
        }

        var chance = (value - minSkill) / (maxSkill - minSkill);

        var size = AntiMacroSystem.Settings.LocationSize;
        var loc = new Point2D(from.Location.X / size, from.Location.Y / size);
        return CheckSkill(from, skill, loc, chance);
    }

    public static bool Mobile_SkillCheckDirectLocation(Mobile from, SkillName skillName, double chance)
    {
        var skill = from.Skills[skillName];

        if (skill == null)
        {
            return false;
        }

        TryGainStat(from, skill);

        var success = CheckDirectLocation(from, skill, chance);
        SkillEvents.InvokeSkillUsed(from, skill, success);
        return success;
    }

    private static bool CheckDirectLocation(Mobile from, Skill skill, double chance)
    {
        if (chance < 0.0)
        {
            return false; // Too difficult
        }

        if (chance >= 1.0)
        {
            return true; // No challenge
        }

        var size = AntiMacroSystem.Settings.LocationSize;
        var loc = new Point2D(from.Location.X / size, from.Location.Y / size);
        return CheckSkill(from, skill, loc, chance);
    }

    public static bool CheckSkill(Mobile from, Skill skill, object amObj, double chance)
    {
        if (from.Skills.Cap == 0)
        {
            return false;
        }

        var success = chance >= Utility.RandomDouble();

        var region = from.Region;
        if (from.Alive && region.AllowGain(from, skill, amObj))
        {
            if (skill.Base < 10.0) // Gain regardless of the AllowGain check
            {
                Gain(from, skill);
            }
            else if (AllowGain(from, skill, amObj))
            {
                // NPC guild members gain faster in their guild's skills via a smaller divisor.
                // It applies twice, so the cap terms benefit more than the difficulty term.
                var gainDivisor = from is PlayerMobile pm && NpcGuildBenefits.IsGuildSkill(pm.NpcGuild, skill.SkillName)
                    ? NpcGuildBenefits.RollGuildGainDivisor()
                    : 2.0;

                var gc = (double)(from.Skills.Cap - from.Skills.Total) / from.Skills.Cap;
                gc += (skill.Cap - skill.Base) / skill.Cap;
                gc /= gainDivisor;

                gc += (1.0 - chance) * (success ? 0.5 : Core.AOS ? 0.0 : 0.2);
                gc /= gainDivisor;

                gc *= skill.Info.GainFactor;
                gc *= AvatarEngine.GetSkillGainMultiplier(from);

                if (gc < 0.01)
                {
                    gc = 0.01;
                }

                if (from is BaseCreature { Controlled: true })
                {
                    gc *= 2;
                }

                if (from is PlayerMobile)
                {
                    gc *= GetSkillLevelGainMultiplier(skill.Base);
                }

                if (gc >= Utility.RandomDouble())
                {
                    Gain(from, skill);
                }
            }
        }

        return success;
    }

    // Memento's accelerated curve: fast below 50, tapering toward GM and beyond.
    // Applied after the 0.01 floor, so it can push the effective minimum below 1%.
    public static double GetSkillLevelGainMultiplier(double skillBase) =>
        skillBase switch
        {
            <= 50.0  => 1.5,
            <= 70.0  => 1.0,
            <= 85.0  => 0.75,
            <= 95.0  => 0.5,
            <= 105.0 => 0.46,
            <= 110.0 => 0.42,
            _        => 0.38
        };

    public static bool Mobile_SkillCheckTarget(
        Mobile from, SkillName skillName, object target, double minSkill,
        double maxSkill
    )
    {
        var skill = from.Skills[skillName];

        if (skill == null)
        {
            return false;
        }

        TryGainStat(from, skill);

        var success = CheckTarget(from, skill, target, minSkill, maxSkill);
        SkillEvents.InvokeSkillUsed(from, skill, success);
        return success;
    }

    private static bool CheckTarget(Mobile from, Skill skill, object target, double minSkill, double maxSkill)
    {
        var value = skill.Value;

        if (value < minSkill)
        {
            return false; // Too difficult
        }

        if (value >= maxSkill || minSkill >= maxSkill)
        {
            return true; // No challenge
        }

        var chance = (value - minSkill) / (maxSkill - minSkill);

        return CheckSkill(from, skill, target, chance);
    }

    public static bool Mobile_SkillCheckDirectTarget(Mobile from, SkillName skillName, object target, double chance)
    {
        var skill = from.Skills[skillName];

        if (skill == null)
        {
            return false;
        }

        TryGainStat(from, skill);

        var success = CheckDirectTarget(from, skill, target, chance);
        SkillEvents.InvokeSkillUsed(from, skill, success);
        return success;
    }

    private static bool CheckDirectTarget(Mobile from, Skill skill, object target, double chance)
    {
        if (chance < 0.0)
        {
            return false; // Too difficult
        }

        if (chance >= 1.0)
        {
            return true; // No challenge
        }

        return CheckSkill(from, skill, target, chance);
    }

    private static bool AllowGain(Mobile from, Skill skill, object obj)
    {
        if (Core.AOS && Faction.InSkillLoss(from)) // Changed some time between the introduction of AoS and SE.
        {
            return false;
        }

        return from is not PlayerMobile mobile || AntiMacroSystem.AntiMacroCheck(mobile, skill, obj);
    }

    public static void Gain(Mobile from, Skill skill)
    {
        if (from is BaseCreature { IsDeadPet: true })
        {
            return;
        }

        if (skill.SkillName == SkillName.Focus && from is BaseCreature)
        {
            return;
        }

        if (!AvatarEngine.CanGainSkill(from, skill))
        {
            return;
        }

        if (skill.Base < skill.Cap && skill.Lock == SkillLock.Up)
        {
            var toGain = 1;

            if (skill.Base <= 10.0)
            {
                toGain = Utility.Random(4) + 1;
            }

            var skills = from.Skills;

            if (from.Player && skills.Total / (double)skills.Cap >= Utility.RandomDouble())
            {
                for (var i = 0; i < skills.Length; ++i)
                {
                    var toLower = skills[i];

                    if (toLower != skill && toLower.Lock == SkillLock.Down && toLower.BaseFixedPoint >= toGain)
                    {
                        toLower.BaseFixedPoint = Math.Max(toLower.BaseFixedPoint - toGain, 0);
                        break;
                    }
                }
            }

            if (from is PlayerMobile pm && skill.SkillName == pm.AcceleratedSkill && pm.AcceleratedStart > Core.Now)
            {
                toGain *= Utility.RandomMinMax(2, 5);
            }

            if (!from.Player || skills.Total < skills.Cap)
            {
                skill.BaseFixedPoint = Math.Min(skill.BaseFixedPoint + toGain, skill.CapFixedPoint);
                AvatarEngine.OnSkillGain(from, skill);
            }
        }

        if (_usePub45StatGain && skill.Lock == SkillLock.Up)
        {
            var info = skill.Info;

            var primaryStat = info.PrimaryStat;
            var secondaryStat = info.SecondaryStat;

            var primaryStatLock = primaryStat.ToLock(from);
            var secondaryStatLock = secondaryStat.ToLock(from);

            if (primaryStatLock != StatLockType.Up && secondaryStatLock != StatLockType.Up)
            {
                return;
            }

            // Flat 1 in 20 chance to gain anything
            if (0.05 * _statGainChanceMultiplier > Utility.RandomDouble())
            {
                // 75% for primary, 25% for secondary - Unless primary is not set to gain.
                var statToGain = primaryStatLock is StatLockType.Up && _primaryStatGainChance > Utility.RandomDouble()
                    ? primaryStat
                    : secondaryStat;

                GainStat(from, statToGain);
            }
        }
    }

    // Rolled on every skill use, before difficulty is checked, so a stat can gain even when the
    // skill cannot. At most one stat gains per use; a stat on cooldown passes the roll to the next.
    private static void TryGainStat(Mobile from, Skill skill)
    {
        if (_usePub45StatGain || !from.Alive || AvatarEngine.DisableSkillGains || skill.Lock != SkillLock.Up)
        {
            return;
        }

        if (from is BaseCreature creature && (creature.IsDeadPet || skill.SkillName == SkillName.Focus))
        {
            return;
        }

        if (!from.Region.AllowGain(from, skill, null))
        {
            return;
        }

        var info = skill.Info;

        if (info.StrGain > 0 && from.StrLock == StatLockType.Up && RollStatIncreaseChance(info.StrGain) &&
            GainStat(from, Stat.Str))
        {
            return;
        }

        if (info.DexGain > 0 && from.DexLock == StatLockType.Up && RollStatIncreaseChance(info.DexGain) &&
            GainStat(from, Stat.Dex))
        {
            return;
        }

        if (info.IntGain > 0 && from.IntLock == StatLockType.Up && RollStatIncreaseChance(info.IntGain))
        {
            GainStat(from, Stat.Int);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StatLockType ToLock(this Stat stat, Mobile from) =>
        stat switch
        {
            Stat.Str => from.StrLock,
            Stat.Dex => from.DexLock,
            Stat.Int => from.IntLock,
            _ => StatLockType.Up
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanLower(Mobile from, Stat stat)
    {
        return stat switch
        {
            Stat.Str => from.StrLock == StatLockType.Down && from.RawStr > 10,
            Stat.Dex => from.DexLock == StatLockType.Down && from.RawDex > 10,
            Stat.Int => from.IntLock == StatLockType.Down && from.RawInt > 10,
            _        => false
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanRaise(Mobile from, Stat stat)
    {
        if (!(from is BaseCreature creature && creature.Controlled))
        {
            if (from.RawStatTotal >= from.StatCap)
            {
                return false;
            }
        }

        return stat switch
        {
            Stat.Str => from.StrLock == StatLockType.Up && from.RawStr < _statMax,
            Stat.Dex => from.DexLock == StatLockType.Up && from.RawDex < _statMax,
            Stat.Int => from.IntLock == StatLockType.Up && from.RawInt < _statMax,
            _        => false
        };
    }

    public static void IncreaseStat(Mobile from, Stat stat, bool atrophy)
    {
        atrophy = atrophy || from.RawStatTotal >= from.StatCap;

        switch (stat)
        {
            case Stat.Str:
                {
                    if (atrophy)
                    {
                        if (CanLower(from, Stat.Dex) && (from.RawDex < from.RawInt || !CanLower(from, Stat.Int)))
                        {
                            from.RawDex -= 1;
                        }
                        else if (CanLower(from, Stat.Int))
                        {
                            from.RawInt -= 1;
                        }
                    }

                    if (CanRaise(from, Stat.Str))
                    {
                        from.RawStr = Math.Min(from.RawStr + 1, _statMax);
                    }

                    break;
                }
            case Stat.Dex:
                {
                    if (atrophy)
                    {
                        if (CanLower(from, Stat.Str) && (from.RawStr < from.RawInt || !CanLower(from, Stat.Int)))
                        {
                            from.RawStr -= 1;
                        }
                        else if (CanLower(from, Stat.Int))
                        {
                            from.RawInt -= 1;
                        }
                    }

                    if (CanRaise(from, Stat.Dex))
                    {
                        from.RawDex = Math.Min(from.RawDex + 1, _statMax);
                    }

                    break;
                }
            case Stat.Int:
                {
                    if (atrophy)
                    {
                        if (CanLower(from, Stat.Str) && (from.RawStr < from.RawDex || !CanLower(from, Stat.Dex)))
                        {
                            from.RawStr -= 1;
                        }
                        else if (CanLower(from, Stat.Dex))
                        {
                            from.RawDex -= 1;
                        }
                    }

                    if (CanRaise(from, Stat.Int))
                    {
                        from.RawInt = Math.Min(from.RawInt + 1, _statMax);
                    }

                    break;
                }
        }
    }

    public static bool GainStat(Mobile from, Stat stat)
    {
        var lastGain = stat switch
        {
            Stat.Str => from.LastStrGain,
            Stat.Dex => from.LastDexGain,
            Stat.Int => from.LastIntGain,
            _ => DateTime.MinValue
        };

        if (from is BaseCreature { Controlled: true })
        {
            if (lastGain + _petStatGainDelay >= Core.Now)
            {
                return false;
            }
        }
        // Avatar characters have no stat gain cooldown.
        else if (!AvatarEngine.GetContextOrDefault(from).Active && lastGain + _statGainDelay >= Core.Now)
        {
            return false;
        }

        switch (stat)
        {
            case Stat.Str:
                {
                    from.LastStrGain = Core.Now;
                    break;
                }
            case Stat.Dex:
                {
                    from.LastDexGain = Core.Now;
                    break;
                }
            case Stat.Int:
                {
                    from.LastIntGain = Core.Now;
                    break;
                }
        }

        var atrophy = from.RawStatTotal / (double)from.StatCap >= Utility.RandomDouble();

        IncreaseStat(from, stat, atrophy);
        return true;
    }
}
