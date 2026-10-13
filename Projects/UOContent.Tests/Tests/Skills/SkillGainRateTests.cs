using Server.Misc;
using Xunit;

namespace UOContent.Tests;

public class SkillGainRateTests
{
    [Theory]
    [InlineData(0.0, 1.5)]
    [InlineData(50.0, 1.5)]
    [InlineData(50.1, 1.0)]
    [InlineData(70.0, 1.0)]
    [InlineData(85.0, 0.75)]
    [InlineData(95.0, 0.5)]
    [InlineData(105.0, 0.46)]
    [InlineData(110.0, 0.42)]
    [InlineData(115.0, 0.38)]
    [InlineData(120.0, 0.38)]
    public void SkillLevelMultiplier_MatchesMementoCurve(double skillBase, double expected)
    {
        Assert.Equal(expected, SkillCheck.GetSkillLevelGainMultiplier(skillBase));
    }
}
