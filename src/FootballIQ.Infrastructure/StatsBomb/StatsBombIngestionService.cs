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

        var alreadyIngestedMatchIds = (await _context.IngestionLogs
                .Select(l => l.StatsBombMatchId)
                .ToListAsync(ct))
            .ToHashSet();

        var clubsByStatsBombTeamId = await _context.Clubs
            .ToDictionaryAsync(c => c.StatsBombTeamId, c => c, ct);

        var playersByStatsBombPlayerId = await _context.Players
            .ToDictionaryAsync(p => p.StatsBombPlayerId, p => p, ct);

        var seasonStatsByPlayerAndClub = await _context.PlayerSeasonStats
            .Where(s => s.CompetitionId == competitionId && s.SeasonId == seasonId)
            .ToDictionaryAsync(s => (s.PlayerId, s.ClubId), s => s, ct);

        var talliesByPlayerId = (await _context.PlayerPositionTallies.ToListAsync(ct))
            .GroupBy(t => t.PlayerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ingestedCount = 0;

        foreach (var match in matches)
        {
            if (alreadyIngestedMatchIds.Contains(match.MatchId))
            {
                continue;
            }

            await IngestMatchAsync(
                match, competitionId, seasonId,
                clubsByStatsBombTeamId, playersByStatsBombPlayerId, seasonStatsByPlayerAndClub, talliesByPlayerId, ct);
            ingestedCount++;
        }

        return ingestedCount;
    }

    private async Task IngestMatchAsync(
        StatsBombMatch match,
        int competitionId,
        int seasonId,
        Dictionary<int, Club> clubsByStatsBombTeamId,
        Dictionary<int, Player> playersByStatsBombPlayerId,
        Dictionary<(Guid PlayerId, Guid ClubId), PlayerSeasonStats> seasonStatsByPlayerAndClub,
        Dictionary<Guid, List<PlayerPositionTally>> talliesByPlayerId,
        CancellationToken ct)
    {
        var events = await _reader.GetEventsAsync(match.MatchId, ct);
        var lineups = await _reader.GetLineupAsync(match.MatchId, ct);

        var homeClub = GetOrCreateClub(clubsByStatsBombTeamId, match.HomeTeam.HomeTeamId, match.HomeTeam.HomeTeamName);
        var awayClub = GetOrCreateClub(clubsByStatsBombTeamId, match.AwayTeam.AwayTeamId, match.AwayTeam.AwayTeamName);

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

        var positionsByStatsBombPlayerId = lineups
            .SelectMany(team => team.Lineup)
            .ToDictionary(p => p.PlayerId, p => p.Positions.Select(pos => pos.Position).ToList());

        var playerStats = _aggregator.ComputeStats(events, lineups);

        foreach (var stats in playerStats)
        {
            if (!clubIdByStatsBombPlayerId.TryGetValue(stats.PlayerId, out var clubId))
            {
                continue;
            }

            var player = GetOrCreatePlayer(playersByStatsBombPlayerId, stats.PlayerId, stats.PlayerName);
            UpsertSeasonStats(seasonStatsByPlayerAndClub, player.Id, clubId, competitionId, seasonId, stats);

            if (positionsByStatsBombPlayerId.TryGetValue(stats.PlayerId, out var statsBombPositions))
            {
                UpdatePositionTally(talliesByPlayerId, player, statsBombPositions);
            }
        }

        _context.IngestionLogs.Add(new IngestionLog
        {
            Id = Guid.NewGuid(),
            StatsBombMatchId = match.MatchId,
            IngestedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);
    }

    private Club GetOrCreateClub(Dictionary<int, Club> clubsByStatsBombTeamId, int statsBombTeamId, string name)
    {
        if (clubsByStatsBombTeamId.TryGetValue(statsBombTeamId, out var club))
        {
            return club;
        }

        club = new Club { Id = Guid.NewGuid(), StatsBombTeamId = statsBombTeamId, Name = name };
        clubsByStatsBombTeamId[statsBombTeamId] = club;
        _context.Clubs.Add(club);
        return club;
    }

    private void UpdatePositionTally(
        Dictionary<Guid, List<PlayerPositionTally>> talliesByPlayerId, Player player, List<string> statsBombPositions)
    {
        var increments = statsBombPositions
            .Select(StatsBombPositionMapper.Map)
            .GroupBy(position => position)
            .ToDictionary(g => g.Key, g => g.Count());

        if (!talliesByPlayerId.TryGetValue(player.Id, out var tallies))
        {
            tallies = [];
            talliesByPlayerId[player.Id] = tallies;
        }

        foreach (var (position, increment) in increments)
        {
            var tally = tallies.FirstOrDefault(t => t.Position == position);

            if (tally is null)
            {
                tally = new PlayerPositionTally { Id = Guid.NewGuid(), PlayerId = player.Id, Position = position };
                _context.PlayerPositionTallies.Add(tally);
                tallies.Add(tally);
            }

            tally.Count += increment;
        }

        player.Position = tallies.OrderByDescending(t => t.Count).First().Position;
    }

    private Player GetOrCreatePlayer(Dictionary<int, Player> playersByStatsBombPlayerId, int statsBombPlayerId, string name)
    {
        if (playersByStatsBombPlayerId.TryGetValue(statsBombPlayerId, out var player))
        {
            return player;
        }

        player = new Player { Id = Guid.NewGuid(), StatsBombPlayerId = statsBombPlayerId, Name = name };
        playersByStatsBombPlayerId[statsBombPlayerId] = player;
        _context.Players.Add(player);
        return player;
    }

    private void UpsertSeasonStats(
        Dictionary<(Guid PlayerId, Guid ClubId), PlayerSeasonStats> seasonStatsByPlayerAndClub,
        Guid playerId, Guid clubId, int competitionId, int seasonId, PlayerAggregateStats stats)
    {
        var key = (playerId, clubId);

        if (!seasonStatsByPlayerAndClub.TryGetValue(key, out var seasonStats))
        {
            seasonStats = new PlayerSeasonStats
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                ClubId = clubId,
                CompetitionId = competitionId,
                SeasonId = seasonId
            };
            seasonStatsByPlayerAndClub[key] = seasonStats;
            _context.PlayerSeasonStats.Add(seasonStats);
        }

        seasonStats.MatchesPlayed += 1;
        seasonStats.MinutesPlayed += (int)stats.MinutesPlayed;
        seasonStats.PassesCompleted += stats.PassesCompleted;
        seasonStats.PassesAttempted += stats.PassesAttempted;
        seasonStats.Goals += stats.Goals;
        seasonStats.Assists += stats.Assists;
        seasonStats.ExpectedGoals += stats.TotalXg;
        seasonStats.ExpectedAssists += stats.TotalXa;
        seasonStats.Pressures += stats.Pressures;
        seasonStats.LastUpdatedAt = DateTime.UtcNow;
    }
}
