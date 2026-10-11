using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items
{
    [Flippable(0xE81, 0xE82)]
    [SerializationGenerator(0, false)]
    public partial class ShepherdsCrook : BaseStaff
    {
        // Runtime only: a Focus does not survive a restart or leaving the wielder's hands.
        private Mobile _focus;

        [Constructible]
        public ShepherdsCrook() : base(0xE81) => Resource = CraftResource.RegularWood;

        public override double DefaultWeight => 4.0;
        public override WeaponAbility PrimaryAbility => WeaponAbility.CrushingBlow;
        public override WeaponAbility SecondaryAbility => WeaponAbility.Disarm;

        public override int AosStrengthReq => 20;
        public override int AosMinDamage => 13;
        public override int AosMaxDamage => 15;
        public override int AosSpeed => 40;
        public override float MlSpeed => 2.75f;

        public override int OldStrengthReq => 10;
        public override int OldMinDamage => 3;
        public override int OldMaxDamage => 12;
        public override int OldSpeed => 30;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 50;

        /// <summary>
        /// The creature the wielder's pets are directed to fight. Pets fighting it get a Dominance check
        /// before they cower. Lasts until it dies, a new Focus is set, or the crook leaves the wielder.
        /// </summary>
        public Mobile Focus
        {
            get
            {
                if (_focus?.Deleted == true || _focus?.Alive == false)
                {
                    _focus = null;
                }

                return _focus;
            }
        }

        public static bool IsFocused(Mobile owner, Mobile target) =>
            target != null && owner.FindItemOnLayer(Layer.TwoHanded) is ShepherdsCrook crook && crook.Focus == target;

        public override void OnDoubleClick(Mobile from)
        {
            if (Parent != from)
            {
                from.SendMessage("You must be wielding the crook to direct your followers.");
                return;
            }

            from.SendMessage("Whom do you wish your followers to focus on?");
            from.Target = new FocusTarget(this);
        }

        public override void OnRemoved(IEntity parent)
        {
            _focus = null;
            base.OnRemoved(parent);
        }

        public override void OnDelete()
        {
            _focus = null;
            base.OnDelete();
        }

        private void SetFocus(Mobile from, Mobile target)
        {
            _focus = target;
            from.SendMessage($"Your followers focus on {target.Name}.");

            if (from is not PlayerMobile { AllFollowers: { } followers })
            {
                return;
            }

            foreach (var follower in followers)
            {
                if (follower is BaseCreature { Controlled: true, Commandable: true, AIObject: { } ai } bc &&
                    bc.ControlMaster == from)
                {
                    // Same validation as "all kill": range, liveness, refusals and the control chance.
                    ai.EndPickTarget(from, target, OrderType.Attack);
                }
            }
        }

        private class FocusTarget : Target
        {
            private readonly ShepherdsCrook _crook;

            public FocusTarget(ShepherdsCrook crook) : base(14, false, TargetFlags.Harmful) => _crook = crook;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (_crook.Deleted || _crook.Parent != from)
                {
                    from.SendMessage("You must be wielding the crook to direct your followers.");
                    return;
                }

                if (targeted is not Mobile target || target == from || !target.Alive ||
                    target is BaseCreature { ControlMaster: { } master } && master == from ||
                    !from.CanBeHarmful(target, false))
                {
                    from.SendMessage("Your followers cannot focus on that.");
                    return;
                }

                _crook.SetFocus(from, target);
            }
        }
    }
}
