using FootballIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FootballIQ.Infrastructure.Persistence;

/// <summary>EF Core database session — all queries and saves go through this class.</summary>
public class FootballIQDbContext : DbContext
{
    public FootballIQDbContext(DbContextOptions<FootballIQDbContext> options) : base(options)
    {
    }

    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerSeasonStats> PlayerSeasonStats => Set<PlayerSeasonStats>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<IngestionLog> IngestionLogs => Set<IngestionLog>();
    public DbSet<PlayerPositionTally> PlayerPositionTallies => Set<PlayerPositionTally>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FootballIQDbContext).Assembly);
    }
}
