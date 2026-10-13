using System;
using Server;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

public class NpcGuildBenefitsTests
{
    [Fact]
    public void EveryGuild_HasSkillsThatFitOnARing()
    {
        foreach (var guild in Enum.GetValues<NpcGuild>())
        {
            var bonuses = NpcGuildBenefits.GetSkillBonuses(guild);

            if (guild == NpcGuild.None)
            {
                Assert.Equal(0, bonuses.Length);
                continue;
            }

            // AosSkillBonuses has five slots.
            Assert.InRange(bonuses.Length, 1, 5);
            Assert.NotEqual("no guild", NpcGuildBenefits.GetName(guild));
        }
    }

    [Theory]
    [InlineData(NpcGuild.MagesGuild, SkillName.EvalInt, true)]
    [InlineData(NpcGuild.MagesGuild, SkillName.Swords, false)]
    [InlineData(NpcGuild.DruidsGuild, SkillName.AnimalTaming, true)]
    [InlineData(NpcGuild.NecromancersGuild, SkillName.SpiritSpeak, true)]
    [InlineData(NpcGuild.AssassinsGuild, SkillName.Poisoning, true)]
    [InlineData(NpcGuild.None, SkillName.Alchemy, false)]
    public void IsGuildSkill_MatchesGuildTable(NpcGuild guild, SkillName skill, bool expected)
    {
        Assert.Equal(expected, NpcGuildBenefits.IsGuildSkill(guild, skill));
    }

    [Fact]
    public void GuildGainDivisor_StaysWithinMementoRange()
    {
        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(NpcGuildBenefits.RollGuildGainDivisor(), 1.0, 1.5 + 1e-9);
        }
    }
}
