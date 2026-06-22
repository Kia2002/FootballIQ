using FootballIQ.Infrastructure.Persistence;
using FootballIQ.Infrastructure.StatsBomb;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FootballIQ.Infrastructure.Tests.StatsBomb;

public class StatsBombIngestionServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    private FootballIQDbContext _context = null!;
    private StatsBombIngestionService _service = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<FootballIQDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new FootballIQDbContext(options);
        await _context.Database.MigrateAsync();

        _service = new StatsBombIngestionService(new FakeStatsBombReader(), _context, new PlayerStatsAggregator());
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task IngestSeasonAsync_RunTwice_ProducesIdenticalRowCounts()
    {
        var firstRunCount = await _service.IngestSeasonAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        Assert.Equal(1, firstRunCount);
        Assert.Equal(2, await _context.Clubs.CountAsync());
        Assert.Equal(2, await _context.Players.CountAsync());
        Assert.Equal(1, await _context.Matches.CountAsync());
        Assert.Equal(2, await _context.PlayerSeasonStats.CountAsync());
        Assert.Equal(1, await _context.IngestionLogs.CountAsync());

        var secondRunCount = await _service.IngestSeasonAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        Assert.Equal(0, secondRunCount);
        Assert.Equal(2, await _context.Clubs.CountAsync());
        Assert.Equal(2, await _context.Players.CountAsync());
        Assert.Equal(1, await _context.Matches.CountAsync());
        Assert.Equal(2, await _context.PlayerSeasonStats.CountAsync());
        Assert.Equal(1, await _context.IngestionLogs.CountAsync());
    }

    [Fact]
    public async Task IngestSeasonAsync_AccumulatesSeasonStatsAcrossMatches()
    {
        await _service.IngestSeasonAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        var homePlayer = await _context.Players.SingleAsync(p => p.StatsBombPlayerId == FakeStatsBombReader.HomePlayerId);
        var seasonStats = await _context.PlayerSeasonStats.SingleAsync(s => s.PlayerId == homePlayer.Id);

        Assert.Equal(1, seasonStats.MatchesPlayed);
        Assert.Equal(1, seasonStats.PassesAttempted);
        Assert.Equal(1, seasonStats.PassesCompleted);
    }

    [Fact]
    public async Task IngestSeasonAsync_WithOneMatch_PersistsPlayerStatsInExpectedRange()
    {
        var ingestedCount = await _service.IngestSeasonAsync(competitionId: 11, seasonId: 90, CancellationToken.None);

        Assert.Equal(1, ingestedCount);

        var homePlayer = await _context.Players.SingleAsync(p => p.StatsBombPlayerId == FakeStatsBombReader.HomePlayerId);
        var homeStats = await _context.PlayerSeasonStats.SingleAsync(s => s.PlayerId == homePlayer.Id);

        Assert.Equal(1, homeStats.PassesAttempted);
        Assert.Equal(1, homeStats.PassesCompleted);
        Assert.Equal(100.0, homeStats.PassAccuracy);

        var awayPlayer = await _context.Players.SingleAsync(p => p.StatsBombPlayerId == FakeStatsBombReader.AwayPlayerId);
        var awayStats = await _context.PlayerSeasonStats.SingleAsync(s => s.PlayerId == awayPlayer.Id);

        Assert.Equal(0, awayStats.PassesAttempted);
        Assert.InRange(awayStats.ExpectedGoals, 0.24, 0.26);
    }
}
