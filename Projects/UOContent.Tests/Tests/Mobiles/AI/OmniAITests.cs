using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.Chivalry;
using Server.Spells.Sixth;
using Xunit;

namespace UOContent.Tests.Mobiles.AI;

[Collection("Sequential Pathfinding Tests")]
public class OmniAITests : IDisposable
{
    private readonly List<Mobile> _created = [];

    private sealed class ProbeAI : OmniAI
    {
        public ProbeAI(BaseCreature m) : base(m)
        {
        }

        public int Combo
        {
            get => _combo;
            set => _combo = value;
        }

        public Spell Select(Mobile c) => SelectCombatSpell(c, null);
    }

    // The pathfinding fixture registers no poisons, and a null timer keeps the creature unharmed.
    private sealed class StubPoison() : Poison(-1)
    {
        public override string Name => "Stub";
        public override int Level => 0;
        public override PoisonFamily Family => default;
        public override Timer ConstructTimer(Mobile m) => null;
    }

    private (OmniAITester tester, PlayerMobile foe) Spawn(int school)
    {
        var map = Map.Maps[1];
        Assert.NotNull(map);
        map.GetAverageZ(1500, 1600, out _, out var avg, out _);
        var z = (sbyte)avg;

        var tester = new OmniAITester(school);
        _created.Add(tester);
        tester.MoveToWorld(new Point3D(1500, 1600, z), map);
        tester.AIObject.AITimer.Stop();

        var foe = new PlayerMobile();
        _created.Add(foe);
        foe.MoveToWorld(new Point3D(1502, 1600, z), map);

        return (tester, foe);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(-1)]
    public void EverySchoolRunsCombatTicks(int school)
    {
        var (tester, foe) = Spawn(school);
        var ai = Assert.IsAssignableFrom<MageAI>(tester.AIObject);
        Assert.IsType<OmniAI>(ai);

        tester.Combatant = foe;
        ai.Action = ActionType.Combat;

        for (var i = 0; i < 20; i++)
        {
            ai.Think();
        }
    }

    [Fact]
    public void RunningComboOutranksSchoolRoll()
    {
        var (tester, foe) = Spawn(0);
        var ai = new ProbeAI(tester) { Combo = 0 };
        ai.AITimer.Stop();

        Assert.IsType<ExplosionSpell>(ai.Select(foe));
        Assert.Equal(1, ai.Combo);
    }

    [Fact]
    public void PoisonedPaladinCleansesInsteadOfCuring()
    {
        var (tester, foe) = Spawn(4);
        var ai = new ProbeAI(tester);
        ai.AITimer.Stop();
        tester.Poison = new StubPoison();

        Assert.IsType<CleanseByFireSpell>(ai.Select(foe));
    }

    [Fact]
    public void NonMageDoesNotRevealHiddenFoes()
    {
        var (tester, _) = Spawn(2);

        Assert.False(((MageAI)tester.AIObject).CanRevealHidden);
    }

    public void Dispose()
    {
        foreach (var m in _created)
        {
            m.Delete();
        }
    }
}
