using FootballIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballIQ.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the PlayerPositionTally entity.</summary>
public class PlayerPositionTallyConfiguration : IEntityTypeConfiguration<PlayerPositionTally>
{
    public void Configure(EntityTypeBuilder<PlayerPositionTally> builder)
    {
        builder.ToTable("player_position_counts");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Position)
            .HasConversion<string>();

        builder.HasIndex(t => new { t.PlayerId, t.Position })
            .IsUnique();
    }
}
