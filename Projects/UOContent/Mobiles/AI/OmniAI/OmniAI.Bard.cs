using System;
using System.Collections.Generic;
using Server.Items;
using Server.SkillHandlers;

namespace Server.Mobiles;

/// <summary>
///     Bard abilities resolved directly by the AI. The stock skill handlers drive a player's target
///     cursor and refuse player targets for discord, so the AI applies the same effects itself.
/// </summary>
public partial class OmniAI
{
    private const string DiscordModName = "OmniAIDiscord";
    private static readonly TimeSpan DiscordPulse = TimeSpan.FromSeconds(1.25);

    // Targets under an OmniAI discord, so a second bard does not stack another set of mods.
    private static readonly Dictionary<Mobile, TimerExecutionToken> _aiDiscords = new();

    public void BardPower()
    {
        var target = Mobile.Combatant;

        if (target?.Deleted != false || !target.Alive || !CheckInstrument(out var instrument))
        {
            return;
        }

        switch (Utility.Random(3))
        {
            case 0:
                {
                    UseDiscord(target, instrument);
                    break;
                }
            case 1:
                {
                    UseProvocation(target, instrument);
                    break;
                }
            default:
                {
                    UsePeacemaking(target, instrument);
                    break;
                }
        }
    }

    public bool CheckInstrument(out BaseInstrument instrument)
    {
        instrument = BaseInstrument.GetInstrument(Mobile);

        if (instrument != null)
        {
            return true;
        }

        var pack = Mobile.Backpack;

        if (pack == null)
        {
            return false;
        }

        instrument = pack.FindItemByType<BaseInstrument>();

        if (instrument == null)
        {
            DebugSay("I need an instrument, fixing my problem");

            instrument = new Harp { SuccessSound = 0x58B, FailureSound = 0x58C };
            pack.DropItem(instrument);
        }

        BaseInstrument.SetInstrument(Mobile, instrument);
        return true;
    }

    private bool CheckBardSkill(SkillName skill, Mobile target, BaseInstrument instrument, double difficultyOffset)
    {
        if (!BaseInstrument.CheckMusicianship(Mobile))
        {
            instrument.PlayInstrumentBadly(Mobile);
            return false;
        }

        var diff = instrument.GetDifficultyFor(target) + difficultyOffset;
        var music = Mobile.Skills.Musicianship.Value;

        if (music > 100.0)
        {
            diff -= (music - 100.0) * 0.5;
        }

        if (!Mobile.CheckTargetSkill(skill, target, diff - 25.0, diff + 25.0))
        {
            instrument.PlayInstrumentBadly(Mobile);
            return false;
        }

        instrument.PlayInstrumentWell(Mobile);
        return true;
    }

    public void UseDiscord(Mobile target, BaseInstrument instrument)
    {
        if (target is BaseCreature { BardImmune: true } || !Mobile.CanBeHarmful(target, false) ||
            _aiDiscords.ContainsKey(target))
        {
            return;
        }

        var existing = 0;

        if (Discordance.GetEffect(target, ref existing))
        {
            return;
        }

        Mobile.RevealingAction();

        if (!CheckBardSkill(SkillName.Discordance, target, instrument, -10.0))
        {
            return;
        }

        DebugSay("Discording");

        Mobile.DoHarmful(target);

        // Same scaling as the Discordance skill: -1% resists and skills per 5 points, -20% at GM.
        var discord = Mobile.Skills.Discordance.Value;
        var effect = discord > 100.0 ? -20 + (int)((discord - 100.0) / -2.5) : (int)(discord / -5.0);

        if (Core.SE && BaseInstrument.GetBaseDifficulty(target) >= 160.0)
        {
            effect /= 2;
        }

        var scalar = effect * 0.01;

        target.AddResistanceMod(new ResistanceMod(ResistanceType.Physical, DiscordModName, effect));
        target.AddResistanceMod(new ResistanceMod(ResistanceType.Fire, DiscordModName, effect));
        target.AddResistanceMod(new ResistanceMod(ResistanceType.Cold, DiscordModName, effect));
        target.AddResistanceMod(new ResistanceMod(ResistanceType.Poison, DiscordModName, effect));
        target.AddResistanceMod(new ResistanceMod(ResistanceType.Energy, DiscordModName, effect));

        for (var i = 0; i < target.Skills.Length; ++i)
        {
            var skill = target.Skills[i];

            if (skill.Value > 0)
            {
                target.AddSkillMod(new DefaultSkillMod(skill.SkillName, DiscordModName, true, skill.Value * scalar));
            }
        }

        var pulses = Math.Max(1, (int)(discord * 2 / DiscordPulse.TotalSeconds));
        var bard = Mobile;
        var remaining = pulses;

        Timer.StartTimer(
            DiscordPulse,
            pulses,
            () =>
            {
                // The song ends early once the bard dies, hides or walks out of range.
                var ended = --remaining <= 0 || target.Deleted || bard.Deleted || !bard.Alive || bard.Hidden ||
                            bard.Map != target.Map ||
                            !bard.InRange(target, BaseInstrument.GetBardRange(bard, SkillName.Discordance));

                if (ended)
                {
                    EndDiscord(target);
                }
                else
                {
                    target.FixedEffect(0x376A, 1, 32);
                }
            },
            out var token
        );

        _aiDiscords[target] = token;
    }

    private static void EndDiscord(Mobile target)
    {
        if (_aiDiscords.Remove(target, out var token))
        {
            token.Cancel();
        }

        target.RemoveResistanceMod(DiscordModName);
        target.RemoveSkillMod(DiscordModName);
    }

    public bool UseProvocation(Mobile target, BaseInstrument instrument)
    {
        Mobile.RevealingAction();

        // Turn a pet on its own master.
        if (target is BaseCreature { Unprovokable: false } pet && pet.GetMaster() is { } master &&
            pet.CanBeHarmful(master))
        {
            if (!CheckBardSkill(SkillName.Provocation, pet, instrument, -5.0))
            {
                return false;
            }

            DebugSay("Provoking a pet onto its master");

            pet.Provoke(Mobile, master, true);
            return true;
        }

        var range = BaseInstrument.GetBardRange(Mobile, SkillName.Provocation);

        foreach (var m in Mobile.GetMobilesInRange(Math.Min(5, range)))
        {
            if (m == Mobile || m == target || m is not BaseCreature bc || bc.Unprovokable ||
                bc.Controlled != Mobile.Controlled || bc.Summoned != Mobile.Summoned || !bc.CanBeHarmful(target))
            {
                continue;
            }

            if (!CheckBardSkill(SkillName.Provocation, bc, instrument, -5.0))
            {
                return false;
            }

            this.DebugSayFormatted($"Provoking {bc.Name} onto {target.Name}");

            bc.Provoke(Mobile, target, true);
            return true;
        }

        return false;
    }

    public void UsePeacemaking(Mobile target, BaseInstrument instrument)
    {
        var bc = target as BaseCreature;

        if (bc?.Uncalmable == true || bc?.BardPacified == true || !Mobile.CanBeHarmful(target, false))
        {
            return;
        }

        if (target is PlayerMobile pm && pm.PeacedUntil > Core.Now)
        {
            return;
        }

        Mobile.RevealingAction();

        if (!CheckBardSkill(SkillName.Peacemaking, target, instrument, -10.0))
        {
            return;
        }

        DebugSay("Peacemaking");

        var seconds = Mobile.Skills.Peacemaking.Value / 5;

        target.Combatant = null;
        target.Warmode = false;

        if (bc != null)
        {
            bc.Pacify(Mobile, Core.Now + TimeSpan.FromSeconds(Math.Clamp(seconds, 10, 120)));
        }
        else
        {
            if (target is PlayerMobile player)
            {
                player.PeacedUntil = Core.Now + TimeSpan.FromSeconds(seconds);
            }

            target.SendLocalizedMessage(500616); // You hear lovely music, and forget to continue battling!
        }
    }
}
