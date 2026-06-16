using FootballIQ.Domain.Entities;

namespace FootballIQ.Domain.Tests;

public class PlayerTests
{
    [Fact]
    public void Player_SeasonStats_DefaultsToEmptyCollection()
    {
        var player = new Player();

        Assert.NotNull(player.SeasonStats);
        Assert.Empty(player.SeasonStats);
    }

    [Fact]
    public void Player_Name_DefaultsToEmptyString()
    {
        var player = new Player();

        Assert.Equal(string.Empty, player.Name);
    }

    [Fact]
    public void Club_SeasonStats_DefaultsToEmptyCollection()
    {
        var club = new Club();

        Assert.NotNull(club.SeasonStats);
        Assert.Empty(club.SeasonStats);
    }
}
