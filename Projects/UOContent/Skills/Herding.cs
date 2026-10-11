using System;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Targeting;

namespace Server.SkillHandlers;

/// <summary>
/// Dominance (Herding) skill use: drive a wild creature a short distance. A successful drive also
/// peels it out of its current fight. Bosses can only be peeled.
/// </summary>
public static class Herding
{
    private const int MaxDriveDistance = 5;

    // Below this, any herdable creature trains the skill; above it only aggressive wild creatures do.
    private const double PassiveGainCap = 50.0;

    public static void Initialize()
    {
        SkillInfo.Table[(int)SkillName.Herding].Callback = OnUse;
    }

    public static TimeSpan OnUse(Mobile m)
    {
        m.SendLocalizedMessage(502464); // Target the animal you wish to herd.
        m.Target = new HerdingTarget();

        return TimeSpan.FromSeconds(6.0);
    }

    private static bool IsBoss(BaseCreature bc) => bc is BaseChampion or Harrower;

    private static bool IsHerdable(BaseCreature bc) =>
        bc.Alive && !bc.Blessed && !bc.IsInvulnerable && !bc.CanGiveMLQuest &&
        bc is not (BaseVendor or BaseEscortable);

    private static bool IsAggressive(BaseCreature bc) =>
        !bc.Controlled && !bc.Summoned && bc.ControlMaster == null && bc.SummonMaster == null &&
        (bc.FightMode is FightMode.Closest or FightMode.Strongest or FightMode.Weakest or FightMode.Evil ||
         bc.Combatant != null);

    // Returns false when the creature ignores the herder; the caller has already messaged.
    private static bool CanCommand(Mobile from, BaseCreature bc)
    {
        if (BaseCreature.GetOwningPlayer(bc) != null)
        {
            bc.PrivateOverheadMessage(MessageType.Regular, 0x3B2, 502467, from.NetState); // That animal looks tame already.
            return false;
        }

        if (!IsHerdable(bc))
        {
            from.SendLocalizedMessage(502468); // That is not a herdable animal.
            return false;
        }

        if (bc.IsEngagedWithOutsider(from))
        {
            from.SendMessage("It is too engaged in battle to heed you.");
            return false;
        }

        return true;
    }

    private static bool CheckHerd(Mobile from, BaseCreature bc)
    {
        var diff = BaseCreature.GetDominanceDifficulty(bc);
        var min = diff - 30.0;
        var max = diff + 30.0 + Utility.Random(10);

        if (max <= from.Skills.Herding.Value)
        {
            bc.PrivateOverheadMessage(MessageType.Regular, 0x3B2, 502471, from.NetState); // That wasn't even challenging.
        }

        if (from.Skills.Herding.Base < PassiveGainCap || IsAggressive(bc))
        {
            return from.CheckTargetSkill(SkillName.Herding, bc, min, max);
        }

        var chance = Math.Clamp((from.Skills.Herding.Value - min) / (max - min), 0.0, 1.0);
        return Utility.RandomDouble() < chance;
    }

    private static bool IsValidDestination(BaseCreature bc, Point2D dest)
    {
        var map = bc.Map;

        if (map == null || map == Map.Internal ||
            !Utility.InRange(new Point2D(bc.X, bc.Y), dest, MaxDriveDistance))
        {
            return false;
        }

        var loc = new Point3D(dest.X, dest.Y, map.GetAverageZ(dest.X, dest.Y));

        if (bc.Home != Point3D.Zero && !Utility.InRange(bc.Home, loc, bc.RangeHome))
        {
            return false;
        }

        foreach (var teleporter in map.GetItemsAt<Teleporter>(loc))
        {
            if (teleporter.Creatures)
            {
                return false;
            }
        }

        if (BaseHouse.FindHouseAt(loc, map, 16) != null || BaseBoat.FindBoatAt(loc, map) != null)
        {
            return false;
        }

        return Region.Find(loc, map).GetRegion<GuardedRegion>()?.IsDisabled() != false;
    }

    private class HerdingTarget : Target
    {
        public HerdingTarget() : base(10, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targ)
        {
            if (targ is not BaseCreature bc)
            {
                from.SendLocalizedMessage(502472); // You don't seem to be able to persuade that to move.
                return;
            }

            if (!CanCommand(from, bc))
            {
                return;
            }

            if (IsBoss(bc))
            {
                if (CheckHerd(from, bc))
                {
                    bc.AIObject?.Peel();
                    from.SendMessage($"You draw {bc.Name}'s attention away from its prey.");
                }
                else
                {
                    from.SendLocalizedMessage(502472); // You don't seem to be able to persuade that to move.
                }

                return;
            }

            from.SendLocalizedMessage(502475); // Click where you wish the animal to go.
            from.Target = new DriveTarget(bc);
        }
    }

    private class DriveTarget : Target
    {
        private readonly BaseCreature _creature;

        public DriveTarget(BaseCreature creature) : base(10, true, TargetFlags.None) => _creature = creature;

        protected override void OnTarget(Mobile from, object targ)
        {
            if (targ is not IPoint2D p || _creature.Deleted)
            {
                return;
            }

            // The fight may have changed while the destination was being picked.
            if (!CanCommand(from, _creature))
            {
                return;
            }

            var dest = new Point2D(p);

            if (!IsValidDestination(_creature, dest))
            {
                from.SendMessage("The creature cannot be driven there.");
                return;
            }

            if (CheckHerd(from, _creature))
            {
                _creature.BeginDrive(dest);
                from.SendLocalizedMessage(502479); // The animal walks where it was instructed to.
            }
            else
            {
                from.SendLocalizedMessage(502472); // You don't seem to be able to persuade that to move.
            }
        }
    }
}
