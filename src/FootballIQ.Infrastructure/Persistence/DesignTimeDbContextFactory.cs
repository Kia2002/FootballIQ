using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FootballIQ.Infrastructure.Persistence;

/// <summary>Used only by the dotnet ef CLI — creates a DbContext for migrations without needing env vars set.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FootballIQDbContext>
{
    public FootballIQDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FootballIQDbContext>()
            .UseNpgsql("Host=localhost;Database=footballiq;Username=postgres;Password=postgres")
            .Options;

        return new FootballIQDbContext(options);
    }
}
