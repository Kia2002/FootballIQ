using FootballIQ.Infrastructure.StatsBomb;

namespace FootballIQ.Infrastructure.Tests.StatsBomb;

public class StatsBombReaderTests
{
    private static string? FindDataRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "FootballIQ.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        if (dir is null)
        {
            return null;
        }

        var dataRoot = Path.Combine(dir, "data", "statsbomb", "data");
        return Directory.Exists(dataRoot) ? dataRoot : null;
    }

    [Fact]
    public async Task GetMatchesAsync_WithRealLaLigaData_ReturnsKnownMatch()
    {
        var dataRoot = FindDataRoot();
        if (dataRoot is null)
        {
            return; // Skipped: clone data/statsbomb (Task 2.1) to run this test.
        }

        var reader = new StatsBombReader(dataRoot);

        var matches = await reader.GetMatchesAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        var match = matches.Single(m => m.MatchId == 3773386);
        Assert.Equal("Deportivo Alavés", match.HomeTeam.HomeTeamName);
        Assert.Equal("Barcelona", match.AwayTeam.AwayTeamName);
    }

    [Fact]
    public async Task GetEventsAsync_WithRealMatchId_ReturnsFullEventList()
    {
        var dataRoot = FindDataRoot();
        if (dataRoot is null)
        {
            return; // Skipped: clone data/statsbomb (Task 2.1) to run this test.
        }

        var reader = new StatsBombReader(dataRoot);

        var events = await reader.GetEventsAsync(matchId: 3773386, CancellationToken.None);

        Assert.True(events.Count > 3000);
        var firstPass = events.First(e => e.Type.Name == "Pass");
        Assert.NotNull(firstPass.Pass);
    }

    [Fact]
    public async Task GetLineupAsync_WithRealMatchId_ReturnsBothTeamsLineup()
    {
        var dataRoot = FindDataRoot();
        if (dataRoot is null)
        {
            return; // Skipped: clone data/statsbomb (Task 2.1) to run this test.
        }

        var reader = new StatsBombReader(dataRoot);

        var lineup = await reader.GetLineupAsync(matchId: 3773386, CancellationToken.None);

        Assert.Equal(2, lineup.Count);
        var barcelona = lineup.Single(t => t.TeamName == "Barcelona");
        Assert.Contains(barcelona.Lineup, p => p.PlayerName.Contains("Messi"));
    }

    [Fact]
    public async Task GetMatchesAsync_WhenFileMissing_ThrowsWithCompetitionAndSeasonContext()
    {
        var emptyDataRoot = Directory.CreateTempSubdirectory().FullName;
        var reader = new StatsBombReader(emptyDataRoot);

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => reader.GetMatchesAsync(competitionId: 11, seasonId: 90, CancellationToken.None));

        Assert.Contains("competitionId=11", ex.Message);
        Assert.Contains("seasonId=90", ex.Message);
    }

    [Fact]
    public async Task GetEventsAsync_WhenFileMissing_ThrowsWithMatchIdContext()
    {
        var emptyDataRoot = Directory.CreateTempSubdirectory().FullName;
        var reader = new StatsBombReader(emptyDataRoot);

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => reader.GetEventsAsync(matchId: 3773386, CancellationToken.None));

        Assert.Contains("matchId=3773386", ex.Message);
    }

    [Fact]
    public async Task GetLineupAsync_WhenFileMissing_ThrowsWithMatchIdContext()
    {
        var emptyDataRoot = Directory.CreateTempSubdirectory().FullName;
        var reader = new StatsBombReader(emptyDataRoot);

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => reader.GetLineupAsync(matchId: 3773386, CancellationToken.None));

        Assert.Contains("matchId=3773386", ex.Message);
    }
}
