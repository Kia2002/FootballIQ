using FootballIQ.Domain.Enums;
using FootballIQ.Infrastructure.StatsBomb;

namespace FootballIQ.Infrastructure.Tests.StatsBomb;

public class StatsBombPositionMapperTests
{
    [Theory]
    [InlineData("Goalkeeper", Position.Goalkeeper)]
    [InlineData("Right Center Back", Position.CentreBack)]
    [InlineData("Left Center Back", Position.CentreBack)]
    [InlineData("Center Back", Position.CentreBack)]
    [InlineData("Right Back", Position.RightBack)]
    [InlineData("Right Wing Back", Position.RightBack)]
    [InlineData("Left Back", Position.LeftBack)]
    [InlineData("Left Wing Back", Position.LeftBack)]
    [InlineData("Center Defensive Midfield", Position.CentreMidfield)]
    [InlineData("Right Defensive Midfield", Position.CentreMidfield)]
    [InlineData("Left Defensive Midfield", Position.CentreMidfield)]
    [InlineData("Center Midfield", Position.CentreMidfield)]
    [InlineData("Right Center Midfield", Position.CentreMidfield)]
    [InlineData("Left Center Midfield", Position.CentreMidfield)]
    [InlineData("Center Attacking Midfield", Position.CentreMidfield)]
    [InlineData("Right Attacking Midfield", Position.Winger)]
    [InlineData("Left Attacking Midfield", Position.Winger)]
    [InlineData("Right Midfield", Position.Winger)]
    [InlineData("Left Midfield", Position.Winger)]
    [InlineData("Right Wing", Position.Winger)]
    [InlineData("Left Wing", Position.Winger)]
    [InlineData("Center Forward", Position.Striker)]
    [InlineData("Right Center Forward", Position.Striker)]
    [InlineData("Left Center Forward", Position.Striker)]
    [InlineData("Secondary Striker", Position.Striker)]
    public void Map_WithKnownStatsBombLabel_ReturnsExpectedPosition(string statsBombLabel, Position expected)
    {
        var result = StatsBombPositionMapper.Map(statsBombLabel);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Map_WithUnknownLabel_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => StatsBombPositionMapper.Map("Sweeper Keeper"));
    }
}
