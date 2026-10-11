using System;
using Server.Items;

namespace Server.Mobiles
{
    /// <summary>
    /// Dominance (Herding): herding drives, pet cowering and passive skill gains from follower combat.
    /// All state here is transient; none of it survives a restart.
    /// </summary>
    public abstract partial class BaseCreature
    {
        private const int DriveDurationMs = 3000;
        private const int CowerDurationMs = 2000;
        private const int DominanceGainThrottleMs = 12000;

        private bool _driven;
        private long _driveEnd;

        private bool _cowering;
        private long _cowerEnd;

        private bool _dominanceGainThrottled;
        private long _nextDominanceGain;

        public bool IsDriven => _driven;

        public long DriveEnd => _driveEnd;

        public bool IsCowering => _cowering && Core.TickCount - _cowerEnd < 0;

        /// <summary>
        /// Shared difficulty for herding, cowering and Dominance gains: taming difficulty when tamable,
        /// otherwise derived from fame so untamable monsters scale too.
        /// </summary>
        public static double GetDominanceDifficulty(BaseCreature bc) =>
            bc.Tamable ? bc.MinTameSkill : Math.Clamp(bc.Fame / 150.0, 0.0, 120.0);

        /// <summary>The player that owns <paramref name="m"/>: the player itself, or a pet/summon's master.</summary>
        public static PlayerMobile GetOwningPlayer(Mobile m) =>
            m as PlayerMobile ?? (m as BaseCreature)?.GetMaster() as PlayerMobile;

        public void BeginDrive(IPoint2D destination)
        {
            AIObject?.Peel();
            SetCurrentSpeedToActive();
            TargetLocation = destination;
            _driven = true;
            _driveEnd = Core.TickCount + DriveDurationMs;
        }

        public void EndDrive()
        {
            if (!_driven)
            {
                return;
            }

            _driven = false;
            TargetLocation = null;
        }

        private void OnDominanceDamage(int amount, Mobile from, bool willKill)
        {
            // Any damage breaks a drive. The aggressive action that preceded this hit was refused while
            // driven, so replay it now that the creature fights normally again.
            if (_driven)
            {
                EndDrive();

                if (from != null && from != this)
                {
                    AIObject?.OnAggressiveAction(from);
                }
            }

            if (from is BaseCreature attacker && !Controlled && !Summoned && GetMaster() == null)
            {
                attacker.TryPassiveDominanceGain(this);
            }

            TryCower(amount, from, willKill);
        }

        private void TryPassiveDominanceGain(BaseCreature victim)
        {
            if (GetMaster() is not PlayerMobile master)
            {
                return;
            }

            // AntiMacroSystem is off by default and only limits repeats per target, so every pet hit
            // would otherwise roll a gain.
            if (_dominanceGainThrottled && Core.TickCount - _nextDominanceGain < 0)
            {
                return;
            }

            _dominanceGainThrottled = true;
            _nextDominanceGain = Core.TickCount + DominanceGainThrottleMs;

            var diff = GetDominanceDifficulty(victim);
            master.CheckTargetSkill(SkillName.Herding, victim, diff - 25.0, diff + 25.0);
        }

        private void TryCower(int amount, Mobile from, bool willKill)
        {
            if (willKill || IsCowering || !Controlled || Summoned || IsDeadBondedPet ||
                ControlMaster is not PlayerMobile owner)
            {
                return;
            }

            // Only pets sent to fight can lose their nerve; a pet resting on a stand-down order keeps it.
            if (ControlOrder is not (OrderType.Attack or OrderType.Guard))
            {
                return;
            }

            // PvE only: players and anything a player owns never frighten a pet.
            if (from is not BaseCreature attacker || GetOwningPlayer(attacker) != null)
            {
                return;
            }

            if (amount < HitsMax * 0.05)
            {
                return;
            }

            var hpMissing = HitsMax > 0 ? 1.0 - (double)Hits / HitsMax : 0.0;
            var loyaltyLevelsMissing = (MaxLoyalty - Loyalty) / 10;

            var combatant = Combatant as BaseCreature ?? attacker;
            var toughness = (GetDominanceDifficulty(combatant) - GetDominanceDifficulty(this)) / 50.0;

            var chance = 0.05 + 0.20 * hpMissing + 0.02 * loyaltyLevelsMissing + 0.10 * Math.Clamp(toughness, 0.0, 1.0);

            if (Utility.RandomDouble() >= chance)
            {
                return;
            }

            var focus = Combatant;

            if (focus != null && ShepherdsCrook.IsFocused(owner, focus))
            {
                var diff = focus is BaseCreature focusCreature ? GetDominanceDifficulty(focusCreature) : 0.0;

                if (owner.CheckTargetSkill(SkillName.Herding, focus, diff - 25.0, diff + 25.0))
                {
                    SendHeldGroundMessage(owner);
                    return;
                }
            }

            BeginCower();
        }

        private void BeginCower()
        {
            _cowering = true;
            _cowerEnd = Core.TickCount + CowerDurationMs;

            AIObject?.Peel();
            PublicOverheadMessage(MessageType.Emote, 0x3B2, false, "*cowers*");
            IssueOrder(OrderType.Come, null);
        }

        private void SendHeldGroundMessage(Mobile owner)
        {
            var name = Name;

            if (owner.Karma < 0)
            {
                switch (Utility.Random(3))
                {
                    case 0:
                        {
                            owner.SendMessage($"{name} flinches, then glances back at you and decides you are the greater threat.");
                            break;
                        }
                    case 1:
                        {
                            owner.SendMessage($"Terror flickers in {name}'s eyes, but its fear of you runs deeper.");
                            break;
                        }
                    default:
                        {
                            owner.SendMessage($"{name} shudders, then presses on rather than face your wrath.");
                            break;
                        }
                }

                return;
            }

            switch (Utility.Random(3))
            {
                case 0:
                    {
                        owner.SendMessage($"{name} falters, but your steady command emboldens it.");
                        break;
                    }
                case 1:
                    {
                        owner.SendMessage($"{name} steadies itself under your watchful guidance.");
                        break;
                    }
                default:
                    {
                        owner.SendMessage($"Your presence steels {name}'s nerve, and it holds its ground.");
                        break;
                    }
            }
        }

        /// <summary>
        /// A frightened pet refuses player combat orders until the cower window ends.
        /// </summary>
        private bool RefusesOrderWhileCowering(OrderType order, Mobile issuer)
        {
            if (issuer == null || order is not (OrderType.Attack or OrderType.Guard) || !IsCowering)
            {
                return false;
            }

            issuer.SendMessage($"{Name} is too frightened to obey.");
            return true;
        }

        /// <summary>
        /// Contested fights: a creature fighting a player (or a player's follower) outside the herder's
        /// party ignores the herder.
        /// </summary>
        public bool IsEngagedWithOutsider(Mobile herder)
        {
            var owner = GetOwningPlayer(Combatant);

            if (owner == null || owner == herder)
            {
                return false;
            }

            // Mobile.Party (object) shadows the Party type inside a Mobile subclass.
            return Engines.PartySystem.Party.Get(herder)?.Contains(owner) != true;
        }
    }
}
