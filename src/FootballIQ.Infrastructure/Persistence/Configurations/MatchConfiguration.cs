using FootballIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballIQ.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the Match entity.</summary>
public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("matches");

        builder.HasKey(m => m.Id);

        builder.HasIndex(m => m.StatsBombMatchId)
            .IsUnique();

        builder.HasOne<Club>()
            .WithMany()
            .HasForeignKey(m => m.HomeClubId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Club>()
            .WithMany()
            .HasForeignKey(m => m.AwayClubId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
