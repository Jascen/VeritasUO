using System;
using Server.Mobiles;

namespace Server.Spells.Fifth
{
    public class SummonCreatureSpell : MagerySpell
    {
        private static readonly SpellInfo _info = new(
            "Summon Creature",
            "Kal Xen",
            16,
            false,
            Reagent.Bloodmoss,
            Reagent.MandrakeRoot,
            Reagent.SpidersSilk
        );

        // Summons a lesser version of one of the eighth circle summons (Energy Vortex excluded).
        private const double LesserScale = 0.5;
        private const int LesserControlSlots = 2;

        public SummonCreatureSpell(Mobile caster, Item scroll = null) : base(caster, scroll, _info)
        {
        }

        public override SpellCircle Circle => SpellCircle.Fifth;

        public override bool CheckCast()
        {
            if (!base.CheckCast())
            {
                return false;
            }

            if (Caster.Followers + LesserControlSlots > Caster.FollowersMax)
            {
                Caster.SendLocalizedMessage(1049645); // You have too many followers to summon that creature.
                return false;
            }

            return true;
        }

        public override void OnCast()
        {
            if (CheckSequence())
            {
                var creature = CreateLesserSummon();

                var duration = Core.Expansion switch
                {
                    Expansion.None => TimeSpan.FromSeconds(Caster.Skills.Magery.Value),
                    _ => TimeSpan.FromSeconds((int)Caster.Skills.Magery.Value * 4)
                };

                SpellHelper.Summon(creature, Caster, 0x215, duration);
            }

            FinishSequence();
        }

        public static BaseCreature CreateLesserSummon()
        {
            BaseCreature creature = Utility.Random(5) switch
            {
                0 => new SummonedAirElemental { Name = "a lesser air elemental" },
                1 => new SummonedEarthElemental { Name = "a lesser earth elemental" },
                2 => new SummonedFireElemental { Name = "a lesser fire elemental" },
                3 => new SummonedWaterElemental { Name = "a lesser water elemental" },
                _ => new SummonedDaemon { Name = "a lesser daemon" }
            };

            creature.ControlSlots = LesserControlSlots;
            Weaken(creature, LesserScale);

            return creature;
        }

        // Resists are left intact. Only Magery and EvalInt are scaled, since the creature's spell power comes from
        // its skills rather than its stats; scaling Tactics/Wrestling would cut melee damage twice.
        private static void Weaken(BaseCreature creature, double k)
        {
            // AOS summons derive HitsMax from Str, which would only drop by a third; pin it to the scaled value.
            var hitsMax = Math.Max(1, (int)(creature.HitsMax * k));

            creature.RawStr = Math.Max(1, (int)(creature.RawStr * k));
            creature.RawDex = Math.Max(1, (int)(creature.RawDex * k));
            creature.RawInt = Math.Max(1, (int)(creature.RawInt * k));

            creature.HitsMaxSeed = hitsMax;

            if (creature.StamMaxSeed != -1)
            {
                creature.StamMaxSeed = Math.Max(1, (int)(creature.StamMaxSeed * k));
            }

            if (creature.ManaMaxSeed != -1)
            {
                creature.ManaMaxSeed = Math.Max(1, (int)(creature.ManaMaxSeed * k));
            }

            creature.Hits = creature.HitsMax;
            creature.Stam = creature.StamMax;
            creature.Mana = creature.ManaMax;

            creature.Skills.Magery.Base *= k;
            creature.Skills.EvalInt.Base *= k;

            creature.SetDamage(
                Math.Max(1, (int)(creature.DamageMin * k)),
                Math.Max(1, (int)(creature.DamageMax * k))
            );
        }

        public override TimeSpan GetCastDelay()
        {
            var delay = base.GetCastDelay() * (Core.AOS ? 5 : 4);

            // SA made everything 0.25 slower, but that is applied after the scalar
            // So remove 0.25 * 5 to compensate
            if (Core.SA)
            {
                delay -= TimeSpan.FromSeconds(1.25);
            }

            return delay;
        }
    }
}
