using FootballIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballIQ.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the PlayerSeasonStats entity.</summary>
public class PlayerSeasonStatsConfiguration : IEntityTypeConfiguration<PlayerSeasonStats>
{
    public void Configure(EntityTypeBuilder<PlayerSeasonStats> builder)
    {
        builder.ToTable("player_season_stats");

        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Player)
            .WithMany(p => p.SeasonStats)
            .HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Club)
            .WithMany(c => c.SeasonStats)
            .HasForeignKey(s => s.ClubId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
