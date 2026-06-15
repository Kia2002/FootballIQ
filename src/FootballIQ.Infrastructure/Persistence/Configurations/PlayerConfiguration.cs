using FootballIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballIQ.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the Player entity.</summary>
public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("players");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired();

        builder.Property(p => p.Position)
            .HasConversion<string>();

        builder.Property(p => p.PreferredFoot)
            .HasConversion<string>();

        builder.HasIndex(p => p.StatsBombPlayerId)
            .IsUnique();
    }
}
