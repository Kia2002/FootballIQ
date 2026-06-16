using FootballIQ.Domain.Entities;

namespace FootballIQ.Domain.Tests;

public class PlayerSeasonStatsTests
{
    [Fact]
    public void PassAccuracy_WhenPassesAttempted_ReturnsCorrectPercentage()
    {
        var stats = new PlayerSeasonStats
        {
            PassesCompleted = 80,
            PassesAttempted = 100
        };

        Assert.Equal(80.0, stats.PassAccuracy);
    }

    [Fact]
    public void PassAccuracy_WhenNoPassesAttempted_ReturnsZero()
    {
        var stats = new PlayerSeasonStats
        {
            PassesCompleted = 0,
            PassesAttempted = 0
        };

        Assert.Equal(0.0, stats.PassAccuracy);
    }

    [Fact]
    public void PassAccuracy_WithPerfectAccuracy_Returns100()
    {
        var stats = new PlayerSeasonStats
        {
            PassesCompleted = 50,
            PassesAttempted = 50
        };

        Assert.Equal(100.0, stats.PassAccuracy);
    }
}
