using FootballIQ.Domain.Entities;
using FootballIQ.Infrastructure.Enrichment;
using FootballIQ.Infrastructure.Enrichment.Models;
using FootballIQ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FootballIQ.Infrastructure.Tests.Enrichment;

public class WikidataEnrichmentServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private FootballIQDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<FootballIQDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new FootballIQDbContext(options);
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task<Guid> SeedPlayerWithClubAsync(string playerName, string clubName)
    {
        var club = new Club { Id = Guid.NewGuid(), StatsBombTeamId = new Random().Next(), Name = clubName };
        var player = new Player { Id = Guid.NewGuid(), StatsBombPlayerId = new Random().Next(), Name = playerName };
        var stats = new PlayerSeasonStats
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            ClubId = club.Id,
            CompetitionId = 11,
            SeasonId = 90
        };

        _context.Clubs.Add(club);
        _context.Players.Add(player);
        _context.PlayerSeasonStats.Add(stats);
        await _context.SaveChangesAsync(CancellationToken.None);

        return player.Id;
    }

    private async Task<Guid> SeedPlayerWithTwoSeasonsAsync(
        string playerName, string oldClubName, int oldSeasonId, string recentClubName, int recentSeasonId)
    {
        var oldClub = new Club { Id = Guid.NewGuid(), StatsBombTeamId = new Random().Next(), Name = oldClubName };
        var recentClub = new Club { Id = Guid.NewGuid(), StatsBombTeamId = new Random().Next(), Name = recentClubName };
        var player = new Player { Id = Guid.NewGuid(), StatsBombPlayerId = new Random().Next(), Name = playerName };

        _context.Clubs.AddRange(oldClub, recentClub);
        _context.Players.Add(player);
        _context.PlayerSeasonStats.AddRange(
            new PlayerSeasonStats { Id = Guid.NewGuid(), PlayerId = player.Id, ClubId = oldClub.Id, CompetitionId = 11, SeasonId = oldSeasonId },
            new PlayerSeasonStats { Id = Guid.NewGuid(), PlayerId = player.Id, ClubId = recentClub.Id, CompetitionId = 11, SeasonId = recentSeasonId });
        await _context.SaveChangesAsync(CancellationToken.None);

        return player.Id;
    }

    [Fact]
    public async Task EnrichPlayerDemographicsAsync_WithConfidentMatch_SetsDateOfBirth()
    {
        var playerId = await SeedPlayerWithClubAsync("Lionel Messi", "FC Barcelona");

        var fakeClient = new FakeWikidataClient(new Dictionary<string, List<WikidataPersonResult>>
        {
            ["Lionel Messi"] = [new() { BirthDate = new DateTime(1987, 6, 24), Clubs = ["FC Barcelona"] }]
        });

        var service = new WikidataEnrichmentService(fakeClient, _context);
        var updatedCount = await service.EnrichPlayerDemographicsAsync(CancellationToken.None);

        Assert.Equal(1, updatedCount);

        var player = await _context.Players.SingleAsync(p => p.Id == playerId);
        Assert.Equal(new DateTime(1987, 6, 24), player.DateOfBirth);
    }

    [Fact]
    public async Task EnrichPlayerDemographicsAsync_WithNoMatch_LeavesDateOfBirthNull()
    {
        var playerId = await SeedPlayerWithClubAsync("Unknown Player", "FC Barcelona");

        var fakeClient = new FakeWikidataClient(new Dictionary<string, List<WikidataPersonResult>>());

        var service = new WikidataEnrichmentService(fakeClient, _context);
        var updatedCount = await service.EnrichPlayerDemographicsAsync(CancellationToken.None);

        Assert.Equal(0, updatedCount);

        var player = await _context.Players.SingleAsync(p => p.Id == playerId);
        Assert.Null(player.DateOfBirth);
    }

    [Fact]
    public async Task EnrichPlayerDemographicsAsync_OnPlayerAlreadyHavingDateOfBirth_SkipsThatPlayer()
    {
        var playerId = await SeedPlayerWithClubAsync("Already Known", "FC Barcelona");
        var existingPlayer = await _context.Players.SingleAsync(p => p.Id == playerId);
        existingPlayer.DateOfBirth = DateTime.SpecifyKind(new DateTime(2000, 1, 1), DateTimeKind.Utc);
        await _context.SaveChangesAsync(CancellationToken.None);

        var fakeClient = new FakeWikidataClient(new Dictionary<string, List<WikidataPersonResult>>
        {
            ["Already Known"] = [new() { BirthDate = new DateTime(1999, 9, 9), Clubs = ["FC Barcelona"] }]
        });

        var service = new WikidataEnrichmentService(fakeClient, _context);
        var updatedCount = await service.EnrichPlayerDemographicsAsync(CancellationToken.None);

        Assert.Equal(0, updatedCount);

        var player = await _context.Players.SingleAsync(p => p.Id == playerId);
        Assert.Equal(new DateTime(2000, 1, 1), player.DateOfBirth);
    }

    [Fact]
    public async Task EnrichPlayerDemographicsAsync_WithTwoSeasonsAtDifferentClubs_DisambiguatesUsingMostRecentClub()
    {
        var playerId = await SeedPlayerWithTwoSeasonsAsync(
            "Ambiguous Player", oldClubName: "Old Club FC", oldSeasonId: 42, recentClubName: "New Club FC", recentSeasonId: 90);

        // Two same-named candidates: one matches the player's old club, the other matches their most recent club.
        // Only the most-recent-club match should be trusted as confident.
        var fakeClient = new FakeWikidataClient(new Dictionary<string, List<WikidataPersonResult>>
        {
            ["Ambiguous Player"] =
            [
                new() { BirthDate = new DateTime(1985, 1, 1), Clubs = ["Old Club FC"] },
                new() { BirthDate = new DateTime(1995, 5, 5), Clubs = ["New Club FC"] }
            ]
        });

        var service = new WikidataEnrichmentService(fakeClient, _context);
        var updatedCount = await service.EnrichPlayerDemographicsAsync(CancellationToken.None);

        Assert.Equal(1, updatedCount);

        var player = await _context.Players.SingleAsync(p => p.Id == playerId);
        Assert.Equal(new DateTime(1995, 5, 5), player.DateOfBirth);
    }
}
