using FootballIQ.Infrastructure.StatsBomb.Models;

namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Reads StatsBomb open-data JSON files from disk. Internal to Infrastructure — see LEARNING.md Task 2.3 for why this isn't an Application-layer interface.</summary>
public interface IStatsBombReader
{
    Task<List<StatsBombMatch>> GetMatchesAsync(int competitionId, int seasonId, CancellationToken ct);

    Task<List<StatsBombEvent>> GetEventsAsync(int matchId, CancellationToken ct);

    Task<List<StatsBombLineupTeam>> GetLineupAsync(int matchId, CancellationToken ct);
}
