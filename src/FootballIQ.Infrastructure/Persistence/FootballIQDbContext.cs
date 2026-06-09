using Microsoft.EntityFrameworkCore;

namespace FootballIQ.Infrastructure.Persistence;

/// <summary>EF Core database session — all queries and saves go through this class.</summary>
public class FootballIQDbContext : DbContext
{
    public FootballIQDbContext(DbContextOptions<FootballIQDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
