using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class SummonModifierTests : IDisposable
{
    private readonly List<Mobile> _created = [];

    public void Dispose()
    {
        for (var i = 0; i < _created.Count; i++)
        {
            _created[i].Delete();
        }
    }

    private class CreatureStub : BaseCreature
    {
        public CreatureStub() : base(AIType.AI_Melee)
        {
            Body = 0xC9;

            SetStr(200);
            SetDex(100);
            SetInt(80);
            SetHits(150);
            SetDamage(8, 10);

            SetResistance(ResistanceType.Physical, 40);
            SetResistance(ResistanceType.Fire, 0);
            SetResistance(ResistanceType.Energy, 70);

            SetSkill(SkillName.Tactics, 100.0);
            SetSkill(SkillName.Wrestling, 80.0);
        }

        public CreatureStub(Serial serial) : base(serial)
        {
        }
    }

    private CreatureStub NewCreature()
    {
        var bc = new CreatureStub();
        _created.Add(bc);
        return bc;
    }

    [Fact]
    public void Bonus_ScalesEverything_WithCaps()
    {
        var bc = NewCreature();

        bc.ApplyModifiers(0.25);

        Assert.Equal(0.25, bc.Modifiers);
        Assert.Equal(250, bc.RawStr);
        Assert.Equal(125, bc.RawDex);
        Assert.Equal(100, bc.RawInt);
        Assert.Equal(187, bc.HitsMaxSeed);
        Assert.Equal(bc.HitsMax, bc.Hits);
        Assert.Equal(10, bc.DamageMin);
        Assert.Equal(12, bc.DamageMax);
        Assert.Equal(50, bc.PhysicalResistanceSeed);
        Assert.Equal(0, bc.FireResistSeed);
        Assert.Equal(80, bc.EnergyResistSeed);      // capped
        Assert.Equal(120.0, bc.Skills.Tactics.Base); // capped
        Assert.Equal(100.0, bc.Skills.Wrestling.Base);
        Assert.Equal(0.0, bc.Skills.Magery.Base);    // untrained skills stay untrained
    }

    [Fact]
    public void Penalty_ShrinksEverything()
    {
        var bc = NewCreature();

        bc.ApplyModifiers(-0.25);

        Assert.Equal(-0.25, bc.Modifiers);
        Assert.Equal(150, bc.RawStr);
        Assert.Equal(112, bc.HitsMaxSeed);
        Assert.Equal(6, bc.DamageMin);
        Assert.Equal(7, bc.DamageMax);
        Assert.Equal(30, bc.PhysicalResistanceSeed);
        Assert.Equal(75.0, bc.Skills.Tactics.Base);
    }

    [Fact]
    public void Zero_IsANoOp()
    {
        var bc = NewCreature();

        bc.ApplyModifiers(0);

        Assert.Equal(0, bc.Modifiers);
        Assert.Equal(200, bc.RawStr);
        Assert.Equal(100.0, bc.Skills.Tactics.Base);
    }

    [Theory]
    [InlineData(0.25, 0.375)]
    [InlineData(-0.25, 0.625)]
    [InlineData(0.0, 0.5)]
    public void DispelChance_ScalesInverselyWithModifier(double mod, double expected)
    {
        var bc = NewCreature();
        bc.Modifiers = mod;

        Assert.Equal(expected, bc.ScaleDispelChance(0.5), 6);
    }
}
