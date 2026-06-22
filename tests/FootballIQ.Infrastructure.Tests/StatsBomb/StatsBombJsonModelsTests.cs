using System.Text.Json;
using FootballIQ.Infrastructure.StatsBomb.Models;

namespace FootballIQ.Infrastructure.Tests.StatsBomb;

public class StatsBombJsonModelsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private static string? FindDataFile(params string[] relativeParts)
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

        var path = Path.Combine(new[] { dir, "data", "statsbomb", "data" }.Concat(relativeParts).ToArray());
        return File.Exists(path) ? path : null;
    }

    [Fact]
    public void StatsBombMatch_DeserializesRealMatchFile_WithZeroErrors()
    {
        var path = FindDataFile("matches", "11", "90.json");
        if (path is null)
        {
            return; // Skipped: clone data/statsbomb (Task 2.1) to run this test.
        }

        var json = File.ReadAllText(path);
        var matches = JsonSerializer.Deserialize<List<StatsBombMatch>>(json, JsonOptions);

        Assert.NotNull(matches);
        Assert.NotEmpty(matches!);
        var match = matches!.Single(m => m.MatchId == 3773386);
        Assert.Equal("Deportivo Alavés", match.HomeTeam.HomeTeamName);
        Assert.Equal("Barcelona", match.AwayTeam.AwayTeamName);
        Assert.Equal(1, match.HomeScore);
        Assert.Equal(1, match.AwayScore);
    }

    [Fact]
    public void StatsBombLineupTeam_DeserializesRealLineupFile_WithZeroErrors()
    {
        var path = FindDataFile("lineups", "3773386.json");
        if (path is null)
        {
            return; // Skipped: clone data/statsbomb (Task 2.1) to run this test.
        }

        var json = File.ReadAllText(path);
        var teams = JsonSerializer.Deserialize<List<StatsBombLineupTeam>>(json, JsonOptions);

        Assert.NotNull(teams);
        Assert.Equal(2, teams!.Count);
        var barcelona = teams.Single(t => t.TeamName == "Barcelona");
        Assert.Contains(barcelona.Lineup, p => p.PlayerName.Contains("Messi"));
    }

    [Fact]
    public void StatsBombEvent_DeserializesRealEventsFile_WithZeroErrors()
    {
        var path = FindDataFile("events", "3773386.json");
        if (path is null)
        {
            return; // Skipped: clone data/statsbomb (Task 2.1) to run this test.
        }

        var json = File.ReadAllText(path);
        var events = JsonSerializer.Deserialize<List<StatsBombEvent>>(json, JsonOptions);

        Assert.NotNull(events);
        Assert.True(events!.Count > 3000);

        var firstPass = events.First(e => e.Type.Name == "Pass");
        Assert.NotNull(firstPass.Pass);

        var firstShot = events.First(e => e.Type.Name == "Shot");
        Assert.NotNull(firstShot.Shot);
    }
}
