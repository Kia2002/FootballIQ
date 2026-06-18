using FootballIQ.Infrastructure.StatsBomb;
using FootballIQ.Infrastructure.StatsBomb.Models;

namespace FootballIQ.Infrastructure.Tests.StatsBomb;

public class PlayerStatsAggregatorTests
{
    private static StatsBombEvent PassEvent(int playerId, string playerName, bool completed)
    {
        return new StatsBombEvent
        {
            Type = new StatsBombNamedId { Id = 30, Name = "Pass" },
            Team = new StatsBombNamedId { Id = 1, Name = "Barcelona" },
            Player = new StatsBombNamedId { Id = playerId, Name = playerName },
            Pass = new StatsBombPassData
            {
                Outcome = completed ? null : new StatsBombNamedId { Id = 9, Name = "Incomplete" }
            }
        };
    }

    private static StatsBombEvent ShotEvent(string id, int playerId, string playerName, double xg)
    {
        return new StatsBombEvent
        {
            Id = id,
            Type = new StatsBombNamedId { Id = 16, Name = "Shot" },
            Team = new StatsBombNamedId { Id = 1, Name = "Barcelona" },
            Player = new StatsBombNamedId { Id = playerId, Name = playerName },
            Shot = new StatsBombShotData { StatsbombXg = xg }
        };
    }

    private static StatsBombEvent AssistPassEvent(int playerId, string playerName, string assistedShotId)
    {
        return new StatsBombEvent
        {
            Type = new StatsBombNamedId { Id = 30, Name = "Pass" },
            Team = new StatsBombNamedId { Id = 1, Name = "Barcelona" },
            Player = new StatsBombNamedId { Id = playerId, Name = playerName },
            Pass = new StatsBombPassData
            {
                ShotAssist = true,
                AssistedShotId = assistedShotId
            }
        };
    }

    private static StatsBombEvent PressureEvent(int playerId, string playerName)
    {
        return new StatsBombEvent
        {
            Type = new StatsBombNamedId { Id = 17, Name = "Pressure" },
            Team = new StatsBombNamedId { Id = 1, Name = "Barcelona" },
            Player = new StatsBombNamedId { Id = playerId, Name = playerName }
        };
    }

    private static StatsBombLineupTeam LineupWithOnePlayer(int playerId, string playerName, string from, string? to)
    {
        return new StatsBombLineupTeam
        {
            TeamId = 1,
            TeamName = "Barcelona",
            Lineup = new List<StatsBombLineupPlayer>
            {
                new()
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    Positions = new List<StatsBombPlayerPosition>
                    {
                        new() { From = from, To = to, FromPeriod = 1, ToPeriod = to is null ? null : 1 }
                    }
                }
            }
        };
    }

    [Fact]
    public void ComputeStats_WithTwoCompleteAndOneIncompletePass_ReturnsCorrectPassCompletionPct()
    {
        var events = new List<StatsBombEvent>
        {
            PassEvent(playerId: 5, playerName: "Lionel Messi", completed: true),
            PassEvent(playerId: 5, playerName: "Lionel Messi", completed: true),
            PassEvent(playerId: 5, playerName: "Lionel Messi", completed: false)
        };

        var aggregator = new PlayerStatsAggregator();

        var stats = aggregator.ComputeStats(events, lineups: new List<StatsBombLineupTeam>());

        var messi = stats.Single(s => s.PlayerId == 5);
        Assert.Equal(2.0 / 3.0, messi.PassCompletionPct, precision: 4);
    }

    [Fact]
    public void ComputeStats_WithTwoShots_ReturnsSummedTotalXg()
    {
        var events = new List<StatsBombEvent>
        {
            ShotEvent(id: "shot-1", playerId: 9, playerName: "Luis Suarez", xg: 0.3),
            ShotEvent(id: "shot-2", playerId: 9, playerName: "Luis Suarez", xg: 0.5)
        };

        var aggregator = new PlayerStatsAggregator();

        var stats = aggregator.ComputeStats(events, lineups: new List<StatsBombLineupTeam>());

        var suarez = stats.Single(s => s.PlayerId == 9);
        Assert.Equal(0.8, suarez.TotalXg, precision: 4);
    }

    [Fact]
    public void ComputeStats_WithAssistedPass_CreditsPasserWithShotXg()
    {
        var events = new List<StatsBombEvent>
        {
            AssistPassEvent(playerId: 5, playerName: "Lionel Messi", assistedShotId: "shot-1"),
            ShotEvent(id: "shot-1", playerId: 9, playerName: "Luis Suarez", xg: 0.42)
        };

        var aggregator = new PlayerStatsAggregator();

        var stats = aggregator.ComputeStats(events, lineups: new List<StatsBombLineupTeam>());

        var messi = stats.Single(s => s.PlayerId == 5);
        Assert.Equal(0.42, messi.TotalXa, precision: 4);
    }

    [Fact]
    public void ComputeStats_WithPlayerWhoPlayedHalfTheMatch_ReturnsDoubledPressuresPer90()
    {
        var events = new List<StatsBombEvent>
        {
            PressureEvent(playerId: 4, playerName: "Sergio Busquets"),
            PressureEvent(playerId: 4, playerName: "Sergio Busquets"),
            PressureEvent(playerId: 4, playerName: "Sergio Busquets")
        };
        var lineups = new List<StatsBombLineupTeam>
        {
            LineupWithOnePlayer(playerId: 4, playerName: "Sergio Busquets", from: "00:00", to: "45:00")
        };

        var aggregator = new PlayerStatsAggregator();

        var stats = aggregator.ComputeStats(events, lineups);

        var busquets = stats.Single(s => s.PlayerId == 4);
        Assert.Equal(6.0, busquets.PressuresPer90, precision: 4);
    }
}
