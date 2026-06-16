using FootballIQ.Domain.Entities;
using FootballIQ.Domain.Enums;
using FootballIQ.Domain.Exceptions;
using FootballIQ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FootballIQ.Infrastructure.Tests;

public class PlayerRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private FootballIQDbContext _context = null!;
    private PlayerRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<FootballIQDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new FootballIQDbContext(options);
        await _context.Database.MigrateAsync();

        _repository = new PlayerRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task PlayerRepository_WhenPlayerSaved_CanBeRetrievedById()
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Name = "Lionel Messi",
            StatsBombPlayerId = 5503,
            Position = Position.Winger
        };

        await _repository.AddAsync(player, CancellationToken.None);
        await _repository.SaveChangesAsync(CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(player.Id, CancellationToken.None);

        Assert.Equal(player.Id, retrieved.Id);
        Assert.Equal("Lionel Messi", retrieved.Name);
        Assert.Equal(5503, retrieved.StatsBombPlayerId);
        Assert.Equal(Position.Winger, retrieved.Position);
    }

    [Fact]
    public async Task PlayerRepository_WhenPlayerDoesNotExist_ThrowsPlayerNotFoundException()
    {
        var nonExistentId = Guid.NewGuid();

        await Assert.ThrowsAsync<PlayerNotFoundException>(
            () => _repository.GetByIdAsync(nonExistentId, CancellationToken.None));
    }
}
