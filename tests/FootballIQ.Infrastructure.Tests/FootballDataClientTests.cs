using FootballIQ.Infrastructure.FootballData;

namespace FootballIQ.Infrastructure.Tests;

public class FootballDataClientTests
{
    [Fact]
    public async Task GetMatchesAsync_WithRealApi_ReturnsDeserializedMatches()
    {
        var apiKey = Environment.GetEnvironmentVariable("FOOTBALLDATA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return; // Skipped: set FOOTBALLDATA_API_KEY to run this test against the real API.
        }

        using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.football-data.org/v4/") };
        httpClient.DefaultRequestHeaders.Add("X-Auth-Token", apiKey);
        var client = new FootballDataClient(httpClient);

        var result = await client.GetMatchesAsync(2021, CancellationToken.None); // 2021 = Premier League

        Assert.NotEmpty(result.Competition.Name);
        Assert.NotEmpty(result.Matches);
    }
}
