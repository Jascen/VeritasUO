using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells.Fifth;
using Xunit;

namespace UOContent.Tests.Spells;

[Collection("Sequential UOContent Tests")]
public class SummonCreatureTests : IDisposable
{
    private readonly List<Mobile> _created = [];

    public void Dispose()
    {
        for (var i = 0; i < _created.Count; i++)
        {
            _created[i].Delete();
        }
    }

    [Fact]
    public void LesserSummonIsHalfStrengthAndTwoSlots()
    {
        for (var i = 0; i < 50; i++)
        {
            var lesser = SummonCreatureSpell.CreateLesserSummon();
            _created.Add(lesser);

            var full = (BaseCreature)Activator.CreateInstance(lesser.GetType());
            _created.Add(full);

            Assert.StartsWith("a lesser ", lesser.Name);
            Assert.Equal(2, lesser.ControlSlots);
            Assert.Equal(lesser.HitsMax, lesser.Hits);

            // Stats and skills roll from ranges per instance, so compare ratios rather than exact values.
            Assert.InRange(lesser.HitsMax, full.HitsMax * 0.4, full.HitsMax * 0.6);
            Assert.InRange(lesser.DamageMax, full.DamageMax * 0.4, full.DamageMax * 0.6);
            Assert.InRange(lesser.Skills.Magery.Base, full.Skills.Magery.Base * 0.4, full.Skills.Magery.Base * 0.6);
            Assert.InRange(lesser.Skills.Tactics.Base, full.Skills.Tactics.Base * 0.85, full.Skills.Tactics.Base * 1.15);
        }
    }
}
