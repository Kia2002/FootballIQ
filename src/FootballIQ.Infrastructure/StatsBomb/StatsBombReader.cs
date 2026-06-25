using System.Text.Json;
using FootballIQ.Infrastructure.StatsBomb.Models;

namespace FootballIQ.Infrastructure.StatsBomb;

/// <summary>Reads StatsBomb open-data JSON files from disk under a given data root (the "data/statsbomb/data" folder).</summary>
public class StatsBombReader : IStatsBombReader
{
    private readonly string _dataRoot;
    private static readonly JsonSerializerOptions JsonOptions = new();

    public StatsBombReader(string dataRoot)
    {
        _dataRoot = dataRoot;
    }

    public async Task<List<StatsBombMatch>> GetMatchesAsync(int competitionId, int seasonId, CancellationToken ct)
    {
        var path = Path.Combine(_dataRoot, "matches", competitionId.ToString(), $"{seasonId}.json");
        var json = await ReadFileAsync(path, $"competitionId={competitionId}, seasonId={seasonId}", ct);
        return JsonSerializer.Deserialize<List<StatsBombMatch>>(json, JsonOptions) ?? [];
    }

    public async Task<List<StatsBombEvent>> GetEventsAsync(int matchId, CancellationToken ct)
    {
        var path = Path.Combine(_dataRoot, "events", $"{matchId}.json");
        var json = await ReadFileAsync(path, $"matchId={matchId}", ct);
        return JsonSerializer.Deserialize<List<StatsBombEvent>>(json, JsonOptions) ?? [];
    }

    public async Task<List<StatsBombLineupTeam>> GetLineupAsync(int matchId, CancellationToken ct)
    {
        var path = Path.Combine(_dataRoot, "lineups", $"{matchId}.json");
        var json = await ReadFileAsync(path, $"matchId={matchId}", ct);
        return JsonSerializer.Deserialize<List<StatsBombLineupTeam>>(json, JsonOptions) ?? [];
    }

    private static async Task<string> ReadFileAsync(string path, string context, CancellationToken ct)
    {
        try
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FileNotFoundException($"StatsBomb data file not found for {context} at path '{path}'.", path, ex);
        }
    }
}
