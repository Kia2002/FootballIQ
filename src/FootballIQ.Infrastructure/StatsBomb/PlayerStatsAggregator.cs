using FootballIQ.Infrastructure.StatsBomb.Models;

namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Aggregates raw StatsBomb match events into per-player summary stats.</summary>
public class PlayerStatsAggregator
{
    public List<PlayerAggregateStats> ComputeStats(
        List<StatsBombEvent> events,
        List<StatsBombLineupTeam> lineups,
        double matchDurationMinutes = 90)
    {
        var shotXgById = events
            .Where(e => e.Type.Name == "Shot")
            .ToDictionary(e => e.Id, e => e.Shot?.StatsbombXg ?? 0);

        var minutesPlayedByPlayerId = lineups
            .SelectMany(team => team.Lineup)
            .ToDictionary(
                player => player.PlayerId,
                player => player.Positions.Sum(p => (ParseMinutes(p.To) ?? matchDurationMinutes) - (ParseMinutes(p.From) ?? 0)));

        var eventsByPlayer = events
            .Where(e => e.Player is not null)
            .GroupBy(e => e.Player!);

        return eventsByPlayer
            .Select(group =>
            {
                var passes = group.Where(e => e.Type.Name == "Pass").ToList();
                var totalPasses = passes.Count;
                var completedPasses = passes.Count(e => e.Pass?.Outcome is null);

                var totalXg = group
                    .Where(e => e.Type.Name == "Shot")
                    .Sum(e => e.Shot?.StatsbombXg ?? 0);

                var totalXa = passes
                    .Where(e => e.Pass?.ShotAssist == true && e.Pass.AssistedShotId is not null)
                    .Sum(e => shotXgById.GetValueOrDefault(e.Pass!.AssistedShotId!, 0));

                var totalPressures = group.Count(e => e.Type.Name == "Pressure");
                var minutesPlayed = minutesPlayedByPlayerId.GetValueOrDefault(group.Key.Id, 0);

                return new PlayerAggregateStats
                {
                    PlayerId = group.Key.Id,
                    PlayerName = group.Key.Name,
                    PassCompletionPct = totalPasses == 0 ? 0 : (double)completedPasses / totalPasses,
                    TotalXg = totalXg,
                    TotalXa = totalXa,
                    PressuresPer90 = minutesPlayed == 0 ? 0 : totalPressures / (minutesPlayed / 90.0)
                };
            })
            .ToList();
    }

    private static double? ParseMinutes(string? time)
    {
        if (time is null)
        {
            return null;
        }

        var parts = time.Split(':');
        return int.Parse(parts[0]) + int.Parse(parts[1]) / 60.0;
    }
}
