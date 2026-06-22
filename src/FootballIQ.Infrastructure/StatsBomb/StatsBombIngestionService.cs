using FootballIQ.Application.Interfaces;
using FootballIQ.Domain.Entities;
using FootballIQ.Infrastructure.Persistence;
using FootballIQ.Infrastructure.StatsBomb.Models;
using Microsoft.EntityFrameworkCore;

namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Reads StatsBomb match data via IStatsBombReader and persists it, skipping matches already recorded in IngestionLog.</summary>
public class StatsBombIngestionService : IStatsBombIngestionService
{
    private readonly IStatsBombReader _reader;
    private readonly FootballIQDbContext _context;
    private readonly PlayerStatsAggregator _aggregator;

    public StatsBombIngestionService(IStatsBombReader reader, FootballIQDbContext context, PlayerStatsAggregator aggregator)
    {
        _reader = reader;
        _context = context;
        _aggregator = aggregator;
    }

    public async Task<int> IngestSeasonAsync(int competitionId, int seasonId, CancellationToken ct)
    {
        var matches = await _reader.GetMatchesAsync(competitionId, seasonId, ct);
        var ingestedCount = 0;

        foreach (var match in matches)
        {
            var alreadyIngested = await _context.IngestionLogs
                .AnyAsync(l => l.StatsBombMatchId == match.MatchId, ct);

            if (alreadyIngested)
            {
                continue;
            }

            await IngestMatchAsync(match, competitionId, seasonId, ct);
            ingestedCount++;
        }

        return ingestedCount;
    }

    private async Task IngestMatchAsync(StatsBombMatch match, int competitionId, int seasonId, CancellationToken ct)
    {
        var events = await _reader.GetEventsAsync(match.MatchId, ct);
        var lineups = await _reader.GetLineupAsync(match.MatchId, ct);

        var homeClub = await GetOrCreateClubAsync(match.HomeTeam.HomeTeamId, match.HomeTeam.HomeTeamName, ct);
        var awayClub = await GetOrCreateClubAsync(match.AwayTeam.AwayTeamId, match.AwayTeam.AwayTeamName, ct);

        _context.Matches.Add(new Match
        {
            Id = Guid.NewGuid(),
            StatsBombMatchId = match.MatchId,
            CompetitionId = competitionId,
            SeasonId = seasonId,
            MatchDate = DateTime.SpecifyKind(DateTime.Parse(match.MatchDate), DateTimeKind.Utc),
            HomeClubId = homeClub.Id,
            AwayClubId = awayClub.Id,
            HomeScore = match.HomeScore,
            AwayScore = match.AwayScore
        });

        var clubIdByStatsBombPlayerId = lineups
            .SelectMany(team => team.Lineup.Select(p => (p.PlayerId, ClubId: team.TeamId == match.HomeTeam.HomeTeamId ? homeClub.Id : awayClub.Id)))
            .ToDictionary(x => x.PlayerId, x => x.ClubId);

        var playerStats = _aggregator.ComputeStats(events, lineups);

        foreach (var stats in playerStats)
        {
            if (!clubIdByStatsBombPlayerId.TryGetValue(stats.PlayerId, out var clubId))
            {
                continue;
            }

            var player = await GetOrCreatePlayerAsync(stats.PlayerId, stats.PlayerName, ct);
            await UpsertSeasonStatsAsync(player.Id, clubId, competitionId, seasonId, stats, ct);
        }

        _context.IngestionLogs.Add(new IngestionLog
        {
            Id = Guid.NewGuid(),
            StatsBombMatchId = match.MatchId,
            IngestedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);
    }

    private async Task<Club> GetOrCreateClubAsync(int statsBombTeamId, string name, CancellationToken ct)
    {
        var club = await _context.Clubs.FirstOrDefaultAsync(c => c.StatsBombTeamId == statsBombTeamId, ct);
        if (club is not null)
        {
            return club;
        }

        club = new Club { Id = Guid.NewGuid(), StatsBombTeamId = statsBombTeamId, Name = name };
        _context.Clubs.Add(club);
        return club;
    }

    private async Task<Player> GetOrCreatePlayerAsync(int statsBombPlayerId, string name, CancellationToken ct)
    {
        var player = await _context.Players.FirstOrDefaultAsync(p => p.StatsBombPlayerId == statsBombPlayerId, ct);
        if (player is not null)
        {
            return player;
        }

        player = new Player { Id = Guid.NewGuid(), StatsBombPlayerId = statsBombPlayerId, Name = name };
        _context.Players.Add(player);
        return player;
    }

    private async Task UpsertSeasonStatsAsync(
        Guid playerId, Guid clubId, int competitionId, int seasonId, PlayerAggregateStats stats, CancellationToken ct)
    {
        var seasonStats = await _context.PlayerSeasonStats.FirstOrDefaultAsync(
            s => s.PlayerId == playerId && s.ClubId == clubId && s.CompetitionId == competitionId && s.SeasonId == seasonId,
            ct);

        if (seasonStats is null)
        {
            seasonStats = new PlayerSeasonStats
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                ClubId = clubId,
                CompetitionId = competitionId,
                SeasonId = seasonId
            };
            _context.PlayerSeasonStats.Add(seasonStats);
        }

        seasonStats.MatchesPlayed += 1;
        seasonStats.MinutesPlayed += (int)stats.MinutesPlayed;
        seasonStats.PassesCompleted += stats.PassesCompleted;
        seasonStats.PassesAttempted += stats.PassesAttempted;
        seasonStats.ExpectedGoals += stats.TotalXg;
        seasonStats.ExpectedAssists += stats.TotalXa;
        seasonStats.Pressures += stats.Pressures;
        seasonStats.LastUpdatedAt = DateTime.UtcNow;
    }
}
