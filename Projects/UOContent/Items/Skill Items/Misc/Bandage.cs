using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.BuffIcons;
using Server.Engines.ConPVP;
using Server.Factions;
using Server.Gumps;
using Server.Misc;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Bandage : Item, IDyable
{
    public const int Range = 3;

    [Constructible]
    public Bandage(int amount = 1) : base(0xE21)
    {
        Stackable = true;
        Amount = amount;
    }

    public override double DefaultWeight => 0.1;

    public virtual bool Dye(Mobile from, DyeTub sender)
    {
        if (Deleted)
        {
            return false;
        }

        Hue = sender.DyedHue;

        return true;
    }

    private static bool CheckBlessed(Mobile from)
    {
        if (!from.Blessed)
        {
            return true;
        }

        from.SendMessage("You cannot use bandages while in this state.");
        return false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!CheckBlessed(from))
        {
            return;
        }

        if (from.InRange(GetWorldLocation(), Range))
        {
            from.RevealingAction();

            from.SendLocalizedMessage(500948); // Who will you use the bandages on?

            from.Target = new InternalTarget(this);
        }
        else
        {
            from.SendLocalizedMessage(500295); // You are too far away to do that.
        }
    }

    public static void BandageTargetRequest(Mobile from, Item item, Mobile target)
    {
        if (item is not Bandage b || b.Deleted || !CheckBlessed(from))
        {
            return;
        }

        if (!from.InRange(b.GetWorldLocation(), Range))
        {
            from.SendLocalizedMessage(500295); // You are too far away to do that.
            return;
        }

        if (from.Target != null)
        {
            Target.Cancel(from);
            from.Target = null;
        }

        from.RevealingAction();
        from.SendLocalizedMessage(500948); // Who will you use the bandages on?

        new InternalTarget(b).Invoke(from, target);
    }

    private class InternalTarget : Target
    {
        private readonly Bandage _bandage;

        public InternalTarget(Bandage bandage) : base(Bandage.Range, false, TargetFlags.Beneficial) =>
            _bandage = bandage;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_bandage.Deleted)
            {
                return;
            }

            if (targeted is Mobile mobile)
            {
                if (from.InRange(_bandage.GetWorldLocation(), Bandage.Range))
                {
                    if (!(BandageContext.BeginHeal(from, mobile) == null || DuelContext.IsFreeConsume(from)))
                    {
                        _bandage.Consume();
                    }
                }
                else
                {
                    from.SendLocalizedMessage(500295); // You are too far away to do that.
                }

                return;
            }

            if (targeted is PlagueBeastInnard innard)
            {
                if (innard.OnBandage(from))
                {
                    _bandage.Consume();
                }

                return;
            }

            from.SendLocalizedMessage(500970); // Bandages can not be used on that.
        }

        protected override void OnNonlocalTarget(Mobile from, object targeted)
        {
            if (targeted is PlagueBeastInnard innard)
            {
                if (innard.OnBandage(from))
                {
                    _bandage.Consume();
                }
            }
            else
            {
                base.OnNonlocalTarget(from, targeted);
            }
        }
    }
}

/// <summary>
/// A bandage in progress. Every whole second before completion it rolls a partial heal that is banked
/// and applied together with the final heal when the bandage completes at exactly <see cref="Duration"/>.
/// Cures, resurrection, and slip penalties only apply on completion.
/// </summary>
public class BandageContext : Timer
{
    private const int SecondsPerPartialGain = 3;

    private static readonly TimeSpan PartialHealInterval = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromMilliseconds(250);

    private static readonly Dictionary<Mobile, BandageContext> _table = new();

    private readonly int _partialHealCount;
    private readonly int _tickHealAmount;
    private readonly int _finalHealAmount;
    private int _partialHealsDone;
    private int _bankedHealAmount;

    public BandageContext(Mobile healer, Mobile patient, TimeSpan duration) : base(
        duration > PartialHealInterval ? PartialHealInterval : duration
    )
    {
        Healer = healer;
        Patient = patient;
        Duration = duration;

        // Partial heals land on each whole second strictly before completion.
        _partialHealCount = Math.Max(0, (int)Math.Ceiling(duration / PartialHealInterval) - 1);
        _finalHealAmount = CalculateHealAmount(healer, patient);

        if (_partialHealCount > 0)
        {
            _tickHealAmount = Math.Min(
                (int)(0.33 * _finalHealAmount) / _partialHealCount,
                (int)(0.1 * _finalHealAmount)
            );
            _finalHealAmount -= _tickHealAmount * _partialHealCount;
        }
    }

    public Mobile Healer { get; }

    public Mobile Patient { get; }

    /// <summary>Total time from application until the bandage completes.</summary>
    public TimeSpan Duration { get; }

    public int Slips { get; set; }

    public void Slip()
    {
        SendHealerMessage(500961); // Your fingers slip!
        ++Slips;
    }

    public void StopHeal()
    {
        _table.Remove(Healer);
        Stop();
        (Healer as PlayerMobile)?.RemoveBuff(BuffIcon.Healing);
    }

    public static BandageContext GetContext(Mobile healer)
    {
        _table.TryGetValue(healer, out var bc);
        return bc;
    }

    public static SkillName GetPrimarySkill(Mobile m)
    {
        if (!m.Player && (m.Body.IsMonster || m.Body.IsAnimal))
        {
            return SkillName.Veterinary;
        }

        return SkillName.Healing;
    }

    public static SkillName GetSecondarySkill(Mobile m)
    {
        if (!m.Player && (m.Body.IsMonster || m.Body.IsAnimal))
        {
            return SkillName.AnimalLore;
        }

        return SkillName.Anatomy;
    }

    protected override void OnTick()
    {
        if (_partialHealsDone >= _partialHealCount)
        {
            EndHeal(false, _finalHealAmount, true);
            return;
        }

        ++_partialHealsDone;
        EndHeal(true, _tickHealAmount, _partialHealsDone % SecondsPerPartialGain == 0);

        // The partial heal may have ended the bandage (healer died).
        if (GetContext(Healer) != this)
        {
            return;
        }

        // Single-shot timer restarted from OnTick; the next event is either another partial or completion.
        Delay = _partialHealsDone < _partialHealCount
            ? PartialHealInterval
            : Duration - PartialHealInterval * _partialHealsDone;
        Start();
    }

    private void SendHealerMessage(int number)
    {
        Healer.SendLocalizedMessage(number);
        Healer.LocalOverheadMessage(MessageType.Regular, 1150, number);
    }

    private void SendPatientMessage(int number)
    {
        Patient.SendLocalizedMessage(number);
        Patient.LocalOverheadMessage(MessageType.Regular, 1150, number);
    }

    private void EndHeal(bool partial, int toHeal, bool canGainSkill)
    {
        if (!Healer.Alive || (Healer as BaseCreature)?.IsDeadPet == true)
        {
            StopHeal();
            SendHealerMessage(500962); // You were unable to finish your work before you died.
            return;
        }

        if (!partial)
        {
            StopHeal();
        }

        // On a partial tick this only warns; the bandage keeps running in case the healer closes the gap.
        if (!Healer.InRange(Patient, Bandage.Range))
        {
            SendHealerMessage(500963); // You did not stay close enough to heal your target.
            return;
        }

        var healerNumber = -1;
        var patientNumber = -1;
        var playSound = true;
        bool checkSkills;

        var primarySkill = GetPrimarySkill(Patient);
        var secondarySkill = GetSecondarySkill(Patient);
        var healing = Healer.Skills[primarySkill].Value;
        var anatomy = Healer.Skills[secondarySkill].Value;
        var slips = partial ? 0 : Slips;

        var petPatient = Patient as BaseCreature;

        if (!partial && (!Patient.Alive || petPatient?.IsDeadPet == true))
        {
            if (Patient.Map?.CanFit(Patient.Location, 16, false, false) != true)
            {
                SendHealerMessage(501042);  // Target can not be resurrected at that location.
                SendPatientMessage(502391); // Thou can not be resurrected there!
                return;
            }

            if (Patient.Region?.IsPartOf("Khaldun") == true)
            {
                // The veil of death in this area is too strong and resists thy efforts to restore life.
                SendHealerMessage(1010395);
                return;
            }

            var chance = (healing - 68.0) / 50.0 - slips * 0.02;
            checkSkills = healing >= 80.0 && anatomy >= 80.0;

            // TODO: Dbl check doesn't check for faction of the horse here?
            if (!(checkSkills && chance > Utility.RandomDouble())
                && (!Core.SE || petPatient is not FactionWarHorse || petPatient.ControlMaster != Healer))
            {
                if (petPatient?.IsDeadPet == true)
                {
                    SendHealerMessage(503256); // You fail to resurrect the creature.
                }
                else
                {
                    SendHealerMessage(500966); // You are unable to resurrect your patient.
                }

                if (checkSkills)
                {
                    Healer.CheckSkill(secondarySkill, 0.0, 120.0);
                    Healer.CheckSkill(primarySkill, 0.0, 120.0);
                }

                return;
            }

            healerNumber = 500965; // You are able to resurrect your patient.

            Patient.PlaySound(0x214);
            Patient.FixedEffect(0x376A, 10, 16);

            if (petPatient?.IsDeadPet == true)
            {
                var master = petPatient.ControlMaster;

                if (master != null && Healer == master)
                {
                    petPatient.ResurrectPet();

                    for (var i = 0; i < petPatient.Skills.Length; ++i)
                    {
                        petPatient.Skills[i].Base -= 0.1;
                    }
                }
                else if (master?.InRange(petPatient, 3) == true)
                {
                    healerNumber = 503255; // You are able to resurrect the creature.

                    master.SendGump(new PetResurrectGump(Healer, petPatient));
                }
                else
                {
                    var found = false;

                    var friends = petPatient.Friends;

                    for (var i = 0; i < friends?.Count; ++i)
                    {
                        var friend = friends[i];

                        if (friend.InRange(petPatient, 3))
                        {
                            healerNumber = 503255; // You are able to resurrect the creature.

                            friend.SendGump(new PetResurrectGump(Healer, petPatient));

                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        healerNumber = 1049670; // The pet's owner must be nearby to attempt resurrection.
                    }
                }
            }
            else
            {
                Patient.SendGump(new ResurrectGump(Healer));
            }
        }
        else if (!partial && Patient.Poisoned)
        {
            SendHealerMessage(500969); // You finish applying the bandages.

            var chance = (healing - 30.0) / 50.0 - Patient.Poison.Level * 0.1 - slips * 0.02;
            checkSkills = healing >= 60.0 && anatomy >= 60.0;

            if (checkSkills && chance > Utility.RandomDouble())
            {
                if (Patient.CurePoison(Healer))
                {
                    healerNumber = Healer == Patient ? -1 : 1010058; // You have cured the target of all poisons.
                    patientNumber = 1010059;                         // You have been cured of all poisons.
                }
            }
            else
            {
                healerNumber = 1010060; // You have failed to cure your target!
            }
        }
        else if (!partial && BleedAttack.IsBleeding(Patient))
        {
            checkSkills = true;
            healerNumber = 1060088;  // You bind the wound and stop the bleeding
            patientNumber = 1060167; // The bleeding wounds have healed, you are no longer bleeding!

            BleedAttack.EndBleed(Patient, false);
        }
        else if (!partial && MortalStrike.IsWounded(Patient))
        {
            // You cannot heal yourself in your current state. / You cannot heal that target in their current state.
            SendHealerMessage(Healer == Patient ? 1005000 : 1010398);
            return;
        }
        else if (!partial && Patient.Hits == Patient.HitsMax)
        {
            checkSkills = false;
            healerNumber = 500967; // You heal what little damage your patient had.
        }
        else
        {
            checkSkills = true;

            var chance = (healing + 10.0) / 100.0 - slips * 0.02;

            if (chance > Utility.RandomDouble())
            {
                healerNumber = 500969; // You finish applying the bandages.

                if (!partial)
                {
                    toHeal = Math.Max(1, toHeal - (int)(toHeal * slips * 0.35));
                }

                _bankedHealAmount += toHeal;
            }
            else
            {
                healerNumber = 500968; // You apply the bandages, but they barely help.
                playSound = false;
            }

            if (partial)
            {
                healerNumber = -1;
                playSound = false;
            }
            else
            {
                if (_bankedHealAmount < 1 || Patient.Hits == Patient.HitsMax)
                {
                    _bankedHealAmount = 1;
                    healerNumber = 500968; // You apply the bandages, but they barely help.
                }

                Patient.Heal(_bankedHealAmount, Healer);
                _bankedHealAmount = 0;
            }
        }

        if (healerNumber != -1)
        {
            SendHealerMessage(healerNumber);
        }

        if (patientNumber != -1)
        {
            SendPatientMessage(patientNumber);
        }

        if (playSound)
        {
            Patient.PlaySound(0x57);
        }

        if (checkSkills && canGainSkill)
        {
            Healer.CheckSkill(secondarySkill, 0.0, 120.0);
            Healer.CheckSkill(primarySkill, 0.0, 120.0);
        }

        if (!partial && Healer != Patient && Patient.Karma > 0)
        {
            Titles.AwardKarma(Healer, Patient.Karma / 5, true);
        }
    }

    private static int CalculateHealAmount(Mobile healer, Mobile patient)
    {
        var healing = healer.Skills[GetPrimarySkill(patient)].Value;
        var anatomy = healer.Skills[GetSecondarySkill(patient)].Value;

        var min = anatomy / 8.0 + healing / 5.0 + 4.0;
        var max = anatomy / 6.0 + healing / 2.5 + 4.0;

        var toHeal = min + Utility.RandomDouble() * (max - min);

        if (patient.Body.IsMonster || patient.Body.IsAnimal)
        {
            toHeal += patient.HitsMax / 100;
        }

        return (int)toHeal;
    }

    public static TimeSpan GetHealDuration(Mobile healer, Mobile patient)
    {
        double seconds;

        if (healer == patient)
        {
            // Breakpoints: 0.25s faster for every 5 dex.
            seconds = 11.0 - 0.25 * (healer.Dex / 5);
        }
        else
        {
            // Dead pets still report Alive, so they need their own check.
            var isResurrection = !patient.Alive || (patient as BaseCreature)?.IsDeadPet == true;
            seconds = isResurrection ? 5.0 : 2.0;
        }

        var duration = TimeSpan.FromSeconds(seconds);
        return duration < MinimumDuration ? MinimumDuration : duration;
    }

    public static BandageContext BeginHeal(Mobile healer, Mobile patient)
    {
        var creature = patient as BaseCreature;

        if (patient is PlayerMobile && patient.Alive && patient.Hunger < 6)
        {
            healer.LocalOverheadMessage(MessageType.Regular, 32, false, "You cannot heal those that are extremely hungry.");
        }
        else if (patient is Golem)
        {
            healer.LocalOverheadMessage(MessageType.Regular, 32, 500970); // Bandages cannot be used on that.
        }
        else if (creature?.IsAnimatedDead == true)
        {
            healer.LocalOverheadMessage(MessageType.Regular, 32, 500951); // You cannot heal that.
        }
        else if (!patient.Poisoned && patient.Hits == patient.HitsMax && !BleedAttack.IsBleeding(patient) &&
                 creature?.IsDeadPet != true)
        {
            healer.LocalOverheadMessage(MessageType.Regular, 1150, 500955); // That being is not damaged!
        }
        else if (!patient.Alive && patient.Map?.CanFit(patient.Location, 16, false, false) != true)
        {
            healer.LocalOverheadMessage(MessageType.Regular, 32, 501042); // Target cannot be resurrected at that location.
        }
        else if ((healer as PlayerMobile)?.Young == true && (patient as PlayerMobile)?.Young == false)
        {
            healer.SendLocalizedMessage(500952); // As a young player, you may not use beneficial skills on older players.
        }
        else if (healer.CanBeBeneficial(patient, true, true))
        {
            healer.DoBeneficial(patient);

            var duration = GetHealDuration(healer, patient);

            GetContext(healer)?.StopHeal();

            var context = new BandageContext(healer, patient, duration);
            _table[healer] = context;
            context.Start();

            if (healer != patient && patient is PlayerMobile)
            {
                patient.LocalOverheadMessage(MessageType.Regular, 1150, 1008078, healer.Name); // : Attempting to heal you.
            }

            healer.LocalOverheadMessage(MessageType.Regular, 1150, 500956); // You begin applying the bandages.

            (healer as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.Healing, 1063670, duration));

            return context;
        }

        return null;
    }
}

