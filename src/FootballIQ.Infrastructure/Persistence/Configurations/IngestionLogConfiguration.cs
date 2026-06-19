using FootballIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballIQ.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the IngestionLog entity.</summary>
public class IngestionLogConfiguration : IEntityTypeConfiguration<IngestionLog>
{
    public void Configure(EntityTypeBuilder<IngestionLog> builder)
    {
        builder.ToTable("ingestion_log");

        builder.HasKey(l => l.Id);

        builder.HasIndex(l => l.StatsBombMatchId)
            .IsUnique();
    }
}
