using System.Net.Http.Json;
using System.Text.Json;
using FootballIQ.Infrastructure.FootballData.Models;

namespace FootballIQ.Infrastructure.FootballData;

/// <summary>Typed HTTP client for the football-data.org API.</summary>
public class FootballDataClient
{
    private readonly HttpClient _httpClient;

    public FootballDataClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>Fetches all matches for the given competition.</summary>
    public async Task<CompetitionMatchesResponse> GetMatchesAsync(int competitionId, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"competitions/{competitionId}/matches", ct);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var result = await response.Content.ReadFromJsonAsync<CompetitionMatchesResponse>(options, ct);

        return result ?? throw new InvalidOperationException("football-data.org returned an empty response.");
    }
}
