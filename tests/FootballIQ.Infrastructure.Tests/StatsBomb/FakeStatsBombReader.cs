using FootballIQ.Infrastructure.StatsBomb;
using FootballIQ.Infrastructure.StatsBomb.Models;

namespace FootballIQ.Infrastructure.Tests.StatsBomb;

/// <summary>Returns fixed, hand-built StatsBomb data for one match, so ingestion tests don't depend on the gitignored data/statsbomb folder.</summary>
public class FakeStatsBombReader : IStatsBombReader
{
    public const int MatchId = 9001;
    public const int HomeTeamId = 1;
    public const int AwayTeamId = 2;
    public const int HomePlayerId = 10;
    public const int AwayPlayerId = 20;

    public Task<List<StatsBombMatch>> GetMatchesAsync(int competitionId, int seasonId, CancellationToken ct)
    {
        return Task.FromResult(new List<StatsBombMatch>
        {
            new()
            {
                MatchId = MatchId,
                MatchDate = "2021-05-01",
                HomeTeam = new StatsBombHomeTeam { HomeTeamId = HomeTeamId, HomeTeamName = "Home FC" },
                AwayTeam = new StatsBombAwayTeam { AwayTeamId = AwayTeamId, AwayTeamName = "Away FC" },
                HomeScore = 2,
                AwayScore = 1,
                Competition = new StatsBombCompetition { CompetitionId = competitionId, CompetitionName = "Test League" },
                Season = new StatsBombSeason { SeasonId = seasonId, SeasonName = "2020/2021" }
            }
        });
    }

    public Task<List<StatsBombEvent>> GetEventsAsync(int matchId, CancellationToken ct)
    {
        return Task.FromResult(new List<StatsBombEvent>
        {
            new()
            {
                Type = new StatsBombNamedId { Id = 30, Name = "Pass" },
                Team = new StatsBombNamedId { Id = HomeTeamId, Name = "Home FC" },
                Player = new StatsBombNamedId { Id = HomePlayerId, Name = "Home Player" },
                Pass = new StatsBombPassData()
            },
            new()
            {
                Type = new StatsBombNamedId { Id = 16, Name = "Shot" },
                Team = new StatsBombNamedId { Id = AwayTeamId, Name = "Away FC" },
                Player = new StatsBombNamedId { Id = AwayPlayerId, Name = "Away Player" },
                Shot = new StatsBombShotData { StatsbombXg = 0.25 }
            }
        });
    }

    public Task<List<StatsBombLineupTeam>> GetLineupAsync(int matchId, CancellationToken ct)
    {
        return Task.FromResult(new List<StatsBombLineupTeam>
        {
            new()
            {
                TeamId = HomeTeamId,
                TeamName = "Home FC",
                Lineup = new List<StatsBombLineupPlayer>
                {
                    new() { PlayerId = HomePlayerId, PlayerName = "Home Player" }
                }
            },
            new()
            {
                TeamId = AwayTeamId,
                TeamName = "Away FC",
                Lineup = new List<StatsBombLineupPlayer>
                {
                    new() { PlayerId = AwayPlayerId, PlayerName = "Away Player" }
                }
            }
        });
    }
}
